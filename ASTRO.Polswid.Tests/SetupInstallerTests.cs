using System.IO;
using ASTRO.Polswid.Setup;
using Xunit;

namespace ASTRO.Polswid.Tests;

/// <summary>Runs only with ASTRO_NET_TESTS=1 (hits GitHub; downloads ~14 MB; never runs the installers).</summary>
public sealed class NetFactAttribute : FactAttribute
{
    public NetFactAttribute() { if (Environment.GetEnvironmentVariable("ASTRO_NET_TESTS") != "1") Skip = "set ASTRO_NET_TESTS=1 to run"; }
}

public class SetupInstallerTests
{
    [NetFact]
    public async Task Official_installers_download_and_pass_the_signature_check()
    {
        var dir = Path.Combine(Path.GetTempPath(), "astro-setup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var d in new[] { Installer.ViGEmBus, Installer.HidHide })
            {
                var url = await Installer.ResolveUrlAsync(d, _ => { });
                Assert.StartsWith($"https://github.com/{d.Repo}/releases/download/", url);
                var path = Path.Combine(dir, Path.GetFileName(new Uri(url).LocalPath));
                await Installer.DownloadAsync(url, path, new Progress<int>());
                Assert.Contains("Nefarius", Installer.VerifySignature(path, d.SignerContains));
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_file_with_no_valid_signature_is_rejected_and_never_run()
    {
        var f = Path.GetTempFileName();
        try
        {
            File.WriteAllText(f, "not an installer");
            Assert.Throws<InvalidOperationException>((Action)(() => Installer.VerifySignature(f, "Nefarius")));
        }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Driver_presence_check_matches_the_registry()
    {
        // Both drivers are checked by their Windows service key; an unknown driver must read as not installed.
        Assert.False(Installer.IsInstalled(new Driver("x", "x/x", "x", "ASTRO_NO_SUCH_SERVICE", "", "")));
    }
}

