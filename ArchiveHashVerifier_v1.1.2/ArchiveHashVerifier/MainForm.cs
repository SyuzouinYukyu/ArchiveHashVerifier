using System.Diagnostics;

namespace ArchiveHashVerifier;

public sealed class MainForm : Form
{
    private const string AppTitle = "ArchiveHashVerifier v1.1.2";
    private readonly SettingsService settingsService = new(AppContext.BaseDirectory);
    private readonly AppSettings settings;
    private readonly GpgService gpg = new();
    private readonly ProcessingCoordinator coordinator;
    private readonly SplitContainer split = new();
    private readonly RadioButton verifyRadio = new() { Text = "検証", AutoSize = true, Checked = true };
    private readonly RadioButton generateRadio = new() { Text = "生成", AutoSize = true };
    private readonly FlowLayoutPanel generationPanel = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
    private readonly CheckBox sha512 = new() { Text = "SHA-512", AutoSize = true };
    private readonly CheckBox sha256 = new() { Text = "SHA-256", AutoSize = true };
    private readonly CheckBox sha3_512 = new() { Text = "SHA3-512", AutoSize = true };
    private readonly CheckBox sha3_256 = new() { Text = "SHA3-256", AutoSize = true };
    private readonly CheckBox pgpAsc = new() { Text = "OpenPGP ASCII Detached Signature (.asc)", AutoSize = true };
    private readonly CheckBox pgpSig = new() { Text = "OpenPGP Binary Detached Signature (.sig)", AutoSize = true };
    private readonly CheckBox autoVerifyGpg = new() { Text = "GPG署名生成後に自動検証する", AutoSize = true, Checked = true };
    private readonly Button keyButton = new() { Text = "GPG署名鍵を選択...", AutoSize = true };
    private readonly Label keyLabel = new() { AutoSize = true };
    private readonly ListBox inputList = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly Panel dropPanel = new() { Dock = DockStyle.Fill, AllowDrop = true, BackColor = Color.FromArgb(13, 38, 68), Padding = new Padding(10) };
    private readonly Label dropLabel = new() { Dock = DockStyle.Fill, AllowDrop = true, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
    private readonly Label progressLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Text = "待機中: 0%" };
    private readonly ProgressBar progressBar = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
    private readonly TextBox results = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };
    private readonly Button addFilesButton = new() { Text = "ファイルを選択...", AutoSize = true };
    private readonly Button addFolderButton = new() { Text = "フォルダーを選択...", AutoSize = true };
    private readonly Button clearInputsButton = new() { Text = "対象をクリア", AutoSize = true };
    private readonly Button startButton = new() { Text = "検証を開始", AutoSize = true };
    private readonly Button cancelButton = new() { Text = "キャンセル", AutoSize = true, Enabled = false };
    private readonly Button clearResultsButton = new() { Text = "結果をクリア", AutoSize = true };
    private readonly Button exitButton = new() { Text = "終了", AutoSize = true };
    private readonly OverwritePolicySession overwriteSession = new();
    private readonly Stopwatch operationWatch = new();
    private readonly CloseGate closeGate = new();
    private CancellationTokenSource? operationCts;
    private WheelMessageFilter? wheelFilter;
    private bool running;

    public MainForm()
    {
        settings = settingsService.Load();
        coordinator = new ProcessingCoordinator(gpg);
        InitializeComponent();
        RestoreSettings();
        UpdateModeUi();
        ApplyFontSize(settings.FontSize);
        AppendResult($"[起動] {AppTitle}");
    }

    private void InitializeComponent()
    {
        Text = AppTitle;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(900, 600);
        ClientSize = new Size(1100, 800);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var modePanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        DpiChanged += (_, _) => UpdateSplitterVisual();
        modePanel.Controls.Add(new Label { Text = "モード:", AutoSize = true, Margin = new Padding(3, 6, 10, 3) });
        modePanel.Controls.Add(verifyRadio);
        modePanel.Controls.Add(generateRadio);
        verifyRadio.CheckedChanged += (_, _) => UpdateModeUi();
        generateRadio.CheckedChanged += (_, _) => UpdateModeUi();

        generationPanel.Controls.AddRange([sha512, sha256, sha3_512, sha3_256, pgpAsc, pgpSig, autoVerifyGpg, keyButton, keyLabel]);
        keyButton.Click += KeyButton_Click;
        generationPanel.SizeChanged += (_, _) => keyLabel.MaximumSize = new Size(Math.Max(300, generationPanel.ClientSize.Width - 20), 0);

        var selectionButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        selectionButtons.Controls.AddRange([addFilesButton, addFolderButton, clearInputsButton]);
        addFilesButton.Click += AddFilesButton_Click;
        addFolderButton.Click += AddFolderButton_Click;
        clearInputsButton.Click += (_, _) => inputList.Items.Clear();

        dropLabel.Text = "ここへ対象ファイル・複数ファイル・フォルダーをD&D";
        dropPanel.Controls.Add(dropLabel);
        foreach (Control target in new Control[] { this, dropPanel, dropLabel, inputList })
        {
            target.AllowDrop = true;
            target.DragEnter += DragTarget_DragEnter;
            target.DragDrop += DragTarget_DragDrop;
        }

        var inputAndDrop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        inputAndDrop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        inputAndDrop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        inputAndDrop.Controls.Add(inputList, 0, 0);
        inputAndDrop.Controls.Add(dropPanel, 1, 0);

        var progressLayout = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        progressLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressLayout.Controls.Add(progressLabel, 0, 0);
        progressLayout.Controls.Add(progressBar, 0, 1);

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        actions.Controls.AddRange([startButton, cancelButton, clearResultsButton, exitButton]);
        startButton.Click += StartButton_Click;
        cancelButton.Click += (_, _) => operationCts?.Cancel();
        clearResultsButton.Click += (_, _) => results.Clear();
        exitButton.Click += (_, _) => Close();

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, RowCount = 6, Padding = new Padding(12) };
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.Controls.Add(modePanel, 0, 0);
        top.Controls.Add(generationPanel, 0, 1);
        top.Controls.Add(selectionButtons, 0, 2);
        top.Controls.Add(inputAndDrop, 0, 3);
        top.Controls.Add(progressLayout, 0, 4);
        top.Controls.Add(actions, 0, 5);

        results.BackColor = Color.FromArgb(18, 18, 18);
        results.ForeColor = Color.FromArgb(235, 235, 235);
        split.Dock = DockStyle.Fill;
        split.Orientation = Orientation.Horizontal;
        UpdateSplitterVisual();
        split.Panel1MinSize = 270;
        split.Panel2MinSize = 120;
        split.Panel1.Controls.Add(top);
        split.Panel2.Controls.Add(results);
        Controls.Add(split);

        Shown += MainForm_Shown;
        FormClosing += MainForm_FormClosing;
    }

    private void UpdateSplitterVisual()
    {
        split.SplitterWidth = Math.Max(6, LogicalToDeviceUnits(8));
    }

    private void RestoreSettings()
    {
        sha512.Checked = settings.GenerateSha512;
        sha256.Checked = settings.GenerateSha256;
        sha3_512.Checked = settings.GenerateSha3_512;
        sha3_256.Checked = settings.GenerateSha3_256;
        pgpAsc.Checked = settings.GenerateOpenPgpAscii;
        pgpSig.Checked = settings.GenerateOpenPgpBinary;
        autoVerifyGpg.Checked = settings.AutoVerifyGeneratedSignatures;
        UpdateKeyLabel();

        Rectangle fallback = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1100, 800);
        if (settings.WindowX is int x && settings.WindowY is int y && settings.WindowWidth is int width && settings.WindowHeight is int height)
        {
            Bounds = SettingsService.NormalizeBounds(new Rectangle(x, y, width, height),
                Screen.AllScreens.Select(s => s.WorkingArea).ToArray(), fallback);
        }
        else
        {
            Bounds = SettingsService.NormalizeBounds(new Rectangle(fallback.X + 50, fallback.Y + 50, 1100, 800), [fallback], fallback);
        }
        WindowState = settings.WindowState;
    }

    private void MainForm_Shown(object? sender, EventArgs e)
    {
        int max = Math.Max(split.Panel1MinSize, split.Height - split.Panel2MinSize - split.SplitterWidth);
        split.SplitterDistance = Math.Clamp(settings.SplitterDistance, split.Panel1MinSize, max);
        wheelFilter = new WheelMessageFilter(this);
        Application.AddMessageFilter(wheelFilter);
        TrySaveSettings(showWarning: true);
    }

    private void UpdateModeUi()
    {
        bool generate = generateRadio.Checked;
        generationPanel.Visible = generate;
        startButton.Text = generate ? "生成を開始" : "検証を開始";
        dropLabel.Text = generate
            ? "ここへ生成対象をD&D\r\n(既存のハッシュ・署名ファイルは自動除外)"
            : "ここへ検証対象をD&D\r\n(本体またはハッシュ/署名ファイル)";
    }

    private async void KeyButton_Click(object? sender, EventArgs e)
    {
        if (!gpg.IsAvailable) { using var missing = new GpgMissingDialog { Font = Font }; missing.ShowDialog(this); return; }
        try
        {
            UseWaitCursor = true;
            IReadOnlyList<GpgKey> keys = await gpg.ListUsableSecretKeysAsync(CancellationToken.None);
            UseWaitCursor = false;
            bool savedValid = settings.GpgFingerprint is not null && keys.Any(x => x.Fingerprint.Equals(settings.GpgFingerprint, StringComparison.OrdinalIgnoreCase));
            string? restored = savedValid ? settings.GpgFingerprint : null;
            if (!savedValid) settings.GpgFingerprint = null;
            using var dialog = new GpgKeySelectionDialog(keys, restored) { Font = Font };
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedKey is not null)
            {
                settings.GpgFingerprint = dialog.SelectedKey.Fingerprint;
                UpdateKeyLabel(dialog.SelectedKey.UserId);
            }
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "GPG鍵一覧エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { UseWaitCursor = false; }
    }

    private void UpdateKeyLabel(string? uid = null) => keyLabel.Text = settings.GpgFingerprint is null
        ? "署名鍵: 未選択"
        : $"署名鍵: {uid ?? "保存済み"} / {settings.GpgFingerprint}";

    private void AddFilesButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "対象ファイルを選択", Multiselect = true, CheckFileExists = true,
            InitialDirectory = GetInitialFolder() };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddInputs(dialog.FileNames);
    }

    private void AddFolderButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "対象フォルダーを選択", UseDescriptionForTitle = true,
            InitialDirectory = GetInitialFolder(), ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddInputs([dialog.SelectedPath]);
    }

    private string GetInitialFolder() => Directory.Exists(settings.LastFolder) ? settings.LastFolder! : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private void AddInputs(IEnumerable<string> paths)
    {
        var existing = inputList.Items.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths.Select(Path.GetFullPath)) if (existing.Add(path)) inputList.Items.Add(path);
        string? first = paths.FirstOrDefault();
        if (first is not null) settings.LastFolder = Directory.Exists(first) ? first : Path.GetDirectoryName(first);
    }

    private void DragTarget_DragEnter(object? sender, DragEventArgs e) => e.Effect = !running && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
        ? DragDropEffects.Copy : DragDropEffects.None;

    private void DragTarget_DragDrop(object? sender, DragEventArgs e)
    {
        if (!running && e.Data?.GetData(DataFormats.FileDrop) is string[] paths) AddInputs(paths);
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        if (running || closeGate.CloseRequested) return;
        string[] rawInputs = inputList.Items.Cast<string>().ToArray();
        if (rawInputs.Length == 0) { MessageBox.Show(this, "対象ファイルまたはフォルダーを追加してください。", AppTitle); return; }
        if (generateRadio.Checked && !sha512.Checked && !sha256.Checked && !sha3_512.Checked && !sha3_256.Checked && !pgpAsc.Checked && !pgpSig.Checked)
        { MessageBox.Show(this, "生成方式を1つ以上選択してください。", AppTitle); return; }

        bool generation = generateRadio.Checked;
        var discoveryErrors = new List<string>();
        IReadOnlyList<string> files = FileDiscovery.Expand(rawInputs, generation, discoveryErrors.Add);
        foreach (string error in discoveryErrors) AppendResult($"ERROR: {error}");
        if (files.Count == 0) { MessageBox.Show(this, "処理可能なファイルがありません。", AppTitle); return; }

        if (generation && (pgpAsc.Checked || pgpSig.Checked) && !gpg.IsAvailable)
        { using var missing = new GpgMissingDialog { Font = Font }; missing.ShowDialog(this); }

        overwriteSession.Clear();
        operationCts = new CancellationTokenSource();
        SetRunning(true);
        operationWatch.Restart();
        AppendResult("");
        AppendResult($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(generation ? "生成" : "検証")}開始: {files.Count} ファイル");
        var progress = new Progress<FileReadProgress>(UpdateProgress);
        var phase = new Progress<OperationPhase>(UpdatePhase);
        bool autoVerifyForReport = autoVerifyGpg.Checked;
        OperationSummary? summary = null;
        try
        {
            if (generation)
            {
                var options = new GenerationOptions
                {
                    Hashes = SelectedHashes(), OpenPgpAscii = pgpAsc.Checked, OpenPgpBinary = pgpSig.Checked,
                    AutoVerifyGpgSignatures = autoVerifyGpg.Checked,
                    SigningFingerprint = settings.GpgFingerprint
                };
                summary = await coordinator.GenerateAsync(files, options, ConfirmOverwriteAsync, progress, AppendResultThreadSafe, operationCts.Token, phase);
            }
            else
            {
                summary = await coordinator.VerifyAsync(files, progress, AppendResultThreadSafe, operationCts.Token, phase);
            }
        }
        catch (OperationCanceledException) { AppendResult("キャンセルされました。未完成の一時ファイルは破棄しました。"); }
        catch (Exception ex) { AppendResult($"ERROR: {ex.Message}"); }
        finally
        {
            operationWatch.Stop();
            bool closeAfterCleanup = closeGate.CloseRequested;
            if (summary is not null && !closeAfterCleanup) ShowSummary(summary, generation, files.Count, autoVerifyForReport);
            SetRunning(false);
            operationCts.Dispose();
            operationCts = null;
            if (closeAfterCleanup)
            {
                closeGate.MarkCleanupCompleted();
                BeginInvoke(Close);
            }
        }
    }

    private HashSet<HashKind> SelectedHashes()
    {
        var selected = new HashSet<HashKind>();
        if (sha512.Checked) selected.Add(HashKind.Sha512);
        if (sha256.Checked) selected.Add(HashKind.Sha256);
        if (sha3_512.Checked) selected.Add(HashKind.Sha3_512);
        if (sha3_256.Checked) selected.Add(HashKind.Sha3_256);
        return selected;
    }

    private Task<bool> ConfirmOverwriteAsync(string path, ArtifactKind kind, CancellationToken token)
    {
        if (overwriteSession.TryGet(kind, out bool decision)) return Task.FromResult(decision);
        if (InvokeRequired)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(() =>
            {
                try { tcs.SetResult(ConfirmOverwriteCore(path, kind)); } catch (Exception ex) { tcs.SetException(ex); }
            });
            token.Register(() => tcs.TrySetCanceled(token));
            return tcs.Task;
        }
        return Task.FromResult(ConfirmOverwriteCore(path, kind));
    }

    private bool ConfirmOverwriteCore(string path, ArtifactKind kind)
    {
        using var dialog = new OverwriteConfirmDialog(path) { Font = Font };
        bool yes = dialog.ShowDialog(this) == DialogResult.Yes;
        if (dialog.ApplyToSameKind) overwriteSession.Remember(kind, yes);
        return yes;
    }

    private void UpdateProgress(FileReadProgress p)
    {
        progressBar.Style = ProgressBarStyle.Continuous;
        int percent = ProgressMath.CalculatePercent(p.OverallBytes, p.OverallLength, operationComplete: false);
        double seconds = Math.Max(operationWatch.Elapsed.TotalSeconds, 0.001);
        double mbps = p.OverallBytes / seconds / 1024d / 1024d;
        double remaining = mbps > 0 && p.OverallLength > 0 ? (p.OverallLength - p.OverallBytes) / (mbps * 1024d * 1024d) : -1;
        string eta = remaining >= 0 ? TimeSpan.FromSeconds(remaining).ToString(@"hh\:mm\:ss") : "--:--:--";
        int filePercent = p.FileLength > 0 ? Math.Clamp((int)Math.Round(p.FileBytes * 100d / p.FileLength), 0, 100) : 100;
        progressBar.Value = percent;
        progressLabel.Text = $"{filePercent}% / 全体 {percent}% / {mbps:N1} MB/s / 残り {eta} / {Path.GetFileName(p.FilePath)}";
    }


    private void UpdatePhase(OperationPhase phase)
    {
        progressBar.Style = ProgressBarStyle.Marquee;
        progressLabel.Text = phase.Text;
    }
    private void ShowSummary(OperationSummary summary, bool generation, int targetCount, bool autoVerifyForReport)
    {
        foreach (OperationResult item in summary.Results)
        {
            string signer = item.Fingerprint is null ? "" : $" / Signer: {item.SignerUid ?? "(不明)"} / Fingerprint: {item.Fingerprint}";
            AppendResult($"{item.State.ToString().ToUpperInvariant()} [{item.ItemName}] {item.SourcePath}: {item.Message}{signer}");
        }
        string text = $"OK: {summary.Ok} / NG: {summary.Ng} / ERROR: {summary.Errors} / SKIP: {summary.Skipped} / CANCEL: {summary.Cancelled}";
        AppendResult($"[結果] {text} / 所要時間 {operationWatch.Elapsed:hh\\:mm\\:ss}");
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Value = 100;
        progressLabel.Text = $"{(generation ? "生成" : "検証")}完了: {text}";
        CompletionReport report = CompletionReport.Create(summary, generation, targetCount, operationWatch.Elapsed, autoVerifyForReport);
        using var dialog = new CompletionDialog(report, Font);
        dialog.ShowDialog(this);
    }

    private void SetRunning(bool value)
    {
        running = value;
        if (value)
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            progressLabel.Text = "処理準備中…";
        }
        bool interactive = !value && !closeGate.CloseRequested;
        startButton.Enabled = interactive;
        cancelButton.Enabled = value;
        addFilesButton.Enabled = addFolderButton.Enabled = clearInputsButton.Enabled = interactive;
        verifyRadio.Enabled = generateRadio.Enabled = generationPanel.Enabled = interactive;
        exitButton.Enabled = !closeGate.CloseRequested;
        dropLabel.Text = value ? "処理中です..." : dropLabel.Text;
        if (!value) UpdateModeUi();
    }

    private void AppendResultThreadSafe(string text)
    {
        if (InvokeRequired) BeginInvoke(() => AppendResult(text)); else AppendResult(text);
    }

    private void AppendResult(string text)
    {
        if (results.TextLength > 0) results.AppendText(Environment.NewLine);
        results.AppendText(text);
    }

    internal void ChangeFontByWheel(int delta)
    {
        float next = Math.Clamp(settings.FontSize + (delta > 0 ? 1F : -1F), 9F, 20F);
        if (Math.Abs(next - settings.FontSize) < 0.01F) return;
        settings.FontSize = next;
        ApplyFontSize(next);
    }

    private void ApplyFontSize(float size)
    {
        Font = new Font("Yu Gothic UI", size, FontStyle.Regular, GraphicsUnit.Point);
        ApplyFontRecursive(this, Font);
        dropLabel.Font = new Font(Font.FontFamily, Math.Min(20F, size + 4F), FontStyle.Bold, GraphicsUnit.Point);
        results.Font = new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);
        progressBar.MinimumSize = new Size(0, Math.Max(24, Font.Height + 8));
        PerformLayout();
    }

    private static void ApplyFontRecursive(Control root, Font font)
    {
        foreach (Control child in root.Controls)
        {
            child.Font = font;
            ApplyFontRecursive(child, font);
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!closeGate.CanClose(running))
        {
            e.Cancel = true;
            startButton.Enabled = cancelButton.Enabled = exitButton.Enabled = false;
            addFilesButton.Enabled = addFolderButton.Enabled = clearInputsButton.Enabled = false;
            verifyRadio.Enabled = generateRadio.Enabled = generationPanel.Enabled = false;
            dropLabel.Text = "終了準備中です…";
            progressLabel.Text = "キャンセルと一時ファイルのcleanup完了を待っています…";
            operationCts?.Cancel();
            return;
        }
        if (wheelFilter is not null) Application.RemoveMessageFilter(wheelFilter);
        Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        settings.WindowX = bounds.X; settings.WindowY = bounds.Y; settings.WindowWidth = bounds.Width; settings.WindowHeight = bounds.Height;
        settings.WindowState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
        settings.SplitterDistance = split.SplitterDistance;
        settings.GenerateSha512 = sha512.Checked; settings.GenerateSha256 = sha256.Checked;
        settings.GenerateSha3_512 = sha3_512.Checked; settings.GenerateSha3_256 = sha3_256.Checked;
        settings.GenerateOpenPgpAscii = pgpAsc.Checked; settings.GenerateOpenPgpBinary = pgpSig.Checked;
        settings.AutoVerifyGeneratedSignatures = autoVerifyGpg.Checked;
        TrySaveSettings(showWarning: true);
    }

    private void TrySaveSettings(bool showWarning)
    {
        try { settingsService.Save(settings); }
        catch (Exception ex)
        {
            if (showWarning) MessageBox.Show(this,
                $"EXEフォルダーへ設定を保存できません。\r\n他の場所へは保存しません。\r\n\r\n{settingsService.SettingsPath}\r\n{ex.Message}",
                "設定保存エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private sealed class WheelMessageFilter(MainForm owner) : IMessageFilter
    {
        private const int WmMouseWheel = 0x020A;
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WmMouseWheel || (Control.ModifierKeys & Keys.Control) != Keys.Control || !owner.Visible) return false;
            int delta = unchecked((short)((long)m.WParam >> 16));
            if (delta != 0) owner.ChangeFontByWheel(delta);
            return true;
        }
    }
}
