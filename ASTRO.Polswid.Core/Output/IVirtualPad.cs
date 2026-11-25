using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.Core.Output;

public enum VirtualPadType { Xbox360, DualShock4 }

public enum VirtualPadBackend { ViGEmBus, DryRun }

/// <summary>
/// Virtual controller output abstraction. ViGEmBus is one backend; any future driver can replace it.
/// </summary>
public interface IVirtualPad : IDisposable
{
    VirtualPadBackend Backend { get; }
    VirtualPadType PadType { get; }

    /// <summary>True only when a real virtual device is attached (a dry-run pad reports false).</summary>
    bool IsConnected { get; }

    /// <summary>USB ids the virtual device enumerates with, used to keep it out of physical input.</summary>
    (ushort Vendor, ushort Product) Signature { get; }

    /// <summary>Attach the virtual device. Throws a descriptive exception on failure.</summary>
    void Connect();

    /// <summary>Release every control, then detach.</summary>
    void Disconnect();

    /// <summary>Submit a complete state in a single driver update.</summary>
    void Submit(in GamepadState state);
}
