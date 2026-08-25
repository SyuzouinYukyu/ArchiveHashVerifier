using System.Text;
using System.Text.Json;

namespace ArchiveHashVerifier;

public enum ChecksumFileClassification { LegacySidecar, StrictChecksumList, MalformedStrictChecksumList }

public static class BatchArtifacts
{
    public const string DefaultBaseName = "ArchiveHashVerifier";
    public const string Manifest = DefaultBaseName + ".manifest";
    public static readonly IReadOnlyDictionary<HashKind, string> Names = new Dictionary<HashKind, string>
    {
        [HashKind.Sha512] = DefaultBaseName + ".sha512", [HashKind.Sha256] = DefaultBaseName + ".sha256",
        [HashKind.Sha3_512] = DefaultBaseName + ".sha3-512", [HashKind.Sha3_256] = DefaultBaseName + ".sha3-256",
        [HashKind.Blake3] = DefaultBaseName + ".blake3"
    };

    public static string PathFor(string directory, HashKind kind) => PathFor(directory, DefaultBaseName, kind);
    public static string PathFor(string directory, string baseName, HashKind kind) => Path.Combine(directory, baseName + HashCatalog.Get(kind).OutputExtension);
    public static string ManifestPathFor(string directory, string baseName) => Path.Combine(directory, baseName + ".manifest");
    public static bool IsManifestPath(string path) => path.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".manifest.asc", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".manifest.sig", StringComparison.OrdinalIgnoreCase);
    public static string ResolveManifestPath(string path) => path.EndsWith(".manifest.asc", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".manifest.sig", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;
    public static bool IsReservedName(string name) => IsGeneratedArtifactName(name);
    public static bool IsGeneratedArtifactName(string name) => HashCatalog.TryFromPath(name, out _) || IsManifestPath(name) ||
        name.EndsWith(".asc", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".sig", StringComparison.OrdinalIgnoreCase);

    public static bool TryGetBaseName(string fullPath, string requiredDirectory, out string baseName, out string error)
    {
        baseName = ""; error = "";
        string selectedDirectory;
        try { selectedDirectory = Path.GetDirectoryName(Path.GetFullPath(fullPath))!; }
        catch { error = "出力ファイル名が不正です。"; return false; }
        if (!selectedDirectory.Equals(Path.GetFullPath(requiredDirectory), StringComparison.OrdinalIgnoreCase)) { error = "一括出力ファイルは対象ファイルと同じフォルダーへ保存してください。"; return false; }
        string candidate = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(candidate) || candidate is "." or ".." || candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        { error = "共通ファイル名が不正です。"; return false; }
        if (HashCatalog.All.SelectMany(x => x.AcceptedExtensions).Append(".manifest").Append(".manifest.asc").Append(".manifest.sig")
            .Any(x => candidate.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
        { error = "拡張子を付けず共通ファイル名のみ入力してください。"; return false; }
        baseName = candidate; return true;
    }

    public static ChecksumFileClassification ClassifyChecksumFile(string checksumPath, HashSpec spec)
    {
        bool strictCandidate = false;
        foreach (string line in File.ReadLines(checksumPath, Encoding.UTF8).Where(x => x.Length > 0))
            if (line.Length >= spec.HexLength + 2 && line.AsSpan(spec.HexLength, 2).SequenceEqual("  ")) strictCandidate = true;
        if (!strictCandidate) return ChecksumFileClassification.LegacySidecar;
        try { _ = ParseChecksumList(checksumPath, spec); return ChecksumFileClassification.StrictChecksumList; }
        catch (InvalidDataException) { return ChecksumFileClassification.MalformedStrictChecksumList; }
    }
    public static IReadOnlyList<ChecksumListEntry> ParseChecksumList(string checksumPath, HashSpec spec)
    {
        var entries = new List<ChecksumListEntry>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadLines(checksumPath, Encoding.UTF8).Where(x => x.Length > 0))
        {
            if (line.Length < spec.HexLength + 3 || !line.AsSpan(spec.HexLength, 2).SequenceEqual("  ")) throw new InvalidDataException("一括Hash形式が不正です。");
            string hash = line[..spec.HexLength], file = line[(spec.HexLength + 2)..];
            if (hash.Length != spec.HexLength || !hash.All(Uri.IsHexDigit) || !IsSafeBasename(file) || !seen.Add(file)) throw new InvalidDataException("一括Hash entryが不正です。");
            entries.Add(new(file, hash.ToLowerInvariant()));
        }
        if (entries.Count == 0) throw new InvalidDataException("一括Hash entryがありません。");
        return entries;
    }

    public static bool TryReadEntry(string checksumPath, HashSpec spec, string name, out string? expected)
    {
        expected = ParseChecksumList(checksumPath, spec).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Hash;
        return expected is not null;
    }

    public static string ManifestText(IReadOnlyList<BatchManifestEntry> entries)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject(); writer.WriteString("format", "ArchiveHashVerifier Manifest"); writer.WriteNumber("version", 1); writer.WritePropertyName("files"); writer.WriteStartArray();
            foreach (BatchManifestEntry entry in entries.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WriteStartObject(); writer.WriteString("name", entry.Name); writer.WriteNumber("size", entry.Size); writer.WritePropertyName("hashes"); writer.WriteStartObject();
                foreach (HashKind kind in new[] { HashKind.Sha512, HashKind.Sha256, HashKind.Sha3_512, HashKind.Sha3_256, HashKind.Blake3 }) if (entry.Hashes.TryGetValue(kind, out string? hash)) writer.WriteString(HashCatalog.Get(kind).DisplayName, hash.ToLowerInvariant());
                writer.WriteEndObject(); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan).Replace("\n", "\r\n") + "\r\n";
    }

    public static IReadOnlyList<BatchManifestEntry> ParseManifest(string manifestPath)
    {
        JsonDocument doc; try { doc = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)); } catch (JsonException ex) { throw new InvalidDataException("manifest JSONが不正です。", ex); }
        using (doc)
        {
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 3 || !root.TryGetProperty("format", out var format) || format.GetString() != "ArchiveHashVerifier Manifest" || !root.TryGetProperty("version", out var version) || version.GetInt32() != 1 || !root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array) throw new InvalidDataException("manifest schemaが不正です。");
            var result = new List<BatchManifestEntry>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement item in files.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Count() != 3 || !item.TryGetProperty("name", out var nameEl) || !item.TryGetProperty("size", out var sizeEl) || !item.TryGetProperty("hashes", out var hashesEl)) throw new InvalidDataException("manifest file entryが不正です。");
                string name = nameEl.GetString() ?? throw new InvalidDataException("nameが不正です。");
                if (!IsSafeBasename(name) || !seen.Add(name) || !sizeEl.TryGetInt64(out long size) || size < 0 || hashesEl.ValueKind != JsonValueKind.Object) throw new InvalidDataException("manifest file entryが不正です。");
                var hashes = new Dictionary<HashKind, string>();
                foreach (JsonProperty p in hashesEl.EnumerateObject())
                {
                    HashSpec? spec = HashCatalog.All.FirstOrDefault(x => x.DisplayName == p.Name); string? hash = p.Value.GetString();
                    if (spec is null || hash is null || hash.Length != spec.HexLength || !hash.All(Uri.IsHexDigit) || !hashes.TryAdd(spec.Kind, hash.ToLowerInvariant())) throw new InvalidDataException("manifest hashが不正です。");
                }
                if (hashes.Count == 0 || !hashes.ContainsKey(HashKind.Sha512)) throw new InvalidDataException("manifestにはSHA-512が必要です。");
                result.Add(new(name, size, hashes));
            }
            if (result.Count == 0) throw new InvalidDataException("manifest filesが空です。");
            return result;
        }
    }

    public static bool IsSafeBasename(string name) => !string.IsNullOrWhiteSpace(name) && name == Path.GetFileName(name) && !Path.IsPathRooted(name) && name is not "." and not ".." && !name.Contains(':') && !name.Contains('/') && !name.Contains('\\');
}
public sealed record ChecksumListEntry(string Name, string Hash);
public sealed record BatchManifestEntry(string Name, long Size, IReadOnlyDictionary<HashKind, string> Hashes);
