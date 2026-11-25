using System.Threading;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Pipeline;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

sealed class FakeInput : IPhysicalInput
{
    public volatile bool Connected = true;
    GamepadState _state;
    readonly object _gate = new();
    public bool Disposed;
    public void Set(GamepadState s) { lock (_gate) _state = s; }
    public void Initialize() { }
    public bool TryPoll(out GamepadState state) { lock (_gate) state = _state; return Connected; }
    public string? ActiveDeviceName => Connected ? "Fake Xbox Series Controller" : null;
    public PhysicalDeviceDetails? ActiveDetails => null;
    public IReadOnlyList<PhysicalDeviceInfo> Devices => Array.Empty<PhysicalDeviceInfo>();
    public void SelectDevice(string? key) { }
    public void BeginVirtualFilter(ushort v, ushort p) { }
    public void EndVirtualFilter() { }
    public void Dispose() => Disposed = true;
}

sealed class FakePad : IVirtualPad
{
    readonly object _gate = new();
    public readonly List<GamepadState> Submitted = new();
    public volatile bool FailSubmits;
    public volatile bool FailConnect;
    public VirtualPadBackend Backend => VirtualPadBackend.ViGEmBus;
    public VirtualPadType PadType => VirtualPadType.DualShock4;
    public bool IsConnected { get; private set; }
    public (ushort Vendor, ushort Product) Signature => (0x054C, 0x05C4);
    public int ConnectCalls, DisconnectCalls;
    public void Connect() { ConnectCalls++; if (FailConnect) throw new InvalidOperationException("bus gone"); IsConnected = true; }
    public void Disconnect() { DisconnectCalls++; IsConnected = false; }
    public void Submit(in GamepadState s)
    {
        if (FailSubmits) throw new InvalidOperationException("virtual device removed");
        lock (_gate) Submitted.Add(s);
    }
    public void Dispose() { }
    public GamepadState? Last { get { lock (_gate) return Submitted.Count == 0 ? null : Submitted[^1]; } }
    public int Count { get { lock (_gate) return Submitted.Count; } }
}

public class PipelineRunnerTests
{
    static bool WaitFor(Func<bool> cond, int ms = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (cond()) return true;
            Thread.Sleep(2);   // test code only; the production loop never sleeps
        }
        return cond();
    }

    static (FakeInput, FakePad, PolswidProcessor, PipelineRunner) Make()
    {
        var input = new FakeInput();
        var pad = new FakePad();
        var proc = new PolswidProcessor(DefaultProfiles.Create(DefaultProfiles.Standard));
        var runner = new PipelineRunner(input, pad, proc, new InputEventLog());
        runner.Start();
        return (input, pad, proc, runner);
    }

    [Fact]
    public void Physical_disconnect_releases_all_virtual_inputs_and_prevents_stale_state()
    {
        var (input, pad, proc, runner) = Make();
        using var _ = runner;

        var held = new GamepadState { R2 = 1f, Buttons = PadButton.Square | PadButton.Cross, LX = 1f };
        input.Set(held);
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.DpadDown | PadButton.Square | PadButton.Cross | PadButton.DpadRight)));
        Assert.True(runner.Snapshot.PhysicalConnected);
        Assert.True(runner.Snapshot.Forwarding);

        input.Connected = false;

        Assert.True(WaitFor(() => pad.Last is { } l && l.Buttons == PadButton.None && l.R2 == 0f && l.LX == 0f), "virtual pad must be neutral");
        Assert.True(WaitFor(() => !runner.Snapshot.PhysicalConnected));
        Assert.Equal(DirectionOwner.None, runner.Snapshot.Owner);
        int count = pad.Count;
        Thread.Sleep(100);
        Assert.Equal(count, pad.Count);                          // no stale replay

        input.Connected = true;
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.DpadDown)));
        Assert.Contains(runner.Events.ReadSince(0), e => e.Text.Contains("DISCONNECTED"));
    }

    [Fact]
    public void Virtual_pad_failure_is_reported_visibly_and_never_claims_forwarding()
    {
        var (input, pad, proc, runner) = Make();
        using var _ = runner;
        input.Set(new GamepadState { Buttons = PadButton.Cross });
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.Cross)));
        Assert.Equal(ConnectionState.Connected, runner.Snapshot.VirtualState);

        pad.FailSubmits = true;                                  // the virtual device disappears
        input.Set(new GamepadState { Buttons = PadButton.Circle });

        Assert.True(WaitFor(() => runner.Snapshot.VirtualState == ConnectionState.Error));
        Assert.False(runner.Snapshot.Forwarding);
        Assert.Contains("virtual", runner.Snapshot.LastError ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.False(pad.IsConnected);

        pad.FailSubmits = false;                                 // automatic recovery attempt reconnects
        Assert.True(WaitFor(() => runner.Snapshot.VirtualState == ConnectionState.Connected, 6000));
        Assert.True(runner.Snapshot.Forwarding);
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.Circle)));
    }

    [Fact]
    public void Virtual_pad_that_cannot_connect_shows_an_error_from_the_start()
    {
        var input = new FakeInput();
        var pad = new FakePad { FailConnect = true };
        var proc = new PolswidProcessor(DefaultProfiles.Create(DefaultProfiles.Standard));
        using var runner = new PipelineRunner(input, pad, proc, new InputEventLog());
        runner.Start();
        input.Set(new GamepadState { Buttons = PadButton.Cross });
        Assert.True(WaitFor(() => runner.Snapshot.VirtualState == ConnectionState.Error));
        Assert.False(runner.Snapshot.Forwarding);
        Assert.Equal(0, pad.Count);
    }

    [Fact]
    public void Stop_releases_every_input_and_disconnects_the_virtual_pad()
    {
        var (input, pad, proc, runner) = Make();
        input.Set(new GamepadState { Buttons = PadButton.Square });
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.Square)));

        runner.Stop();

        Assert.Equal(PadButton.None, pad.Last!.Value.Buttons);
        Assert.False(pad.IsConnected);
        Assert.True(pad.DisconnectCalls >= 1);
        Assert.True(input.Disposed);
    }

    [Fact]
    public void Emergency_stop_through_the_runner_leaves_raw_passthrough_and_nothing_stuck()
    {
        var (input, pad, proc, runner) = Make();
        using var _ = runner;
        input.Set(new GamepadState { R2 = 1f });
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.DpadDown)));
        proc.Post(MacroCommandKind.EmergencyStop);
        Assert.True(WaitFor(() => pad.Last is { } l && !l.Has(PadButton.DpadDown) && l.R2 == 1f));
        input.Set(default);
        Assert.True(WaitFor(() => pad.Last is { } l && l.Buttons == PadButton.None && l.R2 == 0f));
        Assert.True(WaitFor(() => runner.Snapshot.EmergencyStopped));   // snapshot is published at most every 8 ms
    }

    [Fact]
    public void Submission_only_happens_on_change_and_rates_are_measured()
    {
        var (input, pad, proc, runner) = Make();
        using var _ = runner;
        input.Set(new GamepadState { Buttons = PadButton.Triangle });
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.Triangle)));
        Thread.Sleep(200);
        int c = pad.Count;
        Thread.Sleep(200);
        Assert.Equal(c, pad.Count);
        Assert.True(WaitFor(() => runner.Snapshot.PollRateHz > 100, 4000));
        Assert.True(runner.Snapshot.LatencyMaxUs > 0);
    }

    [Fact]
    public void Profile_change_applies_live_without_restart()
    {
        var (input, pad, proc, runner) = Make();
        using var _ = runner;
        input.Set(new GamepadState { Buttons = PadButton.Triangle });
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.Triangle)));
        var custom = DefaultProfiles.Create(DefaultProfiles.Custom);
        custom.Mapping.Bindings.RemoveAll(b => b.From == PadButton.Triangle);
        custom.Mapping.Bindings.Add(new ButtonBinding(PadButton.Triangle, PadButton.Circle));
        proc.SetProfile(custom);
        Assert.True(WaitFor(() => pad.Last is { } l && l.Has(PadButton.Circle) && !l.Has(PadButton.Triangle)));
    }
}
