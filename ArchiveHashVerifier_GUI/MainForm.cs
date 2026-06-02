using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ArchiveHashVerifier_GUI;

public sealed class MainForm : Form
{
    private const string AppDisplayName = "ArchiveHashVerifier";
    private const string AppVersion = "1.0.5";
    private const string DropReadyText = "ここへ対象ファイルまたはハッシュファイルをD&Dしてください";
    private const string StatusReadyText = "待機中 / 対応: SHA-512 / SHA-256 / SHA3-512 / SHA3-256";

    private static readonly Regex Sha256Regex = new(@"\b[0-9A-Fa-f]{64}\b", RegexOptions.Compiled);
    private static readonly Regex Sha512Regex = new(@"\b[0-9A-Fa-f]{128}\b", RegexOptions.Compiled);
    private static readonly ChecksumFormat[] ChecksumFormats =
    [
        new("SHA3-512", [".sha3-512", ".sha3_512"], Sha512Regex),
        new("SHA-512", [".sha512"], Sha512Regex),
        new("SHA3-256", [".sha3-256", ".sha3_256"], Sha256Regex),
        new("SHA-256", [".sha256"], Sha256Regex)
    ];

    private readonly SplitContainer splitContainer = new();
    private readonly Panel dropPanel = new();
    private readonly Label dropLabel = new();
    private readonly Label statusLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly Label progressLabel = new();
    private readonly TextBox logTextBox = new();
    private readonly Button saveLogButton = new();
    private readonly Button clearLogButton = new();
    private readonly Button exitButton = new();

    private bool isVerifying;

    public MainForm()
    {
        InitializeComponent();
        ApplyApplicationIcon();
        AppendLogLine($"[起動] {AppDisplayName} v{AppVersion}");
        AppendLogLine("[起動] MainForm 初期化完了");
        AppendLogLine($"[待機] {DropReadyText}");
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = $"{AppDisplayName} v{AppVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        ClientSize = new Size(980, 720);
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Opacity = 1D;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Yu Gothic UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AllowDrop = true;

        splitContainer.Dock = DockStyle.Fill;
        splitContainer.Orientation = Orientation.Horizontal;
        splitContainer.SplitterWidth = 8;

        dropPanel.AllowDrop = true;
        dropPanel.Dock = DockStyle.Fill;
        dropPanel.BackColor = Color.FromArgb(13, 38, 68);
        dropPanel.BorderStyle = BorderStyle.FixedSingle;
        dropPanel.Padding = new Padding(18);

        dropLabel.AllowDrop = true;
        dropLabel.Dock = DockStyle.Fill;
        dropLabel.Text = DropReadyText;
        dropLabel.TextAlign = ContentAlignment.MiddleCenter;
        dropLabel.ForeColor = Color.White;
        dropLabel.Font = new Font("Yu Gothic UI", 20F, FontStyle.Bold, GraphicsUnit.Point);

        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 30;
        statusLabel.Text = StatusReadyText;
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        statusLabel.ForeColor = Color.FromArgb(206, 226, 245);
        statusLabel.Font = new Font("Yu Gothic UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        dropPanel.Controls.Add(dropLabel);
        dropPanel.Controls.Add(statusLabel);

        progressBar.Dock = DockStyle.Fill;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        progressBar.Style = ProgressBarStyle.Continuous;

        progressLabel.Dock = DockStyle.Fill;
        progressLabel.Text = "待機中: 0%";
        progressLabel.TextAlign = ContentAlignment.MiddleLeft;
        progressLabel.AutoEllipsis = true;

        var progressLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        progressLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        progressLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        progressLayout.Controls.Add(progressLabel, 0, 0);
        progressLayout.Controls.Add(progressBar, 0, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        saveLogButton.Text = "ログを保存";
        saveLogButton.AutoSize = true;
        saveLogButton.Click += SaveLogButton_Click;

        clearLogButton.Text = "ログをクリア";
        clearLogButton.AutoSize = true;
        clearLogButton.Click += (_, _) => logTextBox.Clear();

        exitButton.Text = "終了";
        exitButton.AutoSize = true;
        exitButton.Click += (_, _) => Close();

        buttonPanel.Controls.Add(saveLogButton);
        buttonPanel.Controls.Add(clearLogButton);
        buttonPanel.Controls.Add(exitButton);

        var topLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        topLayout.Controls.Add(dropPanel, 0, 0);
        topLayout.Controls.Add(progressLayout, 0, 1);
        topLayout.Controls.Add(buttonPanel, 0, 2);

        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Multiline = true;
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Both;
        logTextBox.WordWrap = false;
        logTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
        logTextBox.BackColor = Color.FromArgb(18, 18, 18);
        logTextBox.ForeColor = Color.FromArgb(235, 235, 235);

        splitContainer.Panel1.Controls.Add(topLayout);
        splitContainer.Panel2.Controls.Add(logTextBox);
        Controls.Add(splitContainer);

        DragEnter += DragTarget_DragEnter;
        DragDrop += DragTarget_DragDrop;
        dropPanel.DragEnter += DragTarget_DragEnter;
        dropPanel.DragDrop += DragTarget_DragDrop;
        dropLabel.DragEnter += DragTarget_DragEnter;
        dropLabel.DragDrop += DragTarget_DragDrop;
        Shown += (_, _) => InitializeSplitterDistance();

        ResumeLayout(false);
    }

    private void InitializeSplitterDistance()
    {
        try
        {
            int totalHeight = splitContainer.Height;
            int splitterWidth = splitContainer.SplitterWidth;
            int panel1Min = 190;
            int panel2Min = 160;

            if (totalHeight <= panel1Min + panel2Min + splitterWidth)
            {
                return;
            }

            splitContainer.Panel1MinSize = panel1Min;
            splitContainer.Panel2MinSize = panel2Min;
            int desired = Math.Min(300, totalHeight - panel2Min - splitterWidth);
            splitContainer.SplitterDistance = Math.Max(panel1Min, desired);
        }
        catch (Exception ex)
        {
            AppendLogLine($"[起動] SplitContainer 初期化失敗: {ex.Message}");
        }
    }

    private void ApplyApplicationIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(Application.StartupPath, "app.ico");
            }

            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
                AppendLogLine($"[起動] アイコン読込: {iconPath}");
                return;
            }

            Icon? embeddedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (embeddedIcon is not null)
            {
                Icon = embeddedIcon;
                AppendLogLine("[起動] EXE埋め込みアイコンを使用");
            }
            else
            {
                AppendLogLine("[起動] アイコンが見つかりません。標準アイコンで起動します。");
            }
        }
        catch (Exception ex)
        {
            AppendLogLine($"[起動] アイコン読込失敗: {ex.Message}");
        }
    }

    private void DragTarget_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void DragTarget_DragDrop(object? sender, DragEventArgs e)
    {
        if (isVerifying)
        {
            AppendLogLine("検証中のため、新しいD&Dは無視しました。");
            return;
        }

        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            return;
        }

        await RunVerificationAsync(paths);
    }

    private async Task RunVerificationAsync(IReadOnlyCollection<string> droppedPaths)
    {
        isVerifying = true;
        SetUiVerifying(true);
        UpdateProgress(new ProgressInfo(0, "検証準備中..."));

        var progress = new Progress<ProgressInfo>(UpdateProgress);
        var logProgress = new Progress<string>(AppendLogLine);
        var stopwatch = Stopwatch.StartNew();
        VerificationSummary summary;

        try
        {
            summary = await Task.Run(() => VerifyDroppedPaths(droppedPaths, logProgress, progress));
        }
        catch (Exception ex)
        {
            summary = new VerificationSummary { Errors = 1 };
            AppendLogLine($"ERROR : 予期しないエラーが発生しました: {ex.Message}");
        }

        stopwatch.Stop();
        UpdateProgress(new ProgressInfo(100, "検証完了: 100%"));

        string elapsed = FormatDuration(stopwatch.Elapsed);
        string resultMessage = BuildResultMessage(summary);
        AppendLogLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 検証終了");
        AppendLogLine($"所要時間: {elapsed}");
        AppendLogLine($"[結果] {resultMessage.Replace(Environment.NewLine, " ")}");
        AppendLogLine($"OK: {summary.Ok} 件 / NG: {summary.Ng} 件 / ERROR: {summary.Errors} 件");

        MessageBox.Show(
            this,
            $"{resultMessage}{Environment.NewLine}{Environment.NewLine}OK: {summary.Ok} 件{Environment.NewLine}NG: {summary.Ng} 件{Environment.NewLine}ERROR: {summary.Errors} 件{Environment.NewLine}所要時間: {elapsed}",
            "検証完了",
            MessageBoxButtons.OK,
            summary.Ng == 0 && summary.Errors == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        SetUiVerifying(false);
        isVerifying = false;
    }

    private VerificationSummary VerifyDroppedPaths(
        IReadOnlyCollection<string> droppedPaths,
        IProgress<string> logProgress,
        IProgress<ProgressInfo> progress)
    {
        var summary = new VerificationSummary();
        List<string> targets = ExpandDroppedPaths(droppedPaths, logProgress);
        var pairs = new List<VerificationPair>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        logProgress.Report("");
        logProgress.Report($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 検証開始");
        logProgress.Report($"D&D入力: {droppedPaths.Count} 件");

        foreach (string path in targets)
        {
            try
            {
                VerificationPair pair = ResolveVerificationPair(path);
                string key = $"{pair.FilePath}|{pair.Algorithm}";
                if (seen.Add(key))
                {
                    pairs.Add(pair);
                }
            }
            catch (VerificationResolveException ex)
            {
                summary.Errors++;
                logProgress.Report($"ERROR [{ex.Algorithm}] : {path}");
                logProgress.Report($"理由: {ex.Message}");
            }
            catch (Exception ex)
            {
                summary.Errors++;
                logProgress.Report($"ERROR : {path}");
                logProgress.Report($"理由: {ex.Message}");
            }
        }

        logProgress.Report($"検証対象: {pairs.Count} 件");

        long grandTotalBytes = GetGrandTotalBytes(pairs);
        var context = new ProgressContext(pairs.Count, grandTotalBytes);

        for (int i = 0; i < pairs.Count; i++)
        {
            VerifyPair(pairs[i], i + 1, summary, context, logProgress, progress);
        }

        return summary;
    }

    private static List<string> ExpandDroppedPaths(IReadOnlyCollection<string> droppedPaths, IProgress<string> progress)
    {
        var files = new List<string>();

        foreach (string path in droppedPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    files.AddRange(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));
                }
                else if (File.Exists(path))
                {
                    files.Add(path);
                }
                else
                {
                    progress.Report($"ERROR : {path}");
                    progress.Report("理由: ファイルまたはフォルダが見つかりません。");
                }
            }
            catch (Exception ex)
            {
                progress.Report($"ERROR : {path}");
                progress.Report($"理由: フォルダの列挙に失敗しました: {ex.Message}");
            }
        }

        return files;
    }

    private static VerificationPair ResolveVerificationPair(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("ファイルが見つかりません。", fullPath);
        }

        string directory = Path.GetDirectoryName(fullPath) ?? "";
        string fileName = Path.GetFileName(fullPath);
        string extension = Path.GetExtension(fullPath).ToLowerInvariant();

        ChecksumFormat? droppedChecksumFormat = FindChecksumFormatByExtension(extension);
        if (droppedChecksumFormat is not null)
        {
            string bodyName = Path.GetFileNameWithoutExtension(fileName);
            string candidate = Path.Combine(directory, bodyName);
            if (File.Exists(candidate))
            {
                return new VerificationPair(candidate, fullPath, droppedChecksumFormat.Algorithm);
            }

            throw new VerificationResolveException(droppedChecksumFormat.Algorithm, "対応する本体ファイルが見つかりません。");
        }

        foreach (ChecksumFormat format in ChecksumFormats)
        {
            foreach (string candidate in BuildChecksumCandidates(directory, fileName, format.Extensions))
            {
                if (File.Exists(candidate))
                {
                    return new VerificationPair(fullPath, candidate, format.Algorithm);
                }
            }
        }

        throw new InvalidOperationException("対応する .sha512 / .sha256 / .sha3-512 / .sha3-256 ファイルが見つかりません。");
    }

    private static ChecksumFormat? FindChecksumFormatByExtension(string extension)
    {
        foreach (ChecksumFormat format in ChecksumFormats)
        {
            if (format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return format;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildChecksumCandidates(string directory, string fileName, IReadOnlyList<string> extensions)
    {
        foreach (string extension in extensions)
        {
            yield return Path.Combine(directory, fileName + extension);
            yield return Path.Combine(directory, Path.ChangeExtension(fileName, extension.TrimStart('.')));
        }
    }

    private static long GetGrandTotalBytes(IEnumerable<VerificationPair> pairs)
    {
        long total = 0;
        foreach (VerificationPair pair in pairs)
        {
            try
            {
                total += new FileInfo(pair.FilePath).Length;
            }
            catch
            {
                return 0;
            }
        }

        return total;
    }

    private static void VerifyPair(
        VerificationPair pair,
        int index,
        VerificationSummary summary,
        ProgressContext context,
        IProgress<string> logProgress,
        IProgress<ProgressInfo> progress)
    {
        try
        {
            logProgress.Report("");
            logProgress.Report($"[{pair.Algorithm}] 対象: {pair.FilePath}");
            logProgress.Report($"[{pair.Algorithm}] ハッシュ: {pair.ChecksumPath}");
            logProgress.Report($"[{pair.Algorithm}] アルゴリズム: {pair.Algorithm}");

            string expected = ReadExpectedHash(pair.ChecksumPath, pair.Algorithm);
            string actual = ComputeHash(pair, index, context, progress);

            logProgress.Report($"[{pair.Algorithm}] 期待: {expected}");
            logProgress.Report($"[{pair.Algorithm}] 実測: {actual}");

            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                summary.Ok++;
                logProgress.Report($"OK [{pair.Algorithm}] : {pair.FilePath}");
            }
            else
            {
                summary.Ng++;
                logProgress.Report($"NG [{pair.Algorithm}] : {pair.FilePath}");
                logProgress.Report("理由: ハッシュ不一致。");
            }
        }
        catch (Exception ex)
        {
            summary.Errors++;
            logProgress.Report($"ERROR [{pair.Algorithm}] : {pair.FilePath}");
            logProgress.Report($"理由: {ex.Message}");
        }
    }

    private static string ReadExpectedHash(string checksumPath, string algorithm)
    {
        Regex regex = GetChecksumFormat(algorithm).HashRegex;

        foreach (string line in File.ReadLines(checksumPath, Encoding.UTF8))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            Match match = regex.Match(trimmed);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }
        }

        throw new InvalidOperationException($"{algorithm} のハッシュ値を抽出できません。");
    }

    private static string ComputeHash(
        VerificationPair pair,
        int index,
        ProgressContext context,
        IProgress<ProgressInfo> progress)
    {
        HashAlgorithmName name = GetHashAlgorithmName(pair.Algorithm);
        using var hash = IncrementalHash.CreateHash(name);
        using var stream = new FileStream(pair.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);

        byte[] buffer = new byte[1024 * 1024];
        long fileTotal = stream.Length;
        long fileProcessed = 0;
        long grandBeforeFile = context.ProcessedBytes;
        var stopwatch = Stopwatch.StartNew();
        long lastReportMs = -250;
        int read;

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            fileProcessed += read;
            context.ProcessedBytes = grandBeforeFile + fileProcessed;

            if (stopwatch.ElapsedMilliseconds - lastReportMs >= 150 || fileProcessed == fileTotal)
            {
                progress.Report(BuildProgressInfo(pair, index, context, fileProcessed, fileTotal));
                lastReportMs = stopwatch.ElapsedMilliseconds;
            }
        }

        context.ProcessedBytes = grandBeforeFile + fileProcessed;
        context.CompletedFiles++;
        progress.Report(BuildProgressInfo(pair, index, context, fileProcessed, fileTotal));

        return Convert.ToHexString(hash.GetHashAndReset()).ToUpperInvariant();
    }

    private static ChecksumFormat GetChecksumFormat(string algorithm)
    {
        foreach (ChecksumFormat format in ChecksumFormats)
        {
            if (string.Equals(format.Algorithm, algorithm, StringComparison.OrdinalIgnoreCase))
            {
                return format;
            }
        }

        throw new InvalidOperationException($"未対応のハッシュ形式です: {algorithm}");
    }

    private static HashAlgorithmName GetHashAlgorithmName(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA-512" => HashAlgorithmName.SHA512,
            "SHA-256" => HashAlgorithmName.SHA256,
            "SHA3-512" => HashAlgorithmName.SHA3_512,
            "SHA3-256" => HashAlgorithmName.SHA3_256,
            _ => throw new InvalidOperationException($"未対応のハッシュ形式です: {algorithm}")
        };
    }

    private static ProgressInfo BuildProgressInfo(
        VerificationPair pair,
        int index,
        ProgressContext context,
        long fileProcessed,
        long fileTotal)
    {
        int overallPercent;
        if (context.GrandTotalBytes > 0)
        {
            overallPercent = ClampPercent((double)context.ProcessedBytes / context.GrandTotalBytes * 100D);
        }
        else if (context.TotalFiles > 0)
        {
            overallPercent = ClampPercent((double)context.CompletedFiles / context.TotalFiles * 100D);
        }
        else
        {
            overallPercent = 0;
        }

        int filePercent = fileTotal > 0 ? ClampPercent((double)fileProcessed / fileTotal * 100D) : overallPercent;
        double elapsedSeconds = Math.Max(context.Elapsed.TotalSeconds, 0.001D);
        double mbps = context.ProcessedBytes / elapsedSeconds / 1024D / 1024D;
        string eta = FormatEta(context.GrandTotalBytes, context.ProcessedBytes, mbps);
        string fileName = Path.GetFileName(pair.FilePath);
        string text = $"検証中: {index} / {context.TotalFiles} 件目 - {filePercent}% / 全体 {overallPercent}% / {FormatBytes(context.ProcessedBytes)} 読み込み済み / {mbps:N1} MB/s / 残り {eta} / {fileName}";

        return new ProgressInfo(overallPercent, text);
    }

    private static string FormatEta(long totalBytes, long processedBytes, double mbps)
    {
        if (totalBytes <= 0 || processedBytes <= 0 || mbps <= 0)
        {
            return "--:--:--";
        }

        double remainingBytes = Math.Max(0, totalBytes - processedBytes);
        double remainingSeconds = remainingBytes / (mbps * 1024D * 1024D);
        return FormatDuration(TimeSpan.FromSeconds(remainingSeconds));
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024D;
        const double mb = kb * 1024D;
        const double gb = mb * 1024D;

        return bytes >= gb
            ? $"{bytes / gb:N2} GB"
            : $"{bytes / mb:N2} MB";
    }

    private static int ClampPercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(100, (int)Math.Round(value)));
    }

    private void SetUiVerifying(bool verifying)
    {
        dropLabel.Text = verifying ? "検証中です..." : DropReadyText;
        statusLabel.Text = verifying ? "しばらくお待ちください" : StatusReadyText;
        saveLogButton.Enabled = !verifying;
        clearLogButton.Enabled = !verifying;
    }

    private void UpdateProgress(ProgressInfo info)
    {
        progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, info.Percent));
        progressLabel.Text = info.Text;
    }

    private void AppendLogLine(string message)
    {
        if (logTextBox.TextLength > 0)
        {
            logTextBox.AppendText(Environment.NewLine);
        }

        logTextBox.AppendText(message);
    }

    private void SaveLogButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "ログを保存",
            Filter = "テキストファイル (*.txt)|*.txt|ログファイル (*.log)|*.log|すべてのファイル (*.*)|*.*",
            FileName = $"ArchiveHashVerifier_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, logTextBox.Text, new UTF8Encoding(false));
    }

    private static string BuildResultMessage(VerificationSummary summary)
    {
        if (summary.Ng > 0 && summary.Errors > 0)
        {
            return "異常または未検証の項目があります。" + Environment.NewLine + "ログを確認してください。";
        }

        if (summary.Ng > 0)
        {
            return "異常を検出しました。" + Environment.NewLine +
                "ハッシュ値が一致しないファイルがあります。" + Environment.NewLine + Environment.NewLine +
                "ファイルの破損、ダウンロード不完全、または改ざんの可能性があります。" + Environment.NewLine +
                "ログを確認してください。";
        }

        if (summary.Errors > 0)
        {
            return "検証できなかったファイルがあります。" + Environment.NewLine +
                "ハッシュファイル不足、対応する本体ファイル不足、読み取りエラー等の可能性があります。" + Environment.NewLine +
                "ログを確認してください。";
        }

        return "正常終了。" + Environment.NewLine +
            "すべての対象ファイルでハッシュ値が一致しました。" + Environment.NewLine + Environment.NewLine +
            "この検証範囲では、ファイルの破損・ダウンロード不完全・改ざんの形跡は認められませんでした。";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00}",
            (int)duration.TotalHours,
            duration.Minutes,
            duration.Seconds);
    }

    private sealed record VerificationPair(string FilePath, string ChecksumPath, string Algorithm);

    private sealed record ChecksumFormat(string Algorithm, IReadOnlyList<string> Extensions, Regex HashRegex);

    private sealed record ProgressInfo(int Percent, string Text);

    private sealed class VerificationSummary
    {
        public int Ok { get; set; }
        public int Ng { get; set; }
        public int Errors { get; set; }
    }

    private sealed class ProgressContext
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public ProgressContext(int totalFiles, long grandTotalBytes)
        {
            TotalFiles = totalFiles;
            GrandTotalBytes = grandTotalBytes;
        }

        public int TotalFiles { get; }
        public int CompletedFiles { get; set; }
        public long GrandTotalBytes { get; }
        public long ProcessedBytes { get; set; }
        public TimeSpan Elapsed => stopwatch.Elapsed;
    }

    private sealed class VerificationResolveException : Exception
    {
        public VerificationResolveException(string algorithm, string message)
            : base(message)
        {
            Algorithm = algorithm;
        }

        public string Algorithm { get; }
    }
}
