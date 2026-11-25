using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ASTRO.Polswid.App.Localization;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.MacroEngine;
using ASTRO.Polswid.Core.Output;
using ASTRO.Polswid.Core.Polswid;
using ASTRO.Polswid.Core.Profiles;
using ASTRO.Polswid.Core.Timing;
using ASTRO.Polswid.Infrastructure.ViGEm;
using ASTRO.Polswid.Infrastructure.ControllerDetection;
using Microsoft.Win32;

namespace ASTRO.Polswid.App.ViewModels;

public enum AppPage { Home, Advanced, Help, Dev, Settings }

public sealed class MappingRow : ObservableObject
{
    readonly MainViewModel _vm;
    PadButton _target;
    public MappingRow(MainViewModel vm, PadButton source, PadButton target) { _vm = vm; Source = source; _target = target; }
    public PadButton Source { get; }
    public string SourceName => Loc.ButtonName(Source);
    public PadButton Target
    {
        get => _target;
        set { if (Set(ref _target, value)) _vm.SetBinding(Source, value); }
    }
    bool _selected;
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
}

public sealed class MainViewModel : ObservableObject, IDisposable
{
    public AppHost Host { get; }
    readonly DispatcherTimer _timer;
    readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    readonly long _startUs = Clock.NowUs;
    long _lastEventSeq, _baseUs;
    readonly HidHideAccessService _hidHide = new();
    bool _hidHideBusy;
    HidHideAccessStatus? _hidHideStatus;

    /// <summary>Raised after the language changed; the window rebuilds its UI.</summary>
    public event Action? LanguageChanged;

    public MainViewModel(AppHost host)
    {
        Host = host;
        Loc.Instance.SetLanguage(host.Settings.Language == "en" ? Lang.En : Lang.Ar);

        Profiles = new ObservableCollection<Profile>(host.Profiles);
        _profile = host.Current;
        Rows = new ObservableCollection<MappingRow>();
        RebuildRows();
        BuildMonitorRows();
        _wizardVisible = !host.Settings.WizardDone;

        ToggleRemappingCommand = new RelayCommand(() => Host.Post(MacroCommandKind.ToggleRemapping));
        ToggleAnalogCommand = new RelayCommand(() => Host.Post(MacroCommandKind.ToggleAnalog));
        EmergencyStopCommand = new RelayCommand(() => Host.Post(MacroCommandKind.EmergencyStop));
        ClearEmergencyCommand = new RelayCommand(() => Host.Post(MacroCommandKind.ClearEmergency));
        StartStopCommand = new RelayCommand(() => { if (Host.IsRunning) Host.Stop(); else Host.Start(); Refresh(); });
        RestartPipelineCommand = new RelayCommand(() => { Host.Restart(); Refresh(); });
        RetryCommand = new RelayCommand(() => { Host.Restart(); Refresh(); });
        RefreshControllersCommand = new RelayCommand(RefreshDevices,
            () => Host.IsRunning && Host.Runner?.Input is IControllerDiscoveryControl);
        CopyReaderPathCommand = new RelayCommand(() => Clipboard.SetText(ReaderExecutablePath));
        CheckHidHideCommand = new RelayCommand(async () => await CheckHidHideAsync(), () => !_hidHideBusy);
        AllowHidHideCommand = new RelayCommand(async () => await AllowHidHideAsync(), () => !_hidHideBusy);
        TriggerUTurnCommand = new RelayCommand(() => Host.Post(MacroCommandKind.TriggerUTurn));
        TriggerAirCommand = new RelayCommand(() => Host.Post(MacroCommandKind.TriggerAirSwitch));
        CancelHelpersCommand = new RelayCommand(() => Host.Post(MacroCommandKind.CancelHelpers));
        NewProfileCommand = new RelayCommand(() => AddProfile(Host.Store.Duplicate(DefaultProfiles.Create(DefaultProfiles.Custom))));
        DuplicateProfileCommand = new RelayCommand(() => AddProfile(Host.Store.Duplicate(Profile)));
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => Profiles.Count > 1);
        ResetProfileCommand = new RelayCommand(ResetProfile);
        ImportProfileCommand = new RelayCommand(ImportProfile);
        ExportProfileCommand = new RelayCommand(ExportProfile);
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        ClearLogCommand = new RelayCommand(() => LogLines.Clear());
        CopyLogCommand = new RelayCommand(() => Clipboard.SetText(string.Join(Environment.NewLine, LogLines)));
        OpenLogFolderCommand = new RelayCommand(() => System.Diagnostics.Process.Start("explorer.exe", Host.Log.Directory));
        OpenDriverPageCommand = new RelayCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ViGEmDriverProbe.ReleasesUrl) { UseShellExecute = true }));
        ToggleLanguageCommand = new RelayCommand(() => Language = Loc.Instance.IsArabic ? Lang.En : Lang.Ar);
        GoCommand = new RelayCommand(p => Go(Enum.Parse<AppPage>((string)p!)));
        WizNextCommand = new RelayCommand(() => WizardStep = Math.Min(WizardSteps, WizardStep + 1));
        WizBackCommand = new RelayCommand(() => WizardStep = Math.Max(1, WizardStep - 1));
        WizSkipCommand = new RelayCommand(FinishWizard);
        WizFinishCommand = new RelayCommand(FinishWizard);
        RunWizardCommand = new RelayCommand(() => { WizardStep = 1; WizardVisible = true; });
        PickOutputCommand = new RelayCommand(p => OutputType = Enum.Parse<VirtualPadType>((string)p!));
        SetHomeViewVirtualCommand = new RelayCommand(() => HomeViewVirtual = true);
        SetHomeViewPhysicalCommand = new RelayCommand(() => HomeViewVirtual = false);

        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastVisible = false; };
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    // ================================================================ pages, language, level

    AppPage _page = AppPage.Home;
    public AppPage Page { get => _page; set { if (Set(ref _page, value)) { Raise(nameof(IsHome)); Raise(nameof(IsAdvanced)); Raise(nameof(IsHelp)); Raise(nameof(IsDev)); Raise(nameof(IsSettings)); } } }
    public bool IsHome => Page == AppPage.Home;
    public bool IsAdvanced => Page == AppPage.Advanced;
    public bool IsHelp => Page == AppPage.Help;
    public bool IsDev => Page == AppPage.Dev;
    public bool IsSettings => Page == AppPage.Settings;
    public void Go(AppPage p) { if (p == AppPage.Dev && !DeveloperMode) return; Page = p; }

    public Lang Language
    {
        get => Loc.Instance.Language;
        set
        {
            if (value == Loc.Instance.Language) return;
            Loc.Instance.SetLanguage(value);
            Host.Settings.Language = value == Lang.En ? "en" : "ar";
            Host.SaveSettings();
            RebuildRows();
            BuildMonitorRows();
            _devSig = "";
            Raise(string.Empty);            // every bound property re-reads its (now localized) text
            LanguageChanged?.Invoke();
        }
    }
    public bool LanguageIsArabic { get => Language == Lang.Ar; set { if (value) Language = Lang.Ar; } }
    public bool LanguageIsEnglish { get => Language == Lang.En; set { if (value) Language = Lang.En; } }

    public bool DeveloperMode
    {
        get => Host.Settings.DeveloperMode;
        set
        {
            if (Host.Settings.DeveloperMode == value) return;
            if (value && !Confirm(Loc.T("confirm_dev"))) { Raise(); return; }
            Host.Settings.DeveloperMode = value;
            Host.SaveSettings();
            if (!value && Page == AppPage.Dev) Page = AppPage.Home;
            Raise();
        }
    }

    // ================================================================ dialogs & toasts

    public static bool Confirm(string text) =>
        MessageBox.Show(text, Loc.T("dlg_title"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, Loc.Instance.IsArabic ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None) == MessageBoxResult.Yes;

    public static void ShowError(string text) =>
        MessageBox.Show(text, Loc.T("dlg_title"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Loc.Instance.IsArabic ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);

    string _toast = "";
    bool _toastVisible;
    public string Toast { get => _toast; private set => Set(ref _toast, value); }
    public bool ToastVisible { get => _toastVisible; private set => Set(ref _toastVisible, value); }
    public void Notify(string text)
    {
        Toast = text;
        ToastVisible = true;
        _toastTimer.Stop(); _toastTimer.Start();
    }

    // ================================================================ wizard

    public const int WizardSteps = 5;
    bool _wizardVisible;
    int _wizardStep = 1;
    public bool WizardVisible { get => _wizardVisible; set => Set(ref _wizardVisible, value); }
    public int WizardStep
    {
        get => _wizardStep;
        set
        {
            if (!Set(ref _wizardStep, value)) return;
            if (value == 3) { WizButtonSeen = false; WizStickSeen = false; }
            Raise(nameof(WizStepText)); Raise(nameof(WizIs1)); Raise(nameof(WizIs2)); Raise(nameof(WizIs3)); Raise(nameof(WizIs4)); Raise(nameof(WizIs5));
            Raise(nameof(WizCanBack)); Raise(nameof(WizIsLast)); Raise(nameof(WizSummaryPhys)); Raise(nameof(WizSummaryVirt));
        }
    }
    public string WizStepText => Loc.F("wiz_step", WizardStep, WizardSteps);
    public bool WizIs1 => WizardStep == 1; public bool WizIs2 => WizardStep == 2; public bool WizIs3 => WizardStep == 3;
    public bool WizIs4 => WizardStep == 4; public bool WizIs5 => WizardStep == 5;
    public bool WizCanBack => WizardStep > 1;
    public bool WizIsLast => WizardStep == WizardSteps;
    bool _btnSeen, _stickSeen;
    public bool WizButtonSeen { get => _btnSeen; private set { if (Set(ref _btnSeen, value)) Raise(nameof(WizButtonText)); } }
    public bool WizStickSeen { get => _stickSeen; private set { if (Set(ref _stickSeen, value)) Raise(nameof(WizStickText)); } }
    public string WizButtonText => WizButtonSeen ? Loc.T("wiz3_btn_ok") : Loc.T("wiz3_wait");
    public string WizStickText => WizStickSeen ? Loc.T("wiz3_stick_ok") : Loc.T("wiz3_wait");
    public string WizFoundText => PhysicalConnected ? Loc.F("wiz1_found", PhysName) : Loc.T("wiz1_none");
    public string WizSummaryPhys => Loc.F("wiz5_phys", PhysicalConnected ? PhysName : Loc.T("wiz_no_controller"));
    public string WizSummaryVirt => Loc.F("wiz5_virt", OutputName);
    public bool PhysicalConnected { get; private set; }

    void FinishWizard()
    {
        WizardVisible = false;
        Host.Settings.WizardDone = true;
        Host.SaveSettings();
    }

    // ================================================================ profiles

    public ObservableCollection<Profile> Profiles { get; }
    Profile _profile;
    public Profile Profile
    {
        get => _profile;
        set
        {
            if (value == null || ReferenceEquals(value, _profile)) return;
            _profile = value;
            Host.SelectProfile(value);
            RebuildRows();
            Raise(); Raise(nameof(SelectedProfile)); Raise(nameof(ProfileName));
        }
    }
    public Profile SelectedProfile { get => Profile; set => Profile = value; }

    public string ProfileName
    {
        get => Profile.Name;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == Profile.Name) return;
            Profile.Name = value.Trim();
            var i = Profiles.IndexOf(Profile);
            var p = Profile;
            if (i >= 0) { Profiles[i] = p; _profile = p; Raise(nameof(SelectedProfile)); }
            CommitProfile();
        }
    }

    public void CommitProfile() => Host.CommitProfile();

    void AddProfile(Profile p) { Profiles.Add(p); Host.Profiles.Add(p); Profile = p; }

    void DeleteProfile()
    {
        if (Profiles.Count <= 1) return;
        var p = Profile;
        if (!Confirm(Loc.F("confirm_delete", p.Name))) return;
        Host.Store.Delete(p);
        int i = Profiles.IndexOf(p);
        Profiles.Remove(p); Host.Profiles.Remove(p);
        Profile = Profiles[Math.Clamp(i, 0, Profiles.Count - 1)];
    }

    void ResetProfile()
    {
        if (!Confirm(Loc.F("confirm_reset_profile", Profile.Name))) return;
        ReplaceCurrentWithFactory();
    }

    void ReplaceCurrentWithFactory()
    {
        var fresh = Host.Store.Reset(Profile);
        int i = Profiles.IndexOf(Profile);
        Profiles[i] = fresh; Host.Profiles[i] = fresh;
        _profile = null!; Profile = fresh;
    }

    void RestoreDefaults()
    {
        if (!Confirm(Loc.T("confirm_restore"))) return;
        ReplaceCurrentWithFactory();
        bool restart = Host.Settings.DryRun || Host.Settings.OutputType != VirtualPadType.DualShock4;
        Host.Settings.DryRun = false;
        Host.Settings.OutputType = VirtualPadType.DualShock4;
        Host.Settings.PreferredDeviceKey = null;
        Host.Settings.GlobalHotkeys = true;
        Host.Settings.DeveloperMode = false;
        Host.SaveSettings();
        Host.Runner?.Input.SelectDevice(null);
        Host.Post(MacroCommandKind.ClearEmergency);
        Host.Post(MacroCommandKind.EnableRemapping);
        if (restart) Host.Restart();
        if (Page == AppPage.Dev) Page = AppPage.Home;
        _devSig = "";
        Raise(string.Empty);
        GlobalHotkeysChanged?.Invoke();
        Notify(Loc.T("n_restored"));
    }

    void ImportProfile()
    {
        var dlg = new OpenFileDialog { Filter = "ASTRO POLSWID (*.json)|*.json", Title = Loc.T("btn_import") };
        if (dlg.ShowDialog() != true) return;
        try { AddProfile(Host.Store.Import(dlg.FileName)); }
        catch (Exception ex) { ShowError(Loc.F("err_import", ex.Message)); }
    }

    void ExportProfile()
    {
        var dlg = new SaveFileDialog { Filter = "ASTRO POLSWID (*.json)|*.json", FileName = Profile.Name + ".json", Title = Loc.T("btn_export") };
        if (dlg.ShowDialog() != true) return;
        try { Host.Store.Export(Profile, dlg.FileName); }
        catch (Exception ex) { ShowError(Loc.F("err_export", ex.Message)); }
    }

    // ================================================================ mapping

    public ObservableCollection<MappingRow> Rows { get; }
    public PadButton[] AllTargets { get; } = new[] { PadButton.None }.Concat(PadButtons.All).ToArray();
    public Array PriorityModes { get; } = Enum.GetValues(typeof(PriorityMode));
    public Array TriggerModes { get; } = Enum.GetValues(typeof(TriggerMode));
    public Array UTurnModes { get; } = Enum.GetValues(typeof(UTurnExecution));
    public SteeringDirection[] FallbackSides { get; } = { SteeringDirection.Left, SteeringDirection.Right };
    public string[] TimingPresetNames => TimingPresets.Names;

    void RebuildRows()
    {
        Rows.Clear();
        foreach (var b in PadButtons.All)
        {
            var bind = Profile.Mapping.Bindings.FirstOrDefault(x => x.From == b);
            Rows.Add(new MappingRow(this, b, bind?.To ?? b));
        }
    }

    public void SetBinding(PadButton source, PadButton target)
    {
        var list = Profile.Mapping.Bindings;
        var bind = list.FirstOrDefault(x => x.From == source);
        if (bind == null) list.Add(new ButtonBinding(source, target)); else bind.To = target;
        CommitProfile();
    }

    public void SelectRow(PadButton b)
    {
        foreach (var r in Rows) r.IsSelected = r.Source == b;
        Raise(nameof(SelectedRowText));
    }
    public string SelectedRowText
    {
        get
        {
            var r = Rows.FirstOrDefault(x => x.IsSelected);
            return r == null ? Loc.T("map_pick") : $"{r.SourceName}  →  {Loc.ButtonName(r.Target)}";
        }
    }

    // ================================================================ independent output + primary controller

    public Array OutputTypes { get; } = Enum.GetValues(typeof(VirtualPadType));
    public VirtualPadType OutputType
    {
        get => Host.Settings.OutputType;
        set
        {
            if (Host.Settings.OutputType == value) return;
            if (Host.IsRunning && !Confirm(Loc.T("confirm_output"))) { Raise(); return; }
            Host.Settings.OutputType = value;
            Host.SaveSettings();
            Host.Restart();                 // new virtual device; the physical pad is untouched
            Raise(); Raise(nameof(OutputName)); Raise(nameof(OutputNote)); Raise(nameof(OutputIsDs4)); Raise(nameof(OutputIsX360));
            Raise(nameof(WizSummaryVirt));
        }
    }
    public bool OutputIsDs4 => OutputType == VirtualPadType.DualShock4;
    public bool OutputIsX360 => OutputType == VirtualPadType.Xbox360;
    public string OutputName => Loc.T(OutputType == VirtualPadType.Xbox360 ? "out_x360" : "out_ds4");
    public string OutputNote => Loc.T(OutputType == VirtualPadType.Xbox360 ? "out_note_x360" : "out_note_ds4");

    public sealed record DeviceChoice(string? Key, string Display);
    public ObservableCollection<DeviceChoice> DeviceChoices { get; } = new() { new DeviceChoice(null, Loc.T("device_auto")) };
    DeviceChoice? _selectedDevice;
    public DeviceChoice? SelectedDevice
    {
        get => _selectedDevice ?? DeviceChoices.FirstOrDefault(c => c.Key == Host.Settings.PreferredDeviceKey) ?? DeviceChoices[0];
        set
        {
            if (value == null) return;
            _selectedDevice = value;
            Host.Settings.PreferredDeviceKey = value.Key;
            Host.SaveSettings();
            Host.Runner?.Input.SelectDevice(value.Key);
            Raise();
        }
    }

    string _devSig = "";
    void SyncDeviceChoices()
    {
        var devs = Devices.Where(d => d.IsSupported && !d.IsLikelyVirtual).ToList();
        var sig = string.Join("|", devs.Select(d => d.Key + d.Name)) + "/" + Host.Settings.PreferredDeviceKey + Loc.Instance.Language;
        if (sig == _devSig) return;
        _devSig = sig;
        var keep = SelectedDevice?.Key;
        DeviceChoices.Clear();
        DeviceChoices.Add(new DeviceChoice(null, Loc.T("device_auto")));
        foreach (var d in devs) DeviceChoices.Add(new DeviceChoice(d.Key, $"{d.Name}  ({d.Family})"));
        var pref = Host.Settings.PreferredDeviceKey;
        if (pref != null && DeviceChoices.All(c => c.Key != pref)) DeviceChoices.Add(new DeviceChoice(pref, Loc.F("device_not_connected", pref)));
        _selectedDevice = DeviceChoices.FirstOrDefault(c => c.Key == (keep ?? pref)) ?? DeviceChoices[0];
        Raise(nameof(SelectedDevice));
    }

    string _pName = "–", _pFamily = "–", _pConn = "–", _pBattery = "–", _pApi = "–", _pCaps = "–";
    public string PhysName { get => _pName; private set => Set(ref _pName, value); }
    public string PhysFamily { get => _pFamily; private set => Set(ref _pFamily, value); }
    public string PhysConnection { get => _pConn; private set => Set(ref _pConn, value); }
    public string PhysBattery { get => _pBattery; private set => Set(ref _pBattery, value); }
    public string PhysApi { get => _pApi; private set => Set(ref _pApi, value); }
    public string PhysCaps { get => _pCaps; private set => Set(ref _pCaps, value); }

    static string LocConnection(string raw) => raw switch
    {
        "USB / wired" => Loc.T("conn_wired"),
        "Bluetooth / wireless" => Loc.T("conn_wireless"),
        _ => Loc.T("conn_unknown"),
    };

    static string LocBattery(string raw)
    {
        if (raw.StartsWith("wired")) return Loc.T("bat_wired");
        if (raw.StartsWith("charged")) return Loc.T("bat_charged");
        if (raw.StartsWith("not reported") || raw.StartsWith("on battery") || raw == "charging") return Loc.T("bat_unknown");
        var pct = new string(raw.TakeWhile(char.IsDigit).ToArray());
        if (pct.Length == 0) return Loc.T("bat_unknown");
        return raw.Contains("charging") ? Loc.F("bat_charging", pct) : Loc.F("bat_pct", pct);
    }

    static string LocCaps(string raw)
    {
        var map = new Dictionary<string, string>
        {
            ["buttons"] = "cap_buttons", ["D-Pad"] = "cap_dpad", ["2 sticks"] = "cap_sticks", ["analog triggers"] = "cap_triggers",
            ["touchpad"] = "cap_touch", ["gyro (not used)"] = "cap_gyro",
        };
        return string.Join(Loc.Instance.IsArabic ? "، " : ", ", raw.Split(", ").Select(x => map.TryGetValue(x, out var k) ? Loc.T(k) : x));
    }

    void RefreshPhysicalDetails(bool connected)
    {
        var d = connected ? Host.Runner?.Input.ActiveDetails : null;
        PhysName = d?.Name ?? "–";
        PhysFamily = d == null ? "–" : Loc.Family(d.Family);
        PhysConnection = d == null ? "–" : LocConnection(d.Connection);
        PhysBattery = d == null ? "–" : LocBattery(d.Battery);
        PhysApi = d == null ? "–" : Loc.T("api_sdl");
        PhysCaps = d == null ? "–" : LocCaps(d.Capabilities);
    }

    // ================================================================ live state

    public GamepadState Physical { get; private set; }
    public GamepadState Virtual { get; private set; }

    string _physicalText = "", _virtualText = "", _steeringText = "", _ownerText = "", _statusBanner = "", _latencyText = "", _r2Text = "";
    Brush _physicalBrush = Brushes.Gray, _virtualBrush = Brushes.Gray;
    bool _remapping, _analog, _emergency, _running, _virtualOk, _virtualProblem, _r2Active;

    public string PhysicalText { get => _physicalText; private set => Set(ref _physicalText, value); }
    public string VirtualText { get => _virtualText; private set => Set(ref _virtualText, value); }
    public Brush PhysicalBrush { get => _physicalBrush; private set => Set(ref _physicalBrush, value); }
    public Brush VirtualBrush { get => _virtualBrush; private set => Set(ref _virtualBrush, value); }
    public string SteeringText { get => _steeringText; private set => Set(ref _steeringText, value); }
    public string OwnerText { get => _ownerText; private set => Set(ref _ownerText, value); }
    public string StatusBanner { get => _statusBanner; private set => Set(ref _statusBanner, value); }
    public string LatencyText { get => _latencyText; private set => Set(ref _latencyText, value); }
    public string R2Text { get => _r2Text; private set => Set(ref _r2Text, value); }
    public bool R2Active { get => _r2Active; private set => Set(ref _r2Active, value); }
    bool _dirL, _dirR;
    public bool DirLeft { get => _dirL; private set => Set(ref _dirL, value); }
    public bool DirRight { get => _dirR; private set => Set(ref _dirR, value); }
    public bool RemappingEnabled { get => _remapping; private set { if (Set(ref _remapping, value)) Raise(nameof(MasterText)); } }
    public bool AnalogEnabled { get => _analog; private set => Set(ref _analog, value); }
    public bool EmergencyStopped { get => _emergency; private set => Set(ref _emergency, value); }
    public bool IsRunning { get => _running; private set { if (Set(ref _running, value)) Raise(nameof(StartStopText)); } }
    /// <summary>True when the virtual controller has a problem the user can act on (shows the retry button).</summary>
    public bool VirtualProblem { get => _virtualProblem; private set => Set(ref _virtualProblem, value); }
    public string MasterText => Loc.T(RemappingEnabled && !EmergencyStopped ? "home_master_on" : "home_master_off");
    public string StartStopText => Loc.T(IsRunning ? "dev_stop" : "dev_start");

    bool _homeViewVirtual = true;
    public bool HomeViewVirtual
    {
        get => _homeViewVirtual;
        set
        {
            if (Set(ref _homeViewVirtual, value))
            {
                Raise(nameof(HomeViewPhysical));
                Raise(nameof(HomeLiveState));
            }
        }
    }
    public bool HomeViewPhysical => !HomeViewVirtual;
    public GamepadState HomeLiveState => HomeViewVirtual ? Virtual : Physical;
    public string HomeVr2Status => (Virtual.Has(PadButton.R2) || Virtual.R2 > 0.05f) ? $"{Virtual.R2 * 100:0}% ({Loc.T("r2_pressed")})" : Loc.T("home_vr2_suppressed");
    bool _continuousActive;
    public bool ContinuousActive { get => _continuousActive; private set => Set(ref _continuousActive, value); }
    public string HomePolswidStatus => ContinuousActive ? Loc.T("v_active") : Loc.T("v_inactive");

    public string DriverText
    {
        get
        {
            var v = Host.Vigem;
            if (v.BusReachable) return v.DriverVersion != null ? Loc.F("drv_ok_ver", v.DriverVersion) : Loc.T("drv_ok");
            return Loc.T(v.DriverRegistered ? "drv_unreachable" : "drv_missing");
        }
    }
    public string DriverTechnical => Host.Vigem.Message;
    public bool DriverOk => Host.Vigem.BusReachable;

    public ObservableCollection<KeyValueRow> MonitorRows { get; } = new();
    KeyValueRow ContRow = null!, R2Row = null!, VR2Row = null!, VDownRow = null!, SquareRow = null!, SqDelayRow = null!, CrossRow = null!,
        DpadRow = null!, VDpadRow = null!, LeftStickRow = null!, RightStickRow = null!, ConvertedRow = null!,
        OwnerRow = null!, SuppressRow = null!, SubmitRow = null!, LatencyRow = null!, RatesRow = null!;

    void BuildMonitorRows()
    {
        MonitorRows.Clear();
        KeyValueRow R(string key) { var r = new KeyValueRow(Loc.T(key)); MonitorRows.Add(r); return r; }
        ContRow = R("row_cont"); R2Row = R("row_r2"); VR2Row = R("row_vr2"); VDownRow = R("row_vdown");
        SquareRow = R("row_square"); SqDelayRow = R("row_sqdelay"); CrossRow = R("row_cross");
        DpadRow = R("row_dpad"); VDpadRow = R("row_vdpad");
        LeftStickRow = R("row_lstick"); RightStickRow = R("row_rstick_pass"); ConvertedRow = R("row_conv"); OwnerRow = R("row_owner"); SuppressRow = R("row_suppress");
        SubmitRow = R("row_submit"); LatencyRow = R("row_latency"); RatesRow = R("row_rates");
    }

    public ObservableCollection<string> LogLines { get; } = new();

    // developer-mode texts
    string _devRaw = "", _devVirt = "", _devPerf = "", _devError = "";
    public string DevRaw { get => _devRaw; private set => Set(ref _devRaw, value); }
    public string DevVirtualState { get => _devVirt; private set => Set(ref _devVirt, value); }
    public string DevPerf { get => _devPerf; private set => Set(ref _devPerf, value); }
    public string DevError { get => _devError; private set => Set(ref _devError, value); }

    Brush B(string key) => (Brush)Application.Current.FindResource(key);

    ConnectionState _prevVirt = ConnectionState.Disconnected;
    bool _prevPhys, _prevEmergency, _prevRemap, _havePrev;

    void Refresh()
    {
        var runner = Host.Runner;
        IsRunning = Host.IsRunning;
        var s = runner?.Snapshot;
        Raise(nameof(PhysicalDiscoveryError));
        if (s == null)
        {
            PhysicalConnected = false;
            Raise(nameof(NeedsControllerAccessHelp));
            PhysicalText = Loc.T("st_stopped"); PhysicalBrush = B("MutedBrush"); RefreshPhysicalDetails(false);
            VirtualText = Loc.T("st_stopped"); VirtualBrush = B("MutedBrush");
            StatusBanner = Loc.T("banner_stopped");
            Physical = default; Virtual = default; RemappingEnabled = Host.Processor.RemappingEnabled; AnalogEnabled = Host.Processor.AnalogEnabled;
            EmergencyStopped = Host.Processor.EmergencyStopped; SteeringText = Loc.T("dir_neutral"); OwnerText = Loc.T("owner_none");
            VirtualProblem = Host.StartError != null;
            DevError = Host.StartError ?? Loc.T("dev_no_error");
            R2Text = Loc.T("r2_released"); R2Active = false; DirLeft = DirRight = false;
            ContinuousActive = false;
            Raise(nameof(Physical)); Raise(nameof(Virtual)); Raise(nameof(HomeLiveState));
            Raise(nameof(HomeVr2Status)); Raise(nameof(HomePolswidStatus));
            return;
        }

        PhysicalConnected = s.PhysicalConnected;
        Raise(nameof(NeedsControllerAccessHelp));
        PhysicalText = s.PhysicalConnected ? Loc.F("st_phys_ok", s.PhysicalName) : Loc.T("st_phys_none");
        PhysicalBrush = s.PhysicalConnected ? B("GoodBrush") : B("WarnBrush");
        RefreshPhysicalDetails(s.PhysicalConnected);
        SyncDeviceChoices();

        string friendlyError = !Host.Vigem.BusReachable ? Loc.T("err_driver_missing") : Loc.T("err_virtual_generic") + (OutputType == VirtualPadType.DualShock4 ? " " + Loc.T("err_ds4_hint") : "");
        switch (s.VirtualState)
        {
            case ConnectionState.Connected: VirtualText = Loc.F("st_virt_ok", OutputName); VirtualBrush = B("GoodBrush"); break;
            case ConnectionState.DryRun: VirtualText = Loc.T("st_virt_dry"); VirtualBrush = B("WarnBrush"); break;
            case ConnectionState.Error: VirtualText = Loc.T("st_virt_err"); VirtualBrush = B("BadBrush"); break;
            default: VirtualText = Loc.T("st_virt_off"); VirtualBrush = B("BadBrush"); break;
        }
        _virtualOk = s.Forwarding;
        VirtualProblem = s.VirtualState is ConnectionState.Error or ConnectionState.Disconnected;

        StatusBanner = s.EmergencyStopped ? Loc.T("banner_emergency")
            : !s.PhysicalConnected ? Loc.T("banner_wait_phys")
            : VirtualProblem ? friendlyError
            : s.VirtualState == ConnectionState.DryRun ? Loc.T("banner_dry")
            : !s.RemappingEnabled ? Loc.T("banner_off")
            : Loc.T("banner_on");

        RemappingEnabled = s.RemappingEnabled;
        AnalogEnabled = s.AnalogEnabled;
        EmergencyStopped = s.EmergencyStopped;
        SteeringText = Loc.Direction(s.Steering);
        DirLeft = s.Steering is SteeringDirection.Left or SteeringDirection.Both; DirRight = s.Steering is SteeringDirection.Right or SteeringDirection.Both;
        OwnerText = s.Owner switch { DirectionOwner.Continuous => Loc.T("owner_continuous"), DirectionOwner.Analog => Loc.T("owner_analog"), DirectionOwner.DPad => Loc.T("owner_dpad"), _ => Loc.T("owner_none") };
        R2Active = s.R2MappedActive;
        R2Text = Loc.T(s.R2MappedActive ? "r2_pressed" : "r2_released");
        ContinuousActive = s.ContinuousActive;
        Physical = s.Physical; Virtual = s.Virtual;
        Raise(nameof(Physical)); Raise(nameof(Virtual)); Raise(nameof(HomeLiveState));
        Raise(nameof(HomeVr2Status)); Raise(nameof(HomePolswidStatus));

        var p = s.Physical; var v = s.Virtual;
        string Pr(bool b) => Loc.T(b ? "v_pressed" : "v_released");
        ContRow.Value = Loc.T(s.ContinuousActive ? "v_active" : "v_inactive"); ContRow.BrushKey = s.ContinuousActive ? "NeonBrush" : "MutedBrush";
        R2Row.Value = $"{p.R2 * 100:0}%";
        VR2Row.Value = $"{v.R2 * 100:0}% ({Pr(v.Has(PadButton.R2))})"; VR2Row.BrushKey = v.Has(PadButton.R2) || v.R2 > 0.1f ? "WarnBrush" : "MutedBrush";
        VDownRow.Value = Pr(v.Has(PadButton.DpadDown)); VDownRow.BrushKey = v.Has(PadButton.DpadDown) ? "NeonBrush" : "MutedBrush";
        SquareRow.Value = Pr(v.Has(PadButton.Square)); SquareRow.BrushKey = v.Has(PadButton.Square) ? "NeonBrush" : "MutedBrush";
        SqDelayRow.Value = Loc.T(s.ContinuousActive ? (s.ContinuousSquareActive ? "sq_active" : "sq_pending") : "sq_inactive");
        SqDelayRow.BrushKey = s.ContinuousSquareActive ? "NeonBrush" : s.ContinuousActive ? "WarnBrush" : "MutedBrush";
        CrossRow.Value = Pr(v.Has(PadButton.Cross)); CrossRow.BrushKey = v.Has(PadButton.Cross) ? "NeonBrush" : "MutedBrush";
        DpadRow.Value = DescribeDpad(p.Buttons);
        VDpadRow.Value = DescribeDpad(v.Buttons); VDpadRow.BrushKey = (v.Buttons & PadButtons.DpadAll) != 0 ? "NeonBrush" : "MutedBrush";
        LeftStickRow.Value = $"{p.LX:+0.00;-0.00;0.00} / {p.LY:+0.00;-0.00;0.00}   {Loc.T("v_to_virtual")} {v.LX:+0.00;-0.00;0.00} / {v.LY:+0.00;-0.00;0.00}";
        RightStickRow.Value = $"{p.RX:+0.00;-0.00;0.00} / {p.RY:+0.00;-0.00;0.00}   {Loc.T("v_to_virtual")} {v.RX:+0.00;-0.00;0.00} / {v.RY:+0.00;-0.00;0.00}";
        ConvertedRow.Value = s.AnalogEnabled ? $"{Loc.Direction(s.AnalogSteering)} ({s.AnalogShaped:+0.00;-0.00;0.00})" : Loc.T("v_analog_off");
        OwnerRow.Value = OwnerText;
        SuppressRow.Value = Loc.T(s.LeftStickXSuppressed ? "v_yes" : "v_no");
        SubmitRow.Value = s.VirtualState switch { ConnectionState.Connected => Loc.F("v_send_ok", s.Submissions), ConnectionState.DryRun => Loc.T("v_send_dry"), _ => Loc.T("v_send_bad") };
        SubmitRow.BrushKey = s.VirtualState == ConnectionState.Connected ? "GoodBrush" : s.VirtualState == ConnectionState.DryRun ? "WarnBrush" : "BadBrush";
        LatencyRow.Value = $"{s.LatencyAvgUs:0} µs  (p99 {s.LatencyP99Us}, max {s.LatencyMaxUs})";
        RatesRow.Value = $"{s.PollRateHz:0} Hz / {s.SubmitRateHz:0} Hz";
        LatencyText = $"{s.LatencyAvgUs:0} µs";

        // wizard step 3: has the user pressed a button / moved a stick?
        if (WizardVisible && WizardStep == 3)
        {
            if ((p.Buttons & ~PadButton.None) != 0 || p.L2 > 0.3f || p.R2 > 0.3f) WizButtonSeen = true;
            if (Math.Abs(p.LX) > 0.4f || Math.Abs(p.LY) > 0.4f || Math.Abs(p.RX) > 0.4f || Math.Abs(p.RY) > 0.4f) WizStickSeen = true;
        }
        if (WizardVisible) { Raise(nameof(WizFoundText)); Raise(nameof(WizSummaryPhys)); }

        // developer-mode readouts
        if (DeveloperMode && Page == AppPage.Dev)
        {
            DevRaw = $"Buttons: {p.Buttons}\nLX {p.LX:+0.000;-0.000;0.000}  LY {p.LY:+0.000;-0.000;0.000}  RX {p.RX:+0.000;-0.000;0.000}  RY {p.RY:+0.000;-0.000;0.000}\nL2 {p.L2:0.000}  R2 {p.R2:0.000}";
            DevVirtualState = $"{s.PadType} via {s.Backend}  [{s.VirtualState}]\nButtons: {v.Buttons}\nLX {v.LX:+0.000;-0.000;0.000}  LY {v.LY:+0.000;-0.000;0.000}  RX {v.RX:+0.000;-0.000;0.000}  RY {v.RY:+0.000;-0.000;0.000}\nL2 {v.L2:0.000}  R2 {v.R2:0.000}";
            DevPerf = $"Polling {s.PollRateHz:0} Hz (target {Profile.Timing.PollingHz}) | submit {s.SubmitRateHz:0} Hz | latency avg {s.LatencyAvgUs:0} us, p99 {s.LatencyP99Us} us, max {s.LatencyMaxUs} us | reports {s.Submissions} | helper {s.Helper}";
            DevError = s.LastError ?? Host.StartError ?? Loc.T("dev_no_error");
        }

        // toasts on state changes (never on the very first sample)
        if (_havePrev)
        {
            if (s.PhysicalConnected && !_prevPhys) Notify(Loc.F("n_phys_ok", s.PhysicalName));
            else if (!s.PhysicalConnected && _prevPhys) Notify(Loc.T("n_phys_lost"));
            if (s.VirtualState == ConnectionState.Connected && _prevVirt != ConnectionState.Connected) Notify(Loc.T("n_virt_ok"));
            else if (s.VirtualState == ConnectionState.Error && _prevVirt != ConnectionState.Error) Notify(Loc.T("n_virt_err"));
            if (s.EmergencyStopped && !_prevEmergency) Notify(Loc.T("n_emergency"));
            else if (!s.EmergencyStopped && s.RemappingEnabled != _prevRemap && !_prevEmergency) Notify(Loc.T(s.RemappingEnabled ? "n_on" : "n_off"));
        }
        _prevPhys = s.PhysicalConnected; _prevVirt = s.VirtualState; _prevEmergency = s.EmergencyStopped; _prevRemap = s.RemappingEnabled; _havePrev = true;

        foreach (var e in runner!.Events.ReadSince(_lastEventSeq))
        {
            _lastEventSeq = e.Sequence;
            if (_baseUs == 0) _baseUs = e.TimestampUs;
            LogLines.Add($"{(e.TimestampUs - _baseUs) / 1e6:00000.000000}s  {e.Kind,-9} {e.Text}");
            if (LogLines.Count > 1500) LogLines.RemoveAt(0);
        }
    }

    static string DescribeDpad(PadButton b)
    {
        var parts = new List<string>();
        if ((b & PadButton.DpadUp) != 0) parts.Add(Loc.ButtonName(PadButton.DpadUp));
        if ((b & PadButton.DpadDown) != 0) parts.Add(Loc.ButtonName(PadButton.DpadDown));
        if ((b & PadButton.DpadLeft) != 0) parts.Add(Loc.ButtonName(PadButton.DpadLeft));
        if ((b & PadButton.DpadRight) != 0) parts.Add(Loc.ButtonName(PadButton.DpadRight));
        return parts.Count == 0 ? Loc.T("v_neutral") : string.Join(" + ", parts);
    }

    // ================================================================ commands

    public RelayCommand ToggleRemappingCommand { get; }
    public RelayCommand ToggleAnalogCommand { get; }
    public RelayCommand EmergencyStopCommand { get; }
    public RelayCommand ClearEmergencyCommand { get; }
    public RelayCommand StartStopCommand { get; }
    public RelayCommand RestartPipelineCommand { get; }
    public RelayCommand RetryCommand { get; }
    public RelayCommand RefreshControllersCommand { get; }
    public RelayCommand CopyReaderPathCommand { get; }
    public RelayCommand CheckHidHideCommand { get; }
    public RelayCommand AllowHidHideCommand { get; }
    public RelayCommand TriggerUTurnCommand { get; }
    public RelayCommand TriggerAirCommand { get; }
    public RelayCommand CancelHelpersCommand { get; }
    public RelayCommand NewProfileCommand { get; }
    public RelayCommand DuplicateProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand ResetProfileCommand { get; }
    public RelayCommand ImportProfileCommand { get; }
    public RelayCommand ExportProfileCommand { get; }
    public RelayCommand RestoreDefaultsCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand OpenDriverPageCommand { get; }
    public RelayCommand ToggleLanguageCommand { get; }
    public RelayCommand GoCommand { get; }
    public RelayCommand WizNextCommand { get; }
    public RelayCommand WizBackCommand { get; }
    public RelayCommand WizSkipCommand { get; }
    public RelayCommand WizFinishCommand { get; }
    public RelayCommand RunWizardCommand { get; }
    public RelayCommand PickOutputCommand { get; }
    public RelayCommand SetHomeViewVirtualCommand { get; }
    public RelayCommand SetHomeViewPhysicalCommand { get; }

    public bool DryRun
    {
        get => Host.Settings.DryRun;
        set
        {
            if (Host.Settings.DryRun == value) return;
            if (!Confirm(Loc.T("confirm_dryrun"))) { Raise(); return; }
            Host.Settings.DryRun = value; Host.SaveSettings(); Host.Restart(); Raise();
        }
    }
    public bool GlobalHotkeys
    {
        get => Host.Settings.GlobalHotkeys;
        set { if (Host.Settings.GlobalHotkeys == value) return; Host.Settings.GlobalHotkeys = value; Host.SaveSettings(); Raise(); GlobalHotkeysChanged?.Invoke(); }
    }
    public event Action? GlobalHotkeysChanged;

    public IReadOnlyList<PhysicalDeviceInfo> Devices => Host.Runner?.Input.Devices ?? Array.Empty<PhysicalDeviceInfo>();
    public string DeviceListText
    {
        get
        {
            var d = Devices;
            if (d.Count == 0) return "No controllers detected.";
            return string.Join(Environment.NewLine, d.Select(x =>
                $"{(x.IsSelected ? "> " : "  ")}{x.Name}  [{x.VendorId:X4}:{x.ProductId:X4}, {x.Family}, key {x.Key}]" +
                (x.IsLikelyVirtual ? "  - virtual device, ignored" : "") + (x.Path != null ? $"\n    {x.Path}" : "")));
        }
    }
    public string ReaderExecutablePath => Environment.ProcessPath ?? "";
    public string HidHideSetupInstructions => Loc.T("hidhide_setup_steps");
    public string HidHideStatusText
    {
        get
        {
            if (_hidHideBusy) return Loc.T("hidhide_checking");
            if (_hidHideStatus is not { } s) return Loc.T("hidhide_unverified");
            if (!s.Verified) return Loc.T("hidhide_read_error") + "\n" + s.Error;
            if (!s.Installed) return Loc.T("hidhide_not_installed");
            string access = Loc.T(s.ApplicationAllowed == true ? "hidhide_access_allowed" : "hidhide_access_denied");
            return access + "\n" + Loc.F("hidhide_state", Loc.T(s.Active == true ? "v_yes" : "v_no"),
                Loc.T(s.Inverted == true ? "v_yes" : "v_no"), s.HiddenEdgeEntries);
        }
    }

    async Task CheckHidHideAsync()
    {
        if (_hidHideBusy) return;
        _hidHideBusy = true; Raise(nameof(HidHideStatusText));
        try { _hidHideStatus = await Task.Run(() => _hidHide.Read(ReaderExecutablePath)); }
        finally { _hidHideBusy = false; Raise(nameof(HidHideStatusText)); CommandManager.InvalidateRequerySuggested(); }
    }

    async Task AllowHidHideAsync()
    {
        if (_hidHideBusy) return;
        string path = ReaderExecutablePath;
        if (!Confirm(Loc.F("hidhide_confirm_allow", path))) return;
        _hidHideBusy = true; Raise(nameof(HidHideStatusText));
        try
        {
            _hidHideStatus = await Task.Run(() => _hidHide.AllowThisExecutable(path, userApproved: true));
            RefreshDevices();
            if (_hidHideStatus.Verified && _hidHideStatus.ApplicationAllowed == true)
                Notify(Loc.T("hidhide_restart_after_allow"));
            else ShowError(HidHideStatusText);
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { _hidHideBusy = false; Raise(nameof(HidHideStatusText)); CommandManager.InvalidateRequerySuggested(); }
    }
    public bool NeedsControllerAccessHelp => Host.IsRunning && !PhysicalConnected;
    public string PhysicalDiscoveryError => Host.StartError
        ?? (Host.Runner?.Input as IControllerDiscoveryControl)?.DiscoveryError ?? "";

    public void RefreshDevices()
    {
        if (Host.IsRunning && Host.Runner?.Input is IControllerDiscoveryControl discovery)
            discovery.RequestRefresh(); // SDL work is performed by the pipeline thread on its next poll.
        Raise(nameof(DeviceListText));
    }

    public void ApplyTimingPreset(string preset)
    {
        TimingPresets.Apply(Profile, preset);
        CommitProfile();
        Raise(nameof(Profile));
    }

    public void Dispose() { _timer.Stop(); _toastTimer.Stop(); }
}
