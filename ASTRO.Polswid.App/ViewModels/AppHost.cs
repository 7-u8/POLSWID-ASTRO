using System.IO;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Pipeline;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using ASTRO.Polswid.Infrastructure.Configuration;
using ASTRO.Polswid.Infrastructure.ControllerDetection;
using ASTRO.Polswid.Infrastructure.Logging;
using ASTRO.Polswid.Infrastructure.ViGEm;

namespace ASTRO.Polswid.App.ViewModels;

/// <summary>Owns the long-lived services: logging, profile store, the processor and the pipeline runner lifecycle.</summary>
public sealed class AppHost : IDisposable
{
    public SerilogLog Log { get; }
    public ProfileStore Store { get; }
    public AppSettings Settings { get; }
    public InputEventLog Events { get; } = new();
    public PolswidProcessor Processor { get; }
    public List<Profile> Profiles { get; }
    public Profile Current { get; private set; }
    public PipelineRunner? Runner { get; private set; }
    public ViGEmStatus Vigem { get; private set; }
    public string? StartError { get; private set; }

    System.Threading.Timer? _saveTimer;

    public AppHost()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ASTRO POLSWID");
        Log = new SerilogLog(Path.Combine(root, "logs"));
        Store = new ProfileStore(root, Log);
        Settings = Store.LoadSettings();
        Profiles = Store.LoadAll();
        if (Settings.SelectedProfileId == DefaultProfiles.LegacyRightAnalog) Settings.SelectedProfileId = DefaultProfiles.LeftAnalog;
        Current = Profiles.FirstOrDefault(p => p.Id == Settings.SelectedProfileId) ?? Profiles[0];
        Processor = new PolswidProcessor(Current);
        Vigem = ViGEmDriverProbe.Probe();
        Log.Information("Startup. ViGEm: {Message}", Vigem.Message);
    }

    public bool IsRunning => Runner?.IsRunning == true;

    public void Start()
    {
        if (IsRunning) return;
        StartError = null;
        Vigem = ViGEmDriverProbe.Probe();

        var input = new SdlPhysicalInput(Log) { MappingFile = Path.Combine(Store.Root, "gamecontrollerdb.txt") };
        input.SelectDevice(Settings.PreferredDeviceKey);
        IVirtualPad pad = Settings.DryRun ? new DryRunVirtualPad(Settings.OutputType) : new ViGEmVirtualPad(Settings.OutputType);
        var runner = new PipelineRunner(input, pad, Processor, Events, Log);
        try
        {
            runner.Start();
            Runner = runner;
            Processor.ResetAll();
            Log.Information("Pipeline started (dry run: {DryRun})", Settings.DryRun);
        }
        catch (Exception ex)
        {
            StartError = ex.Message;
            Log.Error(ex, "Pipeline failed to start");
            runner.Dispose();
            Runner = null;
        }
    }

    public void Stop()
    {
        Runner?.Stop();
        Runner = null;
    }

    public void Restart() { Stop(); Start(); }

    public void SelectProfile(Profile p)
    {
        Current = p;
        Settings.SelectedProfileId = p.Id;
        Store.SaveSettings(Settings);
        Processor.SetProfile(p);
    }

    /// <summary>Push the edited profile to the live pipeline now and persist it shortly after (debounced).</summary>
    public void CommitProfile()
    {
        Processor.SetProfile(Current);
        _saveTimer?.Dispose();
        var snapshot = Current;
        _saveTimer = new System.Threading.Timer(_ =>
        {
            try { Store.Save(snapshot); }
            catch (Exception ex) { Log.Error(ex, "Saving profile {Name} failed", snapshot.Name); }
        }, null, 400, Timeout.Infinite);
    }

    public void SaveSettings() => Store.SaveSettings(Settings);

    public void Post(MacroCommandKind kind) => Processor.Post(kind);

    public void Dispose()
    {
        _saveTimer?.Dispose();
        try { Store.Save(Current); } catch (Exception ex) { Log.Error(ex, "Final profile save failed"); }
        Stop();
        Log.Information("Shutdown complete");
        Log.Dispose();
    }
}
