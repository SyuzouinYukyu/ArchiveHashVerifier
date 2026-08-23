using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ArchiveHashVerifier;

public sealed partial class GpgService
{
    private readonly string? configuredExecutable;
    private readonly string? homeDirectory;
    private readonly Action<IReadOnlyList<string>>? invocationObserver;

    public GpgService(string? executable = null, string? homeDirectory = null,
        Action<IReadOnlyList<string>>? invocationObserver = null)
    {
        configuredExecutable = executable;
        this.homeDirectory = homeDirectory;
        this.invocationObserver = invocationObserver;
    }

    public string? ExecutablePath => FindExecutable(configuredExecutable);
    public bool IsAvailable => ExecutablePath is not null;

    public static string? FindExecutable(string? configured = null)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? Path.GetFullPath(configured) : null;

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is not null)
        {
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    string candidate = Path.Combine(directory.Trim('"'), "gpg.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
        }

        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GnuPG", "bin", "gpg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GnuPG", "bin", "gpg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Gpg4win", "bin", "gpg.exe")
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<IReadOnlyList<GpgKey>> ListUsableSecretKeysAsync(CancellationToken cancellationToken)
    {
        GpgRunResult run = await RunAsync(["--batch", "--with-colons", "--fixed-list-mode", "--list-secret-keys"], cancellationToken)
            .ConfigureAwait(false);
        if (run.ExitCode != 0) throw new InvalidOperationException($"秘密鍵一覧の取得に失敗しました。(終了コード {run.ExitCode})");
        return ParseSecretKeys(run.StdOut, DateTimeOffset.UtcNow);
    }

    public async Task<GpgVerifyResult> CreateDetachedSignatureAsync(
        string sourcePath, string temporarySignaturePath, bool armor, string primaryFingerprint,
        bool autoVerify, CancellationToken cancellationToken, IProgress<OperationPhase>? phase = null)
    {
        string label = armor ? "ASC" : "SIG";
        phase?.Report(new($"{label}署名生成中…"));
        var arguments = new List<string> { "--status-fd", "1", "--local-user", primaryFingerprint, "--output", temporarySignaturePath };
        if (armor) arguments.Add("--armor");
        arguments.Add("--detach-sign");
        arguments.Add("--");
        arguments.Add(sourcePath);

        GpgRunResult sign = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (sign.ExitCode != 0 || !StatusLines(sign.StdOut).Any(x => x.StartsWith("SIG_CREATED ", StringComparison.Ordinal)))
        {
            return new(GpgVerifyState.Error, $"GPG署名生成エラー (終了コード {sign.ExitCode})");
        }

        var signatureInfo = new FileInfo(temporarySignaturePath);
        signatureInfo.Refresh();
        if (!signatureInfo.Exists || signatureInfo.Length <= 0)
        {
            return new(GpgVerifyState.Error, "GPG署名ファイルが生成されていないか、空です。");
        }

        if (!autoVerify)
        {
            string unverifiedFingerprint = SettingsService.NormalizeFingerprint(primaryFingerprint) ?? primaryFingerprint.ToUpperInvariant();
            return new(GpgVerifyState.CreatedUnverified, "生成成功 / 自動検証未実施",
                SigningFingerprint: unverifiedFingerprint, PrimaryFingerprint: unverifiedFingerprint);
        }

        GpgVerifyResult verified = await VerifyDetachedSignatureAsync(temporarySignaturePath, sourcePath, cancellationToken).ConfigureAwait(false);
        phase?.Report(new($"{label}署名検証中…"));
        if (verified.State != GpgVerifyState.Valid) return verified;

        string selected = SettingsService.NormalizeFingerprint(primaryFingerprint) ?? primaryFingerprint.ToUpperInvariant();
        bool belongs = string.Equals(verified.SigningFingerprint, selected, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(verified.PrimaryFingerprint, selected, StringComparison.OrdinalIgnoreCase);
        return belongs ? verified with { Message = "生成成功 / 自動検証成功" } : new(GpgVerifyState.Error, "生成署名のFingerprintが選択鍵に属していません。",
            verified.SignerUid, verified.SigningFingerprint, verified.PrimaryFingerprint);
    }

    public Task<GpgVerifyResult> CreateAndVerifyDetachedSignatureAsync(
        string sourcePath, string temporarySignaturePath, bool armor, string primaryFingerprint, CancellationToken cancellationToken) =>
        CreateDetachedSignatureAsync(sourcePath, temporarySignaturePath, armor, primaryFingerprint, true, cancellationToken);

    public async Task<GpgVerifyResult> VerifyDetachedSignatureAsync(string signaturePath, string sourcePath, CancellationToken cancellationToken)
    {
        try
        {
            GpgRunResult run = await RunAsync(["--batch", "--status-fd", "1", "--verify", signaturePath, sourcePath], cancellationToken)
                .ConfigureAwait(false);
            return ParseVerificationStatus(run.StdOut, run.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new(GpgVerifyState.Cancelled, "キャンセル");
        }
    }

    public static GpgVerifyResult ParseVerificationStatus(string statusOutput, int exitCode = 0)
    {
        string[] statuses = StatusLines(statusOutput).ToArray();
        string? validSig = statuses.FirstOrDefault(x => x.StartsWith("VALIDSIG ", StringComparison.Ordinal));
        string? goodSig = statuses.FirstOrDefault(x => x.StartsWith("GOODSIG ", StringComparison.Ordinal));
        string? signing = null;
        string? primary = null;
        if (validSig is not null)
        {
            string[] fields = validSig.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            signing = fields.ElementAtOrDefault(1);
            primary = fields.Length > 10 ? fields[10] : signing;
        }
        string? uid = goodSig?.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(2);

        if (statuses.Any(x => x.StartsWith("REVKEYSIG ", StringComparison.Ordinal)))
            return new(GpgVerifyState.Invalid, "失効済みの署名", uid, signing, primary);
        if (statuses.Any(x => x.StartsWith("EXPKEYSIG ", StringComparison.Ordinal) ||
                              x.StartsWith("EXPSIG ", StringComparison.Ordinal) ||
                              x.StartsWith("KEYEXPIRED ", StringComparison.Ordinal) ||
                              x.StartsWith("SIGEXPIRED ", StringComparison.Ordinal)))
            return new(GpgVerifyState.Invalid, "期限切れの署名または鍵", uid, signing, primary);
        if (statuses.Any(x => x.StartsWith("BADSIG ", StringComparison.Ordinal)))
            return new(GpgVerifyState.Invalid, "無効な署名", uid, signing, primary);
        if (statuses.Any(x => x.StartsWith("NO_PUBKEY ", StringComparison.Ordinal)))
            return new(GpgVerifyState.MissingPublicKey, "公開鍵なし", uid, signing, primary);
        if (statuses.Any(x => x.StartsWith("ERRSIG ", StringComparison.Ordinal)))
            return new(GpgVerifyState.Error, "署名検証エラー", uid, signing, primary);
        if (validSig is not null)
            return new(GpgVerifyState.Valid, "有効な署名", uid, signing, primary);
        return new(GpgVerifyState.Error, $"GPG実行エラー (終了コード {exitCode})");
    }

    private async Task<GpgRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        invocationObserver?.Invoke(arguments);
        string executable = ExecutablePath ?? throw new FileNotFoundException("gpg.exe が見つかりません。");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            startInfo.ArgumentList.Add("--homedir");
            startInfo.ArgumentList.Add(homeDirectory);
        }
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new InvalidOperationException("gpg.exe を起動できませんでした。");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string[] output = await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            return new(process.ExitCode, output[0], output[1]);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            throw;
        }
    }

    public static IReadOnlyList<GpgKey> ParseSecretKeys(string colonOutput, DateTimeOffset now)
    {
        var results = new List<GpgKey>();
        KeyBuilder? key = null;
        bool awaitingPrimaryFingerprint = false;
        bool awaitingSubFingerprint = false;

        void Finish()
        {
            if (key is null || !key.Valid || string.IsNullOrWhiteSpace(key.PrimaryFingerprint) || key.SigningFingerprints.Count == 0) return;
            string uid = key.UserId ?? key.PrimaryFingerprint;
            Match match = UidRegex().Match(uid);
            string email = match.Success ? match.Groups[1].Value : "";
            string name = match.Success ? uid[..match.Index].Trim() : uid;
            results.Add(new(uid, name, email, AlgorithmName(key.Algorithm), key.Expires, key.PrimaryFingerprint,
                key.SigningFingerprints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        foreach (string line in colonOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] f = line.Split(':');
            if (f.Length == 0) continue;
            if (f[0] == "sec")
            {
                Finish();
                key = new KeyBuilder
                {
                    Valid = IsValidRecord(f, now), Algorithm = Field(f, 3), Expires = ParseEpoch(Field(f, 6)),
                    PrimaryCanSign = HasSigningCapability(f) && HasSecretMaterial(f)
                };
                awaitingPrimaryFingerprint = true;
                awaitingSubFingerprint = false;
            }
            else if (key is not null && f[0] == "ssb")
            {
                awaitingSubFingerprint = IsValidRecord(f, now) && HasSigningCapability(f) && HasSecretMaterial(f);
                awaitingPrimaryFingerprint = false;
            }
            else if (key is not null && f[0] == "fpr")
            {
                string fingerprint = Field(f, 9).ToUpperInvariant();
                if (awaitingPrimaryFingerprint)
                {
                    key.PrimaryFingerprint = fingerprint;
                    if (key.PrimaryCanSign) key.SigningFingerprints.Add(fingerprint);
                }
                else if (awaitingSubFingerprint)
                {
                    key.SigningFingerprints.Add(fingerprint);
                }
                awaitingPrimaryFingerprint = awaitingSubFingerprint = false;
            }
            else if (key is not null && f[0] == "uid" && key.UserId is null && IsRecordValidityAcceptable(Field(f, 1)))
            {
                key.UserId = DecodeColonText(Field(f, 9));
            }
        }
        Finish();
        return results;
    }

    private static IEnumerable<string> StatusLines(string text)
    {
        const string prefix = "[GNUPG:] ";
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            if (line.StartsWith(prefix, StringComparison.Ordinal)) yield return line[prefix.Length..];
    }

    private static bool IsValidRecord(string[] fields, DateTimeOffset now)
    {
        if (!IsRecordValidityAcceptable(Field(fields, 1)) || Field(fields, 11).Contains('D')) return false;
        DateTimeOffset? expiry = ParseEpoch(Field(fields, 6));
        return expiry is null || expiry > now;
    }

    private static bool IsRecordValidityAcceptable(string validity) => validity is not ("r" or "e" or "d" or "i" or "n" or "q");
    private static bool HasSigningCapability(string[] fields)
    {
        string capabilities = Field(fields, 11);
        return capabilities.Contains('s') && !capabilities.Contains('D');
    }
    private static bool HasSecretMaterial(string[] fields) => Field(fields, 14) != "#";
    private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : "";

    private static DateTimeOffset? ParseEpoch(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long epoch) && epoch > 0
        ? DateTimeOffset.FromUnixTimeSeconds(epoch) : null;

    private static string DecodeColonText(string value)
    {
        var bytes = new List<byte>(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (i + 3 < value.Length && value[i] == '\\' && value[i + 1] == 'x' &&
                byte.TryParse(value.AsSpan(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte escaped))
            {
                bytes.Add(escaped);
                i += 3;
                continue;
            }
            if (i + 1 < value.Length && value[i] == '\\' && value[i + 1] is 'n' or 'e')
            {
                bytes.Add(value[i + 1] == 'n' ? (byte)'\n' : (byte)0x1b);
                i++;
                continue;
            }
            int charCount = char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
            bytes.AddRange(Encoding.UTF8.GetBytes(value.AsSpan(i, charCount).ToString()));
            i += charCount - 1;
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static string AlgorithmName(string id) => id switch
    {
        "1" => "RSA", "16" => "ElGamal", "17" => "DSA", "18" => "ECDH", "19" => "ECDSA", "22" => "EdDSA", _ => $"OpenPGP ({id})"
    };

    [GeneratedRegex(@"<([^<>]+)>", RegexOptions.CultureInvariant)]
    private static partial Regex UidRegex();

    private sealed record GpgRunResult(int ExitCode, string StdOut, string StdErr);
    private sealed class KeyBuilder
    {
        public bool Valid { get; init; }
        public bool PrimaryCanSign { get; init; }
        public string Algorithm { get; init; } = "";
        public DateTimeOffset? Expires { get; init; }
        public string? PrimaryFingerprint { get; set; }
        public string? UserId { get; set; }
        public List<string> SigningFingerprints { get; } = [];
    }
}
