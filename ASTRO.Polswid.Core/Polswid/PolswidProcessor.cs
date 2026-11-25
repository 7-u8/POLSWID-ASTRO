using System.Collections.Concurrent;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Mapping;
using ASTRO.Polswid.Core.Profiles;

namespace ASTRO.Polswid.Core.Polswid;

/// <summary>Everything the UI and the tests want to know about one processed frame.</summary>
public struct FrameResult
{
    public bool ProfileChanged;
    public int ProfileRevision;
    public uint AppliedCommands;
    public PadButton ConsumedButtons;
    public float R2AfterMapping;
    public float R2BeforeFinalSuppression;
    public GamepadState Physical;
    public GamepadState Virtual;
    public SteeringDirection Steering;
    public SteeringDirection AnalogSteering;
    public DirectionOwner Owner;
    public float AnalogShaped;
    public bool DirectionChanged;
    public bool RemappingEnabled;
    public bool AnalogEnabled;
    /// <summary>The virtual LEFT-stick X is currently forced to 0 because digital conversion owns the axis.</summary>
    public bool LeftStickXSuppressed;
    public bool EmergencyStopped;
    public bool R2MappedActive;
    /// <summary>Continuous Steering is currently driving Down + steering (physical R2 held).</summary>
    public bool ContinuousActive;
    public bool ContinuousSquareActive;
    public bool R2Held;
    public bool R2Active;
    public bool R2Down;
    public TriggerMode TriggerMode;
    public bool TriggerPassthrough;
    public HelperPhase Helper;
}

/// <summary>
/// The deterministic physical -> virtual transformation: mapping -> analog conversion -> direction resolution ->
/// optional helpers. It never presses anything on its own: with no helper triggered the output is a pure function
/// of the physical input and the mapping. Thread-safe entry points: <see cref="Post"/> and <see cref="SetProfile"/>.
/// </summary>
public sealed class PolswidProcessor
{
    readonly ButtonMapper _mapper = new();
    readonly AnalogDirectionConverter _analog = new();
    readonly DirectionEngine _direction = new();
    readonly HelperEngine _helpers = new();
    readonly AnalogDirectionConverter _csConv = new();
    bool _csActive;
    long _csSinceUs;
    readonly ConcurrentQueue<MacroCommand> _commands = new();

    Profile _profile;
    Profile? _pendingProfile;
    int _profileRevision;

    public PolswidProcessor(Profile profile)
    {
        _profile = profile.Clone();
        _helpers.SyncProfile(_profile.Analog, _profile.Continuous.Enabled);
    }

    public Profile ActiveProfile => _profile;
    public bool RemappingEnabled => _helpers.RemappingEnabled;
    public bool AnalogEnabled => _helpers.AnalogEnabled;
    public bool EmergencyStopped => _helpers.EmergencyStopped;

    /// <summary>Thread-safe. The profile is cloned; the next frame picks it up (no restart needed).</summary>
    public void SetProfile(Profile profile) => Volatile.Write(ref _pendingProfile, profile.Clone());

    /// <summary>Thread-safe command channel (UI, hotkeys).</summary>
    public void Post(MacroCommandKind kind) => _commands.Enqueue(new MacroCommand(kind));

    /// <summary>Forget all dynamic state (controller disconnected, shutdown). Remapping/analog switches keep their setting.</summary>
    public void ResetAll()
    {
        _csActive = false; _csConv.Reset();
        _mapper.Reset();
        _analog.Reset();
        _direction.Reset();
        _helpers.Reset();
    }

    public FrameResult Process(in GamepadState physical, long nowUs)
    {
        var pending = Interlocked.Exchange(ref _pendingProfile, null);
        if (pending != null)
        {
            _profile = pending;
            _profileRevision++;
            _csActive = false; _csConv.Reset();
            _mapper.Reset();
            _analog.Reset();
            _direction.Reset();
            _helpers.Reset();
            _helpers.SyncProfile(_profile.Analog, _profile.Continuous.Enabled);
        }

        uint commands = 0;
        while (_commands.TryDequeue(out var cmd))
        {
            _helpers.Apply(cmd.Kind);
            commands |= 1u << (int)cmd.Kind;
        }

        var p = _profile;
        var result = new FrameResult { Physical = physical, ProfileChanged = pending != null,
            ProfileRevision = _profileRevision, AppliedCommands = commands };

        // Helper step first: it also tracks the physical emergency-stop button edge.
        var hf = _helpers.Step(physical.Buttons, nowUs, p.Helpers);
        result.ConsumedButtons = hf.Consume;

        // Remapping disabled or emergency stop: clearly defined safe state = raw pass-through of the physical pad.
        if (!_helpers.RemappingEnabled)
        {
            _mapper.Reset();
            _analog.Reset();
            _direction.Reset();
            result.Virtual = physical;
            if (p.Helpers.ConsumeControlButtons)   // the emergency-stop button itself is never forwarded to the game
                result.Virtual.Buttons &= ~p.Helpers.EmergencyStopButton;
            result.Steering = SteeringExtensions.FromButtons(physical.Buttons);
            result.AnalogEnabled = _helpers.AnalogEnabled;
            result.RemappingEnabled = false;
            result.EmergencyStopped = _helpers.EmergencyStopped;
            result.TriggerMode = p.Mapping.R2Mode;
            result.TriggerPassthrough = p.Mapping.TriggerPassthrough;
            return result;
        }

        // Mapping.
        var consumed = physical;
        consumed.Buttons &= ~hf.Consume;
        _mapper.Map(consumed, p.Mapping, out var v);
        result.R2AfterMapping = v.R2;

        // Physical R2 is an internal software trigger and must NEVER reach virtual R2 in Polswid mode.
        if (!p.Mapping.TriggerPassthrough || p.Continuous.Enabled)
        {
            v.R2 = 0f;
            v.Buttons &= ~PadButton.R2;
        }

        var aset = p.Analog;
        SteeringDirection analogDir = SteeringDirection.None, dir;
        bool suppressed = false;
        bool csSquare = false;

        // Polswid Mode (Continuous Steering): active ONLY while physical R2 is held.
        // When R2 is held:
        // - Virtual D-pad Down is activated immediately.
        // - Virtual Square activates after SquareDelayMs.
        // - Left Analog X controls D-pad Left/Right with fallback (default Left).
        // - BOTH Left Analog axes are suppressed (v.LX = 0, v.LY = 0).
        // - Vertical analog movement never reaches the game and never generates D-pad Up/Down.
        // - Right Analog is untouched.
        // When R2 is released:
        // - Normal Mode: Left Analog passes through normally without any D-pad conversion.
        // - Physical D-pad passes through with simultaneous directions preserved.
        bool r2Down = p.Mapping.R2Mode == TriggerMode.Toggle ? _mapper.R2Active : _mapper.R2Held;
        bool cs = p.Continuous.Enabled && r2Down;
        if (cs != _csActive)
        {
            _csActive = cs;
            _csSinceUs = cs ? nowUs : 0;
            _csConv.Reset();
            _analog.Reset();
            _direction.Reset();      // no stale state either way
        }

        if (cs)
        {
            // Polswid Mode: R2 held
            SteeringDirection d = SteeringDirection.None;
            if (_helpers.AnalogEnabled)
            {
                // 1. Convert Left Analog X to digital Left/Right with fallback
                d = _csConv.Process(physical.LX, aset);
                // 2. Suppress BOTH Left Analog axes completely
                v.LX = 0f;
                v.LY = 0f;
                suppressed = true;
            }
            else
            {
                v.LX = physical.LX;
                v.LY = physical.LY;
                suppressed = false;
            }

            analogDir = d;
            bool padLeft = v.Has(PadButton.DpadLeft);
            bool padRight = v.Has(PadButton.DpadRight);
            if (hf.FlipDirection) _direction.Flip();
            dir = _direction.Resolve(padLeft, padRight, analogDir, p.Direction, p.Timing.MinDirectionDwellMs, nowUs);

            // If neither stick nor physical D-pad requests steering, use Polswid fallback direction (default Left)
            if (dir == SteeringDirection.None)
            {
                var fallback = p.Continuous.FallbackDirection == SteeringDirection.Right ? SteeringDirection.Right : SteeringDirection.Left;
                dir = fallback;
            }

            // 3. Set synthetic D-pad Down (immediate) and ensure Up is cleared
            v.Buttons |= PadButton.DpadDown;
            v.Buttons &= ~PadButton.DpadUp;

            // 4. Set synthetic Square after delay
            csSquare = nowUs - _csSinceUs >= p.Continuous.SquareDelayMs * 1000L;
            if (csSquare) v.Buttons |= PadButton.Square;

            // 5. Apply horizontal steering. Changing direction NEVER releases Down or Square!
            v.Buttons = (v.Buttons & ~PadButtons.Steering) | dir.ToButtons();
        }
        else
        {
            // Normal Mode: R2 released
            // Left Analog passes through normally; no analog conversion; no fallback direction forced.
            v.LX = physical.LX;
            v.LY = physical.LY;
            suppressed = false;
            csSquare = false;

            // Physical D-pad passes through with valid simultaneous directions preserved (e.g. Down+Right, Down+Left).
            // D-pad horizontal steering (Left/Right) is resolved via _direction to handle contradictory presses, dwell time, and air flip.
            bool padLeft = v.Has(PadButton.DpadLeft);
            bool padRight = v.Has(PadButton.DpadRight);
            if (hf.FlipDirection) _direction.Flip();
            dir = _direction.Resolve(padLeft, padRight, SteeringDirection.None, p.Direction, p.Timing.MinDirectionDwellMs, nowUs);
            v.Buttons = (v.Buttons & ~PadButtons.Steering) | dir.ToButtons();
        }

        // Optional helpers (empty unless the user triggered one).
        v.Buttons = (v.Buttons & ~hf.ForceOff) | hf.ForceOn;

        // Never emit contradictory D-Pad axes.
        if (v.Has(PadButton.DpadUp) && v.Has(PadButton.DpadDown)) v.Buttons &= ~PadButton.DpadUp;
        if (v.Has(PadButton.DpadLeft) && v.Has(PadButton.DpadRight) && !p.Direction.AllowContradictory)
            v.Buttons &= ~PadButton.DpadRight;

        // Final inviolable guarantee: virtual R2 is NEVER forwarded in Polswid mode under any condition.
        result.R2BeforeFinalSuppression = v.R2;
        if (!p.Mapping.TriggerPassthrough || p.Continuous.Enabled)
        {
            v.R2 = 0f;
            v.Buttons &= ~PadButton.R2;
        }

        result.Virtual = v;
        result.Steering = dir;
        result.AnalogSteering = analogDir;
        result.Owner = _direction.Owner != DirectionOwner.None ? _direction.Owner : (cs ? DirectionOwner.Continuous : DirectionOwner.None);
        result.ContinuousActive = cs;
        result.ContinuousSquareActive = csSquare;
        result.AnalogShaped = cs ? _csConv.Shaped : 0f;
        result.DirectionChanged = _direction.Changed;
        result.RemappingEnabled = true;
        result.AnalogEnabled = _helpers.AnalogEnabled;
        result.LeftStickXSuppressed = suppressed;
        result.EmergencyStopped = false;
        result.R2MappedActive = r2Down;
        result.R2Held = _mapper.R2Held;
        result.R2Active = _mapper.R2Active;
        result.R2Down = r2Down;
        result.TriggerMode = p.Mapping.R2Mode;
        result.TriggerPassthrough = p.Mapping.TriggerPassthrough;
        result.Helper = hf.Phase;
        return result;
    }
}
