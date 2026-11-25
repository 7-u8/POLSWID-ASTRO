using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Win32;

namespace ASTRO.Polswid.Setup;

/// <summary>A driver installer published by nefarius on GitHub. Nothing runs unless Windows says its signature is valid and the signer is Nefarius.</summary>
public sealed record Driver(string Name, string Repo, string AssetPrefix, string ServiceKey, string FallbackUrl, string SignerContains);

public static class Installer
{
    public const string AppRepo = "7-u8/POLSWID-ASTRO";
    public const string AppAsset = "ASTRO-POLSWID.exe";

    public static readonly Driver ViGEmBus = new("ViGEmBus", "nefarius/ViGEmBus", "ViGEmBus_", "ViGEmBus",
        "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe", "Nefarius");

    public static readonly Driver HidHide = new("HidHide", "nefarius/HidHide", "HidHide_", "HidHide",
        "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe", "Nefarius");

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ASTRO-POLSWID-Setup/1.0");
        return c;
    }

    public static bool IsInstalled(Driver d)
    {
        using var k = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{d.ServiceKey}");
        return k != null;
    }

    /// <summary>Latest official download URL from the GitHub API; falls back to the pinned known-good release if the API is unreachable.</summary>
    public static async Task<string> ResolveUrlAsync(Driver d, Action<string> log)
    {
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://api.github.com/repos/{d.Repo}/releases/latest"));
            foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.StartsWith(d.AssetPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return a.GetProperty("browser_download_url").GetString()!;
            }
            log($"{d.Name}: no matching asset in the latest release, using the pinned version.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            log($"{d.Name}: GitHub API unavailable ({ex.Message}), using the pinned version.");
        }
        return d.FallbackUrl;
    }

    public static async Task DownloadAsync(string url, string path, IProgress<int> progress)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync();
        await using var dst = File.Create(path);
        var buf = new byte[81920];
        long done = 0; int n;
        while ((n = await src.ReadAsync(buf)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n));
            done += n;
            if (total > 0) progress.Report((int)(done * 100 / total));
        }
    }

    /// <summary>Throws unless the Authenticode signature is valid (WinVerifyTrust) and the signer subject contains the expected name.</summary>
    public static string VerifySignature(string path, string signerContains)
    {
        uint rc = WinVerify(path);
        if (rc != 0) throw new InvalidOperationException($"Windows rejected the signature of {Path.GetFileName(path)} (WinVerifyTrust 0x{rc:X8}). The file was NOT run.");
        var subject = X509Certificate.CreateFromSignedFile(path).Subject;
        if (!subject.Contains(signerContains, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unexpected signer '{subject}' (expected {signerContains}). The file was NOT run.");
        return subject;
    }

    /// <summary>Runs the official installer with its normal visible UI and waits for it.</summary>
    public static int RunInstaller(string path)
    {
        using var p = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })
                      ?? throw new InvalidOperationException("Could not start the installer.");
        p.WaitForExit();
        return p.ExitCode;
    }

    // ---------------------------------------------------------------- the app itself

    public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ASTRO POLSWID");
    public static string InstalledExe => Path.Combine(InstallDir, "ASTRO POLSWID.exe");

    /// <summary>Uses the exe next to SETUP.exe if present, otherwise downloads it from this project's latest GitHub release.</summary>
    public static async Task<string> InstallAppAsync(string tempDir, bool desktopShortcut, IProgress<int> progress, Action<string> log)
    {
        var local = Path.Combine(AppContext.BaseDirectory, AppAsset);
        string src = local;
        if (!File.Exists(local))
        {
            src = Path.Combine(tempDir, AppAsset);
            log($"Downloading {AppAsset} …");
            await DownloadAsync($"https://github.com/{AppRepo}/releases/latest/download/{AppAsset}", src, progress);
        }
        Directory.CreateDirectory(InstallDir);
        File.Copy(src, InstalledExe, overwrite: true);   // user settings live in %APPDATA% and are never touched
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "ASTRO POLSWID.lnk"));
        if (desktopShortcut) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "ASTRO POLSWID.lnk"));
        return InstalledExe;
    }

    static void Shortcut(string lnk)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic s = shell.CreateShortcut(lnk);
        s.TargetPath = InstalledExe;
        s.WorkingDirectory = InstallDir;
        s.Save();
    }

    // ---------------------------------------------------------------- WinVerifyTrust

    static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential)]
    struct WintrustFileInfo { public uint cbStruct; public IntPtr pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }

    [StructLayout(LayoutKind.Sequential)]
    struct WintrustData
    {
        public uint cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public uint dwUIChoice;
        public uint fdwRevocationChecks; public uint dwUnionChoice; public IntPtr pFile; public uint dwStateAction;
        public IntPtr hWVTStateData; public IntPtr pwszURLReference; public uint dwProvFlags; public uint dwUIContext; public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    static extern uint WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WintrustData data);

    static uint WinVerify(string path)
    {
        var fi = new WintrustFileInfo { cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(), pcwszFilePath = Marshal.StringToCoTaskMemUni(path) };
        var pFi = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fi, pFi, false);
            var data = new WintrustData
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustData>(), dwUIChoice = 2 /* none */, fdwRevocationChecks = 0,
                dwUnionChoice = 1 /* file */, pFile = pFi, dwStateAction = 0,
            };
            return WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, ref data);
        }
        finally { Marshal.FreeCoTaskMem(fi.pcwszFilePath); Marshal.FreeHGlobal(pFi); }
    }
}
