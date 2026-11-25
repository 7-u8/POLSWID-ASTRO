using System.IO;
using System.Reflection;
using System.Text.Json;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Pipeline;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using ASTRO.Polswid.Infrastructure.ViGEm;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ASTRO.Polswid.Tests;

// Intercepts the actual production encoder's controller calls. No ViGEm driver/device is opened.
public class RecordingControllerProxy : DispatchProxy
{
    public readonly List<(string Method, object?[] Args)> Calls = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        Calls.Add((method!.Name, args ?? Array.Empty<object?>()));
        return method.ReturnType == typeof(void) ? null : Activator.CreateInstance(method.ReturnType);
    }
    public object? Value(string method, object key) =>
        Calls.Last(c => c.Method == method && Equals(c.Args[0], key)).Args[1];
}

public sealed class DiagnosticProductionPipelineTests
{
    sealed class Source : IPhysicalInput, IInputReadDiagnostics
    {
        public GamepadState State;
        public bool Valid = true, Throw;
        public bool DiagnosticsEnabled { get; set; }
        public InputReadDiagnostics LastReadDiagnostics { get; private set; }
        public void Initialize() { }
        public bool TryPoll(out GamepadState state)
        {
            LastReadDiagnostics = new("Synthetic input (test)", "test-instance-1", "test mapping,not hardware",
                Valid ? (short?)(State.R2 * 32767) : null, true, Valid, Valid ? null : "test input missing");
            if (Throw) throw new IOException("test input exception");
            state = Valid ? State : default;
            return Valid;
        }
        public string ActiveDeviceName => "Synthetic test controller";
        public PhysicalDeviceDetails? ActiveDetails => null;
        public IReadOnlyList<PhysicalDeviceInfo> Devices => Array.Empty<PhysicalDeviceInfo>();
        public void SelectDevice(string? key) { }
        public void BeginVirtualFilter(ushort vendor, ushort product) { }
        public void EndVirtualFilter() { }
        public void Dispose() { }
    }

    sealed class EncodedPad : IVirtualPad
    {
        readonly object _controller;
        readonly MethodInfo _encode;
        public RecordingControllerProxy Recorder => (RecordingControllerProxy)_controller;
        public GamepadState Last;
        public bool Fail;
        public int Reports;
        public EncodedPad(VirtualPadType type)
        {
            PadType = type;
            _controller = type == VirtualPadType.DualShock4
                ? DispatchProxy.Create<IDualShock4Controller, RecordingControllerProxy>()
                : DispatchProxy.Create<IXbox360Controller, RecordingControllerProxy>();
            _encode = typeof(ViGEmVirtualPad).GetMethod(type == VirtualPadType.DualShock4 ? "SubmitDs4" : "SubmitX360",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        }
        public VirtualPadBackend Backend => VirtualPadBackend.ViGEmBus;
        public VirtualPadType PadType { get; }
        public bool IsConnected { get; private set; }
        public (ushort Vendor, ushort Product) Signature => (0, 0);
        public void Connect() => IsConnected = true;
        public void Disconnect() => IsConnected = false;
        public void Submit(in GamepadState state)
        {
            if (Fail) throw new IOException("test submit failure");
            Recorder.Calls.Clear();
            _encode.Invoke(null, new object[] { _controller, state });
            Last = state;
            Reports++;
            Assert.Single(Recorder.Calls.Where(c => c.Method == "SubmitReport"));
            if (PadType == VirtualPadType.DualShock4)
            {
                Assert.Equal((byte)MathF.Round(state.R2 * 255), Recorder.Value("SetSliderValue", DualShock4Slider.RightTrigger));
                Assert.Equal(state.R2 >= .5f, Recorder.Value("SetButtonState", DualShock4Button.TriggerRight));
            }
            else Assert.Equal((byte)MathF.Round(state.R2 * 255), Recorder.Value("SetSliderValue", Xbox360Slider.RightTrigger));
        }
        public void Dispose() { }
    }

    sealed class Rig : IDisposable
    {
        public readonly Source Input = new();
        public readonly EncodedPad Pad;
        public readonly PolswidProcessor Processor;
        public readonly PipelineRunner Runner;
        public long Now = 1_000_000;
        public Rig(VirtualPadType type, R2DiagnosticLogger? logger = null)
        {
            Pad = new(type);
            Pad.Connect();
            Processor = new(DefaultProfiles.Create(DefaultProfiles.Standard));
            Runner = new(Input, Pad, Processor, new InputEventLog(), diagnostics: logger ?? new(enabled: false));
        }
        public GamepadState Step(int count = 1)
        {
            for (int i = 0; i < count; i++) { Now += 2000; Runner.TickAt(Now); }
            return Pad.Last;
        }
        public void Dispose() => Runner.Dispose();
    }

    public static IEnumerable<object[]> MovementCases()
    {
        foreach (var t in Enum.GetValues<VirtualPadType>())
            foreach (var s in new[] { "R2Only", "Slow", "Normal", "Rapid", "NeutralCrossing", "Deadzone", "Vertical", "Diagonal", "RightStick", "BothSticks" })
                yield return new object[] { t, s };
    }

    [Theory, MemberData(nameof(MovementCases))]
    public void Held_trigger_survives_movements_through_real_tick_and_encoders(VirtualPadType type, string scenario)
    {
        using var rig = new Rig(type);
        rig.Input.State.R2 = 1;
        rig.Step(32);
        for (int i = 0; i < 150; i++)
        {
            float x = scenario switch
            {
                "R2Only" or "RightStick" or "Vertical" => 0,
                "Slow" => MathF.Sin(i * .04f), "Normal" => MathF.Sin(i * .3f),
                "Deadzone" => i % 2 == 0 ? .19f : .21f,
                "NeutralCrossing" => i % 3 == 0 ? 0 : i % 3 == 1 ? -1 : 1,
                _ => i % 2 == 0 ? -1 : 1,
            };
            rig.Input.State.LX = x;
            rig.Input.State.LY = scenario is "Vertical" or "Diagonal" or "BothSticks" ? MathF.Cos(i) : 0;
            rig.Input.State.RX = scenario is "RightStick" or "BothSticks" ? MathF.Sin(i) : 0;
            rig.Input.State.RY = scenario is "RightStick" or "BothSticks" ? MathF.Cos(i) : 0;
            var s = rig.Step();
            Assert.True(s.Has(PadButton.DpadDown | PadButton.Square));
            Assert.True(s.HasAny(PadButtons.Steering));
            Assert.False(s.HasAny(PadButton.DpadUp | PadButton.R2));
            Assert.Equal(0, s.R2); Assert.Equal(0, s.LX); Assert.Equal(0, s.LY);
            Assert.Equal(rig.Input.State.RX, s.RX); Assert.Equal(rig.Input.State.RY, s.RY);
            if (x == 0) Assert.True(s.Has(PadButton.DpadLeft));
        }
        rig.Input.State.R2 = 0;
        var released = rig.Step(5); // Existing five-poll release policy, NOT immediate physical release.
        Assert.False(released.HasAny(PadButton.DpadDown | PadButton.Square));
        Assert.Equal(rig.Input.State.LX, released.LX);
        Assert.Equal(rig.Input.State.LY, released.LY);
    }

    [Theory]
    [InlineData(VirtualPadType.Xbox360)]
    [InlineData(VirtualPadType.DualShock4)]
    public void Early_quick_release_cancels_pending_square_in_real_pipeline(VirtualPadType type)
    {
        using var rig = new Rig(type);
        rig.Input.State.R2 = 1; rig.Step(4);
        rig.Input.State.R2 = 0;
        for (int i = 0; i < 50; i++) Assert.False(rig.Step().Has(PadButton.Square));
    }

    [Theory]
    [InlineData(VirtualPadType.Xbox360)]
    [InlineData(VirtualPadType.DualShock4)]
    public void Existing_late_release_filter_can_activate_square_after_physical_release(VirtualPadType type)
    {
        using var rig = new Rig(type);
        rig.Input.State.R2 = 1; rig.Step(29); // Last held poll at 56 ms after activation.
        rig.Input.State.R2 = 0;
        Assert.False(rig.Step().Has(PadButton.Square)); // 58 ms
        Assert.True(rig.Step().Has(PadButton.Square)); // 60 ms, release filter still held.
        Assert.False(rig.Step(3).Has(PadButton.Square));
        // Characterization of a confirmed specification gap. This test does not call it correct behavior.
    }

    [Theory]
    [InlineData(VirtualPadType.Xbox360)]
    [InlineData(VirtualPadType.DualShock4)]
    public void Real_encoders_preserve_normal_down_and_supported_diagonals(VirtualPadType type)
    {
        using var rig = new Rig(type);
        foreach (var horizontal in new[] { PadButton.None, PadButton.DpadLeft, PadButton.DpadRight })
        {
            rig.Input.State = new() { Buttons = PadButton.DpadDown | horizontal | PadButton.Cross | PadButton.R1, LX = .7f, LY = -.8f, RX = -.3f, RY = .4f, L2 = .6f };
            var s = rig.Step();
            var expectedState = rig.Input.State;
            expectedState.Buttons |= PadButton.L2; // Mapper derives the digital trigger bit from analog L2.
            Assert.Equal(expectedState, s);
            var calls = rig.Pad.Recorder.Calls;
            if (type == VirtualPadType.DualShock4)
            {
                var expected = horizontal == PadButton.None ? DualShock4DPadDirection.South
                    : horizontal == PadButton.DpadLeft ? DualShock4DPadDirection.Southwest : DualShock4DPadDirection.Southeast;
                Assert.Equal(expected, calls.Single(c => c.Method == "SetDPadDirection").Args[0]);
            }
            else
            {
                Assert.Equal(true, rig.Pad.Recorder.Value("SetButtonState", Xbox360Button.Down));
                Assert.Equal(false, rig.Pad.Recorder.Value("SetButtonState", Xbox360Button.Up));
                Assert.Equal(horizontal == PadButton.DpadLeft, rig.Pad.Recorder.Value("SetButtonState", Xbox360Button.Left));
                Assert.Equal(horizontal == PadButton.DpadRight, rig.Pad.Recorder.Value("SetButtonState", Xbox360Button.Right));
            }
        }
    }

    [Fact]
    public void Diagnostic_trace_records_grace_loss_recovery_commands_profiles_and_faults()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../..", "artifacts", "evidence"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "synthetic-pipeline-" + Guid.NewGuid().ToString("N") + ".jsonl");
        var logger = new R2DiagnosticLogger(path, enabled: true);
        using (var rig = new Rig(VirtualPadType.DualShock4, logger))
        {
            Assert.True(rig.Input.DiagnosticsEnabled);
            rig.Input.State.R2 = 1; rig.Step(32);
            var held = rig.Pad.Last;
            rig.Input.Valid = false;
            for (int i = 0; i < 24; i++) Assert.Equal(held, rig.Step());
            Assert.Equal(default, rig.Step()); // 25th invalid poll neutralizes.
            rig.Input.Valid = true; rig.Step(32);
            Assert.True(rig.Pad.Last.Has(PadButton.Square | PadButton.DpadDown));
            rig.Processor.SetProfile(DefaultProfiles.Create(DefaultProfiles.Standard)); rig.Step();
            rig.Processor.Post(MacroCommandKind.EmergencyStop); rig.Step();
            Assert.Equal(1, rig.Pad.Last.R2); // Explicit raw passthrough, not a suppression failure.
            rig.Pad.Fail = true; rig.Input.State.LX = 1; rig.Step();
            rig.Input.Throw = true;
            Assert.Throws<IOException>(() => rig.Step());
        }
        var rows = File.ReadAllLines(path).Select(l => JsonDocument.Parse(l)).ToArray();
        try
        {
            var frames = rows.Where(r => r.RootElement.GetProperty("Kind").GetString() == "Frame").Select(r => r.RootElement).ToArray();
            Assert.Equal(24, frames.Count(r => r.GetProperty("Frame").GetProperty("ReportStatus").GetString() == "InvalidPollGrace: retained output"));
            Assert.Contains(frames, r => r.GetProperty("Frame").GetProperty("ReportStatus").GetString() == "SubmitFault");
            Assert.Contains(frames, r => r.GetProperty("Frame").GetProperty("ReportStatus").GetString() == "InputException: retained output");
            Assert.Contains(frames, r => r.GetProperty("TransitionReason").GetString() == "ProfileApplied: processor state reset");
            Assert.Contains(frames, r => r.GetProperty("TransitionReason").GetString() == "EmergencyStop: raw passthrough");
            var first = frames[0].GetProperty("Frame");
            Assert.Equal(32767, first.GetProperty("Input").GetProperty("RawR2").GetInt32());
            Assert.Equal(1, first.GetProperty("Physical").GetProperty("R2").GetSingle());
            Assert.Equal("test mapping,not hardware", first.GetProperty("Input").GetProperty("Mapping").GetString());
            Assert.Equal(0, logger.DroppedFrames);
            Assert.Null(logger.Error);
            Assert.Equal("End", rows[^1].RootElement.GetProperty("Kind").GetString());
        }
        finally { foreach (var row in rows) row.Dispose(); }
    }

    [Fact]
    public void Disabled_diagnostics_create_no_file_and_queue_overflow_is_observable()
    {
        string path = Path.Combine(Path.GetTempPath(), "astro-disabled-" + Guid.NewGuid() + ".jsonl");
        using (var disabled = new R2DiagnosticLogger(path, enabled: false))
        {
            disabled.Record(default);
            Assert.False(disabled.Enabled);
        }
        Assert.False(File.Exists(path));
        string fullPath = Path.Combine(Path.GetTempPath(), "astro-overflow-" + Guid.NewGuid() + ".jsonl");
        using var full = new R2DiagnosticLogger(fullPath, enabled: true, capacity: 1);
        for (int i = 0; i < 10000; i++) full.Record(default);
        Assert.True(full.DroppedFrames > 0);
    }

    [Fact]
    public void Diagnostic_storage_failure_does_not_throw_into_the_pipeline()
    {
        string directory = Path.Combine(Path.GetTempPath(), "astro-io-failure-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var logger = new R2DiagnosticLogger(directory, enabled: true);
        using (var rig = new Rig(VirtualPadType.Xbox360, logger))
        {
            rig.Input.State.R2 = 1;
            rig.Step(40);
            Assert.True(rig.Pad.Last.Has(PadButton.DpadDown | PadButton.Square));
        }
        Assert.NotNull(logger.Error);
    }
}
