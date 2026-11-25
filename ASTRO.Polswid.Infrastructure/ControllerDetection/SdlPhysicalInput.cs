using System.IO;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Timing;
using SDL3;

namespace ASTRO.Polswid.Infrastructure.ControllerDetection;

/// <summary>
/// Universal physical input through the SDL3 Gamepad API: DualShock 4, DualSense (+Edge), Xbox One/Series, XInput and every
/// HID/DirectInput pad SDL has a mapping for. Everything is translated to the normalized <see cref="GamepadState"/>
/// (SDL South/East/West/North = Cross/Circle/Square/Triangle). Nothing here knows or cares about the virtual output type.
/// All SDL calls happen on the pipeline thread.
/// </summary>
public sealed class SdlPhysicalInput : IPhysicalInput, IInputReadDiagnostics, IControllerDiscoveryControl
{
    const long EnumerateIntervalUs = 250_000;
    const long DetailsIntervalUs = 2_000_000;

    readonly ILog _log;
    readonly Func<IReadOnlyList<PhysicalDeviceInfo>> _enumerateDevices;
    readonly DeviceDiscoveryCatalog _catalog = new();
    readonly Dictionary<uint, ControllerOrigin> _origins = new();
    int _refreshRequested;
    string? _enumerationError, _openError;
    readonly object _gate = new();
    IReadOnlyList<PhysicalDeviceInfo> _devices = Array.Empty<PhysicalDeviceInfo>();
    string? _preferredKey;
    (ushort Vendor, ushort Product)? _filter;
    readonly HashSet<uint> _legitIds = new();
    readonly HashSet<uint> _virtualIds = new();
    readonly HashSet<uint> _loggedIds = new();

    nint _pad;
    uint _padId;
    volatile string? _name;
    PhysicalDeviceDetails? _details;
    long _lastEnumUs, _lastDetailsUs;
    bool _initialized;
    string? _diagnosticId, _diagnosticMapping;
    bool _hasR2Axis;

    public bool DiagnosticsEnabled { get; set; }
    public InputReadDiagnostics LastReadDiagnostics { get; private set; }

    public SdlPhysicalInput(ILog? log = null)
    {
        _enumerateDevices = ReadNativeDevices;
        _log = log ?? NullLog.Instance;
    }

    // Only the enumeration boundary is substituted in tests; production always calls SDL.
    internal SdlPhysicalInput(Func<IReadOnlyList<PhysicalDeviceInfo>> enumerateDevices, ILog? log = null)
    {
        _enumerateDevices = enumerateDevices;
        _log = log ?? NullLog.Instance;
    }

    public void RequestRefresh() => Interlocked.Exchange(ref _refreshRequested, 1);
    public string? DiscoveryError => Volatile.Read(ref _enumerationError) ?? Volatile.Read(ref _openError);

    /// <summary>Optional SDL controller mapping database for HID pads SDL does not know yet.</summary>
    public string? MappingFile { get; set; }

    public string? ActiveDeviceName => _name;
    public PhysicalDeviceDetails? ActiveDetails => Volatile.Read(ref _details);
    public IReadOnlyList<PhysicalDeviceInfo> Devices { get { lock (_gate) return _devices; } }

    public void Initialize()
    {
        // Keep receiving input while the game, not this window, has focus.
        SDL.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        if (!SDL.Init(SDL.InitFlags.Gamepad))
            throw new InvalidOperationException("SDL3 could not initialise the gamepad subsystem: " + SDL.GetError());
        _initialized = true;

        if (MappingFile != null && File.Exists(MappingFile))
        {
            int n = SDL.AddGamepadMappingsFromFile(MappingFile);
            _log.Information("Loaded {Count} SDL controller mappings from {File}", n, MappingFile);
        }
        RefreshDiscovery(Clock.NowUs, force: true);
        _log.Information("SDL3 initialised; {Count} controller(s) found", _devices.Count);
    }

    public void SelectDevice(string? key)
    {
        lock (_gate) _preferredKey = key;
    }

    public void BeginVirtualFilter(ushort vendorId, ushort productId)
    {
        lock (_gate)
        {
            _filter = (vendorId, productId);
            _legitIds.Clear();
            _virtualIds.Clear();
            // Whatever already matches BEFORE our virtual pad exists is a real controller.
            foreach (var d in _devices)
                if (d.VendorId == vendorId && d.ProductId == productId) _legitIds.Add(d.Id);
        }
    }

    public void EndVirtualFilter()
    {
        lock (_gate)
        {
            _filter = null;
            _legitIds.Clear();
            _virtualIds.Clear();
        }
    }

    public bool TryPoll(out GamepadState state)
    {
        state = default;
        if (DiagnosticsEnabled)
        {
            LastReadDiagnostics = new("SDL3 Gamepad", _diagnosticId, _diagnosticMapping,
                null, null, false, null);
            SDL.ClearError();
        }
        if (!_initialized) return false;

        SDL.UpdateGamepads();   // pumps the joystick/HIDAPI/XInput drivers and hot-plug

        long now = Clock.NowUs;
        RefreshDiscovery(now);

        if (_pad != 0 && (!SDL.GamepadConnected(_pad) || !StillListed(_padId)))
            CloseActive("Controller disconnected");

        ChooseActive();
        if (_pad == 0)
        {
            if (DiagnosticsEnabled) LastReadDiagnostics = LastReadDiagnostics with { Error = SDL.GetError() };
            return false;
        }

        if (now - _lastDetailsUs >= DetailsIntervalUs) RefreshDetails();

        var pad = _pad;
        PadButton b = PadButton.None;
        if (Btn(pad, SDL.GamepadButton.South)) b |= PadButton.Cross;
        if (Btn(pad, SDL.GamepadButton.East)) b |= PadButton.Circle;
        if (Btn(pad, SDL.GamepadButton.West)) b |= PadButton.Square;
        if (Btn(pad, SDL.GamepadButton.North)) b |= PadButton.Triangle;
        if (Btn(pad, SDL.GamepadButton.LeftShoulder)) b |= PadButton.L1;
        if (Btn(pad, SDL.GamepadButton.RightShoulder)) b |= PadButton.R1;
        if (Btn(pad, SDL.GamepadButton.Back)) b |= PadButton.Share;      // Select / Share / View / Create
        if (Btn(pad, SDL.GamepadButton.Start)) b |= PadButton.Options;   // Start / Options / Menu
        if (Btn(pad, SDL.GamepadButton.LeftStick)) b |= PadButton.L3;
        if (Btn(pad, SDL.GamepadButton.RightStick)) b |= PadButton.R3;
        if (Btn(pad, SDL.GamepadButton.Guide)) b |= PadButton.Guide;
        if (Btn(pad, SDL.GamepadButton.Touchpad)) b |= PadButton.Touchpad;
        if (Btn(pad, SDL.GamepadButton.DPadUp)) b |= PadButton.DpadUp;
        if (Btn(pad, SDL.GamepadButton.DPadDown)) b |= PadButton.DpadDown;
        if (Btn(pad, SDL.GamepadButton.DPadLeft)) b |= PadButton.DpadLeft;
        if (Btn(pad, SDL.GamepadButton.DPadRight)) b |= PadButton.DpadRight;

        state.Buttons = b;
        state.LX = Stick(SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftX));
        state.LY = -Stick(SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftY));      // SDL: down is positive -> flip so up = +1
        state.RX = Stick(SDL.GetGamepadAxis(pad, SDL.GamepadAxis.RightX));
        state.RY = -Stick(SDL.GetGamepadAxis(pad, SDL.GamepadAxis.RightY));
        state.L2 = Trigger(SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftTrigger));
        short rawR2 = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.RightTrigger);
        state.R2 = Trigger(rawR2);
        if (DiagnosticsEnabled)
            LastReadDiagnostics = new("SDL3 Gamepad", _diagnosticId, _diagnosticMapping,
                rawR2, _hasR2Axis, SDL.GamepadConnected(pad), SDL.GetError());
        return true;
    }

    static bool Btn(nint pad, SDL.GamepadButton b) => SDL.GetGamepadButton(pad, b);
    static float Stick(short v) => v < 0 ? v / 32768f : v / 32767f;
    static float Trigger(short v) => Math.Clamp(v / 32767f, 0f, 1f);

    bool StillListed(uint id)
    {
        lock (_gate)
            foreach (var d in _devices)
                if (d.Id == id) return true;
        return false;
    }

    void ChooseActive()
    {
        string? preferred;
        IReadOnlyList<PhysicalDeviceInfo> devices;
        lock (_gate) { preferred = _preferredKey; devices = _devices; }

        var pick = DeviceSelector.Choose(devices, preferred, _pad != 0 ? _padId : null);

        if (_pad != 0)
        {
            if (pick != null && pick.Id == _padId) return;
            // the user picked a different primary, or the active one is no longer allowed
            CloseActive(pick == null ? "No longer the selected primary controller" : "Switching controller");
        }
        if (pick == null)
        {
            Volatile.Write(ref _openError, null);
            return;
        }

        var handle = SDL.OpenGamepad(pick.Id);
        if (handle == 0)
        {
            string error = SDL.GetError();
            string message = $"SDL could not open {pick.Name}: {error}";
            if (message != Volatile.Read(ref _openError))
                _log.Warning("Could not open controller {Name}: {Error}", pick.Name, error);
            Volatile.Write(ref _openError, message);
            return;
        }
        Volatile.Write(ref _openError, null);
        _pad = handle;
        _padId = pick.Id;
        _name = SDL.GetGamepadName(handle) ?? pick.Name;
        if (DiagnosticsEnabled)
        {
            _diagnosticId = $"{pick.Key}/SDL-instance-{pick.Id}";
            _diagnosticMapping = SDL.GetGamepadMapping(handle);
            _hasR2Axis = SDL.GamepadHasAxis(handle, SDL.GamepadAxis.RightTrigger);
        }
        RefreshDetails();
        _log.Information("Opened controller {Name} ({Family}, id {Id})", _name, pick.Family, pick.Id);
    }

    void RefreshDetails()
    {
        _lastDetailsUs = Clock.NowUs;
        if (_pad == 0) { Volatile.Write(ref _details, null); return; }

        PhysicalDeviceInfo? info;
        lock (_gate) info = _devices.FirstOrDefault(d => d.Id == _padId);

        var power = SDL.GetGamepadPowerInfo(_pad, out int percent);
        string battery = power switch
        {
            SDL.PowerState.OnBattery => percent >= 0 ? $"{percent}%" : "on battery",
            SDL.PowerState.Charging => percent >= 0 ? $"{percent}% (charging)" : "charging",
            SDL.PowerState.Charged => "charged",
            SDL.PowerState.NoBattery => "wired (no battery)",
            _ => "not reported",
        };
        string conn = SDL.GetGamepadConnectionState(_pad).ToString();
        conn = conn switch { "Wired" => "USB / wired", "Wireless" => "Bluetooth / wireless", _ => "not reported" };

        var caps = new List<string> { "buttons", "D-Pad", "2 sticks", "analog triggers" };
        if (SDL.GetNumGamepadTouchpads(_pad) > 0) caps.Add("touchpad");
        if (SDL.GamepadHasSensor(_pad, SDL.SensorType.Gyro)) caps.Add("gyro (not used)");

        Volatile.Write(ref _details, new PhysicalDeviceDetails(
            _name ?? "Controller", info?.Family ?? "Gamepad", info?.VendorId ?? 0, info?.ProductId ?? 0,
            conn, battery, "SDL3 Gamepad API (HIDAPI / XInput / RawInput chosen by SDL)", string.Join(", ", caps)));
    }

    void CloseActive(string reason)
    {
        if (_pad != 0)
        {
            _log.Information("Closing controller {Name}: {Reason}", _name, reason);
            SDL.CloseGamepad(_pad);
        }
        _pad = 0;
        _padId = 0;
        _name = null;
        Volatile.Write(ref _details, null);
    }

    internal bool RefreshDiscovery(long nowUs, bool force = false)
    {
        bool requested = Interlocked.Exchange(ref _refreshRequested, 0) != 0;
        if (!force && !requested && nowUs - _lastEnumUs < EnumerateIntervalUs) return false;
        _lastEnumUs = nowUs;
        IReadOnlyList<PhysicalDeviceInfo> discovered;
        try { discovered = _enumerateDevices(); }
        catch (Exception ex)
        {
            Volatile.Write(ref _enumerationError, "SDL controller enumeration failed: " + ex.Message);
            _log.Error(ex, "SDL controller enumeration failed");
            return false; // Retain the last inventory; a failed enumeration is not a disconnect report.
        }
        Volatile.Write(ref _enumerationError, null);
        lock (_gate)
        {
            var classified = new List<PhysicalDeviceInfo>(discovered.Count);
            foreach (var d in discovered)
            {
                bool isVirtual = d.Origin == ControllerOrigin.ViGEm || d.IsLikelyVirtual
                    || (d.Origin == ControllerOrigin.Unknown && _virtualIds.Contains(d.Id));
                if (d.Origin == ControllerOrigin.Unknown && !isVirtual && _filter is { } f && f.Vendor == d.VendorId && f.Product == d.ProductId && !_legitIds.Contains(d.Id))
                {
                    _virtualIds.Add(d.Id);
                    isVirtual = true;
                }
                if (d.Origin == ControllerOrigin.Unknown && !isVirtual && d.VendorId == ControllerClassifier.Sony && d.Path != null && d.Path.Contains("&rev_", StringComparison.OrdinalIgnoreCase))
                    isVirtual = true;
                classified.Add(d with { IsLikelyVirtual = isVirtual, IsSelected = d.Id == _padId && _pad != 0 });
                if (_loggedIds.Add(d.Id))
                    _log.Information("Controller found {Name} {Vid}:{Pid} family {Family} virtual={Virtual}",
                        d.Name, d.VendorId.ToString("X4"), d.ProductId.ToString("X4"), d.Family, isVirtual);
            }
            _devices = _catalog.Update(classified);
        }
        return true;
    }

    IReadOnlyList<PhysicalDeviceInfo> ReadNativeDevices()
    {
        SDL.ClearError();
        var ids = SDL.GetGamepads(out int count);
        if (ids == null && !string.IsNullOrEmpty(SDL.GetError()))
            throw new InvalidOperationException(SDL.GetError());
        var list = new List<PhysicalDeviceInfo>(count);
        if (ids != null)
            for (int i = 0; i < count; i++)
                {
                    uint id = ids[i];
                    ushort vid = SDL.GetGamepadVendorForID(id), pid = SDL.GetGamepadProductForID(id);
                    var type = SDL.GetGamepadTypeForID(id).ToString();
                    var path = SDL.GetGamepadPathForID(id);
                    if (!_origins.TryGetValue(id, out var origin))
                    {
                        origin = WindowsControllerOrigin.Query(path);
                        if (origin != ControllerOrigin.Unknown) _origins[id] = origin;
                    }

                    string family = ControllerClassifier.Family(vid, pid, type);
                    list.Add(new PhysicalDeviceInfo(id, "", SDL.GetGamepadNameForID(id) ?? "Controller", vid, pid, family,
                        false, false, true, path, origin));
                }
        return list;
    }

    public void Dispose()
    {
        CloseActive("Shutting down");
        if (_initialized)
        {
            SDL.QuitSubSystem(SDL.InitFlags.Gamepad);
            _initialized = false;
        }
    }
}
