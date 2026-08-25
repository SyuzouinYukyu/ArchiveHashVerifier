using ArchiveHashVerifier;
using System.Security.Cryptography;
using System.Text;

internal static class V116TestCases
{
    public static async Task MixedStrictLegacyMustNotFallback()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ahv-v116-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string source = Path.Combine(dir, "a.bin");
        await File.WriteAllTextAsync(source, "payload");
        string digest = Convert.ToHexString(await SHA512.HashDataAsync(File.OpenRead(source))).ToLowerInvariant();
        string sidecar = source + ".sha512";
        await File.WriteAllTextAsync(sidecar, digest + "\r\n" + digest + "  other.bin\r\n", new UTF8Encoding(false));
        OperationSummary result = await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).VerifyAsync([sidecar], null, null, CancellationToken.None);
        Equal(0, result.Ok); True(result.Errors > 0);
        await File.WriteAllTextAsync(sidecar, digest + "  a.bin\r\n", new UTF8Encoding(false));
        Equal(1, (await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).VerifyAsync([sidecar], null, null, CancellationToken.None)).Ok);
        await File.WriteAllTextAsync(sidecar, digest, new UTF8Encoding(false));
        Equal(1, (await new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe")).VerifyAsync([sidecar], null, null, CancellationToken.None)).Ok);
    }
    private static void True(bool value) { if (!value) throw new InvalidOperationException("assert"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; actual {actual}"); }
}
