using System;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

/// <summary>
/// Section 3 & 4: Controlled Diagnostic Scenarios A, B, C, D.
/// Verifies R2 state retention under simulated controller inputs.
/// </summary>
public sealed class ControlledDiagnosticScenariosTests
{
    static Harness MakeHarness()
    {
        var p = DefaultProfiles.Create(DefaultProfiles.Standard);
        p.Continuous.Enabled = true;
        p.Continuous.SquareDelayMs = 60;
        return new Harness(p);
    }

    [Fact]
    public void ScenarioA_R2Only_RemainsActive_NoStickMovement()
    {
        var h = MakeHarness();
        h.Phys.R2 = 0.85f;

        for (int i = 0; i < 100; i++)
        {
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Scenario A: Polswid dropped at frame {i}");
            Assert.True(r.R2Held, $"Scenario A: R2Held dropped at frame {i}");
            Assert.True(r.R2Down, $"Scenario A: r2Down dropped at frame {i}");
            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
        }
    }

    [Fact]
    public void ScenarioB_R2PlusLeftAnalog_RemainsActive_SlowNormalAndRapid()
    {
        var h = MakeHarness();
        h.Phys.R2 = 0.85f;

        // 1. Slow movement
        for (float x = -1.0f; x <= 1.0f; x += 0.05f)
        {
            h.Phys.LX = x;
            h.Phys.LY = x * 0.5f;
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Scenario B (slow): Dropped at LX={x}");
            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
        }

        // 2. Normal speed movement
        for (float x = -1.0f; x <= 1.0f; x += 0.2f)
        {
            h.Phys.LX = x;
            h.Phys.LY = -x;
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Scenario B (normal): Dropped at LX={x}");
            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
        }

        // 3. Rapid movement
        for (int i = 0; i < 50; i++)
        {
            h.Phys.LX = (i % 2 == 0) ? 1.0f : -1.0f;
            h.Phys.LY = (i % 3 == 0) ? 0.8f : -0.8f;
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Scenario B (rapid): Dropped at cycle {i}");
            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
        }
    }

    [Fact]
    public void ScenarioC_R2PlusRightAnalog_RemainsActive_AllDirections()
    {
        var h = MakeHarness();
        h.Phys.R2 = 0.85f;

        for (int i = 0; i < 50; i++)
        {
            h.Phys.RX = (float)Math.Sin(i * 0.3);
            h.Phys.RY = (float)Math.Cos(i * 0.3);
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Scenario C: Dropped at cycle {i}");
            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
            Assert.Equal(h.Phys.RX, r.Virtual.RX);
            Assert.Equal(h.Phys.RY, r.Virtual.RY);
        }
    }

    [Fact]
    public void ScenarioD_R2PlusBothAnalogSticks_RemainsActive_SimultaneousMovement()
    {
        var h = MakeHarness();
        h.Phys.R2 = 0.85f;

        for (int i = 0; i < 100; i++)
        {
            h.Phys.LX = (float)Math.Sin(i * 0.2);
            h.Phys.LY = (float)Math.Cos(i * 0.2);
            h.Phys.RX = (float)Math.Cos(i * 0.4);
            h.Phys.RY = (float)Math.Sin(i * 0.4);
            var r = h.Step();

            Assert.True(r.ContinuousActive, $"Scenario D: Dropped at cycle {i}");
            Assert.Equal(0f, r.Virtual.R2);
            Assert.False(r.Virtual.Has(PadButton.R2));
            Assert.Equal(0f, r.Virtual.LX); // Left stick suppressed
            Assert.Equal(0f, r.Virtual.LY);
            Assert.Equal(h.Phys.RX, r.Virtual.RX); // Right stick passed through
            Assert.Equal(h.Phys.RY, r.Virtual.RY);
        }
    }
}
