using System.Security.Cryptography;

namespace ArchiveHashVerifier;

public enum AppMode { Verify, Generate }

public enum HashKind { Sha512, Sha256, Sha3_512, Sha3_256, Blake3 }

public enum ArtifactKind { Sha512, Sha256, Sha3_512, Sha3_256, Blake3, Manifest, OpenPgpAscii, OpenPgpBinary }

public enum ResultState { Ok, Ng, Error, Skipped, Cancelled }

public sealed record HashSpec(HashKind Kind, ArtifactKind ArtifactKind, string DisplayName,
    string OutputExtension, int HexLength, HashAlgorithmName? Algorithm, IReadOnlyList<string> AcceptedExtensions);

public static class HashCatalog
{
    public static readonly HashSpec[] All =
    [
        new(HashKind.Sha512, ArtifactKind.Sha512, "SHA-512", ".sha512", 128, HashAlgorithmName.SHA512, [".sha512", ".sha-512"]),
        new(HashKind.Sha256, ArtifactKind.Sha256, "SHA-256", ".sha256", 64, HashAlgorithmName.SHA256, [".sha256", ".sha-256"]),
        new(HashKind.Sha3_512, ArtifactKind.Sha3_512, "SHA3-512", ".sha3-512", 128, HashAlgorithmName.SHA3_512, [".sha3-512", ".sha3_512"]),
        new(HashKind.Sha3_256, ArtifactKind.Sha3_256, "SHA3-256", ".sha3-256", 64, HashAlgorithmName.SHA3_256, [".sha3-256", ".sha3_256"]),
        new(HashKind.Blake3, ArtifactKind.Blake3, "BLAKE3", ".blake3", 64, null, [".blake3"])
    ];

    public static HashSpec Get(HashKind kind) => All.First(x => x.Kind == kind);

    public static bool TryFromArtifact(ArtifactKind kind, out HashSpec? spec)
    {
        spec = All.FirstOrDefault(x => x.ArtifactKind == kind);
        return spec is not null;
    }

    public static bool TryFromPath(string path, out HashSpec? spec)
    {
        spec = All.FirstOrDefault(x => x.AcceptedExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
        return spec is not null;
    }

    public static bool IsKnownSidecar(string path) => TryFromPath(path, out _) ||
        path.EndsWith(".asc", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sig", StringComparison.OrdinalIgnoreCase);

    public static string RemoveSidecarExtension(string path)
    {
        HashSpec? spec = All.FirstOrDefault(x => x.AcceptedExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
        if (spec is not null)
        {
            string extension = spec.AcceptedExtensions.First(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
            return path[..^extension.Length];
        }

        if (path.EndsWith(".asc", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sig", StringComparison.OrdinalIgnoreCase))
        {
            return path[..^4];
        }

        return path;
    }
}

public sealed record FileReadProgress(long FileBytes, long FileLength, long OverallBytes, long OverallLength, string FilePath);
public sealed record OperationPhase(string Text);

public static class ProgressMath
{
    public static int CalculatePercent(long processed, long total, bool operationComplete)
    {
        if (operationComplete) return 100;
        if (total <= 0) return 0;
        return Math.Clamp((int)Math.Round(processed * 100d / total), 0, 99);
    }
}

public sealed class CloseGate
{
    public bool CloseRequested { get; private set; }
    public bool CleanupCompleted { get; private set; }

    public bool CanClose(bool operationRunning)
    {
        if (!operationRunning || CleanupCompleted) return true;
        CloseRequested = true;
        return false;
    }

    public void MarkCleanupCompleted() => CleanupCompleted = true;
}


public sealed record OperationResult(string SourcePath, string ItemName, ResultState State, string Message,
    string? SignerUid = null, string? Fingerprint = null);

public sealed class OperationSummary
{
    public List<OperationResult> Results { get; } = [];
    public int Ok => Results.Count(x => x.State == ResultState.Ok);
    public int Ng => Results.Count(x => x.State == ResultState.Ng);
    public int Errors => Results.Count(x => x.State == ResultState.Error);
    public int Skipped => Results.Count(x => x.State == ResultState.Skipped);
    public int Cancelled => Results.Count(x => x.State == ResultState.Cancelled);
}

public sealed record BatchOutputNameRequest(string Directory, string InitialName);

public sealed class GenerationOptions
{
    public HashSet<HashKind> Hashes { get; init; } = [];
    public bool OpenPgpAscii { get; init; }
    public bool OpenPgpBinary { get; init; }
    public bool AutoVerifyGpgSignatures { get; init; } = true;
    public string? SigningFingerprint { get; init; }
    public bool BatchOutput { get; init; } = true;
}

public sealed class OverwritePolicySession
{
    private readonly Dictionary<ArtifactKind, bool> decisions = [];
    public bool TryGet(ArtifactKind kind, out bool overwrite) => decisions.TryGetValue(kind, out overwrite);
    public void Remember(ArtifactKind kind, bool overwrite) => decisions[kind] = overwrite;
    public void Clear() => decisions.Clear();
}

public sealed record GpgKey(string UserId, string Name, string Email, string Algorithm, DateTimeOffset? Expires,
    string Fingerprint, IReadOnlyList<string> SigningFingerprints);

public enum GpgVerifyState { Valid, CreatedUnverified, Invalid, MissingPublicKey, Error, Cancelled }

public sealed record GpgVerifyResult(GpgVerifyState State, string Message, string? SignerUid = null,
    string? SigningFingerprint = null, string? PrimaryFingerprint = null);
