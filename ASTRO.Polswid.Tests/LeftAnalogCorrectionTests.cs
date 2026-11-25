using System.IO;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using ASTRO.Polswid.Infrastructure.Configuration;
using Xunit;

namespace ASTRO.Polswid.Tests;

/// <summary>Project-wide correction: ALL analog steering uses the LEFT stick X. The right stick is never read or changed.</summary>
public class LeftAnalogCorrectionTests
{
    [Fact]
    public void Right_stick_never_influences_steering_or_any_other_output_in_any_state()
    {
        foreach (var key in DefaultProfiles.Keys)
            foreach (bool cont in new[] { false, true })
            {
                var rnd = new Random(11);
                var a = new Harness(Harness.Make(p => p.Continuous.Enabled = cont, key));
                var b = new Harness(Harness.Make(p => p.Continuous.Enabled = cont, key));
                for (int i = 0; i < 1500; i++)
                {
                    var s = new GamepadState
                    {
                        Buttons = (PadButton)((uint)rnd.Next() & 0x3FFFF), LX = (float)(rnd.NextDouble() * 2 - 1), LY = (float)(rnd.NextDouble() * 2 - 1),
                        L2 = (float)rnd.NextDouble(), R2 = (float)rnd.NextDouble(),
                    };
                    a.Phys = s; b.Phys = s;
                    a.Phys.RX = (float)(rnd.NextDouble() * 2 - 1); a.Phys.RY = (float)(rnd.NextDouble() * 2 - 1);   // b keeps the right stick centred
                    var ra = a.Step(); var rb = b.Step();
                    // identical everywhere except the right stick itself, which passes through untouched
                    Assert.Equal(rb.Virtual.Buttons, ra.Virtual.Buttons);
                    Assert.Equal(rb.Virtual.LX, ra.Virtual.LX); Assert.Equal(rb.Virtual.LY, ra.Virtual.LY);
                    Assert.Equal(rb.Steering, ra.Steering);
                    Assert.Equal(a.Phys.RX, ra.Virtual.RX); Assert.Equal(a.Phys.RY, ra.Virtual.RY);
                }
            }
    }

    [Fact]
    public void Analog_to_dpad_conversion_activates_only_while_R2_held_and_suppresses_both_left_axes()
    {
        var h = new Harness();                                  // Standard: analog steering on in R2 mode
        // R2 released: normal Left Analog passthrough
        h.Phys.R2 = 0f;
        h.Phys.LX = 1f; h.Phys.LY = 0.4f; h.Phys.RX = -1f; h.Phys.RY = 0.6f;
        var r = h.Step();
        Assert.Equal(1f, r.Virtual.LX);
        Assert.Equal(0.4f, r.Virtual.LY);
        Assert.False(r.Virtual.HasAny(PadButtons.Steering));

        // R2 held: Left stick X converts to D-pad, both X and Y are suppressed, right stick passes through
        h.Phys.R2 = 1f;
        r = h.Step();
        Assert.True(r.Virtual.Has(PadButton.DpadRight));        // left stick right wins; right stick left is ignored
        Assert.False(r.Virtual.Has(PadButton.DpadLeft));
        Assert.Equal(0f, r.Virtual.LX);                         // both left stick axes are suppressed
        Assert.Equal(0f, r.Virtual.LY);
        Assert.True(r.LeftStickXSuppressed);
        Assert.Equal(-1f, r.Virtual.RX);                        // right stick untouched
        Assert.Equal(0.6f, r.Virtual.RY);
    }

    [Fact]
    public void Continuous_table_from_the_spec_with_the_left_stick()
    {
        var h = new Harness(Harness.Make(p => { p.Continuous.Enabled = true; p.Continuous.SquareDelayMs = 20; p.Analog.Enabled = false; }));
        h.Phys.R2 = 1f;
        Assert.True(h.Step().Virtual.Has(PadButton.DpadLeft));                  // neutral -> Left
        h.Phys.LX = 1f;  Assert.True(h.Step().Virtual.Has(PadButton.DpadRight)); // right -> Right
        h.Phys.LX = 0f;  Assert.True(h.Step().Virtual.Has(PadButton.DpadLeft));  // released -> Left
        h.Phys.LX = -1f; Assert.True(h.Step().Virtual.Has(PadButton.DpadLeft));  // left -> Left
        h.StepMs(40);
        Assert.True(h.Last.Virtual.Has(PadButton.DpadDown | PadButton.Square));  // coexist with Down + delayed Square

        h.Phys.R2 = 0f; h.Phys.LX = 0.8f;
        var r = h.Step(5);                                                         // R2 released: normal left-stick passthrough
        Assert.Equal(0.8f, r.Virtual.LX);                                        // normal left-stick passthrough
        Assert.False(r.Virtual.HasAny(PadButtons.Steering));                      // nothing forced
        Assert.False(r.Virtual.Has(PadButton.Square | PadButton.DpadDown));
    }

    [Fact]
    public void Factory_profile_is_called_left_analog_and_no_right_analog_profile_remains()
    {
        Assert.Contains(DefaultProfiles.CreateAll(), p => p.Id == "left-analog" && p.Name == "Left Analog Steering");
        Assert.DoesNotContain(DefaultProfiles.CreateAll(), p => p.Id == "right-analog" || p.Name.Contains("Right Analog"));
        Assert.DoesNotContain(DefaultProfiles.CreateAll(), p => p.Description.Contains("right stick", StringComparison.OrdinalIgnoreCase) && !p.Description.Contains("passes through"));
        Assert.All(DefaultProfiles.CreateAll(), p => Assert.Equal(DefaultProfiles.CurrentSchema, p.SchemaVersion));
    }

    [Fact]
    public void Old_right_analog_profile_files_are_migrated_keeping_the_users_tuned_values()
    {
        var dir = Path.Combine(Path.GetTempPath(), "astro-migrate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProfileStore(dir);
            // a version-1 file as an older release wrote it
            var old = DefaultProfiles.Create(DefaultProfiles.LeftAnalog);
            old.Id = "right-analog"; old.BuiltInKey = "right-analog"; old.Name = "Right Analog Steering";
            old.Description = "Same layout; right-stick X owns steering";
            old.Analog.Deadzone = 0.33f; old.Analog.ActivationThreshold = 0.71f;
            var node = System.Text.Json.Nodes.JsonNode.Parse(old.ToJson())!.AsObject();
            node.Remove("SchemaVersion"); node.Remove("Continuous");
            File.WriteAllText(Path.Combine(store.ProfilesDir, "right-analog.json"), node.ToJsonString());

            var all = store.LoadAll();
            var p = Assert.Single(all, x => x.BuiltInKey == "left-analog");
            Assert.Equal("left-analog", p.Id);
            Assert.Equal("Left Analog Steering", p.Name);
            Assert.DoesNotContain("right", p.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0.33f, p.Analog.Deadzone);                    // the user's tuning survives
            Assert.Equal(0.71f, p.Analog.ActivationThreshold);
            Assert.Equal(DefaultProfiles.CurrentSchema, p.SchemaVersion);
            Assert.True(p.Continuous.Enabled);
            Assert.False(File.Exists(Path.Combine(store.ProfilesDir, "right-analog.json")));
            Assert.True(File.Exists(Path.Combine(store.ProfilesDir, "left-analog.json")));

            Assert.Contains(new ProfileStore(dir).LoadAll(), x => x.Id == "left-analog");   // second load is stable
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}

public class LegacyProfileCoexistenceTests
{
    [Fact]
    public void Legacy_file_next_to_an_already_migrated_file_does_not_create_a_duplicate_and_is_removed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "astro-coexist-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProfileStore(dir);
            var migrated = DefaultProfiles.Create(DefaultProfiles.LeftAnalog);
            migrated.Analog.Deadzone = 0.44f;                      // newer edit made after the first migration
            store.Save(migrated);
            var old = DefaultProfiles.Create(DefaultProfiles.LeftAnalog);
            old.Id = "right-analog"; old.BuiltInKey = "right-analog"; old.Name = "Right Analog Steering";
            var node = System.Text.Json.Nodes.JsonNode.Parse(old.ToJson())!.AsObject();
            node.Remove("SchemaVersion");
            File.WriteAllText(Path.Combine(store.ProfilesDir, "right-analog.json"), node.ToJsonString());

            var all = store.LoadAll();
            var p = Assert.Single(all, x => x.Id == "left-analog");
            Assert.Equal(0.44f, p.Analog.Deadzone);
            Assert.False(File.Exists(Path.Combine(store.ProfilesDir, "right-analog.json")));
            Assert.Equal(all.Count, all.Select(x => x.Id).Distinct().Count());
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
