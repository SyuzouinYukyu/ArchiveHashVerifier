using ArchiveHashVerifier;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ArchiveHashVerifier-v1.1.3-tests-" + Guid.NewGuid().ToString("N"));
    private static string? gpgPath;
    private static string? gpgHome;
    private static GpgKey? gpgKey;

    [STAThread]
    private static async Task<int> Main()
    {
        Directory.CreateDirectory(Root);
        var tests = new (string Name, Func<Task> Body)[]
        {
            ("SHA-512 known vector", () => KnownHash(HashKind.Sha512, "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e")),
            ("SHA-256 known vector", () => KnownHash(HashKind.Sha256, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")),
            ("SHA3-512 known vector", () => KnownHash(HashKind.Sha3_512, "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a615b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26")),
            ("SHA3-256 known vector", () => KnownHash(HashKind.Sha3_256, "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a")),
            ("BLAKE3 official test_vectors 0/1/1023/1024/1025", Blake3TestCases.OfficialTestVectors),
            ("BLAKE3 supplemental known answer abc", Blake3TestCases.SupplementalKnownAnswerAbc),
            ("BLAKE3 single generation", Blake3TestCases.SingleGeneration),
            ("Batch setting default and migration", BatchTestCases.SettingsDefaultAndMigration),
            ("Batch fixed outputs and deterministic order", BatchTestCases.FixedOutputsAndOrder),
            ("Batch different directories reject", BatchTestCases.DifferentDirectoriesReject),
            ("Batch manifest deterministic and safe parser", BatchTestCases.ManifestDeterministicAndSafe),
            ("Batch reserved artifacts excluded", BatchTestCases.ReservedExcluded),
            ("Batch direct checksum and manifest verification", BatchTestCases.DirectBatchAndManifestVerify),
            ("Batch transaction rollback", BatchTestCases.TransactionRollback),
            ("Batch transaction failure matrix", BatchTestCases.TransactionFailureMatrix),
            ("Batch cancellation before commit", BatchTestCases.BatchCancellationBeforeCommit),
            ("Batch spaces and missing entry", BatchTestCases.SpacesAndMissingEntry),
            ("v1.1.5 single and multiple batch names", V115TestCases.SingleAndMultipleBatchNames),
            ("v1.1.5 output name cancel and arbitrary manifest", V115TestCases.OutputNameCancelAndArbitraryManifest),
            ("Mixed strict checksum list never falls back to legacy", V116TestCases.MixedStrictLegacyMustNotFallback),
            ("BLAKE3 correct sidecar verifies", Blake3TestCases.CorrectVerification),
            ("BLAKE3 modified source is NG", Blake3TestCases.ModifiedIsNg),
            ("BLAKE3 uppercase hex verifies", Blake3TestCases.UppercaseHexVerifies),
            ("BLAKE3 output format", Blake3TestCases.OutputFormat),
            ("BLAKE3 five-way generation", Blake3TestCases.FiveWayGeneration),
            ("BLAKE3 five-way one stream pass", Blake3TestCases.FiveWayOnePass),
            ("BLAKE3 plus SHA-512", Blake3TestCases.Blake3AndSha512),
            ("BLAKE3 split update boundaries", Blake3TestCases.SplitUpdateBoundaries),
            ("BLAKE3 UpdateWithJoin matches Update", Blake3TestCases.UpdateWithJoinMatchesUpdate),
            ("BLAKE3 empty file", Blake3TestCases.EmptyFile),
            ("BLAKE3 Unicode filename", Blake3TestCases.UnicodeFileName),
            ("BLAKE3 recursive folder generate and verify", Blake3TestCases.RecursiveFolder),
            ("BLAKE3 sidecar is excluded from generation", Blake3TestCases.SidecarExcluded),
            ("BLAKE3 overwrite policy", Blake3TestCases.OverwritePolicy),
            ("BLAKE3 cancellation cleanup", Blake3TestCases.CancellationCleanup),
            ("BLAKE3 source change does not commit", Blake3TestCases.SourceModificationDoesNotCommit),
            ("BLAKE3 setting round-trip", Blake3TestCases.SettingsRoundTrip),
            ("BLAKE3 missing setting defaults ON", Blake3TestCases.MissingSettingDefaultsOn),
            ("BLAKE3 broken setting defaults ON", Blake3TestCases.BrokenSettingsDefaultsOn),
            ("BLAKE3 completion aggregation", Blake3TestCases.CompletionAggregation),
            ("BLAKE3 GUI font and layout structure", Blake3TestCases.GuiLayout),
            ("BLAKE3 complete BSD-2-Clause notice embedded", Blake3TestCases.LicenseNotification),
            ("Five hashes use one stream pass", FourHashesOnePass),
            ("Five-way generation and exact format", FourWayGeneration),
            ("Generated hashes verify", GeneratedHashesVerify),
            (".sha-512 compatibility", () => CompatibilityExtension(HashKind.Sha512, ".sha-512")),
            (".sha-256 compatibility", () => CompatibilityExtension(HashKind.Sha256, ".sha-256")),
            (".sha3_512 compatibility", () => CompatibilityExtension(HashKind.Sha3_512, ".sha3_512")),
            (".sha3_256 compatibility", () => CompatibilityExtension(HashKind.Sha3_256, ".sha3_256")),
            ("HASH two spaces filename format", () => ParserFormat("{0}  data.bin")),
            ("HASH star filename format", () => ParserFormat("{0} *data.bin")),
            ("filename colon HASH format", () => ParserFormat("data.bin: {0}")),
            ("HASH-only format", () => ParserFormat("{0}")),
            ("Hash mismatch is NG", HashMismatch),
            ("Broken checksum is ERROR", BrokenChecksum),
            ("Missing body is ERROR", MissingBody),
            ("Zero-byte file", ZeroByte),
            ("Japanese and Unicode filename", UnicodeName),
            ("Long path and filename", LongPath),
            ("Multiple files", MultipleFiles),
            ("Recursive folder enumeration", RecursiveFolder),
            ("Generated artifacts are excluded", GenerationExclusions),
            ("Reparse point is not followed", ReparsePointNotFollowed),
            ("Overwrite No preserves existing", OverwriteNo),
            ("Overwrite Yes replaces existing", OverwriteYes),
            ("Overwrite policy is separated by kind", OverwriteKindsSeparated),
            ("Cancellation removes temporary files", CancellationCleanup),
            ("Source modification is detected", SourceModificationDetected),
            ("Settings save and restore", SettingsRoundTrip),
            ("Broken settings use defaults", BrokenSettings),
            ("Settings omit mode and retain last folder", SettingsSchema),
            ("Off-screen bounds are corrected", BoundsCorrection),
            ("Startup mode is always Verify", StartupMode),
            ("Font is clamped at 9 and 20pt", FontClamp),
            ("GPG missing executable detection", GpgMissing),
            ("GPG auto-verification defaults ON", AutoVerifyDefault),
            ("GPG auto-verification setting round-trip", AutoVerifySettingRoundTrip),
            ("GPG isolated disposable key setup", SetupGpgFixture),
            ("VALIDSIG plus REVKEYSIG is rejected", StatusRevoked),
            ("VALIDSIG plus EXPKEYSIG is rejected", StatusExpiredKeySignature),
            ("VALIDSIG plus EXPSIG is rejected", StatusExpiredSignature),
            ("BADSIG ERRSIG NO_PUBKEY priorities", StatusFailureKinds),
            ("Disabled expired revoked keys are excluded", KeyEligibility),
            ("Signing subkey capability is accepted", SigningSubkeyEligibility),
            ("FormClosing waits for cleanup", FormClosingWaitsForCleanup),
            ("RadioButton labels have no pseudo bullets", RadioLabels),
            ("Verification completion aggregation", VerificationCompletionAggregation),
            ("Generation completion aggregation", GenerationCompletionAggregation),
            ("Completion result copies to Clipboard", CompletionClipboard),
            ("Overall progress stays below 100 until complete", ProgressBeforeCompletion),
            ("GPG signing arguments permit Pinentry", GpgSigningPermitsPinentry),
            ("GPG .asc generation and self-verification", GpgAscii),
            ("GPG .sig generation and self-verification", GpgBinary),
            ("GPG .asc + .sig simultaneous generation", GpgBoth),
            ("Manifest OpenPGP direct input and NO_PUBKEY policy", ManifestOpenPgpPolicies),
            ("GPG detects modified source", GpgModifiedSource),
            ("GPG signer fingerprint ownership", GpgFingerprint),
            ("GPG auto-verification OFF skips verify", GpgAutoVerifyOff),
            ("GPG auto-verification OFF preserves signer UID", GpgAutoVerifyOffPreservesSignerUid),
            ("GPG auto-verification OFF detects source change", GpgOffSourceChange),
            ("GPG auto-verification OFF cancellation cleanup", GpgOffCancellation),
            ("GPG process cancellation", GpgCancellation)
        };

        int passed = 0;
        int failed = 0;
        foreach ((string name, Func<Task> body) in tests)
        {
            SynchronizationContext.SetSynchronizationContext(null);
            Console.WriteLine($"RUN  | {name}");
            try
            {
                await body();
                Console.WriteLine($"PASS | {name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL | {name} | {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"TOTAL={tests.Length}; PASSED={passed}; FAILED={failed}");
        TryDeleteDirectory(Root);
        return failed == 0 ? 0 : 1;
    }

    private static async Task KnownHash(HashKind kind, string expected)
    {
        await using var stream = new MemoryStream();
        var actual = await HashService.ComputeAsync(stream, [kind], "empty", 0, 0, null, CancellationToken.None);
        Equal(expected, actual[kind]);
    }

    private static async Task FourHashesOnePass()
    {
        byte[] data = RandomNumberGenerator.GetBytes(3 * 1024 * 1024 + 17);
        await using var inner = new MemoryStream(data);
        await using var counting = new CountingStream(inner);
        var hashes = await HashService.ComputeAsync(counting, Enum.GetValues<HashKind>(), "memory", 0, data.Length, null, CancellationToken.None);
        Equal(5, hashes.Count);
        Equal(data.Length, counting.TotalBytesRead);
    }

    private static async Task FourWayGeneration()
    {
        string dir = NewDir();
        string file = Path.Combine(dir, "Example.zip");
        await File.WriteAllBytesAsync(file, RandomNumberGenerator.GetBytes(4097));
        OperationSummary summary = await Generate(file, Enum.GetValues<HashKind>());
        Equal(5, summary.Ok);
        foreach (HashSpec spec in HashCatalog.All)
        {
            byte[] bytes = await File.ReadAllBytesAsync(file + spec.OutputExtension);
            True(bytes.Length > spec.HexLength + 2, spec.DisplayName);
            True(!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "BOM must be absent");
            string text = Encoding.UTF8.GetString(bytes);
            True(text.EndsWith("\r\n", StringComparison.Ordinal), "CRLF required");
            True(text.Contains("  Example.zip\r\n", StringComparison.Ordinal), "two spaces and filename required");
            Equal(spec.HexLength, text[..text.IndexOf(' ')].Length);
        }
    }

    private static async Task GeneratedHashesVerify()
    {
        string file = await CreateFile("verify", "payload.bin", 8193);
        await Generate(file, Enum.GetValues<HashKind>());
        OperationSummary result = await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe"))
            .VerifyAsync([file], null, null, CancellationToken.None);
        Equal(5, result.Ok); Equal(0, result.Ng); Equal(0, result.Errors);
    }

    private static async Task CompatibilityExtension(HashKind kind, string extension)
    {
        string file = await CreateFile("compat-" + kind, "file.bin", 777);
        await Generate(file, [kind]);
        string canonical = file + HashCatalog.Get(kind).OutputExtension;
        string compatible = file + extension;
        if (!canonical.Equals(compatible, StringComparison.OrdinalIgnoreCase)) File.Move(canonical, compatible);
        OperationSummary result = await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe"))
            .VerifyAsync([compatible], null, null, CancellationToken.None);
        Equal(1, result.Ok);
    }

    private static async Task ParserFormat(string format)
    {
        string dir = NewDir();
        string checksum = Path.Combine(dir, "data.bin.sha256");
        string hash = new('a', 64);
        await File.WriteAllTextAsync(checksum, string.Format(format, hash), new UTF8Encoding(false));
        Equal(hash, HashService.ReadExpectedHash(checksum, HashKind.Sha256));
    }

    private static async Task HashMismatch()
    {
        string file = await CreateFile("mismatch", "bad.bin", 100);
        await File.WriteAllTextAsync(file + ".sha256", new string('0', 64));
        OperationSummary result = await Verify(file);
        Equal(1, result.Ng);
    }

    private static async Task BrokenChecksum()
    {
        string file = await CreateFile("broken", "broken.bin", 100);
        await File.WriteAllTextAsync(file + ".sha3_256", "not-a-hash");
        OperationSummary result = await Verify(file);
        Equal(1, result.Errors);
    }

    private static async Task MissingBody()
    {
        string checksum = Path.Combine(NewDir(), "missing.bin.sha512");
        await File.WriteAllTextAsync(checksum, new string('0', 128));
        OperationSummary result = await Verify(checksum);
        Equal(1, result.Errors);
    }

    private static async Task ZeroByte()
    {
        string file = await CreateFile("zero", "empty.bin", 0);
        await Generate(file, [HashKind.Sha3_512]);
        Equal(1, (await Verify(file)).Ok);
    }

    private static async Task UnicodeName()
    {
        string file = await CreateFile("unicode", "日本語_😀_資料.zip", 321);
        await Generate(file, [HashKind.Sha512]);
        string text = await File.ReadAllTextAsync(file + ".sha512");
        True(text.Contains("  日本語_😀_資料.zip", StringComparison.Ordinal), "Unicode filename missing");
        Equal(1, (await Verify(file)).Ok);
    }

    private static async Task LongPath()
    {
        string segment = new('長', 80);
        string dir = Path.Combine(NewDir(), segment, segment);
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, new string('名', 120) + ".bin");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        await Generate(file, [HashKind.Sha256]);
        Equal(1, (await Verify(file)).Ok);
    }

    private static async Task MultipleFiles()
    {
        string a = await CreateFile("multi", "a.bin", 101);
        string b = Path.Combine(Path.GetDirectoryName(a)!, "b.bin");
        await File.WriteAllBytesAsync(b, RandomNumberGenerator.GetBytes(202));
        OperationSummary generated = await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe"))
            .GenerateAsync([a, b], new GenerationOptions { BatchOutput = false, Hashes = [HashKind.Sha256] }, AlwaysOverwrite, null, null, CancellationToken.None);
        Equal(2, generated.Ok);
        OperationSummary verified = await Verify([a, b]);
        Equal(2, verified.Ok);
    }

    private static async Task RecursiveFolder()
    {
        string root = NewDir();
        string nested = Path.Combine(root, "one", "two");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(root, "root.bin"), "root");
        await File.WriteAllTextAsync(Path.Combine(nested, "nested.bin"), "nested");
        IReadOnlyList<string> files = FileDiscovery.Expand([root], true);
        Equal(2, files.Count);
    }

    private static async Task GenerationExclusions()
    {
        string dir = NewDir();
        string source = Path.Combine(dir, "data.bin");
        await File.WriteAllTextAsync(source, "x");
        string[] excluded = ["data.bin.sha512", "data.bin.sha-512", "data.bin.sha256", "data.bin.sha-256",
            "data.bin.sha3-512", "data.bin.sha3_512", "data.bin.sha3-256", "data.bin.sha3_256", "data.bin.blake3", "data.bin.asc", "data.bin.sig",
            FileDiscovery.SettingsFileName, ".data.ahvtmp-123"];
        foreach (string name in excluded) await File.WriteAllTextAsync(Path.Combine(dir, name), "x");
        IReadOnlyList<string> files = FileDiscovery.Expand([dir], true);
        Equal(1, files.Count); Equal(source, files[0]);
    }

    private static async Task ReparsePointNotFollowed()
    {
        string root = NewDir();
        string outside = NewDir();
        string outsideFile = Path.Combine(outside, "must-not-be-seen.bin");
        await File.WriteAllTextAsync(outsideFile, "secret");
        string link = Path.Combine(root, "linked");
        int exit = await RunProcess("cmd.exe", ["/c", "mklink", "/J", link, outside]);
        Equal(0, exit);
        IReadOnlyList<string> files = FileDiscovery.Expand([root], true);
        True(!files.Contains(outsideFile, StringComparer.OrdinalIgnoreCase), "reparse target was followed");
    }

    private static async Task OverwriteNo()
    {
        string file = await CreateFile("overwrite-no", "data.bin", 22);
        string output = file + ".sha512";
        await File.WriteAllTextAsync(output, "sentinel");
        OperationSummary result = await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe"))
            .GenerateAsync([file], new GenerationOptions { BatchOutput = false, Hashes = [HashKind.Sha512] }, (_, _, _) => Task.FromResult(false), null, null, CancellationToken.None);
        Equal("sentinel", await File.ReadAllTextAsync(output)); Equal(1, result.Skipped);
    }

    private static async Task OverwriteYes()
    {
        string file = await CreateFile("overwrite-yes", "data.bin", 22);
        string output = file + ".sha512";
        await File.WriteAllTextAsync(output, "sentinel");
        OperationSummary result = await Generate(file, [HashKind.Sha512]);
        True((await File.ReadAllTextAsync(output)) != "sentinel", "not replaced"); Equal(1, result.Ok);
    }

    private static Task OverwriteKindsSeparated()
    {
        var policy = new OverwritePolicySession();
        policy.Remember(ArtifactKind.Sha512, true);
        policy.Remember(ArtifactKind.OpenPgpAscii, false);
        True(policy.TryGet(ArtifactKind.Sha512, out bool sha) && sha, "sha decision");
        True(policy.TryGet(ArtifactKind.OpenPgpAscii, out bool asc) && !asc, "asc decision");
        True(!policy.TryGet(ArtifactKind.OpenPgpBinary, out _), "sig must remain undecided");
        return Task.CompletedTask;
    }

    private static async Task CancellationCleanup()
    {
        string file = await CreateFile("cancel", "large.bin", 24 * 1024 * 1024);
        using var cts = new CancellationTokenSource();
        var progress = new InlineProgress<FileReadProgress>(p => { if (p.FileBytes >= 1024 * 1024) cts.Cancel(); });
        try { await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).GenerateAsync([file],
            new GenerationOptions { BatchOutput = false, Hashes = Enum.GetValues<HashKind>().ToHashSet() }, AlwaysOverwrite, progress, null, cts.Token); }
        catch (OperationCanceledException) { }
        Equal(0, Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.ahvtmp-*", SearchOption.TopDirectoryOnly).Count());
        Equal(0, HashCatalog.All.Count(s => File.Exists(file + s.OutputExtension)));
    }

    private static async Task SourceModificationDetected()
    {
        string file = await CreateFile("source-change", "changing.bin", 8 * 1024 * 1024);
        DateTime changed = DateTime.UtcNow.AddMinutes(-10);
        bool touched = false;
        var progress = new InlineProgress<FileReadProgress>(_ =>
        {
            if (!touched) { File.SetLastWriteTimeUtc(file, changed); touched = true; }
        });
        OperationSummary result = await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).GenerateAsync([file],
            new GenerationOptions { BatchOutput = false, Hashes = [HashKind.Sha256] }, AlwaysOverwrite, progress, null, CancellationToken.None);
        Equal(1, result.Errors); True(!File.Exists(file + ".sha256"), "changed source output must not be committed");
    }

    private static Task SettingsRoundTrip()
    {
        string dir = NewDir();
        var service = new SettingsService(dir);
        var source = new AppSettings { FontSize = 17, SplitterDistance = 555, GenerateSha256 = true,
            GpgFingerprint = new string('A', 40), LastFolder = dir, WindowX = 12, WindowY = 34, WindowWidth = 1200, WindowHeight = 700 };
        service.Save(source);
        AppSettings loaded = service.Load();
        Equal(17F, loaded.FontSize); Equal(555, loaded.SplitterDistance); True(loaded.GenerateSha256, "sha256");
        Equal(new string('A', 40), loaded.GpgFingerprint); Equal(dir, loaded.LastFolder);
        return Task.CompletedTask;
    }

    private static async Task BrokenSettings()
    {
        string dir = NewDir();
        await File.WriteAllTextAsync(Path.Combine(dir, FileDiscovery.SettingsFileName), "{broken-json");
        AppSettings loaded = new SettingsService(dir).Load();
        Equal(9F, loaded.FontSize); True(loaded.GenerateSha512 && loaded.GenerateBlake3 && loaded.GenerateOpenPgpAscii, "defaults");
    }

    private static async Task SettingsSchema()
    {
        string dir = NewDir();
        var service = new SettingsService(dir);
        service.Save(new AppSettings { LastFolder = dir });
        string json = await File.ReadAllTextAsync(service.SettingsPath);
        True(!json.Contains("Mode", StringComparison.OrdinalIgnoreCase), "mode must not be persisted");
        Equal(dir, service.Load().LastFolder);
    }

    private static Task BoundsCorrection()
    {
        Rectangle area = new(0, 0, 1920, 1080);
        Rectangle result = SettingsService.NormalizeBounds(new Rectangle(5000, 5000, 1100, 700), [area], area);
        True(Rectangle.Intersect(result, area).Width >= 100, "x correction");
        True(Rectangle.Intersect(result, area).Height >= 100, "y correction");
        return Task.CompletedTask;
    }

    private static Task StartupMode()
    {
        using var form = new MainForm();
        var verify = (RadioButton)GetField(form, "verifyRadio");
        var autoVerify = (CheckBox)GetField(form, "autoVerifyGpg");
        var generate = (RadioButton)GetField(form, "generateRadio");
        True(verify.Checked && !generate.Checked && autoVerify.Checked, "startup mode must be verify and auto-verify ON");
        return Task.CompletedTask;
    }

    private static Task FontClamp()
    {
        using var form = new MainForm();
        MethodInfo change = typeof(MainForm).GetMethod("ChangeFontByWheel", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (int i = 0; i < 30; i++) change.Invoke(form, [120]);
        Equal(20F, form.Font.Size);
        for (int i = 0; i < 30; i++) change.Invoke(form, [-120]);
        Equal(9F, form.Font.Size);
        return Task.CompletedTask;
    }

    private static Task GpgMissing()
    {
        var missing = new GpgService(Path.Combine(Root, "no-such-gpg.exe"));
        True(!missing.IsAvailable, "configured missing executable must stay missing");
        return Task.CompletedTask;
    }

    private static async Task AutoVerifyDefault()
    {
        True(new AppSettings().AutoVerifyGeneratedSignatures, "settings default must be ON");
        True(new GenerationOptions().AutoVerifyGpgSignatures, "generation option default must be ON");
        string dir = NewDir();
        await File.WriteAllTextAsync(Path.Combine(dir, FileDiscovery.SettingsFileName), "{}");
        True(new SettingsService(dir).Load().AutoVerifyGeneratedSignatures, "missing JSON value must be ON");
        await File.WriteAllTextAsync(Path.Combine(dir, FileDiscovery.SettingsFileName), "{\"AutoVerifyGeneratedSignatures\":\"invalid\"}");
        True(new SettingsService(dir).Load().AutoVerifyGeneratedSignatures, "invalid JSON value must be ON");
    }

    private static Task AutoVerifySettingRoundTrip()
    {
        string dir = NewDir();
        var service = new SettingsService(dir);
        service.Save(new AppSettings { AutoVerifyGeneratedSignatures = false, LastFolder = dir });
        True(!service.Load().AutoVerifyGeneratedSignatures, "OFF restore");
        service.Save(new AppSettings { AutoVerifyGeneratedSignatures = true, LastFolder = dir });
        True(service.Load().AutoVerifyGeneratedSignatures, "ON restore");
        return Task.CompletedTask;
    }

    private static Task StatusRevoked()
    {
        GpgVerifyResult result = GpgService.ParseVerificationStatus(StatusOutput(
            ValidSigLine(), "REVKEYSIG 0123456789ABCDEF Revoked User"));
        Equal(GpgVerifyState.Invalid, result.State);
        True(result.Message.Contains("失効", StringComparison.Ordinal), "revoked message");
        return Task.CompletedTask;
    }

    private static Task StatusExpiredKeySignature()
    {
        GpgVerifyResult result = GpgService.ParseVerificationStatus(StatusOutput(
            ValidSigLine(), "EXPKEYSIG 0123456789ABCDEF Expired Key"));
        Equal(GpgVerifyState.Invalid, result.State);
        True(result.Message.Contains("期限切れ", StringComparison.Ordinal), "expired key signature");
        return Task.CompletedTask;
    }

    private static Task StatusExpiredSignature()
    {
        foreach (string status in new[] { "EXPSIG 0123456789ABCDEF Expired Signature", "KEYEXPIRED 1", "SIGEXPIRED 1" })
        {
            GpgVerifyResult result = GpgService.ParseVerificationStatus(StatusOutput(ValidSigLine(), status));
            Equal(GpgVerifyState.Invalid, result.State);
        }
        return Task.CompletedTask;
    }

    private static Task StatusFailureKinds()
    {
        Equal(GpgVerifyState.Invalid, GpgService.ParseVerificationStatus(StatusOutput("BADSIG 01 Bad")).State);
        Equal(GpgVerifyState.Error, GpgService.ParseVerificationStatus(StatusOutput("ERRSIG 01 22 8 00 0 0")).State);
        Equal(GpgVerifyState.MissingPublicKey,
            GpgService.ParseVerificationStatus(StatusOutput("ERRSIG 01 22 8 00 0 0", "NO_PUBKEY 01")).State);
        return Task.CompletedTask;
    }

    private static Task KeyEligibility()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long future = now.AddDays(2).ToUnixTimeSeconds();
        long past = now.AddDays(-2).ToUnixTimeSeconds();
        var lines = new List<string>();
        AddSyntheticKey(lines, "u", "scD", future, new string('A', 40), "Disabled");
        AddSyntheticKey(lines, "r", "sc", future, new string('B', 40), "Revoked");
        AddSyntheticKey(lines, "u", "sc", past, new string('C', 40), "Expired");
        AddSyntheticKey(lines, "u", "cS", future, new string('D', 40), "AggregateOnly");
        IReadOnlyList<GpgKey> keys = GpgService.ParseSecretKeys(string.Join("\n", lines), now);
        Equal(0, keys.Count);
        return Task.CompletedTask;
    }

    private static Task SigningSubkeyEligibility()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long future = now.AddDays(2).ToUnixTimeSeconds();
        string primary = new('E', 40);
        string sub = new('F', 40);
        var lines = new List<string>();
        AddSyntheticKey(lines, "u", "cS", future, primary, "Subkey User", "s", sub);
        IReadOnlyList<GpgKey> keys = GpgService.ParseSecretKeys(string.Join("\n", lines), now);
        Equal(1, keys.Count);
        Equal(primary, keys[0].Fingerprint);
        True(keys[0].SigningFingerprints.Contains(sub), "signing subkey missing");
        True(!keys[0].SigningFingerprints.Contains(primary), "aggregate S treated as record s");
        return Task.CompletedTask;
    }

    private static Task FormClosingWaitsForCleanup()
    {
        RunSta(() =>
        {
            using var form = new MainForm();
            using var cts = new CancellationTokenSource();
            SetField(form, "running", true);
            SetField(form, "operationCts", cts);
            MethodInfo handler = typeof(MainForm).GetMethod("MainForm_FormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var first = new FormClosingEventArgs(CloseReason.UserClosing, false);
            handler.Invoke(form, [form, first]);
            True(first.Cancel, "close must be deferred");
            True(cts.IsCancellationRequested, "cancellation not requested");
            var gate = (CloseGate)GetField(form, "closeGate");
            True(gate.CloseRequested && !gate.CleanupCompleted, "cleanup gate state");
            gate.MarkCleanupCompleted();
            SetField(form, "running", false);
            var second = new FormClosingEventArgs(CloseReason.UserClosing, false);
            handler.Invoke(form, [form, second]);
            True(!second.Cancel, "close must be allowed after cleanup");
        });
        return Task.CompletedTask;
    }

    private static Task RadioLabels()
    {
        RunSta(() =>
        {
            using var form = new MainForm();
            string verify = ((RadioButton)GetField(form, "verifyRadio")).Text;
            string generate = ((RadioButton)GetField(form, "generateRadio")).Text;
            True(!verify.Contains('●') && !verify.Contains('○') && !generate.Contains('●') && !generate.Contains('○'), "pseudo bullets remain");
        });
        return Task.CompletedTask;
    }

    private static Task VerificationCompletionAggregation()
    {
        var summary = new OperationSummary();
        summary.Results.Add(new("a", "SHA-512 (a.sha512)", ResultState.Ok, "一致"));
        summary.Results.Add(new("b", "SHA-256 (b.sha256)", ResultState.Ng, "不一致"));
        summary.Results.Add(new("b", "OpenPGP .asc", ResultState.Error, "公開鍵なし"));
        CompletionReport report = CompletionReport.Create(summary, false, 2, TimeSpan.FromSeconds(3), true);
        Equal(2, report.TargetCount); Equal(1, report.Ok); Equal(1, report.Ng); Equal(1, report.Errors);
        Equal(1, report.MethodCounts["SHA-512"]); Equal(1, report.MethodCounts["SHA-256"]); Equal(1, report.MethodCounts["ASC"]);
        True(report.ToPlainText().Contains("検証が完了しました。", StringComparison.Ordinal), "verification report");
        return Task.CompletedTask;
    }

    private static Task GenerationCompletionAggregation()
    {
        var summary = new OperationSummary();
        summary.Results.Add(new("a", "SHA-512", ResultState.Ok, "生成成功"));
        summary.Results.Add(new("a", "OpenPGP ASCII (.asc)", ResultState.Ok, "生成成功 / 自動検証成功", "User", new string('A', 40)));
        summary.Results.Add(new("a", "OpenPGP Binary (.sig)", ResultState.Skipped, "skip"));
        CompletionReport report = CompletionReport.Create(summary, true, 1, TimeSpan.FromSeconds(4), true);
        Equal(2, report.Ok); Equal(1, report.Skipped); Equal(1, report.MethodCounts["ASC"]); Equal(0, report.MethodCounts["SIG"]);
        Equal(1, report.AscAutoVerified); Equal(0, report.SigAutoVerified); Equal(new string('A', 40), report.Fingerprint);
        string text = report.ToPlainText();
        True(text.Contains("GPG自動検証: ON", StringComparison.Ordinal) && text.Contains("Fingerprint", StringComparison.Ordinal), "generation report");
        return Task.CompletedTask;
    }

    private static Task CompletionClipboard()
    {
        RunSta(() =>
        {
            var summary = new OperationSummary();
            summary.Results.Add(new("a", "SHA-256", ResultState.Ok, "生成成功"));
            CompletionReport report = CompletionReport.Create(summary, true, 1, TimeSpan.Zero, false);
            using var dialog = new CompletionDialog(report, new Font("Yu Gothic UI", 9F));
            dialog.CopyResultToClipboard();
            Equal(dialog.ReportText, Clipboard.GetText(TextDataFormat.UnicodeText));
        });
        return Task.CompletedTask;
    }

    private static Task ProgressBeforeCompletion()
    {
        Equal(99, ProgressMath.CalculatePercent(100, 100, false));
        Equal(99, ProgressMath.CalculatePercent(200, 100, false));
        Equal(100, ProgressMath.CalculatePercent(100, 100, true));
        return Task.CompletedTask;
    }

    private static async Task GpgSigningPermitsPinentry()
    {
        string file = await CreateFile("pinentry-args", "data.bin", 16);
        IReadOnlyList<string>? captured = null;
        var service = new GpgService(Path.Combine(Root, "missing-gpg.exe"), null, args => captured = args.ToArray());
        try
        {
            await service.CreateDetachedSignatureAsync(file, file + ".tmp", true, new string('A', 40), false, CancellationToken.None);
        }
        catch (FileNotFoundException) { }
        IReadOnlyList<string> arguments = captured ?? throw new InvalidOperationException("sign invocation missing");
        True(arguments.Contains("--detach-sign"), "sign invocation missing");
        True(!arguments.Contains("--batch") && !arguments.Contains("--pinentry-mode") && !arguments.Contains("--passphrase"), "Pinentry-blocking argument present");
    }

    private static async Task SetupGpgFixture()
    {
        gpgPath = GpgService.FindExecutable();
        True(gpgPath is not null, "gpg.exe unavailable");
        gpgHome = Path.Combine(Root, "isolated-gnupg-home");
        Directory.CreateDirectory(gpgHome);
        int exit = await RunProcess(gpgPath!, ["--homedir", gpgHome, "--batch", "--pinentry-mode", "loopback", "--passphrase", "",
            "--quick-generate-key", "ArchiveHashVerifier 使い捨てテスト <ahv-test@example.invalid>", "ed25519", "sign", "1d"]);
        Equal(0, exit);
        IReadOnlyList<GpgKey> keys = await new GpgService(gpgPath, gpgHome).ListUsableSecretKeysAsync(CancellationToken.None);
        Equal(1, keys.Count);
        True(keys[0].UserId.Contains("使い捨てテスト", StringComparison.Ordinal), "Unicode GPG User ID decoding");
        gpgKey = keys[0];
    }

    private static async Task GpgAscii()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-asc", "signed.bin", 1200);
        OperationSummary result = await GenerateGpg(file, true, false);
        Equal(1, result.Ok); True(File.Exists(file + ".asc"), "asc missing");
        Equal("生成成功 / 自動検証成功", result.Results.Single(x => x.State == ResultState.Ok).Message);
        Equal(1, (await new ProcessingCoordinator(new GpgService(gpgPath, gpgHome)).VerifyAsync([file], null, null, CancellationToken.None)).Ok);
    }

    private static async Task GpgBinary()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-sig", "signed.bin", 1200);
        OperationSummary result = await GenerateGpg(file, false, true);
        Equal(1, result.Ok); True(File.Exists(file + ".sig"), "sig missing");
        Equal("生成成功 / 自動検証成功", result.Results.Single(x => x.State == ResultState.Ok).Message);
        Equal(1, (await new ProcessingCoordinator(new GpgService(gpgPath, gpgHome)).VerifyAsync([file], null, null, CancellationToken.None)).Ok);
    }

    private static async Task GpgBoth()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-both", "signed.bin", 1200);
        int verifyCalls = 0;
        var service = new GpgService(gpgPath, gpgHome, args => { if (args.Contains("--verify")) verifyCalls++; });
        OperationSummary result = await GenerateGpg(file, true, true, true, service);
        Equal(2, result.Ok); True(File.Exists(file + ".asc") && File.Exists(file + ".sig"), "both signatures");
        Equal(2, verifyCalls);
    }

    private static async Task GpgModifiedSource()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-modified", "signed.bin", 1200);
        await GenerateGpg(file, true, false);
        await File.AppendAllTextAsync(file, "modified");
        GpgVerifyResult result = await new GpgService(gpgPath, gpgHome).VerifyDetachedSignatureAsync(file + ".asc", file, CancellationToken.None);
        Equal(GpgVerifyState.Invalid, result.State);
    }

    private static async Task GpgFingerprint()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-fingerprint", "signed.bin", 1200);
        OperationSummary result = await GenerateGpg(file, true, false);
        Equal(gpgKey!.Fingerprint, result.Results.Single(x => x.State == ResultState.Ok).Fingerprint);
    }

    private static async Task GpgAutoVerifyOff()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-no-auto-verify", "signed.bin", 4096);
        int verifyCalls = 0;
        var service = new GpgService(gpgPath, gpgHome, args => { if (args.Contains("--verify")) verifyCalls++; });
        OperationSummary result = await GenerateGpg(file, true, false, false, service);
        Equal(1, result.Ok); Equal(0, verifyCalls);
        Equal("生成成功 / 自動検証未実施", result.Results.Single(x => x.State == ResultState.Ok).Message); Equal(gpgKey!.UserId, result.Results.Single(x => x.State == ResultState.Ok).SignerUid);
        True(new FileInfo(file + ".asc").Length > 0, "non-empty signature required");
        Equal(GpgVerifyState.Valid, (await new GpgService(gpgPath, gpgHome)
            .VerifyDetachedSignatureAsync(file + ".asc", file, CancellationToken.None)).State);
    }

    private static async Task GpgAutoVerifyOffPreservesSignerUid()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-no-verify-uid", "signed.bin", 1024);
        int verifyCalls = 0;
        var service = new GpgService(gpgPath, gpgHome, args => { if (args.Contains("--verify")) verifyCalls++; });
        OperationSummary result = await GenerateGpg(file, true, false, false, service);
        OperationResult signature = result.Results.Single(x => x.State == ResultState.Ok);
        Equal(gpgKey!.UserId, signature.SignerUid); Equal(gpgKey.Fingerprint, signature.Fingerprint); Equal(0, verifyCalls);
        Equal("生成成功 / 自動検証未実施", signature.Message);
    }
    private static async Task GpgOffSourceChange()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-no-verify-source-change", "changing.bin", 4096);
        bool touched = false;
        var service = new GpgService(gpgPath, gpgHome, args =>
        {
            if (!touched && args.Contains("--detach-sign"))
            {
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-20));
                touched = true;
            }
        });
        OperationSummary result = await GenerateGpg(file, true, false, false, service);
        Equal(1, result.Errors); True(!File.Exists(file + ".asc"), "changed source signature must not be committed");
        Equal(0, Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.ahvtmp-*", SearchOption.TopDirectoryOnly).Count());
    }

    private static async Task GpgOffCancellation()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-no-verify-cancel", "large.bin", 16 * 1024 * 1024);
        using var cts = new CancellationTokenSource();
        var service = new GpgService(gpgPath, gpgHome, args => { if (args.Contains("--detach-sign")) cts.Cancel(); });
        try { await GenerateGpg(file, true, false, false, service, cts.Token); }
        catch (OperationCanceledException) { }
        True(!File.Exists(file + ".asc"), "cancelled signature must not be committed");
        Equal(0, Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.ahvtmp-*", SearchOption.TopDirectoryOnly).Count());
    }

    private static async Task ManifestOpenPgpPolicies()
    {
        EnsureGpgFixture();
        string dir = NewDir();
        string source = Path.Combine(dir, "signed-manifest.bin");
        await File.WriteAllTextAsync(source, "manifest payload");
        var options = new GenerationOptions
        {
            BatchOutput = true, Hashes = [HashKind.Sha512], OpenPgpAscii = true, OpenPgpBinary = true,
            SigningFingerprint = gpgKey!.Fingerprint
        };
        OperationSummary generated = await new ProcessingCoordinator(new GpgService(gpgPath, gpgHome)).GenerateAsync(
            [source], options, AlwaysOverwrite, null, null, CancellationToken.None);
        Equal(4, generated.Ok);
        string manifest = Path.Combine(dir, "signed-manifest.bin.manifest");
        string asc = manifest + ".asc";
        string sig = manifest + ".sig";
        var trusted = new ProcessingCoordinator(new GpgService(gpgPath, gpgHome));
        Equal(3, (await trusted.VerifyAsync([manifest], null, null, CancellationToken.None)).Ok);
        Equal(3, (await trusted.VerifyAsync([asc], null, null, CancellationToken.None)).Ok);
        Equal(3, (await trusted.VerifyAsync([sig], null, null, CancellationToken.None)).Ok);
        Equal(3, (await trusted.VerifyAsync([manifest, asc, sig], null, null, CancellationToken.None)).Ok);

        string noKeyHome = Path.Combine(Root, "no-public-key-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(noKeyHome);
        var untrusted = new ProcessingCoordinator(new GpgService(gpgPath, noKeyHome));
        int callbacks = 0;
        OperationSummary yes = await untrusted.VerifyAsync([manifest, asc, sig], null, null, CancellationToken.None,
            null, () => { callbacks++; return Task.FromResult(true); });
        Equal(1, callbacks); True(yes.Results.Any(x => x.Message.Contains("ハッシュのみ検証", StringComparison.Ordinal)), "hash-only marker");
        True(yes.Results.Any(x => x.State == ResultState.Ok && x.ItemName == "manifest"), "hash verification after yes");
        callbacks = 0;
        OperationSummary no = await untrusted.VerifyAsync([manifest], null, null, CancellationToken.None,
            null, () => { callbacks++; return Task.FromResult(false); });
        Equal(1, callbacks); True(no.Results.Any(x => x.State == ResultState.Cancelled), "no must cancel manifest hash verification");
        OperationSummary closed = await untrusted.VerifyAsync([manifest], null, null, CancellationToken.None);
        True(closed.Results.Any(x => x.State == ResultState.Cancelled), "missing callback must fail closed");

        byte[] original = await File.ReadAllBytesAsync(sig);
        byte[] broken = original.ToArray(); broken[0] ^= 0x01; await File.WriteAllBytesAsync(sig, broken);
        callbacks = 0;
        OperationSummary bad = await trusted.VerifyAsync([manifest], null, null, CancellationToken.None,
            null, () => { callbacks++; return Task.FromResult(true); });
        Equal(0, callbacks); True(bad.Results.Any(x => x.State is ResultState.Ng or ResultState.Error), "bad signature must stop manifest verification");
        await File.WriteAllBytesAsync(sig, original);
    }
    private static async Task GpgCancellation()
    {
        EnsureGpgFixture();
        string file = await CreateFile("gpg-cancel", "signed.bin", 1024);
        await GenerateGpg(file, true, false);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        GpgVerifyResult result = await new GpgService(gpgPath, gpgHome).VerifyDetachedSignatureAsync(file + ".asc", file, cts.Token);
        Equal(GpgVerifyState.Cancelled, result.State);
    }

    private static async Task<OperationSummary> Generate(string file, IEnumerable<HashKind> hashes) =>
        await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).GenerateAsync([file],
            new GenerationOptions { BatchOutput = false, Hashes = hashes.ToHashSet() }, AlwaysOverwrite, null, null, CancellationToken.None);

    private static Task<OperationSummary> GenerateGpg(string file, bool asc, bool sig, bool autoVerify = true,
        GpgService? service = null, CancellationToken cancellationToken = default) =>
        new ProcessingCoordinator(service ?? new GpgService(gpgPath, gpgHome)).GenerateAsync([file],
            new GenerationOptions { BatchOutput = false, OpenPgpAscii = asc, OpenPgpBinary = sig, AutoVerifyGpgSignatures = autoVerify,
                SigningFingerprint = gpgKey!.Fingerprint }, AlwaysOverwrite, null, null, cancellationToken);

    private static Task<bool> AlwaysOverwrite(string _, ArtifactKind __, CancellationToken ___) => Task.FromResult(true);
    private static Task<OperationSummary> Verify(string file) => Verify([file]);
    private static Task<OperationSummary> Verify(IReadOnlyCollection<string> files) =>
        new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).VerifyAsync(files, null, null, CancellationToken.None);

    private static async Task<string> CreateFile(string folder, string name, int size)
    {
        string dir = Path.Combine(Root, folder + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name);
        await File.WriteAllBytesAsync(path, size == 0 ? [] : RandomNumberGenerator.GetBytes(size));
        return path;
    }

    private static string NewDir()
    {
        string path = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static object GetField(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static string StatusOutput(params string[] statuses) =>
        string.Join("\n", statuses.Select(x => "[GNUPG:] " + x));

    private static string ValidSigLine() =>
        $"VALIDSIG {new string('A', 40)} 20260815 0 0 4 0 22 8 00 {new string('B', 40)}";

    private static void AddSyntheticKey(List<string> lines, string validity, string capabilities, long expires,
        string primaryFingerprint, string uid, string? subCapabilities = null, string? subFingerprint = null)
    {
        lines.Add(ColonRecord("sec", validity, capabilities, expires, primaryFingerprint[..16]));
        lines.Add(ColonFingerprint(primaryFingerprint));
        lines.Add(ColonUid(uid + " <test@example.invalid>"));
        if (subCapabilities is not null && subFingerprint is not null)
        {
            lines.Add(ColonRecord("ssb", "u", subCapabilities, expires, subFingerprint[..16]));
            lines.Add(ColonFingerprint(subFingerprint));
        }
    }

    private static string ColonRecord(string type, string validity, string capabilities, long expires, string keyId)
    {
        var fields = new string[16];
        Array.Fill(fields, "");
        fields[0] = type;
        fields[1] = validity;
        fields[2] = "255";
        fields[3] = "22";
        fields[4] = keyId;
        fields[5] = "0";
        fields[6] = expires.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[11] = capabilities;
        fields[14] = "+";
        return string.Join(':', fields);
    }

    private static string ColonFingerprint(string fingerprint)
    {
        var fields = new string[11];
        Array.Fill(fields, "");
        fields[0] = "fpr";
        fields[9] = fingerprint;
        return string.Join(':', fields);
    }

    private static string ColonUid(string uid)
    {
        var fields = new string[11];
        Array.Fill(fields, "");
        fields[0] = "uid";
        fields[1] = "u";
        fields[9] = uid;
        return string.Join(':', fields);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException(failure.Message, failure);
    }

    private static void SetField(object target, string name, object? value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void EnsureGpgFixture() { True(gpgPath is not null && gpgHome is not null && gpgKey is not null, "GPG fixture unavailable"); }

    private static async Task<int> RunProcess(string executable, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        if (process.ExitCode != 0) Console.WriteLine(stderr.Result);
        return process.ExitCode;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected: {expected}; Actual: {actual}");
    }
    private static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T> { public void Report(T value) => action(value); }

    private sealed class CountingStream(Stream inner) : Stream
    {
        public long TotalBytesRead { get; private set; }
        public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => false;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush(); public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) { int n = inner.Read(buffer, offset, count); TotalBytesRead += n; return n; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { int n = await inner.ReadAsync(buffer, cancellationToken); TotalBytesRead += n; return n; }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); await base.DisposeAsync(); }
    }
}
