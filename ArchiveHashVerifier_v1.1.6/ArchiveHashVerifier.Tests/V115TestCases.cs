using ArchiveHashVerifier;
using System.Security.Cryptography;
using System.Text;

internal static class V115TestCases
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ahv-v115-" + Guid.NewGuid().ToString("N"));

    public static async Task SingleAndMultipleBatchNames()
    {
        string dir = NewDir();
        string zip = await FileAt(dir, "ClariS.zip", "zip");
        OperationSummary one = await Generate([zip], [HashKind.Sha512, HashKind.Blake3]);
        Equal(2, one.Ok); Exists(Path.Combine(dir, "ClariS.zip.sha512")); Exists(Path.Combine(dir, "ClariS.zip.blake3"));
        var verify = new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe"));
        Equal(2, (await verify.VerifyAsync([Path.Combine(dir, "ClariS.zip.sha512"), Path.Combine(dir, "ClariS.zip.blake3")], null, null, CancellationToken.None)).Ok);
        string seven = await FileAt(dir, "ClariS.7z", "seven");
        await Generate([seven], [HashKind.Sha512]);
        Exists(Path.Combine(dir, "ClariS.7z.sha512"));

        int calls = 0;
        OperationSummary multiple = await Generate([zip, seven], [HashKind.Sha512, HashKind.Blake3], (r, _) => { calls++; return Task.FromResult<string?>(Path.Combine(r.Directory, "ClariS_Complete")); });
        Equal(1, calls); Equal(2, multiple.Ok); Exists(Path.Combine(dir, "ClariS_Complete.sha512")); Exists(Path.Combine(dir, "ClariS_Complete.blake3"));
        Equal(4, (await verify.VerifyAsync([Path.Combine(dir, "ClariS_Complete.sha512"), Path.Combine(dir, "ClariS_Complete.blake3")], null, null, CancellationToken.None)).Ok);
    }

    public static async Task OutputNameCancelAndArbitraryManifest()
    {
        string dir = NewDir(); string a = await FileAt(dir, "a.bin", "a"); string b = await FileAt(dir, "b.bin", "b");
        int overwrite = 0;
        OperationSummary cancelled = await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).GenerateAsync([a, b], new GenerationOptions { BatchOutput = true, Hashes = [HashKind.Sha512] }, (_, _, _) => { overwrite++; return Task.FromResult(true); }, null, null, CancellationToken.None, null, (_, _) => Task.FromResult<string?>(null));
        Equal(1, cancelled.Cancelled); Equal(0, overwrite); Missing(Path.Combine(dir, "ArchiveHashVerifier.sha512"));
        True(BatchArtifacts.TryGetBaseName(Path.Combine(dir, "ClariS.2026"), dir, out string name, out _)); Equal("ClariS.2026", name);
        True(!BatchArtifacts.TryGetBaseName(Path.Combine(dir, "bad.sha512"), dir, out _, out _));
        byte[] bytes = await SHA512.HashDataAsync(File.OpenRead(a));
        string manifest = Path.Combine(dir, "Foo.manifest");
        var entries = new[] { new BatchManifestEntry("a.bin", 1, new Dictionary<HashKind, string> { [HashKind.Sha512] = Convert.ToHexString(bytes).ToLowerInvariant() }) };
        await File.WriteAllTextAsync(manifest, BatchArtifacts.ManifestText(entries), new UTF8Encoding(false));
        Equal(1, (await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).VerifyAsync([manifest], null, null, CancellationToken.None)).Ok);
    }

    private static Task<OperationSummary> Generate(string[] files, HashKind[] kinds, Func<BatchOutputNameRequest, CancellationToken, Task<string?>>? selector = null) =>
        new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).GenerateAsync(files, new GenerationOptions { BatchOutput = true, Hashes = kinds.ToHashSet() }, (_, _, _) => Task.FromResult(true), null, null, CancellationToken.None, null, selector);
    private static string NewDir() { string dir = Path.Combine(Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); return dir; }
    private static async Task<string> FileAt(string dir, string name, string value) { string path = Path.Combine(dir, name); await File.WriteAllTextAsync(path, value); return path; }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; actual {actual}"); }
    private static void Exists(string path) { if (!File.Exists(path)) throw new InvalidOperationException("missing: " + path); }
    private static void Missing(string path) { if (File.Exists(path)) throw new InvalidOperationException("unexpected: " + path); }
    private static void True(bool value) { if (!value) throw new InvalidOperationException("assert"); }
}
