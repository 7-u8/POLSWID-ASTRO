using System.Threading;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Pipeline;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

public class ContinuousSteeringTests
{
    static Harness Make(Action<Profile>? tweak = null) =>
        new(Harness.Make(p => { p.Continuous.Enabled = true; p.Continuous.SquareDelayMs = 60; tweak?.Invoke(p); }));

    static bool Steer(FrameResult r) => r.Virtual.HasAny(PadButtons.Steering);

    [Fact]
    public void Feature_is_enabled_by_default_in_standard_profiles_and_disabled_in_dpad_only()
    {
        Assert.True(DefaultProfiles.Create(DefaultProfiles.Standard).Continuous.Enabled);
        Assert.True(DefaultProfiles.Create(DefaultProfiles.LeftAnalog).Continuous.Enabled);
        Assert.False(DefaultProfiles.Create(DefaultProfiles.DPadOnly).Continuous.Enabled);

        var h = new Harness(DefaultProfiles.Create(DefaultProfiles.DPadOnly));
        h.Phys.R2 = 1f;
        var r = h.StepMs(200);
        Assert.False(r.ContinuousActive);
        Assert.False(r.Virtual.Has(PadButton.Square));       // no synthetic Square
        Assert.False(Steer(r));                               // no forced direction
    }

    [Fact]
    public void R2_released_gives_normal_analog_behaviour_and_no_forced_direction()
    {
        var h = Make(p => p.Analog.Enabled = false);
        h.Phys.LX = 0.7f; h.Phys.LY = -0.3f;
        var r = h.Step(5);
        Assert.False(r.ContinuousActive);
        Assert.Equal(0.7f, r.Virtual.LX);                    // left stick passes through untouched
        Assert.Equal(-0.3f, r.Virtual.LY);
        Assert.False(Steer(r));
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
    }

    [Fact]
    public void R2_held_with_neutral_stick_forces_Left_and_Down_immediately()
    {
        var h = Make();
        h.Phys.R2 = 1f;
        var r = h.Step();
        Assert.True(r.ContinuousActive);
        Assert.True(r.Virtual.Has(PadButton.DpadDown));      // immediately
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
        Assert.Equal(DirectionOwner.Continuous, r.Owner);
    }

    [Fact]
    public void R2_held_stick_right_gives_Right_and_stick_left_gives_Left_and_return_to_neutral_gives_Left()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.Step(2);

        h.Phys.LX = 1f; var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadRight)); Assert.False(r.Virtual.Has(PadButton.DpadLeft));

        h.Phys.LX = 0f; r = h.Step();                         // released after moving: back to Left right away
        Assert.True(r.Virtual.Has(PadButton.DpadLeft)); Assert.False(r.Virtual.Has(PadButton.DpadRight));

        h.Phys.LX = -1f; r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadLeft)); Assert.False(r.Virtual.Has(PadButton.DpadRight));

        h.Phys.LX = 1f; h.Step(); h.Phys.LX = 0.1f; r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
    }

    [Fact]
    public void Horizontal_direction_is_never_neutral_while_R2_is_held_even_through_a_full_stick_sweep()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.Phys.LX = -1f; h.Step(2);
        h.Frames.Clear();
        for (float x = -1f; x <= 1f; x += 0.02f) { h.Phys.LX = x; h.Step(); }
        for (float x = 1f; x >= -1f; x -= 0.02f) { h.Phys.LX = x; h.Step(); }
        Assert.All(h.Frames, f => Assert.Equal(1, (f.Virtual.Has(PadButton.DpadLeft) ? 1 : 0) + (f.Virtual.Has(PadButton.DpadRight) ? 1 : 0)));
        Assert.Contains(h.Frames, f => f.Virtual.Has(PadButton.DpadRight));
    }

    [Fact]
    public void Square_appears_only_after_the_configured_delay_and_stays_until_R2_is_released()
    {
        var h = Make();
        h.Phys.R2 = 1f;
        var r = h.Step();
        Assert.False(r.Virtual.Has(PadButton.Square));       // not at the very start
        h.StepMs(30);
        Assert.False(h.Last.Virtual.Has(PadButton.Square));
        h.StepMs(60);
        Assert.True(h.Last.Virtual.Has(PadButton.Square));
        Assert.True(h.Last.ContinuousSquareActive);
        h.StepMs(500);
        Assert.True(h.Last.Virtual.Has(PadButton.Square));   // continuously pressed while held
    }

    [Fact]
    public void Releasing_R2_removes_Square_Down_and_the_synthetic_direction_and_restores_right_stick_passthrough()
    {
        var h = Make(p => p.Analog.Enabled = false);
        h.Phys.R2 = 1f; h.Phys.LX = 1f; h.StepMs(200);
        Assert.True(h.Last.Virtual.Has(PadButton.Square | PadButton.DpadDown | PadButton.DpadRight));
        Assert.Equal(0f, h.Last.Virtual.LX);                  // synthetic steering owns X while held

        h.Phys.R2 = 0f;                                       // R2 released after steering right
        var r = h.Step(5);
        Assert.False(r.ContinuousActive);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(Steer(r));                               // synthetic override removed in the same frame
        Assert.Equal(1f, r.Virtual.LX);                       // physical left stick passes through again
    }

    [Fact]
    public void Physical_square_still_works_after_release_and_is_not_swallowed_while_held()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.Phys.Buttons |= PadButton.Square; h.StepMs(200);
        h.Phys.Buttons &= ~PadButton.Square;
        Assert.True(h.Step().Virtual.Has(PadButton.Square)); // synthetic Square stays although the physical one was let go
        h.Phys.R2 = 0f;
        Assert.False(h.Step(5).Virtual.Has(PadButton.Square));
        h.Phys.Buttons |= PadButton.Square;
        Assert.True(h.Step().Virtual.Has(PadButton.Square)); // plain physical passthrough afterwards
    }

    [Fact]
    public void Right_stick_is_never_used_and_always_passes_through()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.StepMs(100);
        h.Phys.RX = 1f; h.Phys.RY = -0.8f;                    // would mean "right" if the wrong stick were read
        var r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));       // neutral LEFT stick -> Left, regardless of the right stick
        Assert.Equal(1f, r.Virtual.RX); Assert.Equal(-0.8f, r.Virtual.RY);

        h.Phys.RX = -1f; h.Phys.LX = 1f;
        r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadRight));      // left stick decides
        Assert.Equal(-1f, r.Virtual.RX);
    }

    [Fact]
    public void Outputs_coexist_Down_delayed_Square_steering_and_unrelated_buttons()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.Phys.Buttons |= PadButton.Cross | PadButton.R1; h.Phys.LY = 0.5f;
        h.StepMs(120);
        for (int i = 0; i < 10; i++)
        {
            h.Phys.LX = i % 2 == 0 ? 1f : -1f;
            var r = h.Step(3);
            Assert.True(r.Virtual.Has(PadButton.DpadDown | PadButton.Square | PadButton.Cross | PadButton.R1));
            Assert.Equal(i % 2 == 0, r.Virtual.Has(PadButton.DpadRight));
            Assert.Equal(i % 2 != 0, r.Virtual.Has(PadButton.DpadLeft));
            Assert.Equal(0f, r.Virtual.LY);                   // both left stick axes are suppressed in R2 mode
        }
    }

    [Fact]
    public void Fallback_direction_is_configurable()
    {
        var h = Make(p => p.Continuous.FallbackDirection = SteeringDirection.Right);
        h.Phys.R2 = 1f;
        Assert.True(h.Step().Virtual.Has(PadButton.DpadRight));
        h.Phys.LX = -1f;
        Assert.True(h.Step().Virtual.Has(PadButton.DpadLeft));
    }

    [Fact]
    public void Rapid_R2_presses_never_leave_a_synthetic_button_stuck()
    {
        var h = Make(p => p.Analog.Enabled = false);
        var rnd = new Random(7);
        bool prevHeld = false;
        for (int i = 0; i < 500; i++)
        {
            bool held = rnd.Next(0, 3) != 0;
            h.Phys.R2 = held ? 1f : 0f;
            h.Phys.LX = (float)(rnd.NextDouble() * 2 - 1);
            var r = h.Step(held != prevHeld ? 5 : 1);
            prevHeld = held;
            if (held)
            {
                Assert.True(r.Virtual.Has(PadButton.DpadDown));
                Assert.Equal(1, (r.Virtual.Has(PadButton.DpadLeft) ? 1 : 0) + (r.Virtual.Has(PadButton.DpadRight) ? 1 : 0));
            }
            else
            {
                Assert.False(r.Virtual.Has(PadButton.Square), "Square stuck after release");
                Assert.False(r.Virtual.Has(PadButton.DpadDown), "Down stuck after release");
                Assert.False(Steer(r), "synthetic direction stuck after release");
                Assert.Equal(h.Phys.LX, r.Virtual.LX);        // normal left stick passthrough
            }
        }
    }

    [Fact]
    public void Square_delay_restarts_on_every_new_press()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.StepMs(100);
        Assert.True(h.Last.Virtual.Has(PadButton.Square));
        h.Phys.R2 = 0f; h.Step(5);
        h.Phys.R2 = 1f;
        Assert.False(h.Step().Virtual.Has(PadButton.Square));
        h.StepMs(100);
        Assert.True(h.Last.Virtual.Has(PadButton.Square));
    }

    [Fact]
    public void Emergency_stop_and_remapping_off_remove_all_synthetic_output()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.StepMs(150);
        h.Proc.Post(MacroCommandKind.EmergencyStop);
        var r = h.Step();
        Assert.False(r.ContinuousActive);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(Steer(r));
        Assert.Equal(1f, r.Virtual.R2);                       // raw passthrough
    }

    [Fact]
    public void Continuous_state_resets_cleanly_on_ResetAll_and_profile_swap()
    {
        var h = Make();
        h.Phys.R2 = 1f; h.StepMs(150);
        h.Proc.ResetAll();
        h.Phys = default;
        Assert.Equal(default, h.Step().Virtual);

        h.Phys.R2 = 1f; h.StepMs(150);
        h.Proc.SetProfile(Harness.Make(p => p.Continuous.Enabled = false)); // feature explicitly off in the new profile
        h.Phys = default;
        var r = h.Step();
        Assert.False(r.ContinuousActive);
        Assert.Equal(default, r.Virtual);
    }

    [Fact]
    public void Profile_with_the_feature_round_trips_through_json_and_old_files_default_to_disabled()
    {
        var p = Harness.Make(x => { x.Continuous.Enabled = true; x.Continuous.SquareDelayMs = 90; });
        var copy = Profile.FromJson(p.ToJson());
        Assert.True(copy.Continuous.Enabled); Assert.Equal(90, copy.Continuous.SquareDelayMs);

        var old = System.Text.Json.Nodes.JsonNode.Parse(p.ToJson())!.AsObject();
        old.Remove("Continuous");                              // a profile saved by an older version
        Assert.False(Profile.FromJson(old.ToJsonString()).Continuous.Enabled);
    }

    [Fact]
    public void Controller_disconnect_while_R2_is_held_releases_everything_through_the_pipeline()
    {
        var input = new FakeInput();
        var pad = new FakePad();
        var prof = Harness.Make(p => { p.Continuous.Enabled = true; p.Continuous.SquareDelayMs = 20; });
        var proc = new PolswidProcessor(prof);
        using var runner = new PipelineRunner(input, pad, proc, new InputEventLog());
        runner.Start();

        input.Set(new GamepadState { R2 = 1f, LX = 1f });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000 && !(pad.Last is { } l && l.Has(PadButton.Square | PadButton.DpadDown | PadButton.DpadRight))) Thread.Sleep(2);
        Assert.True(pad.Last is { } s && s.Has(PadButton.Square | PadButton.DpadDown | PadButton.DpadRight));
        Assert.True(runner.Snapshot.ContinuousActive);

        input.Connected = false;
        sw.Restart();
        while (sw.ElapsedMilliseconds < 3000 && !(pad.Last is { } l2 && l2.Buttons == PadButton.None)) Thread.Sleep(2);
        Assert.Equal(PadButton.None, pad.Last!.Value.Buttons);
        Assert.Equal(0f, pad.Last!.Value.LX);

        input.Set(default); input.Connected = true;           // replug with R2 released: nothing synthetic comes back
        Thread.Sleep(150);
        Assert.Equal(PadButton.None, pad.Last!.Value.Buttons);
    }
}
