using System.Windows;
using ASTRO.Polswid.App.ViewModels;

namespace ASTRO.Polswid.App;

public partial class App : Application
{
    Mutex? _single;
    AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Explicit read-only diagnostic; no settings, controller handles, or normal startup.
        if (e.Args is ["--hidhide-status", var hidHideStatusPath])
        {
            var status = new ASTRO.Polswid.Infrastructure.ControllerDetection.HidHideAccessService()
                .Read(Environment.ProcessPath ?? "");
            System.IO.File.WriteAllText(hidHideStatusPath, System.Text.Json.JsonSerializer.Serialize(status,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Shutdown();
            return;
        }

        // Packaging verification only: exits before settings, controller input, or the driver is opened.
        if (e.Args is ["--r2-build-info", var buildInfoPath])
        {
            try
            {
                System.IO.File.WriteAllText(buildInfoPath, System.Text.Json.JsonSerializer.Serialize(new
                {
                    AppBuildId = typeof(App).Module.ModuleVersionId,
                    CoreBuildId = typeof(ASTRO.Polswid.Core.Diagnostics.R2DiagnosticLogger).Module.ModuleVersionId,
                    InfrastructureBuildId = typeof(ASTRO.Polswid.Infrastructure.ViGEm.ViGEmVirtualPad).Module.ModuleVersionId,
                    Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    SdlNativeVersion = SDL3.SDL.GetVersion(),
                    ViGEmClient = typeof(Nefarius.ViGEm.Client.ViGEmClient).Assembly.GetName().FullName,
                    DiagnosticSchema = 1,
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Shutdown();
            }
            catch { Shutdown(1); }
            return;
        }

        // Two instances would fight over the same controller and create two virtual pads.
        bool created;
        try
        {
            _single = new Mutex(true, "ASTRO.POLSWID.SingleInstance", out created);
        }
        catch (AbandonedMutexException)
        {
            created = true;
        }

        if (!created)
        {
            var current = System.Diagnostics.Process.GetCurrentProcess();
            var others = System.Diagnostics.Process.GetProcessesByName(current.ProcessName)
                .Where(p => p.Id != current.Id).ToArray();
            if (others.Length > 0)
            {
                MessageBox.Show("ASTRO POLSWID is already running.", "ASTRO POLSWID", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
        }

        try
        {
            _host = new AppHost();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ASTRO POLSWID could not start:\n\n" + ex, "ASTRO POLSWID", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            _host.Log.Error(args.Exception, "Unhandled UI exception");
            MessageBox.Show("Unexpected error (logged):\n\n" + args.Exception.Message, "ASTRO POLSWID", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            _host.Log.Error(args.ExceptionObject as Exception, "Fatal unhandled exception");
            try { _host.Stop(); } catch { /* process is dying; the virtual device disappears with it */ }
        };

        if (_host.Settings.AutoStartPipeline) _host.Start();
        var win = new MainWindow(_host);
        MainWindow = win;
        win.Show();
        if (Environment.GetEnvironmentVariable("ASTRO_AUTOSHOT") is { Length: > 0 } shotDir) _ = win.RunAutoShots(shotDir);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();      // releases all virtual controls and disconnects the virtual pad
        _single?.Dispose();
        base.OnExit(e);
    }
}
