using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ArchiveHashVerifier;

public static partial class HashService
{
    private const int BufferSize = 1024 * 1024;

    public static async Task<IReadOnlyDictionary<HashKind, string>> ComputeAsync(
        Stream source,
        IReadOnlyCollection<HashKind> kinds,
        string sourcePath,
        long overallBefore,
        long overallLength,
        IProgress<FileReadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var hashes = kinds.Distinct().ToDictionary(k => k, k => IncrementalHash.CreateHash(HashCatalog.Get(k).Algorithm));
        byte[] buffer = GC.AllocateUninitializedArray<byte>(BufferSize);
        long fileLength = source.CanSeek ? source.Length : 0;
        long fileBytes = 0;
        long lastReport = Environment.TickCount64 - 250;

        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;

                foreach (IncrementalHash hash in hashes.Values)
                {
                    hash.AppendData(buffer, 0, read);
                }

                fileBytes += read;
                long now = Environment.TickCount64;
                if (now - lastReport >= 150 || fileBytes == fileLength)
                {
                    progress?.Report(new(fileBytes, fileLength, overallBefore + fileBytes, overallLength, sourcePath));
                    lastReport = now;
                }
            }

            progress?.Report(new(fileBytes, fileLength, overallBefore + fileBytes, overallLength, sourcePath));
            return hashes.ToDictionary(x => x.Key, x => Convert.ToHexString(x.Value.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            foreach (IncrementalHash hash in hashes.Values) hash.Dispose();
        }
    }

    public static string ReadExpectedHash(string checksumPath, HashKind kind)
    {
        HashSpec spec = HashCatalog.Get(kind);
        Regex exactToken = new($@"(?<![0-9A-Fa-f])[0-9A-Fa-f]{{{spec.HexLength}}}(?![0-9A-Fa-f])",
            RegexOptions.CultureInvariant);

        foreach (string line in File.ReadLines(checksumPath, Encoding.UTF8))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            Match match = exactToken.Match(trimmed);
            if (match.Success) return match.Value.ToLowerInvariant();
        }

        throw new InvalidDataException($"{spec.DisplayName} のハッシュ値を抽出できません。");
    }

    public static async Task WriteChecksumAsync(string path, string hash, string sourceFileName, CancellationToken cancellationToken)
    {
        string content = $"{hash.ToLowerInvariant()}  {sourceFileName}\r\n";
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }
}
