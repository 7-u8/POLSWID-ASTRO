namespace ASTRO.Polswid.Core.Input;

/// <summary>Optional boundary evidence. RawR2 is pre-normalization SDL state, not a HID wire report.</summary>
public readonly record struct InputReadDiagnostics(string Backend, string? DeviceId, string? Mapping,
    short? RawR2, bool? HasR2Axis, bool? ConnectedAfterRead, string? Error);

public interface IInputReadDiagnostics
{
    bool DiagnosticsEnabled { get; set; }
    InputReadDiagnostics LastReadDiagnostics { get; }
}

/// <summary>Optional UI-safe discovery control. Requests are executed by the input owner thread.</summary>
public interface IControllerDiscoveryControl
{
    void RequestRefresh();
    string? DiscoveryError { get; }
}

public enum ControllerOrigin { Unknown, Physical, ViGEm }

public sealed record PhysicalDeviceInfo(
    uint Id,
    /// <summary>Stable identity across re-plugs: "VID:PID" (+ "#n" when several identical pads are present).</summary>
    string Key,
    string Name,
    ushort VendorId,
    ushort ProductId,
    /// <summary>Controller family derived from capabilities and ids (not from the marketing name).</summary>
    string Family,
    bool IsLikelyVirtual,
    bool IsSelected,
    bool IsSupported = true,
    string? Path = null,
    ControllerOrigin Origin = ControllerOrigin.Unknown);

/// <summary>Everything the Physical Input panel shows about the active controller.</summary>
public sealed record PhysicalDeviceDetails(
    string Name,
    string Family,
    ushort VendorId,
    ushort ProductId,
    string Connection,
    string Battery,
    string InputApi,
    string Capabilities);

/// <summary>
/// Physical controller source. Everything it returns is the controller-neutral <see cref="GamepadState"/>, so the
/// Polswid engine never knows which physical model is connected. All members except the thread-safe ones are called
/// from the pipeline thread only, so implementations (SDL) can keep their thread affinity.
/// </summary>
public interface IPhysicalInput : IDisposable
{
    /// <summary>Initialise the backend. Throws on failure (never fakes success).</summary>
    void Initialize();

    /// <summary>Pump the backend and read the active controller. Returns false when no controller is connected.</summary>
    bool TryPoll(out GamepadState state);

    string? ActiveDeviceName { get; }

    /// <summary>Details of the active controller, or null.</summary>
    PhysicalDeviceDetails? ActiveDetails { get; }

    /// <summary>Latest enumerated devices (thread-safe snapshot).</summary>
    IReadOnlyList<PhysicalDeviceInfo> Devices { get; }

    /// <summary>
    /// Thread-safe. Choose the primary controller by <see cref="PhysicalDeviceInfo.Key"/>; null = automatic (first usable controller,
    /// which is then kept). With an explicit primary, no other controller is ever used, so inputs are never combined.
    /// </summary>
    void SelectDevice(string? key);

    /// <summary>Thread-safe: ignore newly appearing devices with this signature (our own virtual pad).</summary>
    void BeginVirtualFilter(ushort vendorId, ushort productId);

    void EndVirtualFilter();
}
