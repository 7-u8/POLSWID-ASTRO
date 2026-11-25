using System.Text.Json;
using System.Text.Json.Serialization;
using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.Core.Profiles;

public enum TriggerMode { Hold, Toggle }
public enum PriorityMode { AnalogFirst, DPadFirst, LastInputWins }
public enum UTurnExecution { Manual, Assisted }

/// <summary>Physical control -> virtual control. <see cref="To"/> = None suppresses the physical control.</summary>
public sealed class ButtonBinding
{
    public PadButton From { get; set; }
    public PadButton To { get; set; }
    public ButtonBinding() { }
    public ButtonBinding(PadButton from, PadButton to) { From = from; To = to; }
}

public sealed class MappingSettings
{
    public List<ButtonBinding> Bindings { get; set; } = new();
    /// <summary>Hold (default) or Toggle behaviour of the physical R2 trigger.</summary>
    public TriggerMode R2Mode { get; set; } = TriggerMode.Hold;
    /// <summary>When true the analog trigger values are forwarded even if R2/L2 are remapped.</summary>
    public bool TriggerPassthrough { get; set; }
    public float TriggerPressThreshold { get; set; } = 0.30f;
    public float TriggerReleaseThreshold { get; set; } = 0.20f;
}

public sealed class AnalogSettings
{
    /// <summary>Initial state of "Left Analog Steering" (can be toggled live, independent of remapping).</summary>
    public bool Enabled { get; set; } = true;
    public float Deadzone { get; set; } = 0.20f;
    public float ActivationThreshold { get; set; } = 0.60f;
    public float ReleaseThreshold { get; set; } = 0.35f;
    public float Sensitivity { get; set; } = 1.0f;
    /// <summary>Response curve exponent: 1 = linear.</summary>
    public float ResponseCurve { get; set; } = 1.0f;
    /// <summary>0 = off. Per-sample exponential smoothing (0..0.95).</summary>
    public float Smoothing { get; set; }
    public bool InvertX { get; set; }
    /// <summary>Zero the virtual LEFT-stick X while conversion owns the axis.</summary>
    public bool SuppressOriginal { get; set; } = true;
}

public sealed class DirectionSettings
{
    public PriorityMode Priority { get; set; } = PriorityMode.AnalogFirst;
    /// <summary>Experimental: allow Left and Right at the same time.</summary>
    public bool AllowContradictory { get; set; }
}

/// <summary>Optional helper. Disabled by default; only ever runs after an explicit user action.</summary>
public sealed class UTurnSettings
{
    public bool Enabled { get; set; }
    public UTurnExecution Execution { get; set; } = UTurnExecution.Assisted;
    public PadButton TriggerButton { get; set; } = PadButton.None;
    public PadButton ReleaseButtons { get; set; } = PadButton.Cross;
    public int ReleaseMs { get; set; } = 50;
}

/// <summary>Optional helper. Disabled by default; only ever runs after an explicit user action.</summary>
public sealed class AirSwitchSettings
{
    public bool Enabled { get; set; }
    public PadButton TriggerButton { get; set; } = PadButton.None;
    public PadButton JumpButton { get; set; } = PadButton.R1;
    public int JumpPressMs { get; set; } = 80;
    /// <summary>Time from trigger until the steering direction is flipped.</summary>
    public int SwitchAfterMs { get; set; } = 250;
}

public sealed class HelperSettings
{
    public PadButton EmergencyStopButton { get; set; } = PadButton.None;
    public PadButton CancelHelpersButton { get; set; } = PadButton.None;
    /// <summary>Hide helper trigger buttons from the game.</summary>
    public bool ConsumeControlButtons { get; set; } = true;
    public UTurnSettings UTurn { get; set; } = new();
    public AirSwitchSettings Air { get; set; } = new();
}

/// <summary>
/// R2-activated Continuous Steering. Disabled by default. Works ONLY while the physical R2 is held and reads the LEFT stick X only (the right stick is never touched).
/// While held: D-Pad Down at once, Square after a delay, and Left/Right from the left stick (neutral = fallback, default Left).
/// </summary>
public sealed class ContinuousSteeringSettings
{
    public bool Enabled { get; set; } = false;
    public int SquareDelayMs { get; set; } = 60;
    /// <summary>Direction while the stick is neutral. Left (default) or Right.</summary>
    public SteeringDirection FallbackDirection { get; set; } = SteeringDirection.Left;
}

public sealed class TimingSettings
{
    public string Preset { get; set; } = "Balanced";
    /// <summary>Physical input polling frequency.</summary>
    public int PollingHz { get; set; } = 500;
    /// <summary>Upper bound for virtual controller submissions (changes only are submitted).</summary>
    public int MaxSubmitHz { get; set; } = 500;
    /// <summary>Minimum time a direction is held before it may change (0 = switch immediately).</summary>
    public int MinDirectionDwellMs { get; set; }
}

public sealed class Profile
{
    /// <summary>Missing in old files, so they load as 1 and get migrated by the profile store.</summary>
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Profile";
    public string Description { get; set; } = "";
    /// <summary>Set for the factory profiles; used by Reset.</summary>
    public string? BuiltInKey { get; set; }
    public MappingSettings Mapping { get; set; } = new();
    public AnalogSettings Analog { get; set; } = new();
    public DirectionSettings Direction { get; set; } = new();
    public HelperSettings Helpers { get; set; } = new();
    public ContinuousSteeringSettings Continuous { get; set; } = new();
    public TimingSettings Timing { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static Profile FromJson(string json) =>
        JsonSerializer.Deserialize<Profile>(json, JsonOptions) ?? throw new JsonException("Profile JSON was empty.");

    /// <summary>Deep copy. The pipeline always receives its own copy so the UI can never mutate live state.</summary>
    public Profile Clone() => FromJson(ToJson());
}
