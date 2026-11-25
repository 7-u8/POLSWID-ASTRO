using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Profiles;

namespace ASTRO.Polswid.Core.MacroEngine;

public enum MacroCommandKind
{
    EnableRemapping, DisableRemapping, ToggleRemapping,
    EnableAnalog, DisableAnalog, ToggleAnalog,
    EmergencyStop, ClearEmergency,
    TriggerUTurn, TriggerAirSwitch, CancelHelpers,
}

public readonly record struct MacroCommand(MacroCommandKind Kind);

public enum HelperPhase { None, UTurn, AirSwitch }

/// <summary>What the optional helpers want applied to the virtual state this frame (nothing unless the user triggered one).</summary>
public struct HelperFrame
{
    public PadButton ForceOn;
    public PadButton ForceOff;
    /// <summary>Physical buttons hidden from the mapping stage (helper triggers).</summary>
    public PadButton Consume;
    public bool FlipDirection;
    public HelperPhase Phase;
}

/// <summary>
/// Remapping on/off, analog-steering on/off, emergency stop and the OPTIONAL timed helpers (Grounded U-Turn, Air Switch).
/// Nothing here ever presses a button on its own: helpers are disabled by default and only run after an explicit
/// trigger (a configured controller button or a UI command). All timing comes from caller timestamps, so it is deterministic.
/// Single-threaded: only the pipeline thread may call into it.
/// </summary>
public sealed class HelperEngine
{
    public bool RemappingEnabled { get; private set; } = true;
    public bool AnalogEnabled { get; private set; }
    public bool EmergencyStopped { get; private set; }
    public HelperPhase Phase { get; private set; }

    PadButton _releaseMask;
    long _releaseUntilUs;
    bool _airActive, _airFlipped;
    long _airStartUs;
    bool _pendingUTurn, _pendingAir;
    PadButton _rawPrev;

    public void Reset()
    {
        CancelHelpers();
        _rawPrev = PadButton.None;
    }

    /// <summary>Called when a profile is loaded: the profile defines the starting state of the analog switch.</summary>
    public void SyncProfile(AnalogSettings a, bool continuous = false) => AnalogEnabled = a.Enabled || continuous;

    public void Apply(MacroCommandKind cmd)
    {
        switch (cmd)
        {
            case MacroCommandKind.EnableRemapping: if (!EmergencyStopped) RemappingEnabled = true; break;
            case MacroCommandKind.DisableRemapping: RemappingEnabled = false; CancelHelpers(); break;
            case MacroCommandKind.ToggleRemapping: Apply(RemappingEnabled ? MacroCommandKind.DisableRemapping : MacroCommandKind.EnableRemapping); break;
            case MacroCommandKind.EnableAnalog: AnalogEnabled = true; break;
            case MacroCommandKind.DisableAnalog: AnalogEnabled = false; break;
            case MacroCommandKind.ToggleAnalog: AnalogEnabled = !AnalogEnabled; break;
            case MacroCommandKind.EmergencyStop: EmergencyStopped = true; RemappingEnabled = false; CancelHelpers(); break;
            case MacroCommandKind.ClearEmergency: EmergencyStopped = false; break;   // remapping stays off until the user re-enables it
            case MacroCommandKind.CancelHelpers: CancelHelpers(); break;
            case MacroCommandKind.TriggerUTurn: _pendingUTurn = true; break;
            case MacroCommandKind.TriggerAirSwitch: _pendingAir = true; break;
        }
    }

    void CancelHelpers()
    {
        _releaseMask = PadButton.None;
        _releaseUntilUs = 0;
        _airActive = false;
        _pendingUTurn = _pendingAir = false;
        Phase = HelperPhase.None;
    }

    static bool Pressed(PadButton now, PadButton prev, PadButton b) =>
        b != PadButton.None && (now & b) != 0 && (prev & b) == 0;

    public HelperFrame Step(PadButton rawNow, long nowUs, HelperSettings h)
    {
        var frame = new HelperFrame();
        PadButton prev = _rawPrev;
        _rawPrev = rawNow;

        if (h.ConsumeControlButtons)
        {
            PadButton c = h.EmergencyStopButton | h.CancelHelpersButton;
            if (h.UTurn.Enabled) c |= h.UTurn.TriggerButton;
            if (h.Air.Enabled) c |= h.Air.TriggerButton;
            frame.Consume = c;
        }

        // Physical shortcuts that work in every state (emergency stop must always be reachable).
        if (Pressed(rawNow, prev, h.EmergencyStopButton)) Apply(MacroCommandKind.EmergencyStop);
        if (EmergencyStopped || !RemappingEnabled)
        {
            _pendingUTurn = _pendingAir = false;
            Phase = HelperPhase.None;
            return frame;
        }
        if (Pressed(rawNow, prev, h.CancelHelpersButton)) CancelHelpers();

        PadButton forceOn = PadButton.None, forceOff = PadButton.None;
        var phase = HelperPhase.None;

        // ---- Grounded U-Turn
        var u = h.UTurn;
        if (u.Enabled)
        {
            if (u.Execution == UTurnExecution.Manual)
            {
                if (u.TriggerButton != PadButton.None && (rawNow & u.TriggerButton) != 0)
                {
                    forceOff |= u.ReleaseButtons;
                    phase = HelperPhase.UTurn;
                }
            }
            else if (Pressed(rawNow, prev, u.TriggerButton) || _pendingUTurn)
            {
                _releaseMask = u.ReleaseButtons;
                _releaseUntilUs = nowUs + u.ReleaseMs * 1000L;
            }
            if (_releaseMask != PadButton.None)
            {
                if (nowUs < _releaseUntilUs) { forceOff |= _releaseMask; phase = HelperPhase.UTurn; }
                else _releaseMask = PadButton.None;
            }
        }
        _pendingUTurn = false;

        // ---- Air switch
        var a = h.Air;
        if (a.Enabled && !_airActive && (Pressed(rawNow, prev, a.TriggerButton) || _pendingAir))
        {
            _airActive = true; _airFlipped = false; _airStartUs = nowUs;
        }
        _pendingAir = false;
        if (_airActive)
        {
            long el = nowUs - _airStartUs;
            phase = HelperPhase.AirSwitch;
            if (el < a.JumpPressMs * 1000L && a.JumpButton != PadButton.None) forceOn |= a.JumpButton;
            if (!_airFlipped && el >= a.SwitchAfterMs * 1000L) { _airFlipped = true; frame.FlipDirection = true; }
            if (_airFlipped && el >= a.JumpPressMs * 1000L) _airActive = false;
        }

        Phase = phase;
        frame.Phase = phase;
        frame.ForceOn = forceOn & ~forceOff;
        frame.ForceOff = forceOff;
        return frame;
    }
}
