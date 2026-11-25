using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.Core.Profiles;

public static class TimingPresets
{
    public static readonly string[] Names = { "Balanced", "Low latency", "Frame-locked 60 fps", "Frame-locked 30 fps", "Custom" };

    /// <summary>Apply a preset in place. Frame-locked presets use one game frame for helper durations.</summary>
    public static void Apply(Profile p, string preset)
    {
        var t = p.Timing;
        t.Preset = preset;
        switch (preset)
        {
            case "Low latency":
                t.PollingHz = 1000; t.MaxSubmitHz = 1000; break;
            case "Frame-locked 60 fps":
                t.PollingHz = 500; t.MaxSubmitHz = 500; p.Helpers.UTurn.ReleaseMs = 17; break;
            case "Frame-locked 30 fps":
                t.PollingHz = 500; t.MaxSubmitHz = 500; p.Helpers.UTurn.ReleaseMs = 34; break;
            case "Balanced":
                t.PollingHz = 500; t.MaxSubmitHz = 500; break;
        }
    }
}

public static class DefaultProfiles
{
    /// <summary>3 = physical R2 trigger-only, D-pad 1:1 passthrough, left analog suppressed in R2 mode.</summary>
    public const int CurrentSchema = 3;
    public const string Standard = "standard";
    public const string LeftAnalog = "left-analog";
    /// <summary>Id of the profile that was called "right-analog" before the project-wide left-stick correction.</summary>
    public const string LegacyRightAnalog = "right-analog";
    public const string DPadOnly = "dpad-only";
    public const string GroundedUTurn = "grounded-uturn";
    public const string AirSwitching = "air-switching";
    public const string Custom = "custom";

    public static readonly string[] Keys = { Standard, LeftAnalog, DPadOnly, GroundedUTurn, AirSwitching, Custom };

    /// <summary>
    /// Default DS4 layout. Physical R2 is software-trigger only (bound to None).
    /// All D-pad directions map 1:1 to themselves.
    /// </summary>
    public static MappingSettings PolswidMapping()
    {
        var m = new MappingSettings();
        m.Bindings.Add(new ButtonBinding(PadButton.R2, PadButton.None));
        m.Bindings.Add(new ButtonBinding(PadButton.DpadUp, PadButton.DpadUp));
        m.Bindings.Add(new ButtonBinding(PadButton.DpadDown, PadButton.DpadDown));
        m.Bindings.Add(new ButtonBinding(PadButton.DpadLeft, PadButton.DpadLeft));
        m.Bindings.Add(new ButtonBinding(PadButton.DpadRight, PadButton.DpadRight));
        foreach (var b in new[]
        {
            PadButton.Square, PadButton.Cross, PadButton.Circle, PadButton.Triangle, PadButton.L1, PadButton.R1,
            PadButton.L2, PadButton.Options, PadButton.Share, PadButton.L3, PadButton.R3, PadButton.Guide, PadButton.Touchpad,
        })
            m.Bindings.Add(new ButtonBinding(b, b));
        return m;
    }

    public static List<Profile> CreateAll() => Keys.Select(Create).ToList();

    public static Profile Create(string key)
    {
        var p = new Profile { BuiltInKey = key, Mapping = PolswidMapping() };
        switch (key)
        {
            case Standard:
                p.Name = "Standard Polswid (DS4)";
                p.Description = "Default Polswid layout: R2 triggers Polswid mode (Down + Square + Left Analog steering). Normal D-Pad passes through 1:1.";
                p.Continuous.Enabled = true;
                break;
            case LeftAnalog:
                p.Name = "Left Analog Steering";
                p.Description = "Same layout; left-stick X steering in R2 mode with a slightly softer response.";
                p.Continuous.Enabled = true;
                p.Analog.Enabled = true;
                p.Analog.Smoothing = 0.1f;
                break;
            case DPadOnly:
                p.Name = "D-Pad Only";
                p.Description = "Left-stick conversion off: steer with D-Pad Left / D-Pad Right. Both sticks pass through unchanged.";
                p.Continuous.Enabled = false;
                p.Analog.Enabled = false;
                break;
            case GroundedUTurn:
                p.Name = "Grounded U-Turn Helper";
                p.Description = "Adds the optional U-Turn helper on L1: each press releases Cross for the configured time. Nothing happens unless you press L1.";
                p.Continuous.Enabled = true;
                p.Helpers.UTurn.Enabled = true;
                p.Helpers.UTurn.Execution = UTurnExecution.Assisted;
                p.Helpers.UTurn.TriggerButton = PadButton.L1;
                break;
            case AirSwitching:
                p.Name = "Air Switch Helper";
                p.Description = "Adds the optional air-switch helper on Circle: one press jumps, then flips the steering direction after the configured delay.";
                p.Continuous.Enabled = true;
                p.Helpers.Air.Enabled = true;
                p.Helpers.Air.TriggerButton = PadButton.Circle;
                break;
            case Custom:
                p.Name = "Custom";
                p.Description = "Based on the default layout. Everything is editable.";
                p.Continuous.Enabled = true;
                break;
            default:
                throw new ArgumentException($"Unknown built-in profile '{key}'.", nameof(key));
        }
        p.Id = key;
        p.SchemaVersion = CurrentSchema;
        return p;
    }
}
