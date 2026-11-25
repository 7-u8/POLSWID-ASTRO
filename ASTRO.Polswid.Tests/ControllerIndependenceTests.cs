using System.Threading;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Pipeline;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

public class ClassifierTests
{
    [Theory]
    [InlineData(0x054C, 0x05C4, "PS4", "DualShock 4")]
    [InlineData(0x054C, 0x09CC, "PS4", "DualShock 4")]
    [InlineData(0x054C, 0x0CE6, "PS5", "DualSense")]
    [InlineData(0x054C, 0x0DF2, "PS5", "DualSense Edge")]
    [InlineData(0x045E, 0x02FD, "XBoxOne", "Xbox One / One S")]
    [InlineData(0x045E, 0x0B13, "XBoxOne", "Xbox Series X|S")]
    [InlineData(0x045E, 0x028E, "XBox360", "Xbox 360")]
    [InlineData(0x045E, 0x1234, "XBoxOne", "Xbox One / Series")]
    [InlineData(0x046D, 0xC216, "Standard", "Generic gamepad")]
    public void Family_comes_from_ids_and_api_type_not_names(int vid, int pid, string type, string expected) =>
        Assert.Equal(expected, ControllerClassifier.Family((ushort)vid, (ushort)pid, type));
}

public class DeviceSelectorTests
{
    static PhysicalDeviceInfo Dev(uint id, string key, bool virt = false, bool supported = true) =>
        new(id, key, "pad" + id, 1, 1, "Generic gamepad", virt, false, supported);

    [Fact]
    public void Automatic_picks_first_usable_and_skips_virtual_and_unsupported()
    {
        var d = new[] { Dev(1, "A", virt: true), Dev(2, "B", supported: false), Dev(3, "C") };
        Assert.Equal(3u, DeviceSelector.Choose(d, null, null)!.Id);
    }

    [Fact]
    public void Automatic_keeps_the_active_device_when_another_one_appears_so_inputs_are_never_combined()
    {
        var d = new[] { Dev(1, "A"), Dev(2, "B") };
        Assert.Equal(2u, DeviceSelector.Choose(d, null, 2)!.Id);
    }

    [Fact]
    public void Explicit_primary_is_the_only_device_ever_used_even_if_absent()
    {
        var d = new[] { Dev(1, "A"), Dev(2, "B") };
        Assert.Equal(2u, DeviceSelector.Choose(d, "B", 1)!.Id);
        Assert.Null(DeviceSelector.Choose(d, "Z", 1));           // primary unplugged: do NOT fall back to another pad
    }

    [Fact]
    public void Replugged_primary_is_found_again_by_key_even_with_a_new_instance_id()
    {
        Assert.Equal(9u, DeviceSelector.Choose(new[] { Dev(9, "B") }, "B", null)!.Id);
    }

    [Fact]
    public void Virtual_device_is_never_selected_even_if_it_is_the_preferred_key()
    {
        Assert.Null(DeviceSelector.Choose(new[] { Dev(1, "B", virt: true) }, "B", null));
    }
}

/// <summary>Physical model and virtual output type are independent: the engine only ever sees normalized state.</summary>
public class OutputIndependenceTests
{
    sealed class TypedPad : IVirtualPad
    {
        readonly object _g = new();
        public readonly List<GamepadState> Sent = new();
        public TypedPad(VirtualPadType t) => PadType = t;
        public VirtualPadBackend Backend => VirtualPadBackend.ViGEmBus;
        public VirtualPadType PadType { get; }
        public bool IsConnected { get; private set; }
        public (ushort Vendor, ushort Product) Signature => PadType == VirtualPadType.Xbox360 ? ((ushort)0x045E, (ushort)0x028E) : ((ushort)0x054C, (ushort)0x05C4);
        public void Connect() => IsConnected = true;
        public void Disconnect() => IsConnected = false;
        public void Submit(in GamepadState s) { lock (_g) Sent.Add(s); }
        public void Dispose() { }
        public GamepadState? Last { get { lock (_g) return Sent.Count == 0 ? null : Sent[^1]; } }
    }

    [Theory]
    [InlineData(VirtualPadType.Xbox360)]
    [InlineData(VirtualPadType.DualShock4)]
    public void Same_normalized_input_gives_identical_output_state_for_either_virtual_type(VirtualPadType type)
    {
        var input = new FakeInput();
        var pad = new TypedPad(type);
        var proc = new PolswidProcessor(DefaultProfiles.Create(DefaultProfiles.Standard));
        using var runner = new PipelineRunner(input, pad, proc, new InputEventLog());
        runner.Start();

        // "Physical Xbox" state, normalized: R2 pulled, D-Pad Down, left stick right.
        input.Set(new GamepadState { R2 = 1f, Buttons = PadButton.DpadDown, LX = 1f });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000 && !(pad.Last is { } l && l.Has(PadButton.DpadDown | PadButton.DpadRight))) Thread.Sleep(2);

        Assert.True(pad.Last is { } s && s.Has(PadButton.DpadDown) && s.Has(PadButton.DpadRight) && s.LX == 0f);
        Assert.Equal(type, runner.Snapshot.PadType);
        Assert.True(runner.Snapshot.Forwarding);
        Assert.Equal("Fake Xbox Series Controller", runner.Snapshot.PhysicalName);   // non-Sony pad drives a DS4 output with no complaint
    }
}
