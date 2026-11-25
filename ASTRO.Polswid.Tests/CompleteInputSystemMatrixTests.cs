using System;
using System.Threading;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Pipeline;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

/// <summary>
/// Part 11: Mandatory 20-Test Input System Matrix verifying physical R2 suppression,
/// Left Analog conversion isolation, D-pad 1:1 passthrough, diagonal support, and
/// Polswid Continuous Steering state transitions.
/// </summary>
public sealed class CompleteInputSystemMatrixTests
{
    static Harness CreateHarness(Action<Profile>? tweak = null)
    {
        var p = DefaultProfiles.Create(DefaultProfiles.Standard);
        p.Continuous.Enabled = true;
        p.Continuous.SquareDelayMs = 60;
        tweak?.Invoke(p);
        return new Harness(p);
    }

    [Fact]
    public void Test01_NormalMode_LeftAnalogPassthrough_NoDpadConversion()
    {
        // LX=0.75, LY=-0.5 without R2 must pass through purely as analog without Dpad conversion
        var h = CreateHarness();
        h.Phys.R2 = 0f;
        h.Phys.LX = 0.75f;
        h.Phys.LY = -0.5f;

        var r = h.Step();

        Assert.Equal(0.75f, r.Virtual.LX);
        Assert.Equal(-0.5f, r.Virtual.LY);
        Assert.False(r.Virtual.HasAny(PadButtons.Steering));
        Assert.False(r.Virtual.Has(PadButton.DpadUp | PadButton.DpadDown));
        Assert.False(r.LeftStickXSuppressed);
        Assert.False(r.ContinuousActive);
    }

    [Fact]
    public void Test02_PhysicalDpadDown_MapsToVirtualDownOnly_NeverRight()
    {
        // Physical Down without R2 must produce ONLY Virtual Down
        var h = CreateHarness();
        h.Phys.R2 = 0f;
        h.Press(PadButton.DpadDown);

        var r = h.Step();

        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadUp));
    }

    [Fact]
    public void Test03_PhysicalDownAndRight_BothActiveSimultaneously()
    {
        // Diagonal: Down + Right simultaneously preserved in normal mode
        var h = CreateHarness();
        h.Phys.R2 = 0f;
        h.Press(PadButton.DpadDown | PadButton.DpadRight);

        var r = h.Step();

        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.True(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadUp));
    }

    [Fact]
    public void Test04_PhysicalDownAndLeft_BothActiveSimultaneously()
    {
        // Diagonal: Down + Left simultaneously preserved in normal mode
        var h = CreateHarness();
        h.Phys.R2 = 0f;
        h.Press(PadButton.DpadDown | PadButton.DpadLeft);

        var r = h.Step();

        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadUp));
    }

    [Fact]
    public void Test05_OtherPhysicalDiagonals_UpRight_And_UpLeft_Preserved()
    {
        var h = CreateHarness();
        h.Phys.R2 = 0f;

        // Up + Right
        h.Press(PadButton.DpadUp | PadButton.DpadRight);
        var r1 = h.Step();
        Assert.True(r1.Virtual.Has(PadButton.DpadUp));
        Assert.True(r1.Virtual.Has(PadButton.DpadRight));
        Assert.False(r1.Virtual.Has(PadButton.DpadDown | PadButton.DpadLeft));

        // Up + Left
        h.Release(PadButton.DpadUp | PadButton.DpadRight).Press(PadButton.DpadUp | PadButton.DpadLeft);
        var r2 = h.Step();
        Assert.True(r2.Virtual.Has(PadButton.DpadUp));
        Assert.True(r2.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r2.Virtual.Has(PadButton.DpadDown | PadButton.DpadRight));
    }

    [Fact]
    public void Test06_RightAnalog_UnchangedInAllModes()
    {
        var h = CreateHarness();
        h.Phys.RX = 0.42f;
        h.Phys.RY = -0.73f;

        // Normal mode
        var rNorm = h.Step();
        Assert.Equal(0.42f, rNorm.Virtual.RX);
        Assert.Equal(-0.73f, rNorm.Virtual.RY);

        // Polswid mode (R2 held)
        h.Phys.R2 = 1f;
        h.Phys.LX = 0.9f;
        var rPol = h.StepMs(100);
        Assert.Equal(0.42f, rPol.Virtual.RX);
        Assert.Equal(-0.73f, rPol.Virtual.RY);

        // Released back to normal
        h.Phys.R2 = 0f;
        var rBack = h.Step();
        Assert.Equal(0.42f, rBack.Virtual.RX);
        Assert.Equal(-0.73f, rBack.Virtual.RY);
    }

    [Fact]
    public void Test07_PressPhysicalR2_InternalActive_VirtualR2Released_DownPressed_SquarePending()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;

        var r = h.Step();

        Assert.True(r.ContinuousActive);
        Assert.Equal(0f, r.Virtual.R2);
        Assert.False(r.Virtual.Has(PadButton.R2));
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.ContinuousSquareActive);
    }

    [Fact]
    public void Test08_HoldR2PastSquareDelay_SquarePressed_DownRemainsPressed_VirtualR2Released()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;

        h.StepMs(70);
        var r = h.Last;

        Assert.True(r.ContinuousActive);
        Assert.True(r.ContinuousSquareActive);
        Assert.True(r.Virtual.Has(PadButton.Square));
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.Equal(0f, r.Virtual.R2);
        Assert.False(r.Virtual.Has(PadButton.R2));
    }

    [Fact]
    public void Test09_R2Held_LeftStickRight_ProducesRight_Down_Square_LXLYSuppressed()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;
        h.Phys.LX = 0.85f;
        h.Phys.LY = 0.40f;

        h.StepMs(70);
        var r = h.Last;

        Assert.True(r.Virtual.Has(PadButton.DpadRight));
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.True(r.Virtual.Has(PadButton.Square));
        Assert.Equal(0f, r.Virtual.LX);
        Assert.Equal(0f, r.Virtual.LY);
        Assert.Equal(0f, r.Virtual.R2);
        Assert.False(r.Virtual.Has(PadButton.R2));
        Assert.True(r.LeftStickXSuppressed);
        Assert.Equal(0f, r.Virtual.R2);
        Assert.False(r.Virtual.Has(PadButton.R2));
    }

    [Fact]
    public void Test10_R2Held_LeftStickLeft_ProducesLeft_Down_Square_LXLYSuppressed()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;
        h.Phys.LX = -0.85f;
        h.Phys.LY = -0.30f;

        h.StepMs(70);
        var r = h.Last;

        Assert.True(r.Virtual.Has(PadButton.DpadLeft));
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.True(r.Virtual.Has(PadButton.Square));
        Assert.Equal(0f, r.Virtual.LX);
        Assert.Equal(0f, r.Virtual.LY);
        Assert.Equal(0f, r.Virtual.R2);
        Assert.False(r.Virtual.Has(PadButton.R2));
        Assert.True(r.LeftStickXSuppressed);
    }

    [Fact]
    public void Test11_R2Held_LeftStickVerticalOnly_LXLYSuppressed_NoDpadUpDownFromStick()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;
        h.Phys.LX = 0f;
        h.Phys.LY = 0.90f; // Extreme vertical analog

        h.StepMs(70);
        var r = h.Last;

        Assert.Equal(0f, r.Virtual.LX);
        Assert.Equal(0f, r.Virtual.LY);
        Assert.False(r.Virtual.Has(PadButton.DpadUp));   // LY must NEVER produce DpadUp
        Assert.True(r.Virtual.Has(PadButton.DpadDown));  // Down is only from R2 synthetic sequence
    }

    [Fact]
    public void Test12_R2Held_LeftStickNeutral_FallbackLeft_Down_Square_LXLYSuppressed()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;
        h.Phys.LX = 0f;
        h.Phys.LY = 0f;

        h.StepMs(70);
        var r = h.Last;

        Assert.True(r.Virtual.Has(PadButton.DpadLeft));  // Fallback direction: Left
        Assert.False(r.Virtual.Has(PadButton.DpadRight));
        Assert.True(r.Virtual.Has(PadButton.DpadDown));
        Assert.True(r.Virtual.Has(PadButton.Square));
        Assert.Equal(0f, r.Virtual.LX);
        Assert.Equal(0f, r.Virtual.LY);
    }

    [Fact]
    public void Test13_ReleaseR2BeforeSquareDelay_SquareCancelled_DownReleased_NormalAnalogRestored()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;
        h.Phys.LX = 0.65f;
        h.StepMs(25); // before 60ms delay

        h.Phys.R2 = 0f; // early release
        var r = h.Step(5);

        Assert.False(r.ContinuousActive);
        Assert.False(r.ContinuousSquareActive);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(r.Virtual.HasAny(PadButtons.Steering));
        Assert.Equal(0.65f, r.Virtual.LX);
        Assert.False(r.LeftStickXSuppressed);
    }

    [Fact]
    public void Test14_ReleaseR2AfterSquareActive_SquareReleased_DownReleased_NormalAnalogRestored()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;
        h.Phys.LX = 0.75f;
        h.StepMs(80); // Square is now active

        Assert.True(h.Last.Virtual.Has(PadButton.Square));

        h.Phys.R2 = 0f;
        var r = h.Step(5);

        Assert.False(r.ContinuousActive);
        Assert.False(r.ContinuousSquareActive);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(r.Virtual.HasAny(PadButtons.Steering));
        Assert.Equal(0.75f, r.Virtual.LX);
        Assert.False(r.LeftStickXSuppressed);
    }

    [Fact]
    public void Test15_RapidR2Cycles_NoStuckButtons_NoVirtualR2Leak()
    {
        var h = CreateHarness();

        for (int i = 0; i < 5; i++)
        {
            h.Phys.R2 = 1f;
            h.StepMs(10);
            Assert.Equal(0f, h.Last.Virtual.R2);
            Assert.False(h.Last.Virtual.Has(PadButton.R2));

            h.Phys.R2 = 0f;
            h.StepMs(10);
            Assert.Equal(0f, h.Last.Virtual.R2);
            Assert.False(h.Last.Virtual.Has(PadButton.R2));
        }

        var r = h.Last;
        Assert.False(r.ContinuousActive);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
        Assert.False(r.Virtual.Has(PadButton.R2));
        Assert.Equal(0f, r.Virtual.R2);
    }

    [Fact]
    public void Test16_ControllerDisconnectWhileR2Held_AllSyntheticStatesCleared()
    {
        var input = new FakeInput();
        var pad = new FakePad();
        var prof = DefaultProfiles.Create(DefaultProfiles.Standard);
        prof.Continuous.Enabled = true;
        prof.Continuous.SquareDelayMs = 20;

        var proc = new PolswidProcessor(prof);
        using var runner = new PipelineRunner(input, pad, proc, new InputEventLog());
        runner.Start();

        input.Set(new GamepadState { R2 = 1f, LX = 0.8f });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000 && !(pad.Last is { } l && l.Has(PadButton.Square | PadButton.DpadDown | PadButton.DpadRight)))
            Thread.Sleep(2);

        Assert.True(pad.Last is { } active && active.Has(PadButton.Square | PadButton.DpadDown));

        // Disconnect physical pad
        input.Connected = false;
        sw.Restart();
        while (sw.ElapsedMilliseconds < 3000 && !(pad.Last is { } l2 && l2.Buttons == PadButton.None))
            Thread.Sleep(2);

        Assert.Equal(PadButton.None, pad.Last!.Value.Buttons);
        Assert.Equal(0f, pad.Last!.Value.LX);
        Assert.False(runner.Snapshot.PhysicalConnected);
        Assert.False(runner.Snapshot.ContinuousActive);
    }

    [Fact]
    public void Test17_R2Held_RepeatedSteeringChanges_DownAndSquareNeverRelease()
    {
        var h = CreateHarness();
        h.Phys.R2 = 1f;
        h.StepMs(70); // Activate Square and Down

        Assert.True(h.Last.Virtual.Has(PadButton.Square));
        Assert.True(h.Last.Virtual.Has(PadButton.DpadDown));

        // Rapidly oscillate steering between left, right, and neutral
        for (int i = 0; i < 15; i++)
        {
            h.Phys.LX = 0.9f;
            h.Step();
            Assert.True(h.Last.Virtual.Has(PadButton.DpadRight));
            Assert.True(h.Last.Virtual.Has(PadButton.Square), $"Square released at cycle {i} (right)");
            Assert.True(h.Last.Virtual.Has(PadButton.DpadDown), $"Down released at cycle {i} (right)");

            h.Phys.LX = 0f;
            h.Step();
            Assert.True(h.Last.Virtual.Has(PadButton.DpadLeft)); // Fallback
            Assert.True(h.Last.Virtual.Has(PadButton.Square), $"Square released at cycle {i} (neutral)");
            Assert.True(h.Last.Virtual.Has(PadButton.DpadDown), $"Down released at cycle {i} (neutral)");

            h.Phys.LX = -0.9f;
            h.Step();
            Assert.True(h.Last.Virtual.Has(PadButton.DpadLeft));
            Assert.True(h.Last.Virtual.Has(PadButton.Square), $"Square released at cycle {i} (left)");
            Assert.True(h.Last.Virtual.Has(PadButton.DpadDown), $"Down released at cycle {i} (left)");
        }
    }

    [Fact]
    public void Test18_PhysicalDpadDown_WithoutR2_VirtualRightNeverActivates()
    {
        var h = CreateHarness();
        h.Phys.R2 = 0f;
        h.Press(PadButton.DpadDown);

        for (int i = 0; i < 50; i++)
        {
            var r = h.Step();
            Assert.True(r.Virtual.Has(PadButton.DpadDown));
            Assert.False(r.Virtual.Has(PadButton.DpadRight), $"Virtual Right activated at frame {i}");
        }
    }

    [Fact]
    public void Test19_ModeSwitching_NormalAndR2_TenCycles_NoStaleValues()
    {
        var h = CreateHarness();

        for (int cycle = 0; cycle < 10; cycle++)
        {
            // 1. Normal mode: R2 released, LX active
            h.Phys.R2 = 0f;
            h.Phys.LX = 0.65f;
            h.Phys.LY = -0.45f;
            h.Release(PadButton.DpadDown);
            var rNorm = h.Step(5);

            Assert.False(rNorm.ContinuousActive);
            Assert.Equal(0.65f, rNorm.Virtual.LX);
            Assert.Equal(-0.45f, rNorm.Virtual.LY);
            Assert.False(rNorm.Virtual.HasAny(PadButtons.Steering));
            Assert.False(rNorm.Virtual.Has(PadButton.Square | PadButton.DpadDown | PadButton.R2));

            // 2. Polswid mode: R2 pressed, hold past square delay
            h.Phys.R2 = 1f;
            h.Phys.LX = 0.8f;
            h.StepMs(70);
            var rPol = h.Last;

            Assert.True(rPol.ContinuousActive);
            Assert.True(rPol.ContinuousSquareActive);
            Assert.Equal(0f, rPol.Virtual.LX);
            Assert.Equal(0f, rPol.Virtual.LY);
            Assert.True(rPol.Virtual.Has(PadButton.DpadRight));
            Assert.True(rPol.Virtual.Has(PadButton.DpadDown));
            Assert.True(rPol.Virtual.Has(PadButton.Square));
            Assert.Equal(0f, rPol.Virtual.R2);
        }
    }

    [Fact]
    public void Test20_CompleteMatrix_ExecutionVerification()
    {
        // Meta-verification: asserts that default profile settings align with the zero-leakage contract
        var standard = DefaultProfiles.Create(DefaultProfiles.Standard);
        Assert.True(standard.Continuous.Enabled);
        Assert.Equal(60, standard.Continuous.SquareDelayMs);
        Assert.Equal(SteeringDirection.Left, standard.Continuous.FallbackDirection);
        Assert.False(standard.Mapping.TriggerPassthrough);
        Assert.Equal(TriggerMode.Hold, standard.Mapping.R2Mode);

        var bindings = standard.Mapping.Bindings;
        Assert.Contains(bindings, b => b.From == PadButton.DpadDown && b.To == PadButton.DpadDown);
        Assert.Contains(bindings, b => b.From == PadButton.R2 && b.To == PadButton.None);
        Assert.DoesNotContain(bindings, b => b.From == PadButton.DpadDown && b.To == PadButton.DpadRight);
    }
}
