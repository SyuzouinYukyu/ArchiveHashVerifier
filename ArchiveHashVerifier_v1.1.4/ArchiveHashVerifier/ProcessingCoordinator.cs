namespace ArchiveHashVerifier;

public sealed partial class ProcessingCoordinator
{
    private readonly GpgService gpg;
    public ProcessingCoordinator(GpgService gpg) => this.gpg = gpg;

    public async Task<OperationSummary> GenerateAsync(
        IReadOnlyCollection<string> sourceFiles,
        GenerationOptions options,
        Func<string, ArtifactKind, CancellationToken, Task<bool>> confirmOverwrite,
        IProgress<FileReadProgress>? progress,
        Action<string>? message,
        CancellationToken cancellationToken, IProgress<OperationPhase>? phase = null)
    {
        if (options.BatchOutput) return await GenerateBatchAsync(sourceFiles, options, confirmOverwrite, progress, message, cancellationToken, phase).ConfigureAwait(false);
        var summary = new OperationSummary();
        long overallLength = SafeTotalLength(sourceFiles);
        long overallBefore = 0;

        foreach (string sourcePath in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long sourceLength = SafeLength(sourcePath);
            try
            {
                await GenerateOneAsync(sourcePath, options, confirmOverwrite, overallBefore, overallLength, progress, message, summary,
                    cancellationToken, phase).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                summary.Results.Add(new(sourcePath, "処理", ResultState.Cancelled, "キャンセル"));
                return summary;
            }
            catch (Exception ex)
            {
                summary.Results.Add(new(sourcePath, "処理", ResultState.Error, ex.Message));
            }
            overallBefore += sourceLength;
        }
        return summary;
    }

    private async Task GenerateOneAsync(
        string sourcePath,
        GenerationOptions options,
        Func<string, ArtifactKind, CancellationToken, Task<bool>> confirmOverwrite,
        long overallBefore,
        long overallLength,
        IProgress<FileReadProgress>? progress,
        Action<string>? message,
        OperationSummary summary,
        CancellationToken cancellationToken, IProgress<OperationPhase>? phase)
    {
        var requested = new List<RequestedArtifact>();
        foreach (HashKind kind in options.Hashes)
        {
            HashSpec spec = HashCatalog.Get(kind);
            requested.Add(new(spec.ArtifactKind, spec.DisplayName, sourcePath + spec.OutputExtension, kind, false));
        }
        if (options.OpenPgpAscii) requested.Add(new(ArtifactKind.OpenPgpAscii, "OpenPGP ASCII (.asc)", sourcePath + ".asc", null, true));
        if (options.OpenPgpBinary) requested.Add(new(ArtifactKind.OpenPgpBinary, "OpenPGP Binary (.sig)", sourcePath + ".sig", null, false));

        var active = new List<RequestedArtifact>();
        foreach (RequestedArtifact item in requested)
        {
            if (File.Exists(item.DestinationPath) && !await confirmOverwrite(item.DestinationPath, item.Kind, cancellationToken).ConfigureAwait(false))
            {
                summary.Results.Add(new(sourcePath, item.Name, ResultState.Skipped, "既存成果物を上書きしませんでした。"));
            }
            else
            {
                active.Add(item);
            }
        }
        if (active.Count == 0) return;

        var prepared = new List<PreparedArtifact>();
        var allTemps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var guard = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            SourceSnapshot before = SourceSnapshot.Capture(sourcePath);
            RequestedArtifact[] hashes = active.Where(x => x.HashKind is not null).ToArray();
            if (hashes.Length > 0)
            {
                guard.Position = 0;
                IReadOnlyDictionary<HashKind, string> values = await HashService.ComputeAsync(guard,
                    hashes.Select(x => x.HashKind!.Value).Distinct().ToArray(), sourcePath, overallBefore, overallLength, progress,
                    cancellationToken).ConfigureAwait(false);
                foreach (RequestedArtifact item in hashes)
                {
                    string temp = CreateTempPath(item.DestinationPath);
                    allTemps.Add(temp);
                    try
                    {
                        await HashService.WriteChecksumAsync(temp, values[item.HashKind!.Value], Path.GetFileName(sourcePath), cancellationToken)
                            .ConfigureAwait(false);
                        prepared.Add(new(item, temp, null));
                    }
                    catch
                    {
                        TryDelete(temp);
                        throw;
                    }
                }
            }

            RequestedArtifact[] signatures = active.Where(x => x.HashKind is null).ToArray();
            if (signatures.Length > 0 && !gpg.IsAvailable)
            {
                foreach (RequestedArtifact item in signatures)
                    summary.Results.Add(new(sourcePath, item.Name, ResultState.Error, "GPGがインストールされていません。"));
            }
            else if (signatures.Length > 0 && string.IsNullOrWhiteSpace(options.SigningFingerprint))
            {
                foreach (RequestedArtifact item in signatures)
                    summary.Results.Add(new(sourcePath, item.Name, ResultState.Error, "有効なGPG署名鍵が選択されていません。"));
            }
            else
            {
                foreach (RequestedArtifact item in signatures)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string temp = CreateTempPath(item.DestinationPath);
                    allTemps.Add(temp);
                    GpgVerifyResult result;
                    try
                    {
                        result = await gpg.CreateDetachedSignatureAsync(sourcePath, temp, item.AsciiArmor,
                            options.SigningFingerprint!, options.AutoVerifyGpgSignatures, cancellationToken, phase).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        result = new(GpgVerifyState.Error, ex.Message);
                    }

                    if (result.State is GpgVerifyState.Valid or GpgVerifyState.CreatedUnverified)
                        prepared.Add(new(item, temp, result));
                    else
                    {
                        TryDelete(temp);
                        summary.Results.Add(new(sourcePath, item.Name,
                            result.State == GpgVerifyState.Cancelled ? ResultState.Cancelled : ResultState.Error, result.Message,
                            result.SignerUid, result.SigningFingerprint));
                    }
                }
            }

            if (!before.Matches(sourcePath))
            {
                foreach (PreparedArtifact item in prepared)
                {
                    TryDelete(item.TempPath);
                    summary.Results.Add(new(sourcePath, item.Request.Name, ResultState.Error,
                        "処理中に元ファイルが変更されたため成果物を確定しませんでした。"));
                }
                return;
            }

            foreach (PreparedArtifact item in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(item.TempPath, item.Request.DestinationPath, true);
                    summary.Results.Add(new(sourcePath, item.Request.Name, ResultState.Ok,
                        item.SignatureResult?.Message ?? "生成成功", item.SignatureResult?.SignerUid,
                        item.SignatureResult?.SigningFingerprint));
                    message?.Invoke($"OK [{item.Request.Name}] {item.Request.DestinationPath}");
                }
                catch (Exception ex)
                {
                    summary.Results.Add(new(sourcePath, item.Request.Name, ResultState.Error, $"成果物の確定に失敗しました: {ex.Message}"));
                }
            }
        }
        finally
        {
            foreach (string temp in allTemps) TryDelete(temp);
        }
    }

    public async Task<OperationSummary> VerifyAsync(
        IReadOnlyCollection<string> inputs,
        IProgress<FileReadProgress>? progress,
        Action<string>? message,
        CancellationToken cancellationToken, IProgress<OperationPhase>? phase = null, Func<Task<bool>>? confirmMissingPublicKey = null)
    {
        var summary = new OperationSummary();
        string[] canonical = inputs.Where(File.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        bool IsManifest(string x) => Path.GetFileName(x).Equals(BatchArtifacts.Manifest, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(x).Equals(BatchArtifacts.Manifest + ".asc", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(x).Equals(BatchArtifacts.Manifest + ".sig", StringComparison.OrdinalIgnoreCase);
        bool IsBatch(string x) => BatchArtifacts.Names.Values.Any(n => n.Equals(Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
        foreach (string manifest in canonical.Where(IsManifest).GroupBy(x => Path.GetDirectoryName(x)!, StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
            summary.Results.AddRange((await VerifyManifestAsync(manifest, progress, message, cancellationToken, phase, confirmMissingPublicKey).ConfigureAwait(false)).Results);
        foreach (string batch in canonical.Where(IsBatch))
            summary.Results.AddRange((await VerifyBatchChecksumAsync(batch, progress, message, cancellationToken).ConfigureAwait(false)).Results);
        IReadOnlyList<VerificationGroup> groups = BuildVerificationGroups(canonical.Where(x => !IsManifest(x) && !IsBatch(x)).ToArray(), summary);        long overallLength = SafeTotalLength(groups.Select(x => x.SourcePath));
        long overallBefore = 0;
        foreach (VerificationGroup group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long length = SafeLength(group.SourcePath);
            try
            {
                await VerifyOneAsync(group, overallBefore, overallLength, progress, message, summary, cancellationToken, phase).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!summary.Results.Any(x => x.SourcePath == group.SourcePath && x.State == ResultState.Cancelled))
                    summary.Results.Add(new(group.SourcePath, "検証", ResultState.Cancelled, "キャンセル"));
                return summary;
            }
            catch (Exception ex)
            {
                summary.Results.Add(new(group.SourcePath, "検証", ResultState.Error, ex.Message));
            }
            overallBefore += length;
        }
        return summary;
    }

    private async Task VerifyOneAsync(VerificationGroup group, long overallBefore, long overallLength,
        IProgress<FileReadProgress>? progress, Action<string>? message, OperationSummary summary, CancellationToken cancellationToken,
        IProgress<OperationPhase>? phase)
    {
        var parseable = new List<(string Path, HashSpec Spec, string Expected)>();
        foreach (string sidecar in group.Sidecars.Where(x => HashCatalog.TryFromPath(x, out _)))
        {
            HashCatalog.TryFromPath(sidecar, out HashSpec? spec);
            try { string? value; bool isBatch = BatchArtifacts.Names.Values.Any(n => n.Equals(Path.GetFileName(sidecar), StringComparison.OrdinalIgnoreCase)); if (isBatch) { if (!BatchArtifacts.TryReadEntry(sidecar, spec!, Path.GetFileName(group.SourcePath), out value)) continue; } else value = HashService.ReadExpectedHash(sidecar, spec!.Kind); parseable.Add((sidecar, spec!, value!)); }
            catch (Exception ex) { summary.Results.Add(new(group.SourcePath, spec!.DisplayName, ResultState.Error, ex.Message)); }
        }

        await using var guard = new FileStream(group.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        SourceSnapshot before = SourceSnapshot.Capture(group.SourcePath);
        if (parseable.Count > 0)
        {
            IReadOnlyDictionary<HashKind, string> actual = await HashService.ComputeAsync(guard,
                parseable.Select(x => x.Spec.Kind).Distinct().ToArray(), group.SourcePath, overallBefore, overallLength, progress,
                cancellationToken).ConfigureAwait(false);
            foreach ((string path, HashSpec spec, string expected) in parseable)
            {
                bool ok = string.Equals(expected, actual[spec.Kind], StringComparison.OrdinalIgnoreCase);
                summary.Results.Add(new(group.SourcePath, $"{spec.DisplayName} ({Path.GetFileName(path)})",
                    ok ? ResultState.Ok : ResultState.Ng, ok ? "一致" : "ハッシュ不一致"));
                message?.Invoke($"{(ok ? "OK" : "NG")} [{spec.DisplayName}] {group.SourcePath}");
            }
        }

        foreach (string signature in group.Sidecars.Where(x => x.EndsWith(".asc", StringComparison.OrdinalIgnoreCase) ||
                                                                 x.EndsWith(".sig", StringComparison.OrdinalIgnoreCase)))
        {
            if (!gpg.IsAvailable)
            {
                summary.Results.Add(new(group.SourcePath, Path.GetExtension(signature), ResultState.Error, "GPGがインストールされていません。"));
                continue;
            }
            string label = signature.EndsWith(".asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "SIG";
            phase?.Report(new($"{label}署名検証中…"));
            GpgVerifyResult result = await gpg.VerifyDetachedSignatureAsync(signature, group.SourcePath, cancellationToken).ConfigureAwait(false);
            ResultState state = result.State switch
            {
                GpgVerifyState.Valid => ResultState.Ok,
                GpgVerifyState.Invalid => ResultState.Ng,
                GpgVerifyState.Cancelled => ResultState.Cancelled,
                _ => ResultState.Error
            };
            summary.Results.Add(new(group.SourcePath, $"OpenPGP {Path.GetExtension(signature)}", state, result.Message,
                result.SignerUid, result.SigningFingerprint));
            if (state == ResultState.Cancelled) cancellationToken.ThrowIfCancellationRequested();
        }

        if (!before.Matches(group.SourcePath))
        {
            summary.Results.Add(new(group.SourcePath, "元ファイル", ResultState.Error, "処理中に元ファイルが変更されました。"));
        }
    }

    public static IReadOnlyList<VerificationGroup> BuildVerificationGroups(IReadOnlyCollection<string> inputs, OperationSummary summary)
    {
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in inputs)
        {
            if (HashCatalog.IsKnownSidecar(path))
            {
                string source = HashCatalog.RemoveSidecarExtension(path);
                if (File.Exists(source)) sources.Add(source);
                else summary.Results.Add(new(source, Path.GetFileName(path), ResultState.Error, "対応する本体ファイルが見つかりません。"));
            }
            else if (File.Exists(path)) sources.Add(path);
        }

        var groups = new List<VerificationGroup>();
        foreach (string source in sources)
        {
            var sidecars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HashSpec spec in HashCatalog.All)
                foreach (string extension in spec.AcceptedExtensions)
                    if (File.Exists(source + extension)) sidecars.Add(source + extension);
            if (File.Exists(source + ".asc")) sidecars.Add(source + ".asc");
            if (File.Exists(source + ".sig")) sidecars.Add(source + ".sig");
            foreach (HashSpec batchSpec in HashCatalog.All) { string batch = Path.Combine(Path.GetDirectoryName(source)!, BatchArtifacts.Names[batchSpec.Kind]); if (!File.Exists(batch)) continue; try { if (BatchArtifacts.TryReadEntry(batch, batchSpec, Path.GetFileName(source), out _)) sidecars.Add(batch); } catch (Exception ex) { summary.Results.Add(new(source, batchSpec.DisplayName, ResultState.Error, ex.Message)); } }

            if (sidecars.Count == 0)
                summary.Results.Add(new(source, "検証", ResultState.Error, "対応するハッシュまたは署名ファイルが見つかりません。"));
            else
                groups.Add(new(source, sidecars.Order(StringComparer.OrdinalIgnoreCase).ToArray()));
        }
        return groups;
    }

    private static long SafeTotalLength(IEnumerable<string> paths)
    {
        long total = 0;
        foreach (string path in paths)
        {
            try { total = checked(total + new FileInfo(path).Length); }
            catch { return 0; }
        }
        return total;
    }

    private static long SafeLength(string path) { try { return new FileInfo(path).Length; } catch { return 0; } }
    private static string CreateTempPath(string destination) => Path.Combine(Path.GetDirectoryName(destination)!,
        $".{Path.GetFileName(destination)}.ahvtmp-{Guid.NewGuid():N}");
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private sealed record RequestedArtifact(ArtifactKind Kind, string Name, string DestinationPath, HashKind? HashKind, bool AsciiArmor);
    private sealed record PreparedArtifact(RequestedArtifact Request, string TempPath, GpgVerifyResult? SignatureResult);
}

public sealed record VerificationGroup(string SourcePath, IReadOnlyList<string> Sidecars);

public sealed record SourceSnapshot(long Length, DateTime LastWriteTimeUtc)
{
    public static SourceSnapshot Capture(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        return new(info.Length, info.LastWriteTimeUtc);
    }

    public bool Matches(string path)
    {
        try
        {
            var info = new FileInfo(path);
            info.Refresh();
            return info.Exists && info.Length == Length && info.LastWriteTimeUtc == LastWriteTimeUtc;
        }
        catch { return false; }
    }
}
