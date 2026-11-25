using System.Diagnostics;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using ASTRO.Polswid.Core.Timing;

namespace ASTRO.Polswid.Core.Pipeline;

/// <summary>
/// Owns the dedicated real-time thread: poll physical -> process -> submit virtual. The UI only talks to it through
/// thread-safe commands and by reading <see cref="Snapshot"/>; nothing the UI does can block this loop.
/// </summary>
public sealed class PipelineRunner : IDisposable
{
    readonly IPhysicalInput _input;
    readonly IVirtualPad _pad;
    readonly PolswidProcessor _processor;
    readonly ILog _log;
    readonly LatencyStats _latency = new();
    readonly ManualResetEventSlim _started = new();

    Thread? _thread;
    volatile bool _stop;
    Exception? _initError;

    // loop-thread state
    bool _physConnected;
    GamepadState _lastSubmitted;
    bool _haveSubmitted;
    bool _padFaulted;
    long _lastSubmitUs, _nextReconnectUs, _lastPublishUs, _rateWindowStartUs;
    long _pollCount, _submitCount, _submitTotal;
    double _pollRate, _submitRate;
    long _lastLatencyLogUs;
    FrameResult _prev;
    bool _havePrev;
    string? _lastError;
    string? _activeName;
    int _consecutivePollFailures;
    readonly R2DiagnosticLogger _diag;
    long _frameSequence;
    readonly string _outputType, _outputBackend;

    PipelineSnapshot _snapshot = new();

    public PipelineRunner(IPhysicalInput input, IVirtualPad pad, PolswidProcessor processor, InputEventLog events, ILog? log = null,
        R2DiagnosticLogger? diagnostics = null)
    {
        _input = input;
        _pad = pad;
        _processor = processor;
        Events = events;
        _log = log ?? NullLog.Instance;
        _diag = diagnostics ?? new R2DiagnosticLogger();
        _outputType = pad.PadType.ToString();
        _outputBackend = pad.Backend.ToString();
        if (input is IInputReadDiagnostics source) source.DiagnosticsEnabled = _diag.Enabled;
        _snapshot = new PipelineSnapshot { PadType = pad.PadType, Backend = pad.Backend };
    }

    public InputEventLog Events { get; }
    public PolswidProcessor Processor => _processor;
    public IVirtualPad Pad => _pad;
    public IPhysicalInput Input => _input;
    public bool IsRunning => _thread is { IsAlive: true };

    /// <summary>Latest published state (immutable object, safe to read from any thread).</summary>
    public PipelineSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Starts the loop thread and waits until the input backend has initialised. Throws if it failed.</summary>
    public void Start()
    {
        if (IsRunning) return;
        _stop = false;
        _initError = null;
        _started.Reset();
        _thread = new Thread(Run) { Name = "ASTRO.Pipeline", IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
        _started.Wait();
        if (_initError != null) throw new InvalidOperationException("Input backend failed to initialise: " + _initError.Message, _initError);
    }

    public void Stop()
    {
        _stop = true;
        var t = _thread;
        if (t != null && t.IsAlive && !t.Join(2000))
            _log.Warning("Pipeline thread did not stop within 2 s");
        _thread = null;
    }

    public void Dispose() { Stop(); _diag.Dispose(); }

    // ------------------------------------------------------------------ loop

    void Run()
    {
        using var waiter = new PrecisionWaiter();
        try
        {
            _input.Initialize();
        }
        catch (Exception ex)
        {
            _initError = ex;
            _started.Set();
            return;
        }

        Log(InputEventKind.System, $"Input backend ready; high-resolution timer: {waiter.HighResolution}");
        TryConnectPad(force: true);
        _started.Set();

        long next = Clock.NowUs;
        _rateWindowStartUs = next;
        while (!_stop)
        {
            try
            {
                Tick(waiter.HighResolution);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _log.Error(ex, "Pipeline tick failed");
                Log(InputEventKind.System, "ERROR: " + ex.Message);
            }

            int hz = Math.Clamp(_processor.ActiveProfile.Timing.PollingHz, 30, 4000);
            long period = 1_000_000L / hz;
            next += period;
            long now = Clock.NowUs;
            if (next < now - period * 4) next = now + period;   // recover after a stall instead of bursting
            waiter.WaitUntil(next);
        }

        Shutdown();
    }

    void Tick(bool highRes) => TickAt(Clock.NowUs);

    // Same production tick, with explicit time for deterministic pipeline tests.
    internal void TickAt(long t0)
    {
        _frameSequence++;
        bool ok;
        GamepadState phys;
        try { ok = _input.TryPoll(out phys); }
        catch (Exception ex)
        {
            RecordDiagnostic(t0, false, false, default, _prev, "InputException: retained output", ex.Message);
            throw;
        }
        _pollCount++;

        if (!ok)
        {
            _consecutivePollFailures++;
            // Grace period: allow up to 25 consecutive failed polls (~50 ms at 500 Hz)
            // to tolerate transient USB HID report hiccups or brief frame drops without disconnecting or dropping Polswid mode.
            if (_consecutivePollFailures < 25 && _physConnected)
            {
                // Transient frame drop: hold current state, do not wipe processor or disconnect pad
                Publish(t0, _prev.Physical, _prev.Virtual, true, _prev);
                RecordDiagnostic(t0, false, false, default, _prev, "InvalidPollGrace: retained output");
                return;
            }

            if (_physConnected) OnPhysicalLost(t0);
            RecordDiagnostic(t0, false, false, default, default, "InputLost: neutral requested");
            Publish(t0, default, default, false);
            return;
        }

        _consecutivePollFailures = 0;
        if (!_physConnected)
        {
            _physConnected = true;
            _activeName = _input.ActiveDeviceName;
            Log(InputEventKind.Device, $"Physical controller connected: {_activeName}");
            _log.Information("Physical controller connected {Name}", _activeName);
        }
        else if (_activeName != _input.ActiveDeviceName)
        {
            _activeName = _input.ActiveDeviceName;
            Log(InputEventKind.Device, $"Active controller changed: {_activeName}");
        }

        var r = _processor.Process(phys, t0);

        var timing = _processor.ActiveProfile.Timing;
        long minInterval = 1_000_000L / Math.Clamp(timing.MaxSubmitHz, 30, 4000);
        bool canSubmit = _pad.IsConnected || _pad.Backend == VirtualPadBackend.DryRun;
        if (!canSubmit) TryConnectPad(force: false);
        string reportStatus = !canSubmit || _padFaulted ? "Unavailable"
            : _haveSubmitted && r.Virtual == _lastSubmitted ? "Unchanged" : "RateLimited";

        if (canSubmit && !_padFaulted && (!_haveSubmitted || r.Virtual != _lastSubmitted) && t0 - _lastSubmitUs >= minInterval)
        {
            try
            {
                _pad.Submit(r.Virtual);
                long t2 = Clock.NowUs;
                _latency.Record(t2 - t0);
                _lastSubmitted = r.Virtual;
                _haveSubmitted = true;
                _lastSubmitUs = t0;
                _submitCount++;
                _submitTotal++;
                reportStatus = _pad.Backend == VirtualPadBackend.DryRun ? "DryRunAccepted" : "Submitted";
            }
            catch (Exception ex)
            {
                OnPadFault(ex);
                reportStatus = "SubmitFault";
            }
        }

        RecordDiagnostic(t0, true, true, phys, r, reportStatus);

        LogTransitions(t0, r);
        _prev = r;
        _havePrev = true;
        Publish(t0, r.Physical, r.Virtual, true, r);

        if (t0 - _lastLatencyLogUs > 10_000_000)
        {
            _lastLatencyLogUs = t0;
            var s = _latency.Snapshot();
            _log.Information("Latency avg={AvgUs:0}us p99={P99Us}us max={MaxUs}us poll={Poll:0}Hz submit={Submit:0}Hz",
                s.AvgUs, s.P99Us, s.MaxUs, _pollRate, _submitRate);
        }
    }

    void OnPhysicalLost(long nowUs)
    {
        _physConnected = false;
        _activeName = null;
        _processor.ResetAll();
        _havePrev = false;
        // Release every virtual input immediately; never leave a stale state on the bus.
        SubmitNeutral();
        Log(InputEventKind.Device, "Physical controller DISCONNECTED - all virtual inputs released");
        _log.Warning("Physical controller disconnected; virtual state cleared");
    }

    void OnPadFault(Exception ex)
    {
        _padFaulted = true;
        _lastError = "Virtual pad error: " + ex.Message;
        _log.Error(ex, "Virtual pad submit failed");
        Log(InputEventKind.System, "Virtual pad FAULT: " + ex.Message);
        try { _pad.Disconnect(); } catch (Exception d) { _log.Error(d, "Virtual pad disconnect after fault failed"); }
        _haveSubmitted = false;
        _nextReconnectUs = Clock.NowUs + 2_000_000;
    }

    void SubmitNeutral()
    {
        if (!(_pad.IsConnected || _pad.Backend == VirtualPadBackend.DryRun) || _padFaulted) return;
        try
        {
            _pad.Submit(default);
            _lastSubmitted = default;
            _haveSubmitted = true;
        }
        catch (Exception ex)
        {
            OnPadFault(ex);
        }
    }

    void TryConnectPad(bool force)
    {
        long now = Clock.NowUs;
        if (!force && now < _nextReconnectUs) return;
        if (_pad.Backend == VirtualPadBackend.DryRun) { _padFaulted = false; return; }
        try
        {
            _input.BeginVirtualFilter(_pad.Signature.Vendor, _pad.Signature.Product);
            _pad.Connect();
            _padFaulted = false;
            _haveSubmitted = false;
            _lastError = null;
            Log(InputEventKind.Device, $"Virtual {_pad.PadType} connected via {_pad.Backend}");
            _log.Information("Virtual pad connected {Type}", _pad.PadType);
        }
        catch (Exception ex)
        {
            _input.EndVirtualFilter();
            _padFaulted = true;
            _lastError = ex.Message;
            _nextReconnectUs = now + 2_000_000;
            Log(InputEventKind.System, "Virtual pad connect failed: " + ex.Message);
            _log.Error(ex, "Virtual pad connect failed");
        }
    }

    void Shutdown()
    {
        _frameSequence++;
        try { SubmitNeutral(); } catch (Exception ex) { _log.Error(ex, "Neutral submit during shutdown failed"); }
        RecordDiagnostic(Clock.NowUs, false, false, default, default, "Shutdown: neutral requested");
        try { _pad.Disconnect(); } catch (Exception ex) { _log.Error(ex, "Virtual pad disconnect during shutdown failed"); }
        try { _input.EndVirtualFilter(); _input.Dispose(); } catch (Exception ex) { _log.Error(ex, "Input dispose failed"); }
        try { _diag.Dispose(); } catch { }
        Log(InputEventKind.System, "Pipeline stopped; all virtual inputs released");
        _log.Information("Pipeline stopped");
        Publish(Clock.NowUs, default, default, false);
    }

    // ------------------------------------------------------------------ diagnostics

    void RecordDiagnostic(long nowUs, bool readOk, bool processed, GamepadState physical,
        FrameResult result, string status, string? error = null)
    {
        if (!_diag.Enabled) return;
        var input = _input is IInputReadDiagnostics source ? source.LastReadDiagnostics
            : new InputReadDiagnostics(_input.GetType().Name, _input.ActiveDeviceName, null, null, null, null, null);
        var p = _processor.ActiveProfile;
        _diag.Record(new R2DiagnosticFrame(_frameSequence, nowUs, input, readOk, processed,
            _consecutivePollFailures, physical, result, p.Mapping.TriggerPressThreshold,
            p.Mapping.TriggerReleaseThreshold, p.Continuous.SquareDelayMs, p.Continuous.Enabled,
            _outputType, _outputBackend, status, _haveSubmitted, _lastSubmitted, error ?? _lastError, 0));
    }

    void Log(InputEventKind kind, string text) => Events.Add(Clock.NowUs, kind, text);

    void LogTransitions(long nowUs, in FrameResult r)
    {
        if (!_havePrev) return;
        var diffPhys = r.Physical.Buttons ^ _prev.Physical.Buttons;
        var diffVirt = r.Virtual.Buttons ^ _prev.Virtual.Buttons;
        if (diffPhys != 0)
            foreach (var b in PadButtons.All)
                if ((diffPhys & b) != 0)
                    Events.Add(nowUs, InputEventKind.Physical, $"{PadButtons.Display(b)} {((r.Physical.Buttons & b) != 0 ? "pressed" : "released")}");
        if (diffVirt != 0)
        {
            // one line per frame so simultaneous changes (atomic transitions) are visibly simultaneous
            var sb = new System.Text.StringBuilder("VIRTUAL ");
            foreach (var b in PadButtons.All)
                if ((diffVirt & b) != 0)
                    sb.Append((r.Virtual.Buttons & b) != 0 ? '+' : '-').Append(PadButtons.Display(b)).Append(' ');
            Events.Add(nowUs, InputEventKind.Virtual, sb.ToString().TrimEnd());
        }
        if (r.Steering != _prev.Steering)
            Events.Add(nowUs, InputEventKind.Direction, $"Steering {_prev.Steering} -> {r.Steering}");
        if (r.AnalogSteering != _prev.AnalogSteering)
            Events.Add(nowUs, InputEventKind.Direction, $"Left stick conversion {_prev.AnalogSteering} -> {r.AnalogSteering} (shaped {r.AnalogShaped:0.00})");
        if (r.ContinuousActive != _prev.ContinuousActive) Events.Add(nowUs, InputEventKind.Macro, r.ContinuousActive ? "Continuous steering ACTIVE (R2 held)" : "Continuous steering ended (R2 released)");
        if (r.Owner != _prev.Owner) Events.Add(nowUs, InputEventKind.Direction, $"Direction ownership {_prev.Owner} -> {r.Owner}");
        if (r.RemappingEnabled != _prev.RemappingEnabled) Events.Add(nowUs, InputEventKind.Macro, r.RemappingEnabled ? "Remapping ENABLED" : "Remapping disabled (raw passthrough)");
        if (r.AnalogEnabled != _prev.AnalogEnabled) Events.Add(nowUs, InputEventKind.Macro, r.AnalogEnabled ? "Left analog steering ENABLED" : "Left analog steering disabled");
        if (r.EmergencyStopped != _prev.EmergencyStopped) Events.Add(nowUs, InputEventKind.Macro, r.EmergencyStopped ? "EMERGENCY STOP - raw passthrough only" : "Emergency stop cleared");
        if (r.Helper != _prev.Helper) Events.Add(nowUs, InputEventKind.Macro, r.Helper == HelperPhase.None ? $"Helper {_prev.Helper} finished" : $"Helper {r.Helper} active");
    }

    void Publish(long nowUs, GamepadState phys, GamepadState virt, bool physOk, FrameResult? r = null)
    {
        long window = nowUs - _rateWindowStartUs;
        if (window >= 1_000_000)
        {
            _pollRate = _pollCount * 1_000_000.0 / window;
            _submitRate = _submitCount * 1_000_000.0 / window;
            _pollCount = _submitCount = 0;
            _rateWindowStartUs = nowUs;
        }
        if (nowUs - _lastPublishUs < 8_000 && physOk) return;   // ~120 Hz max, plenty for a 60 Hz UI
        _lastPublishUs = nowUs;

        var lat = _latency.Snapshot();
        var rr = r ?? default;
        ConnectionState vs = _pad.Backend == VirtualPadBackend.DryRun ? ConnectionState.DryRun
            : _pad.IsConnected && !_padFaulted ? ConnectionState.Connected
            : _padFaulted ? ConnectionState.Error : ConnectionState.Disconnected;

        Volatile.Write(ref _snapshot, new PipelineSnapshot
        {
            Physical = phys,
            Virtual = virt,
            PhysicalConnected = physOk,
            PhysicalName = physOk ? _input.ActiveDeviceName : null,
            VirtualState = vs,
            Backend = _pad.Backend,
            PadType = _pad.PadType,
            Steering = rr.Steering,
            AnalogSteering = rr.AnalogSteering,
            AnalogShaped = rr.AnalogShaped,
            R2MappedActive = rr.R2MappedActive,
            RemappingEnabled = _processor.RemappingEnabled,
            ContinuousActive = rr.ContinuousActive,
            ContinuousSquareActive = rr.ContinuousSquareActive,
            AnalogEnabled = _processor.AnalogEnabled,
            LeftStickXSuppressed = rr.LeftStickXSuppressed,
            Owner = rr.Owner,
            EmergencyStopped = _processor.EmergencyStopped,
            Helper = rr.Helper,
            Forwarding = vs == ConnectionState.Connected,
            LatencyAvgUs = lat.AvgUs,
            LatencyMaxUs = lat.MaxUs,
            LatencyP99Us = lat.P99Us,
            PollRateHz = _pollRate,
            SubmitRateHz = _submitRate,
            Submissions = _submitTotal,
            LastError = _lastError,
            HighResolutionTimer = true,
        });
    }
}
