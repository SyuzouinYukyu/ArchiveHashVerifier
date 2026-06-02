using System;
using System.Windows.Forms;

namespace ArchiveHashVerifier_GUI
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "アプリケーションの起動中にエラーが発生しました。\r\n\r\n" + ex,
                    "ArchiveHashVerifier - 起動エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
