using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Polswid;

namespace ASTRO.Polswid.Core.Diagnostics;

/// <summary>A boundary-state copy queued without formatting or disk I/O on the polling thread.</summary>
public readonly record struct R2DiagnosticFrame(
    long Sequence, long TimeUs, InputReadDiagnostics Input, bool ReadOk, bool Processed,
    int ConsecutiveFailures, GamepadState Physical, FrameResult Processing,
    float PressThreshold, float ReleaseThreshold, int SquareDelayMs, bool ContinuousEnabled,
    string OutputType, string OutputBackend, string ReportStatus, bool HaveSubmitted,
    GamepadState LastSubmitted, string? Error, long DroppedFrames);

/// <summary>Opt-in, bounded, single-producer diagnostics. Overflow loses evidence, never blocks input.</summary>
public sealed class R2DiagnosticLogger : IDisposable
{
    readonly Channel<R2DiagnosticFrame>? _queue;
    readonly Task? _worker;
    long _dropped;
    int _disposed;
    public bool Enabled => _queue != null && Volatile.Read(ref _disposed) == 0;
    public long DroppedFrames => Interlocked.Read(ref _dropped);
    public string? LogPath { get; }
    public string? Error { get; private set; }

    public R2DiagnosticLogger(string? path = null, bool? enabled = null, int capacity = 8192)
    {
        if (!(enabled ?? Environment.GetEnvironmentVariable("ASTRO_R2_DIAGNOSTICS") == "1")) return;
        var root = Environment.GetEnvironmentVariable("ASTRO_R2_LOG_DIR") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ASTRO POLSWID", "logs");
        LogPath = path ?? Path.Combine(root, $"r2-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl");
        _queue = Channel.CreateBounded<R2DiagnosticFrame>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        });
        _worker = Task.Run(WriteAsync);
    }

    public void Record(in R2DiagnosticFrame frame)
    {
        if (!Enabled) return;
        if (!_queue!.Writer.TryWrite(frame with { DroppedFrames = DroppedFrames }))
            Interlocked.Increment(ref _dropped);
    }

    async Task WriteAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await using var stream = new FileStream(LogPath!, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                65536, FileOptions.Asynchronous);
            await using var writer = new StreamWriter(stream);
            var options = new JsonSerializerOptions { IncludeFields = true,
                Converters = { new JsonStringEnumConverter() } };
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                Kind = "Session", Schema = 1, StartedUtc = DateTime.UtcNow,
                CoreBuildId = typeof(R2DiagnosticLogger).Module.ModuleVersionId,
                Application = System.Reflection.Assembly.GetEntryAssembly()?.GetName().FullName,
                RawR2Boundary = "SDL mapped signed axis; not a hardware wire report",
            }, options));
            await writer.FlushAsync();
            R2DiagnosticFrame? previous = null;
            long lastFlush = 0;
            while (await _queue!.Reader.WaitToReadAsync())
            {
                while (_queue.Reader.TryRead(out var f))
                {
                    string reason = TransitionReason(previous, f);
                    byte slider = (byte)Math.Clamp((int)MathF.Round(f.LastSubmitted.R2 * 255f), 0, 255);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        Kind = "Frame", Frame = f, TransitionReason = reason,
                        SubmittedR2Slider = f.HaveSubmitted ? (byte?)slider : null,
                        SubmittedDs4R2Button = f.HaveSubmitted && f.OutputType == nameof(VirtualPadType.DualShock4)
                            ? (bool?)(f.LastSubmitted.R2 >= .5f) : null,
                    }, options));
                    previous = f;
                    if (f.TimeUs - lastFlush >= 250_000 || reason.Length > 0)
                    {
                        await writer.FlushAsync();
                        lastFlush = f.TimeUs;
                    }
                }
            }
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { Kind = "End", DroppedFrames, Error }, options));
        }
        catch (Exception ex)
        {
            Error = ex.GetType().Name + ": " + ex.Message;
            _queue!.Writer.TryComplete();
        }
    }

    static string TransitionReason(R2DiagnosticFrame? previous, in R2DiagnosticFrame f)
    {
        if (!f.ReadOk) return f.ReportStatus ?? "";
        var r = f.Processing;
        if (r.EmergencyStopped) return "EmergencyStop: raw passthrough";
        if (!r.RemappingEnabled) return "RemappingDisabled: raw passthrough";
        if (r.ProfileChanged) return "ProfileApplied: processor state reset";
        if (previous is { } p && p.Processing.ContinuousActive != r.ContinuousActive)
            return r.ContinuousActive ? "ContinuousActivated"
                : !f.ContinuousEnabled ? "ContinuousDisabledInProfile"
                : !r.R2Held ? "PhysicalTriggerConditionReleased"
                : "R2EffectiveInactive: inspect trigger mode and commands";
        return "";
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _queue?.Writer.TryComplete();
        // Shutdown only; never called from a polling tick. Bound the wait if storage is stuck.
        _worker?.Wait(TimeSpan.FromSeconds(2));
    }
}
