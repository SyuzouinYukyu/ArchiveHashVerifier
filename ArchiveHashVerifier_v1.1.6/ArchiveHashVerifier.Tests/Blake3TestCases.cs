using ArchiveHashVerifier;
using Blake3;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

internal static class Blake3TestCases
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ArchiveHashVerifier-v1.1.3-blake3-tests-" + Guid.NewGuid().ToString("N"));

    // Source: BLAKE3-team/BLAKE3 test_vectors/test_vectors.json.
    public static async Task OfficialTestVectors()
    {
        await AssertOfficialVector(0, "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262e00f03e7b69af26b7faaf09fcd333050338ddfe085b8cc869ca98b206c08243a26f5487789e8f660afe6c99ef9e0c52b92e7393024a80459cf91f476f9ffdbda7001c22e159b402631f277ca96f2defdf1078282314e763699a31c5363165421cce14d");
        await AssertOfficialVector(1, "2d3adedff11b61f14c886e35afa036736dcd87a74d27b5c1510225d0f592e213c3a6cb8bf623e20cdb535f8d1a5ffb86342d9c0b64aca3bce1d31f60adfa137b358ad4d79f97b47c3d5e79f179df87a3b9776ef8325f8329886ba42f07fb138bb502f4081cbcec3195c5871e6c23e2cc97d3c69a613eba131e5f1351f3f1da786545e5");
        await AssertOfficialVector(1023, "10108970eeda3eb932baac1428c7a2163b0e924c9a9e25b35bba72b28f70bd11a182d27a591b05592b15607500e1e8dd56bc6c7fc063715b7a1d737df5bad3339c56778957d870eb9717b57ea3d9fb68d1b55127bba6a906a4a24bbd5acb2d123a37b28f9e9a81bbaae360d58f85e5fc9d75f7c370a0cc09b6522d9c8d822f2f28f485");
        await AssertOfficialVector(1024, "42214739f095a406f3fc83deb889744ac00df831c10daa55189b5d121c855af71cf8107265ecdaf8505b95d8fcec83a98a6a96ea5109d2c179c47a387ffbb404756f6eeae7883b446b70ebb144527c2075ab8ab204c0086bb22b7c93d465efc57f8d917f0b385c6df265e77003b85102967486ed57db5c5ca170ba441427ed9afa684e");
        await AssertOfficialVector(1025, "d00278ae47eb27b34faecf67b4fe263f82d5412916c1ffd97c8cb7fb814b8444f4c4a22b4b399155358a994e52bf255de60035742ec71bd08ac275a1b51cc6bfe332b0ef84b409108cda080e6269ed4b3e2c3f7d722aa4cdc98d16deb554e5627be8f955c98e1d5f9565a9194cad0c4285f93700062d9595adb992ae68ff12800ab67a");
    }

    public static Task SupplementalKnownAnswerAbc() => AssertVector(Encoding.UTF8.GetBytes("abc"), "6437b3ac38465133ffb63b75273a8db548c558465d79db03fd359c6cd5bd9d85");

    public static async Task SingleGeneration()
    {
        string file = await CreateFile("single.bin", 4097);
        OperationSummary result = await Generate(file, [HashKind.Blake3]);
        Equal(1, result.Ok); True(File.Exists(file + ".blake3"), "blake3 missing");
    }

    public static async Task CorrectVerification()
    {
        string file = await CreateFile("verify.bin", 1200);
        await Generate(file, [HashKind.Blake3]);
        Equal(1, (await Verify(file)).Ok);
    }

    public static async Task ModifiedIsNg()
    {
        string file = await CreateFile("modified.bin", 1200);
        await Generate(file, [HashKind.Blake3]);
        await File.AppendAllTextAsync(file, "changed");
        Equal(1, (await Verify(file)).Ng);
    }

    public static async Task UppercaseHexVerifies()
    {
        string file = await CreateFile("uppercase.bin", 333);
        await Generate(file, [HashKind.Blake3]);
        string path = file + ".blake3";
        string text = await File.ReadAllTextAsync(path, Encoding.UTF8);
        await File.WriteAllTextAsync(path, text[..64].ToUpperInvariant() + text[64..], new UTF8Encoding(false));
        Equal(1, (await Verify(path)).Ok);
    }

    public static async Task OutputFormat()
    {
        string file = await CreateFile("format name.bin", 77);
        await Generate(file, [HashKind.Blake3]);
        byte[] bytes = await File.ReadAllBytesAsync(file + ".blake3");
        True(!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "BOM present");
        string text = Encoding.UTF8.GetString(bytes);
        True(System.Text.RegularExpressions.Regex.IsMatch(text, "^[0-9a-f]{64}  format name\\.bin\\r\\n$"), "format");
    }

    public static async Task FiveWayGeneration()
    {
        string file = await CreateFile("five.bin", 8193);
        OperationSummary result = await Generate(file, Enum.GetValues<HashKind>());
        Equal(5, result.Ok); Equal(5, HashCatalog.All.Count(spec => File.Exists(file + spec.OutputExtension)));
    }

    public static async Task FiveWayOnePass()
    {
        byte[] data = RandomNumberGenerator.GetBytes(3 * 1024 * 1024 + 17);
        await using var counting = new CountingStream(new MemoryStream(data));
        IReadOnlyDictionary<HashKind, string> hashes = await HashService.ComputeAsync(counting, Enum.GetValues<HashKind>(), "memory", 0, data.Length, null, CancellationToken.None);
        Equal(5, hashes.Count); Equal(data.Length, counting.TotalBytesRead);
    }

    public static async Task Blake3AndSha512()
    {
        string file = await CreateFile("pair.bin", 1600);
        OperationSummary result = await Generate(file, [HashKind.Blake3, HashKind.Sha512]);
        Equal(2, result.Ok); True(File.Exists(file + ".blake3") && File.Exists(file + ".sha512"), "pair outputs");
    }

    public static Task SplitUpdateBoundaries()
    {
        foreach (int length in new[] { 1, 1023, 1024, 1025, 1024 * 1024 - 1, 1024 * 1024, 1024 * 1024 + 1, 2 * 1024 * 1024 + 37 })
        {
            byte[] data = RandomNumberGenerator.GetBytes(length);
            string expected = Convert.ToHexString(Hasher.Hash(data).AsSpan()).ToLowerInvariant();
            using var hasher = Hasher.New();
            for (int offset = 0; offset < data.Length; offset += 1023)
                hasher.Update(data.AsSpan(offset, Math.Min(1023, data.Length - offset)));
            Equal(expected, Convert.ToHexString(hasher.Finalize().AsSpan()).ToLowerInvariant());
        }
        return Task.CompletedTask;
    }

    public static Task UpdateWithJoinMatchesUpdate()
    {
        byte[] data = RandomNumberGenerator.GetBytes(4 * 1024 * 1024 + 123);
        using var joined = Hasher.New(); using var normal = Hasher.New();
        joined.UpdateWithJoin(data); normal.Update(data);
        Equal(Convert.ToHexString(normal.Finalize().AsSpan()), Convert.ToHexString(joined.Finalize().AsSpan()));
        return Task.CompletedTask;
    }

    public static async Task EmptyFile()
    {
        string file = await CreateFile("empty.bin", 0);
        await Generate(file, [HashKind.Blake3]);
        Equal(1, (await Verify(file)).Ok);
    }

    public static async Task UnicodeFileName()
    {
        string file = await CreateFile("日本語 空白 😀.bin", 256);
        await Generate(file, [HashKind.Blake3]);
        True((await File.ReadAllTextAsync(file + ".blake3")).Contains("  日本語 空白 😀.bin", StringComparison.Ordinal), "unicode name");
        Equal(1, (await Verify(file)).Ok);
    }

    public static async Task RecursiveFolder()
    {
        string root = NewDir(); string nested = Path.Combine(root, "a", "b"); Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(root, "root.bin"), "root"); await File.WriteAllTextAsync(Path.Combine(nested, "nested.bin"), "nested");
        IReadOnlyList<string> files = FileDiscovery.Expand([root], true);
        OperationSummary generated = await new ProcessingCoordinator(MissingGpg()).GenerateAsync(files, new GenerationOptions { BatchOutput = false, Hashes = [HashKind.Blake3] }, AlwaysOverwrite, null, null, CancellationToken.None);
        Equal(2, generated.Ok); Equal(2, (await new ProcessingCoordinator(MissingGpg()).VerifyAsync(files, null, null, CancellationToken.None)).Ok);
    }

    public static async Task SidecarExcluded()
    {
        string root = NewDir(); string source = Path.Combine(root, "data.bin"); await File.WriteAllTextAsync(source, "data"); await File.WriteAllTextAsync(source + ".blake3", "x");
        IReadOnlyList<string> files = FileDiscovery.Expand([root], true);
        Equal(1, files.Count); Equal(source, files[0]);
    }

    public static async Task OverwritePolicy()
    {
        string file = await CreateFile("overwrite.bin", 20); string output = file + ".blake3"; await File.WriteAllTextAsync(output, "sentinel");
        OperationSummary result = await new ProcessingCoordinator(MissingGpg()).GenerateAsync([file], new GenerationOptions { BatchOutput = false, Hashes = [HashKind.Blake3] }, (_, _, _) => Task.FromResult(false), null, null, CancellationToken.None);
        Equal(1, result.Skipped); Equal("sentinel", await File.ReadAllTextAsync(output));
        var policy = new OverwritePolicySession(); policy.Remember(ArtifactKind.Blake3, true); True(policy.TryGet(ArtifactKind.Blake3, out bool overwrite) && overwrite, "policy");
    }

    public static async Task CancellationCleanup()
    {
        string file = await CreateFile("cancel.bin", 24 * 1024 * 1024); using var cts = new CancellationTokenSource();
        var progress = new InlineProgress<FileReadProgress>(p => { if (p.FileBytes >= 1024 * 1024) cts.Cancel(); });
        await new ProcessingCoordinator(MissingGpg()).GenerateAsync([file], new GenerationOptions { BatchOutput = false, Hashes = [HashKind.Blake3] }, AlwaysOverwrite, progress, null, cts.Token);
        True(!File.Exists(file + ".blake3"), "cancelled output"); Equal(0, Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.ahvtmp-*", SearchOption.TopDirectoryOnly).Count());
    }

    public static async Task SourceModificationDoesNotCommit()
    {
        string file = await CreateFile("change.bin", 8 * 1024 * 1024); bool touched = false;
        var progress = new InlineProgress<FileReadProgress>(_ => { if (!touched) { File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-10)); touched = true; } });
        OperationSummary result = await new ProcessingCoordinator(MissingGpg()).GenerateAsync([file], new GenerationOptions { BatchOutput = false, Hashes = [HashKind.Blake3] }, AlwaysOverwrite, progress, null, CancellationToken.None);
        Equal(1, result.Errors); True(!File.Exists(file + ".blake3"), "changed output committed");
    }

    public static Task SettingsRoundTrip()
    {
        string dir = NewDir(); var service = new SettingsService(dir); service.Save(new AppSettings { GenerateBlake3 = false });
        True(!service.Load().GenerateBlake3, "roundtrip"); return Task.CompletedTask;
    }

    public static async Task MissingSettingDefaultsOn()
    {
        string dir = NewDir(); await File.WriteAllTextAsync(Path.Combine(dir, FileDiscovery.SettingsFileName), "{\"GenerateSha512\":false}");
        True(new SettingsService(dir).Load().GenerateBlake3, "missing default");
    }

    public static async Task BrokenSettingsDefaultsOn()
    {
        string dir = NewDir(); await File.WriteAllTextAsync(Path.Combine(dir, FileDiscovery.SettingsFileName), "{broken");
        True(new SettingsService(dir).Load().GenerateBlake3, "broken default");
    }

    public static Task CompletionAggregation()
    {
        var summary = new OperationSummary(); summary.Results.Add(new("a", "BLAKE3 (a.blake3)", ResultState.Ok, "生成成功"));
        CompletionReport report = CompletionReport.Create(summary, true, 1, TimeSpan.Zero, false);
        Equal(1, report.MethodCounts["BLAKE3"]); True(report.ToPlainText().Contains("BLAKE3: 1", StringComparison.Ordinal), "completion text"); return Task.CompletedTask;
    }

    public static Task GuiLayout()
    {
        RunSta(() =>
        {
            using var form = new MainForm(); var checkbox = (CheckBox)GetField(form, "blake3"); var panel = (FlowLayoutPanel)GetField(form, "generationPanel"); var split = (SplitContainer)GetField(form, "split");
            True(checkbox.Checked && checkbox.Text == "BLAKE3" && panel.WrapContents, "BLAKE3 checkbox"); Equal(8, split.SplitterWidth);
            foreach (float size in new[] { 9F, 20F }) { form.Font = new Font("Yu Gothic UI", size); form.PerformLayout(); }
        });
        return Task.CompletedTask;
    }

    public static Task LicenseNotification()
    {
        Assembly assembly = typeof(HashService).Assembly; string name = assembly.GetManifestResourceNames().Single(x => x.EndsWith("THIRD-PARTY-NOTICES.txt", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!); string text = reader.ReadToEnd();
        True(text.Contains("Blake3.NET", StringComparison.Ordinal) && text.Contains("BSD-2-Clause", StringComparison.Ordinal) && text.Contains("Copyright (c) 2020, Alexandre Mutel", StringComparison.Ordinal) && text.Contains("All rights reserved.", StringComparison.Ordinal) && text.Contains("EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.", StringComparison.Ordinal), "embedded notice");
        TryDelete(Root); return Task.CompletedTask;
    }

    private static async Task AssertOfficialVector(int length, string expectedXof)
    {
        byte[] data = Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
        byte[] expected = Convert.FromHexString(expectedXof);
        string expectedDefault = expectedXof[..64];
        using var updated = Hasher.New(); updated.Update(data); byte[] updatedXof = new byte[expected.Length]; updated.Finalize(updatedXof);
        Equal(expectedXof, Convert.ToHexString(updatedXof).ToLowerInvariant()); Equal(expectedDefault, Convert.ToHexString(updated.Finalize().AsSpan()).ToLowerInvariant());
        using var joined = Hasher.New(); joined.UpdateWithJoin(data); byte[] joinedXof = new byte[expected.Length]; joined.Finalize(joinedXof);
        Equal(expectedXof, Convert.ToHexString(joinedXof).ToLowerInvariant()); Equal(expectedDefault, Convert.ToHexString(joined.Finalize().AsSpan()).ToLowerInvariant());
        await using var stream = new MemoryStream(data); IReadOnlyDictionary<HashKind, string> result = await HashService.ComputeAsync(stream, [HashKind.Blake3], "official-vector", 0, data.Length, null, CancellationToken.None);
        Equal(expectedDefault, result[HashKind.Blake3]);
    }
    private static async Task AssertVector(byte[] data, string expected)
    {
        await using var stream = new MemoryStream(data); IReadOnlyDictionary<HashKind, string> result = await HashService.ComputeAsync(stream, [HashKind.Blake3], "vector", 0, data.Length, null, CancellationToken.None);
        Equal(expected, result[HashKind.Blake3]); using var direct = Hasher.New(); direct.UpdateWithJoin(data); Equal(32, direct.Finalize().AsSpan().Length);
    }

    private static GpgService MissingGpg() => new("Z:\\missing\\gpg.exe");
    private static Task<bool> AlwaysOverwrite(string _, ArtifactKind __, CancellationToken ___) => Task.FromResult(true);
    private static Task<OperationSummary> Generate(string file, IEnumerable<HashKind> kinds) => new ProcessingCoordinator(MissingGpg()).GenerateAsync([file], new GenerationOptions { BatchOutput = false, Hashes = kinds.ToHashSet() }, AlwaysOverwrite, null, null, CancellationToken.None);
    private static Task<OperationSummary> Verify(string path) => new ProcessingCoordinator(MissingGpg()).VerifyAsync([path], null, null, CancellationToken.None);
    private static async Task<string> CreateFile(string name, int size) { string dir = NewDir(); string path = Path.Combine(dir, name); await File.WriteAllBytesAsync(path, size == 0 ? [] : RandomNumberGenerator.GetBytes(size)); return path; }
    private static string NewDir() { string path = Path.Combine(Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static object GetField(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected: {expected}; Actual: {actual}"); }
    private static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static void RunSta(Action action) { Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (failure is not null) throw new InvalidOperationException(failure.Message, failure); }
    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T> { public void Report(T value) => action(value); }
    private sealed class CountingStream(Stream inner) : Stream { public long TotalBytesRead { get; private set; } public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => false; public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; } public override void Flush() => inner.Flush(); public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException(); public override int Read(byte[] buffer, int offset, int count) { int read = inner.Read(buffer, offset, count); TotalBytesRead += read; return read; } public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { int read = await inner.ReadAsync(buffer, token); TotalBytesRead += read; return read; } }
}

