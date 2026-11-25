using System.Threading;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

/// <summary>Deterministic harness: a fake clock advances 2 ms per processed frame.</summary>
public sealed class Harness
{
    public const long FrameUs = 2000;
    public PolswidProcessor Proc;
    public GamepadState Phys;
    public long Now = 1_000_000;
    public FrameResult Last;
    public readonly List<FrameResult> Frames = new();

    public Harness(Profile? profile = null) =>
        Proc = new PolswidProcessor(profile ?? DefaultProfiles.Create(DefaultProfiles.Standard));

    public FrameResult Step(int frames = 1)
    {
        for (int i = 0; i < frames; i++)
        {
            Now += FrameUs;
            Last = Proc.Process(Phys, Now);
            Frames.Add(Last);
        }
        return Last;
    }

    public FrameResult StepMs(int ms) => Step((int)Math.Ceiling(ms * 1000.0 / FrameUs));
    public Harness Press(PadButton b) { Phys.Buttons |= b; return this; }
    public Harness Release(PadButton b) { Phys.Buttons &= ~b; return this; }

    public static Profile Make(Action<Profile>? tweak = null, string key = DefaultProfiles.Standard)
    {
        var p = DefaultProfiles.Create(key);
        tweak?.Invoke(p);
        return p;
    }
}

public class MappingTests
{
    [Fact]
    public void R2_pressed_gives_virtual_DpadDown_and_release_clears_it_and_R2_is_not_forwarded()
    {
        var h = new Harness();
        h.Step();
        Assert.False(h.Last.Virtual.Has(PadButton.DpadDown));

        h.Phys.R2 = 1f; var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.Equal(0f, r.Virtual.R2);
        Assert.False(r.Virtual.Has(PadButton.R2));

        h.Phys.R2 = 0f; r = h.Step(5);
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
    }

    [Fact]
    public void R2_hysteresis_prevents_chatter_near_threshold()
    {
        var h = new Harness();
        h.Phys.R2 = 0.35f; Assert.True(h.Step().Virtual.Has(PadButton.DpadDown));
        h.Phys.R2 = 0.25f; Assert.True(h.Step().Virtual.Has(PadButton.DpadDown));
        h.Phys.R2 = 0.15f; Assert.False(h.Step(5).Virtual.Has(PadButton.DpadDown));
        h.Phys.R2 = 0.25f; Assert.False(h.Step().Virtual.Has(PadButton.DpadDown));
    }

    [Fact]
    public void R2_toggle_mode_is_optional_and_latches()
    {
        var h = new Harness(Harness.Make(p => p.Mapping.R2Mode = TriggerMode.Toggle));
        h.Phys.R2 = 1f; Assert.True(h.Step().Virtual.Has(PadButton.DpadDown));
        h.Phys.R2 = 0f; Assert.True(h.Step(5).Virtual.Has(PadButton.DpadDown));
        h.Phys.R2 = 1f; h.Step();
        h.Phys.R2 = 0f; Assert.False(h.Step(5).Virtual.Has(PadButton.DpadDown));
    }

    [Fact]
    public void Physical_DpadLeft_gives_virtual_DpadLeft()
    {
        var h = new Harness();
        h.Press(PadButton.DpadLeft);
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
    }

    [Fact]
    public void Physical_DpadDown_gives_virtual_DpadDown_and_never_Right()
    {
        var h = new Harness();
        h.Press(PadButton.DpadDown);
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
    }

    [Fact]
    public void L2_analog_trigger_and_all_other_buttons_pass_through()
    {
        var h = new Harness();
        h.Press(PadButton.Square | PadButton.Cross | PadButton.Circle | PadButton.Triangle | PadButton.L1 | PadButton.R1
                | PadButton.Options | PadButton.Share | PadButton.Guide | PadButton.Touchpad | PadButton.L3 | PadButton.R3);
        h.Phys.L2 = 0.62f;
        var r = h.Step();
        Assert.Equal(0.62f, r.Virtual.L2);                 // analog value preserved, not just a digital press
        Assert.True(r.Virtual.Has(PadButton.L2));
        Assert.True(r.Virtual.Has(PadButton.Square | PadButton.Cross | PadButton.Circle | PadButton.Triangle | PadButton.L1 | PadButton.R1
                | PadButton.Options | PadButton.Share | PadButton.Guide | PadButton.Touchpad | PadButton.L3 | PadButton.R3));
    }

    [Fact]
    public void Trigger_passthrough_option_forwards_analog_R2_in_addition()
    {
        var h = new Harness(Harness.Make(p =>
        {
            p.Mapping.TriggerPassthrough = true;
            p.Continuous.Enabled = false;
            p.Mapping.Bindings.Add(new ButtonBinding(PadButton.R2, PadButton.DpadDown));
        }));
        h.Phys.R2 = 0.8f; var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.Equal(0.8f, r.Virtual.R2);
    }

    [Fact]
    public void Right_stick_and_left_stick_Y_pass_through_unchanged()
    {
        var h = new Harness();
        h.Phys.RX = -0.7f; h.Phys.RY = 0.3f; h.Phys.LY = -0.55f; h.Phys.LX = 0.9f;
        var r = h.Step();
        Assert.Equal(-0.7f, r.Virtual.RX);
        Assert.Equal(0.3f, r.Virtual.RY);
        Assert.Equal(-0.55f, r.Virtual.LY);
    }

    [Fact]
    public void PS_button_and_touchpad_are_remappable()
    {
        var h = new Harness();
        h.Proc.SetProfile(Harness.Make(p =>
        {
            p.Mapping.Bindings.RemoveAll(b => b.From is PadButton.Touchpad or PadButton.Guide);
            p.Mapping.Bindings.Add(new ButtonBinding(PadButton.Touchpad, PadButton.Triangle));
            p.Mapping.Bindings.Add(new ButtonBinding(PadButton.Guide, PadButton.None));
        }));
        h.Press(PadButton.Touchpad | PadButton.Guide);
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.Triangle));
        Assert.False(r.Virtual.Has(PadButton.Touchpad));
        Assert.False(r.Virtual.Has(PadButton.Guide));
    }

    [Fact]
    public void Mapping_changes_apply_at_runtime_without_restart()
    {
        var h = new Harness();
        h.Press(PadButton.Triangle);
        Assert.True(h.Step().Virtual.Has(PadButton.Triangle));
        h.Proc.SetProfile(Harness.Make(p =>
        {
            p.Mapping.Bindings.RemoveAll(b => b.From == PadButton.Triangle);
            p.Mapping.Bindings.Add(new ButtonBinding(PadButton.Triangle, PadButton.Circle));
        }));
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.Circle));
        Assert.False(r.Virtual.Has(PadButton.Triangle));
    }
}

public class NoAutomationTests
{
    [Fact]
    public void Idle_physical_controller_produces_a_completely_idle_virtual_controller()
    {
        foreach (var key in DefaultProfiles.Keys)
        {
            var h = new Harness(DefaultProfiles.Create(key));
            var r = h.Step(50);
            Assert.Equal(default, r.Virtual);              // nothing held, nothing steered, nothing pressed
        }
    }

    [Fact]
    public void Square_Cross_and_steering_are_never_held_for_the_user()
    {
        var h = new Harness();
        h.Press(PadButton.DpadLeft); h.Step(3);
        h.Release(PadButton.DpadLeft);
        var r = h.Step(10);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.Cross));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
        Assert.Equal(SteeringDirection.None, r.Steering);   // neutral allowed, no direction lock
    }

    [Fact]
    public void Helpers_are_disabled_by_default_and_ignore_triggers()
    {
        var h = new Harness();
        h.Proc.Post(MacroCommandKind.TriggerUTurn);
        h.Proc.Post(MacroCommandKind.TriggerAirSwitch);
        h.Press(PadButton.Cross | PadButton.L1 | PadButton.Circle);
        var r = h.Step(20);
        Assert.True(r.Virtual.Has(PadButton.Cross));         // U-turn did not release X
        Assert.True(r.Virtual.Has(PadButton.L1));
        Assert.True(r.Virtual.Has(PadButton.Circle));
        Assert.Equal(HelperPhase.None, r.Helper);
    }

    [Fact]
    public void Physical_input_timing_is_preserved_frame_for_frame()
    {
        var h = new Harness();
        var expected = new List<bool>();
        for (int i = 0; i < 30; i++)
        {
            bool down = i % 3 == 0;
            h.Phys.Buttons = down ? PadButton.Cross : PadButton.None;
            expected.Add(down);
            Assert.Equal(down, h.Step().Virtual.Has(PadButton.Cross));
        }
    }
}

public class AnalogConversionTests
{
    static Profile AnalogProfile(Action<Profile>? t = null) => Harness.Make(p => { p.Analog.Enabled = true; t?.Invoke(p); });

    [Fact]
    public void Default_thresholds_match_the_specification()
    {
        var a = new AnalogSettings();
        Assert.Equal(0.20f, a.Deadzone);
        Assert.Equal(0.60f, a.ActivationThreshold);
        Assert.Equal(0.35f, a.ReleaseThreshold);
    }

    [Fact]
    public void Left_stick_right_in_R2_mode_suppresses_both_axes_and_presses_DpadRight()
    {
        var h = new Harness(AnalogProfile());
        h.Phys.R2 = 1f;
        h.Phys.LX = 1f;
        h.Phys.LY = 0.5f;
        var r = h.Step();
        Assert.Equal(0f, r.Virtual.LX);
        Assert.Equal(0f, r.Virtual.LY);
        Assert.True(r.LeftStickXSuppressed);
        Assert.True(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
        Assert.Equal(DirectionOwner.Analog, r.Owner);
    }

    [Fact]
    public void Left_stick_left_in_R2_mode_presses_DpadLeft()
    {
        var h = new Harness(AnalogProfile());
        h.Phys.R2 = 1f;
        h.Phys.LX = -1f;
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
    }

    [Fact]
    public void Left_stick_passes_through_normally_when_R2_is_released()
    {
        var h = new Harness(AnalogProfile());
        h.Phys.R2 = 0f;
        h.Phys.LX = 1f; h.Phys.LY = -0.42f;
        var r = h.Step();
        Assert.Equal(-0.42f, r.Virtual.LY);
        Assert.Equal(1f, r.Virtual.LX);
        Assert.False(r.LeftStickXSuppressed);
        Assert.False(r.Virtual.HasAny(PadButtons.Steering));
    }

    [Fact]
    public void Left_stick_X_is_forwarded_when_analog_steering_is_switched_off_live()
    {
        var h = new Harness(AnalogProfile());
        h.Phys.R2 = 1f;
        h.Phys.LX = 0.6f;
        Assert.Equal(0f, h.Step().Virtual.LX);
        h.Proc.Post(MacroCommandKind.DisableAnalog);
        var r = h.Step();
        Assert.Equal(0.6f, r.Virtual.LX);
        Assert.False(r.LeftStickXSuppressed);
        h.Proc.Post(MacroCommandKind.EnableAnalog);
        Assert.Equal(0f, h.Step().Virtual.LX);
    }

    [Fact]
    public void Neutral_zone_gives_no_analog_direction_and_neutral_steering()
    {
        var h = new Harness(AnalogProfile());
        foreach (var x in new[] { 0f, 0.1f, 0.2f, 0.4f, -0.4f })    // deadzone 0.20, activation 0.60 on the shaped axis
        {
            h.Phys.LX = x;
            var r = h.Step();
            Assert.Equal(SteeringDirection.None, r.AnalogSteering);
            Assert.Equal(SteeringDirection.None, r.Steering);
            Assert.Equal(DirectionOwner.None, r.Owner);
        }
    }

    [Fact]
    public void Hysteresis_keeps_direction_until_release_threshold()
    {
        var conv = new AnalogDirectionConverter();
        var s = new AnalogSettings { Deadzone = 0, ActivationThreshold = 0.6f, ReleaseThreshold = 0.3f };
        Assert.Equal(SteeringDirection.None, conv.Process(0.5f, s));
        Assert.Equal(SteeringDirection.Right, conv.Process(0.65f, s));
        Assert.Equal(SteeringDirection.Right, conv.Process(0.45f, s));
        Assert.Equal(SteeringDirection.Right, conv.Process(0.31f, s));
        Assert.Equal(SteeringDirection.None, conv.Process(0.29f, s));
        Assert.Equal(SteeringDirection.None, conv.Process(0.45f, s));
    }

    [Fact]
    public void Direct_reversal_has_no_intermediate_none()
    {
        var conv = new AnalogDirectionConverter();
        var s = new AnalogSettings { Deadzone = 0, ActivationThreshold = 0.6f, ReleaseThreshold = 0.3f };
        Assert.Equal(SteeringDirection.Left, conv.Process(-0.9f, s));
        Assert.Equal(SteeringDirection.Right, conv.Process(0.9f, s));
    }

    [Fact]
    public void Sensitivity_curve_smoothing_and_invert_shape_the_axis()
    {
        var s = new AnalogSettings { Deadzone = 0, ActivationThreshold = 0.9f, ReleaseThreshold = 0.5f, ResponseCurve = 2f };
        var c = new AnalogDirectionConverter();
        c.Process(0.5f, s);
        Assert.Equal(0.25f, c.Shaped, 3);

        var sm = new AnalogSettings { Deadzone = 0, Smoothing = 0.5f };
        var c2 = new AnalogDirectionConverter();
        c2.Process(1f, sm);
        Assert.Equal(0.5f, c2.Shaped, 3);
        c2.Process(1f, sm);
        Assert.Equal(0.75f, c2.Shaped, 3);

        Assert.Equal(SteeringDirection.Left, new AnalogDirectionConverter().Process(1f, new AnalogSettings { Deadzone = 0, InvertX = true }));
    }
}

public class DirectionTests
{
    [Fact]
    public void Left_to_Right_via_analog_is_atomic_with_no_neutral_frame()
    {
        var h = new Harness();                                  // Standard: analog on, AnalogFirst
        h.Press(PadButton.DpadLeft);
        var before = h.Step(3);
        Assert.True(before.Virtual.Has(PadButton.DpadLeft));
        h.Frames.Clear();

        h.Phys.R2 = 1f;
        h.Phys.LX = 1f;                                         // physical Left still held, R2 held activates analog steering
        var r = h.Step();

        Assert.True(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
        Assert.All(h.Frames, f => Assert.True(f.Virtual.HasAny(PadButtons.Steering)));
        Assert.All(h.Frames, f => Assert.False(f.Virtual.Has(PadButtons.Steering)));
    }

    [Fact]
    public void Dpad_to_Dpad_switch_Left_to_Right_is_one_update_and_preserves_R2_mapping_and_other_buttons()
    {
        var h = new Harness();
        h.Press(PadButton.DpadLeft | PadButton.Square | PadButton.Cross); h.Phys.R2 = 1f; h.Step(3);
        h.Frames.Clear();
        h.Release(PadButton.DpadLeft).Press(PadButton.DpadRight);
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
        Assert.True(r.Virtual.Has(PadButton.DpadDown));          // R2 -> Down untouched
        Assert.True(r.Virtual.Has(PadButton.Square | PadButton.Cross));
    }

    [Fact]
    public void Stick_sweep_from_full_left_to_full_right_never_yields_a_neutral_while_dpad_is_held()
    {
        var h = new Harness();
        h.Phys.R2 = 1f;
        h.Press(PadButton.DpadLeft);
        h.Phys.LX = -1f; h.Step(2);
        h.Frames.Clear();
        for (float x = -1f; x <= 1f; x += 0.05f) { h.Phys.LX = x; h.Step(); }
        Assert.Contains(h.Frames, f => f.Virtual.Has(PadButton.DpadRight));
        Assert.All(h.Frames, f => Assert.Equal(1, (f.Virtual.Has(PadButton.DpadLeft) ? 1 : 0) + (f.Virtual.Has(PadButton.DpadRight) ? 1 : 0)));
    }

    [Fact]
    public void Releasing_the_stick_returns_ownership_to_the_physical_dpad_without_neutral()
    {
        var h = new Harness();
        h.Phys.R2 = 1f;
        h.Press(PadButton.DpadLeft);
        h.Phys.LX = 1f; Assert.Equal(DirectionOwner.Analog, h.Step(3).Owner);
        h.Frames.Clear();
        h.Phys.LX = 0f;
        var r = h.Step(5);
        Assert.Equal(DirectionOwner.DPad, r.Owner);
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
        Assert.All(h.Frames, f => Assert.True(f.Virtual.HasAny(PadButtons.Steering)));
    }

    [Fact]
    public void Releasing_both_sources_gives_neutral_which_is_allowed()
    {
        var h = new Harness();
        h.Press(PadButton.DpadDown); h.Step(2);
        h.Release(PadButton.DpadDown);
        var r = h.Step();
        Assert.Equal(SteeringDirection.None, r.Steering);
        Assert.False(r.Virtual.HasAny(PadButtons.Steering));
    }

    [Fact]
    public void Analog_first_priority_wins_over_opposite_dpad_without_contradiction()
    {
        var h = new Harness();
        h.Phys.R2 = 1f;
        h.Press(PadButton.DpadLeft); h.Phys.LX = 1f;
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
    }

    [Fact]
    public void DPad_first_priority_prefers_dpad_over_stick()
    {
        var h = new Harness(Harness.Make(p => p.Direction.Priority = PriorityMode.DPadFirst));
        h.Phys.R2 = 1f;
        h.Press(PadButton.DpadLeft); h.Phys.LX = 1f;
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
        Assert.Equal(DirectionOwner.DPad, r.Owner);
    }

    [Fact]
    public void Last_input_wins_follows_the_most_recent_source()
    {
        var h = new Harness(Harness.Make(p => p.Direction.Priority = PriorityMode.LastInputWins));
        h.Phys.R2 = 1f;
        h.Phys.LX = 1f; h.Step(2);
        h.Press(PadButton.DpadLeft);
        Assert.True(h.Step().Virtual.Has(PadButton.DpadLeft));
        h.Phys.LX = 0f; h.Step(); h.Phys.LX = -1f;
        Assert.True(h.Step().Virtual.Has(PadButton.DpadLeft));
    }

    [Fact]
    public void Both_physical_directions_held_never_emit_contradiction_and_newest_press_wins()
    {
        var h = new Harness(Harness.Make(p => p.Analog.Enabled = false));
        h.Press(PadButton.DpadLeft); h.Step(2);
        h.Press(PadButton.DpadRight);
        var r = h.Step();
        Assert.False(r.Virtual.Has(PadButtons.Steering));
        Assert.True(r.Virtual.Has(PadButton.DpadRight));
    }

    [Fact]
    public void Contradictory_output_is_allowed_only_in_experimental_mode()
    {
        var h = new Harness(Harness.Make(p => p.Direction.AllowContradictory = true));
        h.Press(PadButton.DpadLeft | PadButton.DpadRight);
        Assert.True(h.Step().Virtual.Has(PadButtons.Steering));
    }

    [Fact]
    public void Dwell_time_blocks_direction_changes_that_are_too_fast()
    {
        var h = new Harness(Harness.Make(p => p.Timing.MinDirectionDwellMs = 20));
        h.Press(PadButton.DpadLeft); h.Step();
        h.Release(PadButton.DpadLeft).Press(PadButton.DpadRight);
        Assert.True(h.Step().Virtual.Has(PadButton.DpadLeft));   // too soon: held
        h.StepMs(30);
        Assert.True(h.Last.Virtual.Has(PadButton.DpadRight));
    }
}

public class SafetyTests
{
    [Fact]
    public void Emergency_stop_disables_remapping_and_enters_defined_raw_passthrough()
    {
        var h = new Harness();
        h.Press(PadButton.Square); h.Phys.R2 = 1f; h.Phys.LX = 1f;
        var r = h.Step(3);
        Assert.True(r.Virtual.Has(PadButton.DpadDown));

        h.Proc.Post(MacroCommandKind.EmergencyStop);
        r = h.Step();
        Assert.True(r.EmergencyStopped);
        Assert.False(r.RemappingEnabled);
        Assert.False(r.Virtual.Has(PadButton.DpadDown));        // no mapped output left behind
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
        Assert.Equal(1f, r.Virtual.R2);                          // raw pad passes: R2 as R2
        Assert.Equal(1f, r.Virtual.LX);                          // left stick X raw
        Assert.True(r.Virtual.Has(PadButton.Square));

        h.Phys = default;                                        // user lets go: nothing is stuck
        Assert.Equal(default, h.Step().Virtual);

        h.Proc.Post(MacroCommandKind.EnableRemapping);           // cannot be re-enabled while latched
        Assert.False(h.Step().RemappingEnabled);
        h.Proc.Post(MacroCommandKind.ClearEmergency);
        Assert.False(h.Step().RemappingEnabled);                 // still off until the user turns it back on
        h.Proc.Post(MacroCommandKind.EnableRemapping);
        Assert.True(h.Step().RemappingEnabled);
    }

    [Fact]
    public void Emergency_stop_button_on_the_controller_works_from_any_state()
    {
        var h = new Harness(Harness.Make(p => p.Helpers.EmergencyStopButton = PadButton.Options));
        h.Press(PadButton.Square); h.Step(2);
        h.Press(PadButton.Options);
        var r = h.Step();
        Assert.True(r.EmergencyStopped);
        Assert.False(r.Virtual.Has(PadButton.Options));          // never forwarded in the mapped state either
    }

    [Fact]
    public void Remapping_toggle_gives_raw_passthrough_and_is_independent_of_analog_toggle()
    {
        var h = new Harness(Harness.Make(p => p.Mapping.Bindings.Add(new ButtonBinding(PadButton.Triangle, PadButton.Circle))));
        h.Press(PadButton.Triangle);
        Assert.True(h.Step().Virtual.Has(PadButton.Circle));
        h.Proc.Post(MacroCommandKind.DisableRemapping);
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.Triangle));
        Assert.False(r.Virtual.Has(PadButton.Circle));
        Assert.True(r.AnalogEnabled);                             // analog switch unchanged
        h.Proc.Post(MacroCommandKind.DisableAnalog);
        h.Proc.Post(MacroCommandKind.EnableRemapping);
        r = h.Step();
        Assert.True(r.RemappingEnabled);
        Assert.False(r.AnalogEnabled);
    }

    [Fact]
    public void ResetAll_clears_toggle_and_direction_ownership()
    {
        var h = new Harness(Harness.Make(p => p.Mapping.R2Mode = TriggerMode.Toggle));
        h.Phys.R2 = 1f; h.Phys.LX = 1f; h.Step(2);
        h.Proc.ResetAll();
        h.Phys = default;
        var r = h.Step();
        Assert.Equal(default, r.Virtual);
        Assert.Equal(DirectionOwner.None, r.Owner);
    }
}

public class HelperTests
{
    [Fact]
    public void UTurn_assisted_releases_only_X_for_the_configured_time_after_an_explicit_press()
    {
        var h = new Harness(Harness.Make(p => { p.Helpers.UTurn.Enabled = true; p.Helpers.UTurn.TriggerButton = PadButton.L1; p.Helpers.UTurn.ReleaseMs = 40; }));
        h.Press(PadButton.DpadLeft | PadButton.Cross | PadButton.Square); h.Phys.R2 = 1f; h.Step(3);
        Assert.True(h.Last.Virtual.Has(PadButton.Cross));
        h.Frames.Clear();

        h.Press(PadButton.L1); var r = h.Step(); h.Release(PadButton.L1);
        Assert.False(r.Virtual.Has(PadButton.Cross));
        Assert.True(r.Virtual.Has(PadButton.Square | PadButton.DpadDown | PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.L1));                // trigger button consumed
        Assert.Equal(HelperPhase.UTurn, r.Helper);

        h.StepMs(60);
        Assert.True(h.Last.Virtual.Has(PadButton.Cross));
        Assert.All(h.Frames, f => Assert.True(f.Virtual.Has(PadButton.Square | PadButton.DpadDown | PadButton.DpadLeft)));
        int released = h.Frames.Count(f => !f.Virtual.Has(PadButton.Cross));
        Assert.InRange(released * Harness.FrameUs / 1000, 36, 46);
    }

    [Fact]
    public void UTurn_does_not_press_X_if_the_user_is_not_holding_it()
    {
        var h = new Harness(Harness.Make(p => { p.Helpers.UTurn.Enabled = true; p.Helpers.UTurn.TriggerButton = PadButton.L1; }));
        h.Press(PadButton.L1); h.Step(); h.Release(PadButton.L1);
        Assert.False(h.StepMs(200).Virtual.Has(PadButton.Cross));
    }

    [Fact]
    public void UTurn_manual_releases_X_only_while_trigger_is_held()
    {
        var h = new Harness(Harness.Make(p => { p.Helpers.UTurn.Enabled = true; p.Helpers.UTurn.Execution = UTurnExecution.Manual; p.Helpers.UTurn.TriggerButton = PadButton.L1; }));
        h.Press(PadButton.Cross | PadButton.L1);
        Assert.False(h.StepMs(500).Virtual.Has(PadButton.Cross));
        h.Release(PadButton.L1);
        Assert.True(h.Step().Virtual.Has(PadButton.Cross));
    }

    [Fact]
    public void UTurn_can_be_cancelled_immediately()
    {
        var h = new Harness(Harness.Make(p => { p.Helpers.UTurn.Enabled = true; p.Helpers.UTurn.ReleaseMs = 500; }));
        h.Press(PadButton.Cross); h.Step(2);
        h.Proc.Post(MacroCommandKind.TriggerUTurn);
        Assert.False(h.Step().Virtual.Has(PadButton.Cross));
        h.Proc.Post(MacroCommandKind.CancelHelpers);
        Assert.True(h.Step().Virtual.Has(PadButton.Cross));
    }

    [Fact]
    public void AirSwitch_jumps_once_and_flips_direction_atomically_after_explicit_trigger()
    {
        var h = new Harness(Harness.Make(p =>
        {
            p.Helpers.Air.Enabled = true; p.Helpers.Air.TriggerButton = PadButton.Circle;
            p.Helpers.Air.JumpButton = PadButton.R1; p.Helpers.Air.JumpPressMs = 20; p.Helpers.Air.SwitchAfterMs = 60;
        }));
        h.Press(PadButton.DpadLeft); h.Step(3);
        h.Frames.Clear();

        h.Press(PadButton.Circle); h.Step(); h.Release(PadButton.Circle);
        Assert.True(h.Last.Virtual.Has(PadButton.R1));
        h.StepMs(150);

        Assert.Contains(h.Frames, f => f.Virtual.Has(PadButton.DpadRight));
        Assert.True(h.Last.Virtual.Has(PadButton.DpadRight));
        Assert.False(h.Last.Virtual.Has(PadButton.R1));
        Assert.All(h.Frames, f => Assert.True(f.Virtual.HasAny(PadButtons.Steering)));
    }
}

public class ProfileTests
{
    [Fact]
    public void Factory_profiles_exist_and_round_trip_through_json()
    {
        var all = DefaultProfiles.CreateAll();
        Assert.Equal(6, all.Count);
        foreach (var p in all)
        {
            var copy = Profile.FromJson(p.ToJson());
            Assert.Equal(p.ToJson(), copy.ToJson());
        }
    }

    [Fact]
    public void Default_layout_matches_the_specification_and_helpers_are_off()
    {
        var p = DefaultProfiles.Create(DefaultProfiles.Standard);
        var m = p.Mapping.Bindings;
        Assert.Contains(m, b => b.From == PadButton.R2 && b.To == PadButton.None);
        Assert.Contains(m, b => b.From == PadButton.DpadLeft && b.To == PadButton.DpadLeft);
        Assert.Contains(m, b => b.From == PadButton.DpadRight && b.To == PadButton.DpadRight);
        Assert.Contains(m, b => b.From == PadButton.DpadDown && b.To == PadButton.DpadDown);
        Assert.False(p.Helpers.UTurn.Enabled);
        Assert.False(p.Helpers.Air.Enabled);
        Assert.Equal(TriggerMode.Hold, p.Mapping.R2Mode);
        Assert.False(p.Mapping.TriggerPassthrough);
    }

    [Fact]
    public void Randomised_input_never_violates_the_output_invariants_in_any_profile()
    {
        foreach (var p in DefaultProfiles.CreateAll())
        {
            var h = new Harness(p);
            var rnd = new Random(42);
            for (int i = 0; i < 3000; i++)
            {
                h.Phys.Buttons = (PadButton)((uint)rnd.Next() & 0x3FFFF);
                h.Phys.LX = (float)(rnd.NextDouble() * 2 - 1);
                h.Phys.LY = (float)(rnd.NextDouble() * 2 - 1);
                h.Phys.R2 = (float)rnd.NextDouble();
                var r = h.Step();
                Assert.False(r.Virtual.Has(PadButtons.Steering));
                Assert.Equal(0f, r.Virtual.R2);
                Assert.False(r.Virtual.Has(PadButton.R2));
                if (r.ContinuousActive)
                {
                    Assert.Equal(0f, r.Virtual.LX);
                    Assert.Equal(0f, r.Virtual.LY);
                }
                else
                {
                    Assert.Equal(h.Phys.LY, r.Virtual.LY);
                    Assert.Equal(h.Phys.LX, r.Virtual.LX);
                }
            }
        }
    }
}
