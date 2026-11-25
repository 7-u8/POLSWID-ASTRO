using System;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

/// <summary>
/// Section 7: Mandatory Regression Test Suite for Rapid Analog Movement & R2 State Stability.
/// Verifies that rapid Left Analog movements, stick sweeps, and biomechanical trigger micro-dips
/// never cause Polswid mode to exit or drop R2/Square/Down state while physical R2 is held.
/// </summary>
public sealed class RapidAnalogMovementTests
{
    static Harness MakeHarness(Action<Profile>? tweak = null)
    {
        var p = DefaultProfiles.Create(DefaultProfiles.Standard);
        p.Continuous.Enabled = true;
        p.Continuous.SquareDelayMs = 60;
        tweak?.Invoke(p);
        return new Harness(p);
    }

    /// <summary>
    /// Test 01 — Rapid Left/Right Alternation:
    /// Hold physical R2, alternate LX between +1.0 and -1.0 rapidly across 50 frames.
    /// Verify Polswid remains active on every frame, virtual R2 is 0, Square/Down active,
    /// and steering direction flips cleanly without interrupting Polswid.
    /// </summary>
    [Fact]
    public void Test01_RapidLeftRightAlternation_PolswidRemainsActive_EverySingleFrame()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100); // allow Square to activate

        for (int i = 0; i < 50; i++)
        {
            float targetLx = (i % 2 == 0) ? 1.0f : -1.0f;
            h.Phys.LX = targetLx;
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Polswid dropped on frame {i}");
            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
            Assert.True(r.Virtual.Has(PadButton.DpadDown), $"Down dropped on frame {i}");
            Assert.True(r.Virtual.Has(PadButton.Square), $"Square dropped on frame {i}");
            Assert.Equal(0f, r.Virtual.LX);
            Assert.Equal(0f, r.Virtual.LY);

            if (targetLx > 0)
            {
                Assert.True(r.Virtual.Has(PadButton.DpadRight), $"Expected Right steering on frame {i}");
                Assert.False(r.Virtual.Has(PadButton.DpadLeft), $"Unexpected Left steering on frame {i}");
            }
            else
            {
                Assert.True(r.Virtual.Has(PadButton.DpadLeft), $"Expected Left steering on frame {i}");
                Assert.False(r.Virtual.Has(PadButton.DpadRight), $"Unexpected Right steering on frame {i}");
            }
        }
    }

    /// <summary>
    /// Test 02 — Rapid Neutral and Direction Changes:
    /// Hold physical R2, sweep Left Analog: Neutral -> Full Right -> Full Left -> Neutral -> Full Right rapidly.
    /// Verify Polswid remains active through all transitions with zero false release.
    /// </summary>
    [Fact]
    public void Test02_RapidNeutralAndDirectionChanges_PolswidRemainsActive_NoFalseRelease()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100);

        float[] sweep = [0f, 1.0f, -1.0f, 0f, 1.0f, -1.0f, 0f, 0.5f, -0.5f, 1.0f];
        for (int i = 0; i < sweep.Length; i++)
        {
            h.Phys.LX = sweep[i];
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Polswid exited during sweep step {i} (LX={sweep[i]})");
            Assert.True(r.Virtual.Has(PadButton.DpadDown));
            Assert.True(r.Virtual.Has(PadButton.Square));
            Assert.Equal(0f, r.Virtual.R2);
            Assert.Equal(0f, r.Virtual.LX);
            Assert.Equal(0f, r.Virtual.LY);

            if (sweep[i] == 0f)
            {
                // Fallback direction (Left) applied when neutral
                Assert.True(r.Virtual.Has(PadButton.DpadLeft));
            }
        }
    }

    /// <summary>
    /// Test 03 — Maximum Analog Movement:
    /// Hold physical R2, send extreme Left Analog values (+1.0 and -1.0 repeatedly for 100 frames).
    /// Verify R2 activation remains solid and Left Analog axes are completely suppressed.
    /// </summary>
    [Fact]
    public void Test03_MaximumAnalogMovement_ExtremeValues_LXLYSuppressedThroughout()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100);

        for (int i = 0; i < 100; i++)
        {
            h.Phys.LX = (i % 2 == 0) ? 1.0f : -1.0f;
            var r = h.Step();

            Assert.True(r.ContinuousActive);
            Assert.True(r.LeftStickXSuppressed);
            Assert.Equal(0f, r.Virtual.LX);
            Assert.Equal(0f, r.Virtual.LY);
            Assert.True(r.Virtual.Has(PadButton.DpadDown));
            Assert.True(r.Virtual.Has(PadButton.Square));
        }
    }

    /// <summary>
    /// Test 04 — Rapid Two-Axis Movement:
    /// Hold physical R2, rapidly change both LX and LY simultaneously across extreme values.
    /// Verify only LX controls horizontal steering, LY never generates D-pad Up or Down.
    /// </summary>
    [Fact]
    public void Test04_RapidTwoAxisMovement_OnlyLXControlsSteering_LYNeverGeneratesUpDown()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100);

        (float lx, float ly)[] points =
        [
            (1.0f, 1.0f), (-1.0f, -1.0f), (1.0f, -1.0f), (-1.0f, 1.0f),
            (0.8f, -0.9f), (-0.7f, 0.85f), (1.0f, 0.0f), (0.0f, 1.0f)
        ];

        for (int i = 0; i < points.Length; i++)
        {
            h.Phys.LX = points[i].lx;
            h.Phys.LY = points[i].ly;
            var r = h.Step();

            Assert.True(r.ContinuousActive);
            Assert.False(r.Virtual.Has(PadButton.DpadUp), $"Unexpected DpadUp at step {i}");
            Assert.True(r.Virtual.Has(PadButton.DpadDown));
            Assert.True(r.Virtual.Has(PadButton.Square));
            Assert.Equal(0f, r.Virtual.LX);
            Assert.Equal(0f, r.Virtual.LY);
        }
    }

    /// <summary>
    /// Test 05 — Rapid Movement With Square Active:
    /// Beyond SquareDelayMs, rapidly alternate Left Analog. Square and Down must remain continuously active.
    /// </summary>
    [Fact]
    public void Test05_RapidMovementWithSquareActive_SquareAndDownNeverDrop()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(80); // Square active

        for (int i = 0; i < 60; i++)
        {
            h.Phys.LX = (i % 2 == 0) ? 0.95f : -0.95f;
            var r = h.Step();

            Assert.True(r.ContinuousSquareActive, $"Square dropped at iteration {i}");
            Assert.True(r.Virtual.Has(PadButton.Square), $"Virtual Square bit missing at iteration {i}");
            Assert.True(r.Virtual.Has(PadButton.DpadDown), $"Virtual Down bit missing at iteration {i}");
        }
    }

    /// <summary>
    /// Test 06 — Genuine Physical R2 Release:
    /// Hold physical R2 with rapid analog movement, then release R2.
    /// Polswid must deactivate promptly (~10 ms), Square and Down released, normal analog restored.
    /// </summary>
    [Fact]
    public void Test06_GenuinePhysicalR2Release_PolswidDeactivatesPromptly_NormalAnalogRestored()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100);

        // Rapid movement while held
        for (int i = 0; i < 20; i++)
        {
            h.Phys.LX = (i % 2 == 0) ? 1.0f : -1.0f;
            h.Step();
        }

        // Genuine physical release
        h.Phys.R2 = 0f;
        h.Phys.LX = 0.7f;
        h.Phys.LY = -0.4f;
        var r = h.StepMs(10); // promptly ~10 ms

        Assert.False(r.ContinuousActive);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));
        Assert.Equal(0.7f, r.Virtual.LX);
        Assert.Equal(-0.4f, r.Virtual.LY);
    }

    /// <summary>
    /// Test 07 — Rapid R2 Press/Release Cycles:
    /// Repeated press and release with rapid analog movement.
    /// No stuck buttons, no false activation after release, correct reactivation on press.
    /// </summary>
    [Fact]
    public void Test07_RapidR2PressReleaseCycles_NoStuckButtons_CorrectReactivation()
    {
        var h = MakeHarness();

        for (int cycle = 0; cycle < 10; cycle++)
        {
            // Press R2
            h.Phys.R2 = 1f;
            h.Phys.LX = 1.0f;
            h.StepMs(80);
            Assert.True(h.Last.ContinuousActive);
            Assert.True(h.Last.Virtual.Has(PadButton.Square));
            Assert.True(h.Last.Virtual.Has(PadButton.DpadDown));

            // Release R2
            h.Phys.R2 = 0f;
            h.Phys.LX = -0.8f;
            h.StepMs(15);
            Assert.False(h.Last.ContinuousActive);
            Assert.False(h.Last.Virtual.Has(PadButton.Square));
            Assert.False(h.Last.Virtual.Has(PadButton.DpadDown));
            Assert.Equal(-0.8f, h.Last.Virtual.LX);
        }
    }

    /// <summary>
    /// Test 08 — Long-Duration Hold:
    /// Extended sequence of 500+ rapid analog movements while holding R2.
    /// Polswid remains continuously active throughout the entire sequence.
    /// </summary>
    [Fact]
    public void Test08_LongDurationHold_500RapidMovements_ContinuouslyActive()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100);

        for (int i = 0; i < 500; i++)
        {
            h.Phys.LX = (i % 3 == 0) ? 1.0f : (i % 3 == 1) ? -1.0f : 0.0f;
            h.Phys.LY = (i % 2 == 0) ? 0.5f : -0.5f;
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Dropped at cycle {i}");
            Assert.True(r.Virtual.Has(PadButton.Square));
            Assert.True(r.Virtual.Has(PadButton.DpadDown));
            Assert.Equal(0f, r.Virtual.R2);
            Assert.Equal(0f, r.Virtual.LX);
            Assert.Equal(0f, r.Virtual.LY);
        }
    }

    /// <summary>
    /// Test 09 — Input Snapshot Consistency:
    /// Rapidly changing LX/LY values while maintaining constant R2 never produces a virtual report
    /// where R2 is released or remapped R2 is forwarded.
    /// </summary>
    [Fact]
    public void Test09_InputSnapshotConsistency_AnalogUpdatesNeverInvalidateR2State()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100);

        for (int i = 0; i < 50; i++)
        {
            h.Phys.LX = (float)Math.Sin(i * 0.5);
            h.Phys.LY = (float)Math.Cos(i * 0.5);
            var r = h.Step();

            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
            Assert.True(r.R2MappedActive);
            Assert.True(r.ContinuousActive);
        }
    }

    /// <summary>
    /// Test 10 — Final Virtual Report Integrity:
    /// For every frame during rapid movement, inspect all fields of the virtual report.
    /// </summary>
    [Fact]
    public void Test10_FinalVirtualReportIntegrity_AllFieldsVerified()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.StepMs(100);

        for (int i = 0; i < 30; i++)
        {
            h.Phys.LX = (i % 2 == 0) ? 1.0f : -1.0f;
            var r = h.Step();

            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
            Assert.Equal(0f, r.Virtual.LX);
            Assert.Equal(0f, r.Virtual.LY);
            Assert.True(r.Virtual.Has(PadButton.DpadDown));
            Assert.False(r.Virtual.Has(PadButton.DpadUp));
            Assert.True(r.Virtual.Has(PadButton.Square));

            // No other unexpected buttons set
            var unexpected = r.Virtual.Buttons & ~(PadButton.DpadDown | PadButton.DpadLeft | PadButton.DpadRight | PadButton.Square);
            Assert.Equal(PadButton.None, unexpected);
        }
    }

    /// <summary>
    /// Test 11 — Toggle Mode Regression:
    /// In Toggle Mode, rapid analog movement must not cause the toggle state to flip unexpectedly.
    /// </summary>
    [Fact]
    public void Test11_ToggleModeRegression_RapidMovementDoesNotFlipToggle()
    {
        var h = MakeHarness(p => p.Mapping.R2Mode = TriggerMode.Toggle);

        // Tap R2 to activate Toggle mode
        h.Phys.R2 = 1f; h.Step();
        h.Phys.R2 = 0f; h.Step();
        h.StepMs(100); // let Square activate

        Assert.True(h.Last.ContinuousActive);

        // Rapid movements while toggled
        for (int i = 0; i < 100; i++)
        {
            h.Phys.LX = (i % 2 == 0) ? 1.0f : -1.0f;
            var r = h.Step();
            Assert.True(r.ContinuousActive, $"Toggle state flipped at step {i}");
            Assert.True(r.Virtual.Has(PadButton.Square));
            Assert.True(r.Virtual.Has(PadButton.DpadDown));
        }

        // Tap R2 again to turn toggle off
        h.Phys.R2 = 1f; h.Step();
        h.Phys.R2 = 0f; h.Step();
        Assert.False(h.Last.ContinuousActive);
    }

    /// <summary>
    /// Test 12 — Disconnect and Reconnect:
    /// While holding R2 with rapid analog movement, simulate controller disconnect.
    /// State cleans up safely, replug works cleanly.
    /// </summary>
    [Fact]
    public void Test12_DisconnectAndReconnect_StateCleansUpSafely()
    {
        var h = MakeHarness();
        h.Phys.R2 = 1f;
        h.Phys.LX = 1.0f;
        h.StepMs(100);
        Assert.True(h.Last.ContinuousActive);

        // Disconnect
        h.Proc.ResetAll();
        h.Phys = default;
        var r = h.Step();

        Assert.False(r.ContinuousActive);
        Assert.False(r.Virtual.Has(PadButton.Square));
        Assert.False(r.Virtual.Has(PadButton.DpadDown));

        // Reconnect with R2 held
        h.Phys.R2 = 1f;
        h.Phys.LX = -1.0f;
        var r2 = h.Step();
        Assert.True(r2.ContinuousActive);
        Assert.True(r2.Virtual.Has(PadButton.DpadDown));
    }

    /// <summary>
    /// Test 13 — Biomechanical Trigger Micro-Dip Resilience:
    /// While holding R2 (0.80f), rapid stick movements cause R2 to momentarily dip to 0.15f
    /// (below 0.20f release threshold) for 1 to 4 frames before rebounding to 0.80f.
    /// Verify Polswid remains continuously active and never drops Square or Down.
    /// </summary>
    [Fact]
    public void Test13_BiomechanicalTriggerMicroDips_FilteredWithoutInterruption()
    {
        var h = MakeHarness();
        h.Phys.R2 = 0.80f;
        h.StepMs(100); // Square active

        Assert.True(h.Last.ContinuousActive);
        Assert.True(h.Last.Virtual.Has(PadButton.Square));

        // Simulate 10 violent stick flick cycles, each causing a 3-frame micro-dip on R2
        for (int cycle = 0; cycle < 10; cycle++)
        {
            // Rapid flick to right
            h.Phys.LX = 1.0f;
            h.Phys.R2 = 0.15f; // micro-dip below release threshold (0.20f)
            h.Step(3);         // 3 frames (6 ms) micro-dip

            Assert.True(h.Last.ContinuousActive, $"Polswid dropped during micro-dip at cycle {cycle}");
            Assert.True(h.Last.Virtual.Has(PadButton.Square), $"Square dropped during micro-dip at cycle {cycle}");
            Assert.True(h.Last.Virtual.Has(PadButton.DpadDown), $"Down dropped during micro-dip at cycle {cycle}");

            // Grip stabilizes
            h.Phys.R2 = 0.80f;
            h.Step(5);

            // Rapid flick to left
            h.Phys.LX = -1.0f;
            h.Phys.R2 = 0.12f; // micro-dip
            h.Step(2);         // 2 frames (4 ms) micro-dip

            Assert.True(h.Last.ContinuousActive, $"Polswid dropped during left flick micro-dip at cycle {cycle}");
            Assert.True(h.Last.Virtual.Has(PadButton.Square));
            Assert.True(h.Last.Virtual.Has(PadButton.DpadDown));

            // Grip stabilizes
            h.Phys.R2 = 0.80f;
            h.Step(5);
        }

        // Now test that sustained release (> 5 frames) DOES release promptly
        h.Phys.R2 = 0.15f;
        h.Step(5); // 5 consecutive frames sustained release
        Assert.False(h.Last.ContinuousActive, "Sustained release did not deactivate Polswid promptly");
    }
}
