using System.Text.Json;

namespace ArchiveHashVerifier;

public sealed class AppSettings
{
    public float FontSize { get; set; } = 9F;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }
    public FormWindowState WindowState { get; set; } = FormWindowState.Normal;
    public int SplitterDistance { get; set; } = 430;
    public bool GenerateSha512 { get; set; } = true;
    public bool GenerateSha256 { get; set; }
    public bool GenerateSha3_512 { get; set; }
    public bool GenerateSha3_256 { get; set; }
    public bool GenerateOpenPgpAscii { get; set; } = true;
    public bool GenerateOpenPgpBinary { get; set; }
    public bool AutoVerifyGeneratedSignatures { get; set; } = true;
    public string? GpgFingerprint { get; set; }
    public string? LastFolder { get; set; }
}

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public SettingsService(string baseDirectory) => SettingsPath = Path.Combine(baseDirectory, FileDiscovery.SettingsFileName);
    public string SettingsPath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new();
            settings.FontSize = Math.Clamp(settings.FontSize, 9F, 20F);
            settings.SplitterDistance = Math.Max(100, settings.SplitterDistance);
            if (!string.IsNullOrWhiteSpace(settings.LastFolder) && !Directory.Exists(settings.LastFolder)) settings.LastFolder = null;
            settings.GpgFingerprint = NormalizeFingerprint(settings.GpgFingerprint);
            if (settings.WindowState == FormWindowState.Minimized) settings.WindowState = FormWindowState.Normal;
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string directory = Path.GetDirectoryName(SettingsPath)!;
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(SettingsPath)}.ahvtmp-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions), new System.Text.UTF8Encoding(false));
            File.Move(tempPath, SettingsPath, true);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public static Rectangle NormalizeBounds(Rectangle requested, IReadOnlyCollection<Rectangle> workingAreas, Rectangle fallback)
    {
        int width = Math.Max(900, requested.Width);
        int height = Math.Max(600, requested.Height);
        Rectangle resized = new(requested.X, requested.Y, width, height);
        if (workingAreas.Any(area => Rectangle.Intersect(area, resized).Width >= 100 && Rectangle.Intersect(area, resized).Height >= 100))
        {
            return resized;
        }

        return new Rectangle(fallback.X + 30, fallback.Y + 30,
            Math.Min(width, Math.Max(900, fallback.Width - 60)), Math.Min(height, Math.Max(600, fallback.Height - 60)));
    }

    public static string? NormalizeFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string compact = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return compact.Length >= 40 ? compact : null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
