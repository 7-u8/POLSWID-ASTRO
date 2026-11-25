using Nefarius.Drivers.HidHide;

namespace ASTRO.Polswid.Infrastructure.ControllerDetection;

public sealed record HidHideAccessStatus(bool Verified, bool Installed, bool? Active, bool? Inverted,
    bool? ApplicationListed, bool? ApplicationAllowed, IReadOnlyList<string> HiddenInstanceIds, string? Error)
{
    // Configuration entries, not proof that any particular device is currently connected.
    public int HiddenEdgeEntries => HiddenInstanceIds.Count(id =>
        id.Contains("VID_054C&PID_0DF2", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Official managed API. Reads are separate from the single, explicitly approved allow-list operation.</summary>
public sealed class HidHideAccessService
{
    readonly IHidHideControlService _driver;
    public HidHideAccessService() : this(new HidHideControlService()) { }
    internal HidHideAccessService(IHidHideControlService driver) => _driver = driver;

    public HidHideAccessStatus Read(string executablePath)
    {
        try
        {
            if (!_driver.IsInstalled) return new(true, false, null, null, null, null, Array.Empty<string>(), null);
            bool active = _driver.IsActive, inverted = _driver.IsAppListInverted;
            bool listed = _driver.ApplicationPaths.Any(p => string.Equals(p, executablePath, StringComparison.OrdinalIgnoreCase));
            var ids = _driver.BlockedInstanceIds.ToArray();
            return new(true, true, active, inverted, listed, !active || (inverted ? !listed : listed), ids, null);
        }
        catch (Exception ex)
        {
            // Busy configuration client, driver unavailable, access denied, and other failures stay unverified.
            return new(false, false, null, null, null, null, Array.Empty<string>(), ex.Message);
        }
    }

    public HidHideAccessStatus AllowThisExecutable(string executablePath, bool userApproved)
    {
        if (!userApproved) throw new InvalidOperationException("Explicit user approval is required.");
        if (!Path.IsPathFullyQualified(executablePath) || !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(executablePath)) throw new ArgumentException("An existing absolute executable path is required.");
        var before = Read(executablePath);
        if (!before.Verified || !before.Installed || before.Active != true || before.Inverted != false)
            throw new InvalidOperationException("Cannot grant access: close the HidHide configuration client and check normal allow-list mode with hiding enabled.");
        if (before.ApplicationListed == true) return before;
        // Never changes Active, inverted mode, hidden devices, or clears/replaces application lists in our code.
        _driver.AddApplicationPath(executablePath);
        return Read(executablePath); // Report the actual result instead of assuming the operation succeeded.
    }
}
