using System.Diagnostics;
using Microsoft.Win32;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace ASTRO.Polswid.Infrastructure.ViGEm;

public sealed record ViGEmStatus(bool DriverRegistered, bool BusReachable, string? DriverVersion, string Message);

/// <summary>Detects ViGEmBus without ever installing anything.</summary>
public static class ViGEmDriverProbe
{
    public const string ReleasesUrl = "https://github.com/nefarius/ViGEmBus/releases";

    public const string InstallHelp =
        "ViGEmBus is not installed or not running. ViGEmBus is a retired project, but the final official build is still " +
        "available at " + ReleasesUrl + " (download the latest ViGEmBus setup, run it as administrator, then reboot if asked). " +
        "ASTRO POLSWID never installs kernel drivers for you.";

    public static ViGEmStatus Probe()
    {
        bool registered;
        string? version = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus");
            registered = key != null;
            var sys = Path.Combine(Environment.SystemDirectory, "drivers", "ViGEmBus.sys");
            if (File.Exists(sys)) version = FileVersionInfo.GetVersionInfo(sys).FileVersion;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new ViGEmStatus(false, false, null, "Could not inspect the ViGEmBus service: " + ex.Message);
        }

        if (!registered) return new ViGEmStatus(false, false, null, InstallHelp);

        try
        {
            using var client = new ViGEmClient();   // opens the bus device: a real connection test
            return new ViGEmStatus(true, true, version, $"ViGEmBus is installed and reachable{(version != null ? " (driver " + version + ")" : "")}.");
        }
        catch (VigemBusNotFoundException)
        {
            return new ViGEmStatus(true, false, version, "ViGEmBus is registered but the bus device is not available. Reboot or reinstall the driver. " + InstallHelp);
        }
        catch (Exception ex)
        {
            return new ViGEmStatus(true, false, version, "ViGEmBus is registered but could not be opened: " + ex.Message);
        }
    }
}
