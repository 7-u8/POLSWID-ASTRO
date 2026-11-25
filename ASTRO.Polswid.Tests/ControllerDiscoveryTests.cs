using System.IO;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Infrastructure.ControllerDetection;

namespace ASTRO.Polswid.Tests;

public sealed class ControllerDiscoveryTests
{
    static PhysicalDeviceInfo Pad(uint id, string? path, bool virt = false, ControllerOrigin origin = ControllerOrigin.Unknown) =>
        new(id, "", "Test controller", 0x054C, 0x05C4, "DualShock 4", virt, false, true, path, origin);

    [Fact]
    public void Refresh_is_marshaled_to_owner_and_reveals_only_authorized_visible_inventory()
    {
        IReadOnlyList<PhysicalDeviceInfo> visible = Array.Empty<PhysicalDeviceInfo>();
        int enumerations = 0, ownerThread = Environment.CurrentManagedThreadId;
        using var input = new SdlPhysicalInput(() =>
        {
            Assert.Equal(ownerThread, Environment.CurrentManagedThreadId);
            enumerations++;
            return visible;
        });
        input.RefreshDiscovery(1_000_000, force: true);
        Assert.Empty(input.Devices); // Unauthorized/hidden and disconnected are deliberately indistinguishable.
        Assert.Null(input.DiscoveryError);
        visible = new[] { Pad(1, "physical-path", origin: ControllerOrigin.Physical) };
        Assert.False(input.RefreshDiscovery(1_001_000));
        var uiThread = new System.Threading.Thread(input.RequestRefresh);
        uiThread.Start();
        uiThread.Join();
        Assert.Equal(1, enumerations); // The UI request never calls the backend itself.
        Assert.True(input.RefreshDiscovery(1_002_000));
        Assert.Equal(2, enumerations);
        Assert.Equal(1u, DeviceSelector.Choose(input.Devices, null, null)!.Id);
        Assert.False(input.RefreshDiscovery(1_003_000));
    }

    [Fact]
    public void Automatic_discovery_reconnects_after_start_and_preserves_primary_across_reorder()
    {
        IReadOnlyList<PhysicalDeviceInfo> visible = Array.Empty<PhysicalDeviceInfo>();
        using var input = new SdlPhysicalInput(() => visible);
        input.RefreshDiscovery(1_000_000, force: true);
        visible = new[] { Pad(1, "physical-A"), Pad(2, "physical-B") };
        input.RefreshDiscovery(1_250_000);
        string primary = input.Devices.Single(d => d.Id == 1).Key;
        input.SelectDevice(primary);
        visible = new[] { Pad(2, "physical-B"), Pad(1, "physical-A") };
        input.RefreshDiscovery(1_500_000);
        Assert.Equal(1u, DeviceSelector.Choose(input.Devices, primary, 1)!.Id);
        visible = new[] { Pad(2, "physical-B") };
        input.RefreshDiscovery(1_750_000);
        Assert.Null(DeviceSelector.Choose(input.Devices, primary, null)); // No fallback to another pad.
        visible = new[] { Pad(2, "physical-B"), Pad(9, "physical-A") };
        input.RefreshDiscovery(2_000_000);
        Assert.Equal(9u, DeviceSelector.Choose(input.Devices, primary, null)!.Id);
    }

    [Fact]
    public void Enumeration_access_failure_is_reported_without_replacing_last_inventory_then_clears_on_retry()
    {
        bool denied = false;
        using var input = new SdlPhysicalInput(() => denied
            ? throw new UnauthorizedAccessException("test access denied") : new[] { Pad(1, "A") });
        input.RefreshDiscovery(1_000_000, force: true);
        denied = true;
        input.RequestRefresh();
        Assert.False(input.RefreshDiscovery(1_001_000));
        Assert.Single(input.Devices);
        Assert.Contains("access denied", input.DiscoveryError);
        denied = false;
        input.RequestRefresh();
        Assert.True(input.RefreshDiscovery(1_002_000));
        Assert.Null(input.DiscoveryError);
    }

    [Fact]
    public void Duplicate_instances_and_equal_paths_are_collapsed_without_merging_distinct_identical_models()
    {
        var catalog = new DeviceDiscoveryCatalog();
        var found = catalog.Update(new[] { Pad(1, "A"), Pad(1, "A"), Pad(2, "a"), Pad(3, "B"), Pad(4, null), Pad(5, null) });
        Assert.Equal(new uint[] { 1, 3, 4, 5 }, found.Select(d => d.Id));
        Assert.Equal(4, found.Select(d => d.Key).Distinct().Count());
        var activeAlias = Pad(2, "a") with { IsSelected = true };
        Assert.Equal(2u, Assert.Single(catalog.Update(new[] { Pad(1, "A"), activeAlias })).Id);
    }

    [Fact]
    public void Own_virtual_output_is_excluded_and_does_not_steal_the_physical_selection_key()
    {
        IReadOnlyList<PhysicalDeviceInfo> visible = new[] { Pad(1, "physical", origin: ControllerOrigin.Physical) };
        using var input = new SdlPhysicalInput(() => visible);
        input.RefreshDiscovery(1_000_000, force: true);
        string primary = Assert.Single(input.Devices).Key;
        input.BeginVirtualFilter(0x054C, 0x05C4);
        visible = new[] { Pad(7, "own virtual"), Pad(1, "physical", origin: ControllerOrigin.Physical) };
        input.RefreshDiscovery(1_250_000);
        Assert.True(input.Devices.Single(d => d.Id == 7).IsLikelyVirtual);
        Assert.Equal(primary, input.Devices.Single(d => d.Id == 1).Key);
        Assert.Equal(1u, DeviceSelector.Choose(input.Devices, primary, null)!.Id);
        input.RequestRefresh(); input.RefreshDiscovery(1_252_000);
        Assert.Equal(1u, DeviceSelector.Choose(input.Devices, null, null)!.Id);
    }

    [Fact]
    public void Newly_visible_physical_pad_with_same_vid_pid_as_virtual_is_not_misclassified()
    {
        IReadOnlyList<PhysicalDeviceInfo> visible = Array.Empty<PhysicalDeviceInfo>();
        using var input = new SdlPhysicalInput(() => visible);
        input.RefreshDiscovery(1_000_000, force: true);
        input.BeginVirtualFilter(0x054C, 0x05C4);
        visible = new[] { Pad(7, "virtual", origin: ControllerOrigin.ViGEm),
            Pad(9, "physical&rev_test", origin: ControllerOrigin.Physical) };
        input.RequestRefresh(); input.RefreshDiscovery(1_002_000);
        Assert.True(input.Devices.Single(d => d.Id == 7).IsLikelyVirtual);
        Assert.False(input.Devices.Single(d => d.Id == 9).IsLikelyVirtual);
        Assert.Equal("054C:05C4", input.Devices.Single(d => d.Id == 9).Key);
        Assert.Equal(9u, DeviceSelector.Choose(input.Devices, null, null)!.Id);
    }

    [Theory]
    [InlineData("USB\\VID_054C&PID_05C4\\physical", ControllerOrigin.Physical)]
    [InlineData("BTHENUM\\physical", ControllerOrigin.Physical)]
    [InlineData("ROOT\\VIGEMBUS\\0000", ControllerOrigin.ViGEm)]
    [InlineData("ROOT\\OTHERBUS\\0000", ControllerOrigin.Unknown)]
    public void Origin_classification_uses_device_ancestry_not_a_product_name(string ancestor, ControllerOrigin expected) =>
        Assert.Equal(expected, WindowsControllerOrigin.ClassifyAncestors(new[] { "HID\\controller", ancestor }));

    [Fact]
    public void ViGEm_ancestor_overrides_its_emulated_USB_child() =>
        Assert.Equal(ControllerOrigin.ViGEm, WindowsControllerOrigin.ClassifyAncestors(new[]
            { "USB\\VID_054C&PID_05C4\\emulated", "ROOT\\VIGEMBUS\\0000" }));

    [Fact]
    public void Unresolvable_Windows_interface_is_unknown_rather_than_claimed_physical_or_virtual() =>
        Assert.Equal(ControllerOrigin.Unknown, WindowsControllerOrigin.Query(@"\\?\HID#ASTRO_TEST_NONEXISTENT#{00000000-0000-0000-0000-000000000000}"));
}
