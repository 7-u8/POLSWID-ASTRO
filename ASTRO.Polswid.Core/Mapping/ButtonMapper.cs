using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Profiles;

namespace ASTRO.Polswid.Core.Mapping;

/// <summary>
/// Physical -> virtual control mapping. Bindings override identity; any control without a binding passes through unchanged.
/// R2 supports Hold (default) and Toggle behaviour. Per-frame work is allocation-free.
/// </summary>
public sealed class ButtonMapper
{
    bool _r2Down, _l2Down, _r2Latched, _r2WasEffective;
    int _r2ReleaseFrames, _l2ReleaseFrames;
    public bool R2Latched => _r2Latched;

    /// <summary>True while physical R2 (or its toggle) is considered pressed after hysteresis and glitch filtering.</summary>
    public bool R2Active { get; private set; }

    /// <summary>Physical R2 is held (authoritative digital state with glitch filter), independent of Toggle mode.</summary>
    public bool R2Held => _r2Down;

    public void Reset()
    {
        _r2Down = _l2Down = _r2Latched = _r2WasEffective = false;
        _r2ReleaseFrames = _l2ReleaseFrames = 0;
        R2Active = false;
    }

    public void Map(in GamepadState physical, MappingSettings s, out GamepadState virt)
    {
        // Digital trigger state: instantaneous press activation (0 ms delay),
        // with glitch-filtered release confirmation to ignore single-frame micro-drops during rapid analog stick movements.
        _r2Down = UpdateTriggerState(_r2Down, physical.R2, physical.Has(PadButton.R2), s.TriggerPressThreshold, s.TriggerReleaseThreshold, ref _r2ReleaseFrames);
        _l2Down = UpdateTriggerState(_l2Down, physical.L2, physical.Has(PadButton.L2), s.TriggerPressThreshold, s.TriggerReleaseThreshold, ref _l2ReleaseFrames);

        bool r2Effective = _r2Down;
        if (s.R2Mode == TriggerMode.Toggle)
        {
            if (_r2Down && !_r2WasEffective) _r2Latched = !_r2Latched;
            _r2WasEffective = _r2Down;
            r2Effective = _r2Latched;
        }
        else
        {
            _r2Latched = false;
            _r2WasEffective = _r2Down;
        }
        R2Active = r2Effective;

        PadButton src = physical.Buttons & ~(PadButton.L2 | PadButton.R2);
        if (_l2Down) src |= PadButton.L2;
        if (r2Effective) src |= PadButton.R2;

        // Bindings: bound sources are replaced, unbound sources keep identity.
        PadButton bound = PadButton.None, outButtons = PadButton.None, forceTrigger = PadButton.None;
        var bindings = s.Bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            bound |= b.From;
            if ((src & b.From) == 0) continue;
            outButtons |= b.To;
            // another control remapped onto a trigger drives that trigger fully
            if (b.To != b.From) forceTrigger |= b.To & (PadButton.L2 | PadButton.R2);
        }
        outButtons |= src & ~bound;

        virt = default;
        virt.LX = physical.LX; virt.LY = physical.LY;
        virt.RX = physical.RX; virt.RY = physical.RY;

        // Analog triggers: identity bindings (or explicit passthrough) forward the analog value.
        // Physical R2 is an internal trigger for ASTRO POLSWID: it must NEVER reach virtual R2 unless TriggerPassthrough is explicitly enabled.
        bool r2Identity = IsIdentity(s, PadButton.R2), l2Identity = IsIdentity(s, PadButton.L2);
        virt.R2 = (r2Identity || s.TriggerPassthrough) ? (s.R2Mode == TriggerMode.Toggle ? (r2Effective ? 1f : 0f) : physical.R2) : 0f;
        virt.L2 = (l2Identity || s.TriggerPassthrough) ? physical.L2 : 0f;

        // Any button mapped onto a trigger must drive the analog value too (Xbox 360 has no digital triggers).
        if ((forceTrigger & PadButton.R2) != 0) virt.R2 = 1f;
        if ((forceTrigger & PadButton.L2) != 0) virt.L2 = 1f;
        // Toggle-mode R2 identity: the latched state is already folded into virt.R2 above.
        outButtons &= ~(PadButton.L2 | PadButton.R2);
        if (virt.R2 >= 0.5f) outButtons |= PadButton.R2;
        if (virt.L2 >= 0.5f) outButtons |= PadButton.L2;

        if (!s.TriggerPassthrough && (forceTrigger & PadButton.R2) == 0)
        {
            virt.R2 = 0f;
            outButtons &= ~PadButton.R2;
        }

        virt.Buttons = outButtons;
    }

    static bool IsIdentity(MappingSettings s, PadButton trigger)
    {
        // Physical R2 is an internal activation trigger only: it must never default to identity passthrough
        if (trigger == PadButton.R2) return false;
        bool any = false;
        for (int i = 0; i < s.Bindings.Count; i++)
        {
            var b = s.Bindings[i];
            if (b.From != trigger) continue;
            any = true;
            if (b.To != trigger) return false;
        }
        _ = any; // no binding at all == identity for non-trigger controls
        return true;
    }

    /// <summary>
    /// Evaluates trigger state with instantaneous press activation (0 ms delay) and glitch-filtered release confirmation.
    /// During an active hold, transient micro-drops (&lt; 10 ms / 5 frames at 500 Hz) caused by biomechanical grip shifts or sensor noise
    /// during rapid stick movements are filtered out. Genuine sustained release deactivates promptly (~10 ms).
    /// </summary>
    static bool UpdateTriggerState(bool current, float value, bool digitalPressed, float press, float release, ref int releaseFrames)
    {
        // Instant press: crossing threshold or digital button activates immediately on frame 0.
        if (digitalPressed || value >= press)
        {
            releaseFrames = 0;
            return true;
        }

        // Not currently held: requires value >= press to activate.
        if (!current)
        {
            releaseFrames = 0;
            return false;
        }

        // Currently held, and value is still above the release threshold: hold continues solidly.
        if (value > release)
        {
            releaseFrames = 0;
            return true;
        }

        // Value dropped into the release zone (value <= release):
        // Filter transient micro-drops (< 10 ms): require 5 consecutive confirmed release frames (~10 ms at 500 Hz)
        // before declaring genuine release, protecting against micro-drops down to zero during rapid analog movements.
        releaseFrames++;
        if (releaseFrames >= 5)
        {
            releaseFrames = 0;
            return false;
        }

        return true;
    }
}
