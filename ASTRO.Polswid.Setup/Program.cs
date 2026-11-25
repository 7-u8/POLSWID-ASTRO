using System.Windows.Forms;

namespace ASTRO.Polswid.Setup;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Headless check used by CI and by humans: downloads + verifies both official installers WITHOUT running them.
        if (args is ["--download-only", var dir])
            return DownloadOnlyAsync(dir).GetAwaiter().GetResult();

        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
        return 0;
    }

    static async Task<int> DownloadOnlyAsync(string dir)
    {
        Directory.CreateDirectory(dir);
        int bad = 0;
        foreach (var d in new[] { Installer.ViGEmBus, Installer.HidHide })
        {
            try
            {
                var url = await Installer.ResolveUrlAsync(d, Console.WriteLine);
                var path = Path.Combine(dir, Path.GetFileName(new Uri(url).LocalPath));
                await Installer.DownloadAsync(url, path, new Progress<int>());
                var signer = Installer.VerifySignature(path, d.SignerContains);
                File.AppendAllText(Path.Combine(dir, "result.txt"), $"OK  {d.Name}  {Path.GetFileName(path)}  signer={signer}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                bad++;
                File.AppendAllText(Path.Combine(dir, "result.txt"), $"FAIL {d.Name}: {ex.Message}{Environment.NewLine}");
            }
        }
        return bad;
    }
}
