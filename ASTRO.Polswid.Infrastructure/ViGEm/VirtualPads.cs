using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Output;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ASTRO.Polswid.Infrastructure.ViGEm;

/// <summary>Virtual DualShock 4 (default) or Xbox 360 through ViGEmBus. A report is built completely, then submitted once.</summary>
public sealed class ViGEmVirtualPad : IVirtualPad
{
    ViGEmClient? _client;
    IXbox360Controller? _x360;
    IDualShock4Controller? _ds4;

    public ViGEmVirtualPad(VirtualPadType type = VirtualPadType.DualShock4) => PadType = type;

    public VirtualPadBackend Backend => VirtualPadBackend.ViGEmBus;
    public VirtualPadType PadType { get; }
    public bool IsConnected { get; private set; }
    public (ushort Vendor, ushort Product) Signature =>
        PadType == VirtualPadType.Xbox360 ? ((ushort)0x045E, (ushort)0x028E) : ((ushort)0x054C, (ushort)0x05C4);

    public void Connect()
    {
        if (IsConnected) return;
        try
        {
            _client ??= new ViGEmClient();
            if (PadType == VirtualPadType.Xbox360)
            {
                _x360 = _client.CreateXbox360Controller();
                _x360.AutoSubmitReport = false;
                _x360.Connect();
            }
            else
            {
                _ds4 = _client.CreateDualShock4Controller();
                _ds4.AutoSubmitReport = false;
                _ds4.Connect();
            }
            IsConnected = true;
        }
        catch (VigemBusNotFoundException ex)
        {
            Cleanup();
            throw new InvalidOperationException(ViGEmDriverProbe.InstallHelp, ex);
        }
        catch (Exception ex)
        {
            Cleanup();
            throw new InvalidOperationException($"ViGEmBus could not create the virtual {(PadType == VirtualPadType.Xbox360 ? "Xbox 360" : "DualShock 4")} controller: {ex.Message}" + (PadType == VirtualPadType.DualShock4 ? " Virtual DualShock 4 may be unsupported by the installed ViGEmBus version; you can switch Output to Xbox 360 in Settings (it is not switched automatically)." : ""), ex);
        }
    }

    public void Disconnect()
    {
        if (!IsConnected && _client == null) return;
        try
        {
            if (IsConnected) Submit(default);            // release everything before the device disappears
        }
        finally
        {
            Cleanup();
        }
    }

    void Cleanup()
    {
        IsConnected = false;
        try { _x360?.Disconnect(); } catch (VigemTargetNotPluggedInException) { }
        try { _ds4?.Disconnect(); } catch (VigemTargetNotPluggedInException) { }
        _x360 = null;
        _ds4 = null;
        _client?.Dispose();
        _client = null;
    }

    public void Submit(in GamepadState s)
    {
        if (!IsConnected) throw new InvalidOperationException("Virtual controller is not connected.");
        if (_x360 != null) SubmitX360(_x360, s);
        else if (_ds4 != null) SubmitDs4(_ds4, s);
    }

    static void SubmitX360(IXbox360Controller c, in GamepadState s)
    {
        var b = s.Buttons;
        c.SetButtonState(Xbox360Button.A, (b & PadButton.Cross) != 0);
        c.SetButtonState(Xbox360Button.B, (b & PadButton.Circle) != 0);
        c.SetButtonState(Xbox360Button.X, (b & PadButton.Square) != 0);
        c.SetButtonState(Xbox360Button.Y, (b & PadButton.Triangle) != 0);
        c.SetButtonState(Xbox360Button.LeftShoulder, (b & PadButton.L1) != 0);
        c.SetButtonState(Xbox360Button.RightShoulder, (b & PadButton.R1) != 0);
        c.SetButtonState(Xbox360Button.Back, (b & PadButton.Share) != 0);
        c.SetButtonState(Xbox360Button.Start, (b & PadButton.Options) != 0);
        c.SetButtonState(Xbox360Button.LeftThumb, (b & PadButton.L3) != 0);
        c.SetButtonState(Xbox360Button.RightThumb, (b & PadButton.R3) != 0);
        c.SetButtonState(Xbox360Button.Guide, (b & PadButton.Guide) != 0);
        c.SetButtonState(Xbox360Button.Up, (b & PadButton.DpadUp) != 0);
        c.SetButtonState(Xbox360Button.Down, (b & PadButton.DpadDown) != 0);
        c.SetButtonState(Xbox360Button.Left, (b & PadButton.DpadLeft) != 0);
        c.SetButtonState(Xbox360Button.Right, (b & PadButton.DpadRight) != 0);
        c.SetAxisValue(Xbox360Axis.LeftThumbX, Axis16(s.LX));
        c.SetAxisValue(Xbox360Axis.LeftThumbY, Axis16(s.LY));
        c.SetAxisValue(Xbox360Axis.RightThumbX, Axis16(s.RX));
        c.SetAxisValue(Xbox360Axis.RightThumbY, Axis16(s.RY));
        c.SetSliderValue(Xbox360Slider.LeftTrigger, Trig8(s.L2));
        c.SetSliderValue(Xbox360Slider.RightTrigger, Trig8(s.R2));
        c.SubmitReport();
    }

    static void SubmitDs4(IDualShock4Controller c, in GamepadState s)
    {
        var b = s.Buttons;
        c.SetButtonState(DualShock4Button.Cross, (b & PadButton.Cross) != 0);
        c.SetButtonState(DualShock4Button.Circle, (b & PadButton.Circle) != 0);
        c.SetButtonState(DualShock4Button.Square, (b & PadButton.Square) != 0);
        c.SetButtonState(DualShock4Button.Triangle, (b & PadButton.Triangle) != 0);
        c.SetButtonState(DualShock4Button.ShoulderLeft, (b & PadButton.L1) != 0);
        c.SetButtonState(DualShock4Button.ShoulderRight, (b & PadButton.R1) != 0);
        c.SetButtonState(DualShock4Button.TriggerLeft, s.L2 >= 0.5f);
        c.SetButtonState(DualShock4Button.TriggerRight, s.R2 >= 0.5f);
        c.SetButtonState(DualShock4Button.Share, (b & PadButton.Share) != 0);
        c.SetButtonState(DualShock4Button.Options, (b & PadButton.Options) != 0);
        c.SetButtonState(DualShock4Button.ThumbLeft, (b & PadButton.L3) != 0);
        c.SetButtonState(DualShock4Button.ThumbRight, (b & PadButton.R3) != 0);
        c.SetButtonState(DualShock4SpecialButton.Ps, (b & PadButton.Guide) != 0);
        c.SetButtonState(DualShock4SpecialButton.Touchpad, (b & PadButton.Touchpad) != 0);

        bool up = (b & PadButton.DpadUp) != 0, down = (b & PadButton.DpadDown) != 0;
        bool left = (b & PadButton.DpadLeft) != 0, right = (b & PadButton.DpadRight) != 0;
        if (up && down) up = down = false;
        if (left && right) left = right = false;
        c.SetDPadDirection(
            up ? (left ? DualShock4DPadDirection.Northwest : right ? DualShock4DPadDirection.Northeast : DualShock4DPadDirection.North)
            : down ? (left ? DualShock4DPadDirection.Southwest : right ? DualShock4DPadDirection.Southeast : DualShock4DPadDirection.South)
            : left ? DualShock4DPadDirection.West
            : right ? DualShock4DPadDirection.East
            : DualShock4DPadDirection.None);

        c.SetAxisValue(DualShock4Axis.LeftThumbX, Axis8(s.LX));
        c.SetAxisValue(DualShock4Axis.LeftThumbY, Axis8(-s.LY));   // DS4: 0 = up
        c.SetAxisValue(DualShock4Axis.RightThumbX, Axis8(s.RX));
        c.SetAxisValue(DualShock4Axis.RightThumbY, Axis8(-s.RY));
        c.SetSliderValue(DualShock4Slider.LeftTrigger, Trig8(s.L2));
        c.SetSliderValue(DualShock4Slider.RightTrigger, Trig8(s.R2));
        c.SubmitReport();
    }

    static short Axis16(float v) => (short)Math.Clamp((int)MathF.Round(v * 32767f), -32768, 32767);
    static byte Axis8(float v) => (byte)Math.Clamp((int)MathF.Round((v + 1f) * 127.5f), 0, 255);
    static byte Trig8(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    public void Dispose() => Disconnect();
}

/// <summary>Pad that processes everything but talks to no driver. The UI labels it clearly as a dry run.</summary>
public sealed class DryRunVirtualPad : IVirtualPad
{
    public DryRunVirtualPad(VirtualPadType type = VirtualPadType.DualShock4) => PadType = type;
    public VirtualPadBackend Backend => VirtualPadBackend.DryRun;
    public VirtualPadType PadType { get; }
    public bool IsConnected => false;
    public (ushort Vendor, ushort Product) Signature => (0, 0);
    public void Connect() { }
    public void Disconnect() { }
    public void Submit(in GamepadState state) { }
    public void Dispose() { }
}
