namespace ArchiveHashVerifier;

public sealed class OverwriteConfirmDialog : Form
{
    private readonly CheckBox applyCheckBox = new() { Text = "以降の同種ファイルにもこの選択を適用する", AutoSize = true };
    public bool ApplyToSameKind => applyCheckBox.Checked;

    public OverwriteConfirmDialog(string path)
    {
        Text = "上書き確認";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var message = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            Text = $"既に「{Path.GetFileName(path)}」が存在します。\r\n処理を続行してファイルを上書きしますか？"
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var no = new Button { Text = "いいえ", AutoSize = true, DialogResult = DialogResult.No };
        var yes = new Button { Text = "はい", AutoSize = true, DialogResult = DialogResult.Yes };
        buttons.Controls.Add(no);
        buttons.Controls.Add(yes);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 3, Dock = DockStyle.Fill };
        layout.Controls.Add(message, 0, 0);
        layout.Controls.Add(applyCheckBox, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = yes;
        CancelButton = no;
    }
}

public sealed class GpgKeySelectionDialog : Form
{
    private readonly ListView list = new();
    public GpgKey? SelectedKey => list.SelectedItems.Count == 1 ? (GpgKey)list.SelectedItems[0].Tag! : null;

    public GpgKeySelectionDialog(IReadOnlyList<GpgKey> keys, string? selectedFingerprint)
    {
        Text = "GPG署名鍵の選択";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 420);
        MinimumSize = new Size(700, 320);

        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.MultiSelect = false;
        list.HideSelection = false;
        list.Columns.Add("User ID / 鍵名", 250);
        list.Columns.Add("メールアドレス", 180);
        list.Columns.Add("アルゴリズム", 100);
        list.Columns.Add("有効期限", 120);
        list.Columns.Add("Fingerprint", 330);
        foreach (GpgKey key in keys)
        {
            var item = new ListViewItem([key.Name, key.Email, key.Algorithm,
                key.Expires?.ToLocalTime().ToString("yyyy-MM-dd") ?? "無期限", key.Fingerprint]) { Tag = key };
            list.Items.Add(item);
            if (string.Equals(key.Fingerprint, selectedFingerprint, StringComparison.OrdinalIgnoreCase)) item.Selected = true;
        }

        var explanation = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "現在有効で、署名可能な秘密鍵のみ表示しています。パスフレーズ入力は GnuPG Pinentry が行います。"
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "キャンセル", AutoSize = true, DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "選択", AutoSize = true, DialogResult = DialogResult.OK, Enabled = list.SelectedItems.Count == 1 };
        list.SelectedIndexChanged += (_, _) => ok.Enabled = list.SelectedItems.Count == 1;
        list.DoubleClick += (_, _) => { if (ok.Enabled) DialogResult = DialogResult.OK; };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(explanation, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

public sealed class GpgMissingDialog : Form
{
    public GpgMissingDialog()
    {
        Text = "GPGが必要です";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        var label = new Label { AutoSize = true, Text = "GPGがインストールされていません。\r\n処理を続行するためGPGをインストールして下さい。" };
        var link = new LinkLabel { AutoSize = true, Text = "https://gpg4win.org/download.html" };
        link.LinkClicked += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.Text) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "ブラウザーを開けません", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right };
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 3, Dock = DockStyle.Fill };
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(link, 0, 1);
        layout.Controls.Add(ok, 0, 2);
        Controls.Add(layout);
        AcceptButton = ok;
    }
}

public sealed class CompletionReport
{
    public required bool Generation { get; init; }
    public required int TargetCount { get; init; }
    public required int Ok { get; init; }
    public required int Ng { get; init; }
    public required int Errors { get; init; }
    public required int Skipped { get; init; }
    public required int Cancelled { get; init; }
    public required IReadOnlyDictionary<string, int> MethodCounts { get; init; }
    public required bool AutoVerifyGpg { get; init; }
    public required int AscAutoVerified { get; init; }
    public required int SigAutoVerified { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public string? UserId { get; init; }
    public string? Fingerprint { get; init; }

    public static CompletionReport Create(OperationSummary summary, bool generation, int targetCount,
        TimeSpan elapsed, bool autoVerifyGpg)
    {
        string[] methods = ["SHA-512", "SHA-256", "SHA3-512", "SHA3-256", "ASC", "SIG"];
        var counts = methods.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        foreach (OperationResult result in summary.Results)
        {
            string? method = Classify(result.ItemName);
            if (method is not null && (!generation || result.State == ResultState.Ok)) counts[method]++;
        }
        OperationResult? signer = summary.Results.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Fingerprint));
        return new CompletionReport
        {
            Generation = generation,
            TargetCount = targetCount,
            Ok = summary.Ok,
            Ng = summary.Ng,
            Errors = summary.Errors,
            Skipped = summary.Skipped,
            Cancelled = summary.Cancelled,
            MethodCounts = counts,
            AutoVerifyGpg = autoVerifyGpg,
            AscAutoVerified = summary.Results.Count(x => Classify(x.ItemName) == "ASC" &&
                x.Message.Contains("自動検証成功", StringComparison.Ordinal)),
            SigAutoVerified = summary.Results.Count(x => Classify(x.ItemName) == "SIG" &&
                x.Message.Contains("自動検証成功", StringComparison.Ordinal)),
            Elapsed = elapsed,
            UserId = signer?.SignerUid,
            Fingerprint = signer?.Fingerprint
        };
    }

    public string ToPlainText()
    {
        var lines = new List<string>
        {
            Generation ? "生成が完了しました。" : "検証が完了しました。",
            "",
            $"対象{(Generation ? "ファイル" : "")}数: {TargetCount}"
        };
        if (Generation)
        {
            lines.Add($"正常生成数: {Ok}");
            lines.Add($"失敗: {Ng + Errors}");
        }
        else
        {
            lines.Add($"OK: {Ok}");
            lines.Add($"NG: {Ng}");
            lines.Add($"ERROR: {Errors}");
        }
        lines.Add($"SKIP: {Skipped}");
        lines.Add($"CANCEL: {Cancelled}");
        lines.Add("");
        lines.Add(Generation ? "方式別生成数:" : "方式別件数:");
        foreach (string method in new[] { "SHA-512", "SHA-256", "SHA3-512", "SHA3-256", "ASC", "SIG" })
            lines.Add($"{method}: {MethodCounts[method]}");
        if (Generation)
        {
            lines.Add("");
            lines.Add($"GPG自動検証: {(AutoVerifyGpg ? "ON" : "OFF")}");
            if (AutoVerifyGpg)
            {
                lines.Add($"ASC自動検証成功数: {AscAutoVerified}");
                lines.Add($"SIG自動検証成功数: {SigAutoVerified}");
            }
        }
        if (!string.IsNullOrWhiteSpace(Fingerprint))
        {
            lines.Add("");
            lines.Add($"User ID: {UserId ?? "(不明)"}");
            lines.Add($"Fingerprint: {Fingerprint}");
        }
        lines.Add("");
        lines.Add($"所要時間: {Elapsed:hh\\:mm\\:ss}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string? Classify(string itemName)
    {
        if (itemName.Contains("SHA3-512", StringComparison.OrdinalIgnoreCase)) return "SHA3-512";
        if (itemName.Contains("SHA3-256", StringComparison.OrdinalIgnoreCase)) return "SHA3-256";
        if (itemName.Contains("SHA-512", StringComparison.OrdinalIgnoreCase)) return "SHA-512";
        if (itemName.Contains("SHA-256", StringComparison.OrdinalIgnoreCase)) return "SHA-256";
        if (itemName.Contains(".asc", StringComparison.OrdinalIgnoreCase) || itemName.Contains("ASCII", StringComparison.OrdinalIgnoreCase)) return "ASC";
        if (itemName.Contains(".sig", StringComparison.OrdinalIgnoreCase) || itemName.Contains("Binary", StringComparison.OrdinalIgnoreCase)) return "SIG";
        return null;
    }
}

public sealed class CompletionDialog : Form
{
    private readonly string reportText;

    public CompletionDialog(CompletionReport report, Font ownerFont)
    {
        reportText = report.ToPlainText();
        Text = report.Generation ? "生成完了" : "検証完了";
        Font = ownerFont;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(650, 560);
        MinimumSize = new Size(500, 380);

        string status = report.Ng + report.Errors + report.Cancelled > 0 ? "問題のある項目があります。" :
            report.Skipped > 0 ? "一部の項目をスキップしました。" : "すべて正常に完了しました。";
        var heading = new Label { Text = status, AutoSize = true, Dock = DockStyle.Fill,
            Font = new Font(ownerFont, FontStyle.Bold), Padding = new Padding(0, 0, 0, 6) };
        var text = new TextBox { Text = reportText, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both, WordWrap = false, MinimumSize = new Size(0, ownerFont.Height * 8) };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        var copy = new Button { Text = "結果をコピー", AutoSize = true };
        copy.Click += (_, _) =>
        {
            try { CopyResultToClipboard(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Clipboardエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(copy);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(text, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = ok;
    }

    public string ReportText => reportText;
    public void CopyResultToClipboard() => Clipboard.SetText(reportText, TextDataFormat.UnicodeText);
}
