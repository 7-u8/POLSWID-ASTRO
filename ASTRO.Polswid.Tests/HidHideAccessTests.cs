using System.IO;
using System.Reflection;
using ASTRO.Polswid.Infrastructure.ControllerDetection;
using Nefarius.Drivers.HidHide;

namespace ASTRO.Polswid.Tests;

public class HidHideDriverProxy : DispatchProxy
{
    public bool Installed = true, Active = true, Inverse, FailReads;
    public readonly List<string> Apps = new(), Hidden = new();
    public int Writes;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (FailReads) throw new IOException("test configuration client busy");
        return method!.Name switch
        {
            "get_IsInstalled" => Installed,
            "get_IsActive" => Active,
            "get_IsAppListInverted" => Inverse,
            "get_ApplicationPaths" => Apps.ToArray(),
            "get_BlockedInstanceIds" => Hidden.ToArray(),
            "AddApplicationPath" => Add((string)args![0]!),
            _ => throw new InvalidOperationException("Forbidden/unknown HidHide call in test: " + method.Name),
        };
    }
    object? Add(string path) { Writes++; Apps.Add(path); return null; }
}

public sealed class HidHideAccessTests
{
    static (HidHideDriverProxy, HidHideAccessService) Make()
    {
        var api = DispatchProxy.Create<IHidHideControlService, HidHideDriverProxy>();
        return ((HidHideDriverProxy)api, new HidHideAccessService(api));
    }

    [Fact]
    public void Official_API_read_identifies_blocked_executable_and_Edge_entries_without_any_write()
    {
        var (driver, service) = Make();
        driver.Hidden.Add(@"HID\VID_054C&PID_0DF2\test-instance");
        driver.Hidden.Add(@"HID\VID_054C&PID_09CC\another-model");
        var s = service.Read(@"C:\New Build\ASTRO POLSWID.exe");
        Assert.True(s.Verified); Assert.True(s.Installed);
        Assert.False(s.ApplicationAllowed); Assert.False(s.ApplicationListed);
        Assert.Equal(1, s.HiddenEdgeEntries);
        Assert.Equal(0, driver.Writes);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Policy_evaluation_respects_normal_and_inverse_lists(bool inverted, bool listed, bool allowed)
    {
        var (driver, service) = Make();
        driver.Inverse = inverted;
        if (listed) driver.Apps.Add(@"c:\ASTRO.exe");
        Assert.Equal(allowed, service.Read(@"C:\astro.exe").ApplicationAllowed);
        Assert.Equal(0, driver.Writes);
    }

    [Fact]
    public void Driver_missing_or_busy_never_claims_verified_permission()
    {
        var (driver, service) = Make();
        driver.Installed = false;
        var absent = service.Read("test");
        Assert.True(absent.Verified); Assert.False(absent.Installed); Assert.Null(absent.ApplicationAllowed);
        driver.FailReads = true;
        var busy = service.Read("test");
        Assert.False(busy.Verified); Assert.Null(busy.ApplicationAllowed);
        Assert.Contains("busy", busy.Error);
        Assert.Equal(0, driver.Writes);
    }

    [Fact]
    public void Grant_is_impossible_without_explicit_approval()
    {
        var (driver, service) = Make();
        Assert.Throws<InvalidOperationException>(() => service.AllowThisExecutable(Environment.ProcessPath!, false));
        Assert.Equal(0, driver.Writes);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Grant_refuses_inverse_mode_or_disabled_hiding_without_changing_them(bool inverse, bool active)
    {
        var (driver, service) = Make();
        driver.Inverse = inverse; driver.Active = active;
        Assert.Throws<InvalidOperationException>(() => service.AllowThisExecutable(Environment.ProcessPath!, true));
        Assert.Equal(0, driver.Writes);
        Assert.Equal(inverse, driver.Inverse); Assert.Equal(active, driver.Active);
    }

    [Fact]
    public void Approved_grant_adds_only_exact_executable_and_preserves_device_and_application_entries()
    {
        var (driver, service) = Make();
        string exe = Environment.ProcessPath!;
        driver.Apps.Add(@"C:\Other feeder\other.exe");
        driver.Hidden.Add(@"HID\VID_054C&PID_0DF2\physical");
        var after = service.AllowThisExecutable(exe, true);
        Assert.True(after.ApplicationAllowed);
        Assert.Equal(new[] { @"C:\Other feeder\other.exe", exe }, driver.Apps);
        Assert.Single(driver.Hidden); Assert.True(driver.Active); Assert.False(driver.Inverse);
        Assert.Equal(1, driver.Writes);
        service.AllowThisExecutable(exe, true);
        Assert.Equal(1, driver.Writes); // Already registered: no write.
    }
}
