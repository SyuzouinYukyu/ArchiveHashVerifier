namespace ArchiveHashVerifier;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ApplicationConfiguration.Initialize();
            if (args.Length == 2 && args[0] == "--startup-probe")
            {
                using var form = new MainForm();
                _ = form.Handle;
                File.WriteAllText(args[1], "ArchiveHashVerifier v1.1.2 startup OK");
                return;
            }
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show("ArchiveHashVerifier v1.1.2 の起動中にエラーが発生しました。\r\n\r\n" + ex,
                "ArchiveHashVerifier v1.1.2 - 起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
