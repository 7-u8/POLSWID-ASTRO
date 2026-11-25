using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Polswid;

namespace ASTRO.Polswid.Core.Diagnostics;

/// <summary>Minimal logging abstraction so Core has no dependency on a logging library.</summary>
public interface ILog
{
    void Debug(string template, params object?[] args);
    void Information(string template, params object?[] args);
    void Warning(string template, params object?[] args);
    void Error(Exception? ex, string template, params object?[] args);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();
    public void Debug(string template, params object?[] args) { }
    public void Information(string template, params object?[] args) { }
    public void Warning(string template, params object?[] args) { }
    public void Error(Exception? ex, string template, params object?[] args) { }
}

public enum InputEventKind { Physical, Virtual, Direction, Macro, Device, System }

public readonly record struct InputEvent(long Sequence, long TimestampUs, InputEventKind Kind, string Text);

/// <summary>Bounded, thread-safe ring of timestamped input transitions for the diagnostic log.</summary>
public sealed class InputEventLog
{
    readonly InputEvent[] _ring;
    readonly object _gate = new();
    long _seq;

    public InputEventLog(int capacity = 4000) => _ring = new InputEvent[capacity];

    public long LastSequence { get { lock (_gate) return _seq; } }

    public void Add(long timestampUs, InputEventKind kind, string text)
    {
        lock (_gate)
        {
            _seq++;
            _ring[(int)((_seq - 1) % _ring.Length)] = new InputEvent(_seq, timestampUs, kind, text);
        }
    }

    /// <summary>Events with a sequence greater than <paramref name="afterSequence"/>.</summary>
    public List<InputEvent> ReadSince(long afterSequence)
    {
        var list = new List<InputEvent>();
        lock (_gate)
        {
            long first = Math.Max(afterSequence + 1, _seq - _ring.Length + 1);
            for (long s = first; s <= _seq; s++)
                list.Add(_ring[(int)((s - 1) % _ring.Length)]);
        }
        return list;
    }
}

public enum ConnectionState { Disconnected, Connected, DryRun, Error }

/// <summary>Immutable snapshot the UI reads at its own pace; the pipeline thread never waits on the UI.</summary>
public sealed class PipelineSnapshot
{
    public GamepadState Physical { get; init; }
    public GamepadState Virtual { get; init; }
    public bool PhysicalConnected { get; init; }
    public string? PhysicalName { get; init; }
    public ConnectionState VirtualState { get; init; }
    public VirtualPadBackend Backend { get; init; }
    public VirtualPadType PadType { get; init; }
    public SteeringDirection Steering { get; init; }
    public SteeringDirection AnalogSteering { get; init; }
    public float AnalogShaped { get; init; }
    public bool R2MappedActive { get; init; }
    public bool RemappingEnabled { get; init; }
    public bool ContinuousActive { get; init; }
    public bool ContinuousSquareActive { get; init; }
    public bool AnalogEnabled { get; init; }
    public bool LeftStickXSuppressed { get; init; }
    public DirectionOwner Owner { get; init; }
    public bool EmergencyStopped { get; init; }
    public HelperPhase Helper { get; init; }
    /// <summary>True only while a real virtual device is attached and accepting reports (never true for dry run or after a fault).</summary>
    public bool Forwarding { get; init; }
    public double LatencyAvgUs { get; init; }
    public long LatencyMaxUs { get; init; }
    public long LatencyP99Us { get; init; }
    public double PollRateHz { get; init; }
    public double SubmitRateHz { get; init; }
    public long Submissions { get; init; }
    public string? LastError { get; init; }
    public bool HighResolutionTimer { get; init; }
}
