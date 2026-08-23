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
                File.WriteAllText(args[1], "ArchiveHashVerifier v1.1.4 startup OK");
                return;
            }
            if (args.Length == 3 && args[0] == "--batch-probe")
            {
                var coordinator = new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe"));
                var options = new GenerationOptions { Hashes = [HashKind.Sha512, HashKind.Blake3], BatchOutput = true };
                OperationSummary generated = coordinator.GenerateAsync([args[1]], options, null!, null, null, CancellationToken.None).GetAwaiter().GetResult();
                string directory = Path.GetDirectoryName(args[1])!;
                OperationSummary verified = coordinator.VerifyAsync([Path.Combine(directory, "ArchiveHashVerifier.sha512"), Path.Combine(directory, "ArchiveHashVerifier.blake3")], null, null, CancellationToken.None).GetAwaiter().GetResult();
                if (generated.Ok != 2 || verified.Ok != 2) throw new InvalidOperationException("Batch probe failed.");
                File.WriteAllText(args[2], "ArchiveHashVerifier v1.1.4 batch generation and verification OK");
                return;
            }
            if (args.Length == 3 && args[0] == "--blake3-probe")
            {
                var coordinator = new ProcessingCoordinator(new GpgService("Z:\\missing\\gpg.exe"));
                var options = new GenerationOptions { Hashes = [HashKind.Blake3], BatchOutput = false };
                OperationSummary generated = coordinator.GenerateAsync([args[1]], options, null!, null, null, CancellationToken.None).GetAwaiter().GetResult();
                OperationSummary verified = coordinator.VerifyAsync([args[1]], null, null, CancellationToken.None).GetAwaiter().GetResult();
                if (generated.Ok != 1 || verified.Ok != 1) throw new InvalidOperationException("BLAKE3 probe failed.");
                File.WriteAllText(args[2], "ArchiveHashVerifier v1.1.4 BLAKE3 generation and verification OK");
                return;
            }
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show("ArchiveHashVerifier v1.1.4 の起動中にエラーが発生しました。\r\n\r\n" + ex,
                "ArchiveHashVerifier v1.1.4 - 起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
