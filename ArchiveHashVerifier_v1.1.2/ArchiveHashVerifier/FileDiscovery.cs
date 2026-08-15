namespace ArchiveHashVerifier;

public static class FileDiscovery
{
    public const string SettingsFileName = "ArchiveHashVerifier.settings.json";

    public static IReadOnlyList<string> Expand(IEnumerable<string> inputs, bool generationMode, Action<string>? error = null)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string input in inputs)
        {
            try
            {
                string fullPath = Path.GetFullPath(input);
                if (File.Exists(fullPath))
                {
                    if (!generationMode || !ShouldExcludeFromGeneration(fullPath)) files.Add(fullPath);
                }
                else if (Directory.Exists(fullPath))
                {
                    EnumerateDirectory(new DirectoryInfo(fullPath), generationMode, files, error);
                }
                else
                {
                    error?.Invoke($"見つかりません: {input}");
                }
            }
            catch (Exception ex)
            {
                error?.Invoke($"入力を処理できません: {input} ({ex.Message})");
            }
        }
        return files.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void EnumerateDirectory(DirectoryInfo directory, bool generationMode, HashSet<string> files, Action<string>? error)
    {
        try
        {
            foreach (FileSystemInfo item in directory.EnumerateFileSystemInfos())
            {
                try
                {
                    if ((item.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if (item is DirectoryInfo child)
                    {
                        EnumerateDirectory(child, generationMode, files, error);
                    }
                    else if (item is FileInfo file && (!generationMode || !ShouldExcludeFromGeneration(file.FullName)))
                    {
                        files.Add(file.FullName);
                    }
                }
                catch (Exception ex)
                {
                    error?.Invoke($"列挙できません: {item.FullName} ({ex.Message})");
                }
            }
        }
        catch (Exception ex)
        {
            error?.Invoke($"フォルダーを列挙できません: {directory.FullName} ({ex.Message})");
        }
    }

    public static bool ShouldExcludeFromGeneration(string path)
    {
        string name = Path.GetFileName(path);
        return HashCatalog.IsKnownSidecar(name) ||
               name.Equals(SettingsFileName, StringComparison.OrdinalIgnoreCase) ||
               name.Contains(".ahvtmp-", StringComparison.OrdinalIgnoreCase);
    }
}
