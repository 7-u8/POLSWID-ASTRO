using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ASTRO.Polswid.App.Localization;
using ASTRO.Polswid.App.ViewModels;
using ASTRO.Polswid.App.Views;
using ASTRO.Polswid.Core.MacroEngine;

namespace ASTRO.Polswid.App;

public partial class MainWindow : Window
{
    const int WM_HOTKEY = 0x0312;
    const uint MOD_CONTROL = 0x2, MOD_ALT = 0x1, MOD_NOREPEAT = 0x4000;
    const int HkEmergency = 0xA01, HkRemap = 0xA02, HkAnalog = 0xA03;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    readonly MainViewModel _vm;
    HwndSource? _source;

    public MainWindow(AppHost host)
    {
        InitializeComponent();
        _vm = new MainViewModel(host);
        DataContext = _vm;
        BuildUi();
        _vm.LanguageChanged += BuildUi;
        _vm.GlobalHotkeysChanged += RegisterHotkeys;
        SourceInitialized += (_, _) =>
        {
            _source = (HwndSource)PresentationSource.FromVisual(this);
            _source.AddHook(WndProc);
            RegisterHotkeys();
        };
        Closed += (_, _) =>
        {
            UnregisterAll();
            _vm.Dispose();
        };
    }

    /// <summary>The whole shell is rebuilt when the language changes so every text, tooltip and layout direction is consistent.</summary>
    void BuildUi()
    {
        FlowDirection = Loc.Instance.Flow;
        Title = "ASTRO POLSWID";
        LangBtn.Content = Loc.T("lang_toggle");
        LangBtn.ToolTip = Loc.T("tip_lang");
        ShellHost.Content = new ShellView { DataContext = _vm };
    }

    void Lang_Click(object sender, RoutedEventArgs e) => _vm.ToggleLanguageCommand.Execute(null);

    void RegisterHotkeys()
    {
        UnregisterAll();
        if (_source == null || !_vm.GlobalHotkeys) return;
        var h = _source.Handle;
        // Ctrl+Alt+F10 emergency stop, Ctrl+Alt+F9 remapping on/off, Ctrl+Alt+F8 analog steering on/off
        bool ok = RegisterHotKey(h, HkEmergency, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x79)
                & RegisterHotKey(h, HkRemap, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x78)
                & RegisterHotKey(h, HkAnalog, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x77);
        if (!ok)
            _vm.Host.Log.Warning("One or more global hotkeys could not be registered (already in use by another program)");
    }

    void UnregisterAll()
    {
        if (_source == null) return;
        UnregisterHotKey(_source.Handle, HkEmergency);
        UnregisterHotKey(_source.Handle, HkRemap);
        UnregisterHotKey(_source.Handle, HkAnalog);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case HkEmergency: _vm.Host.Post(MacroCommandKind.EmergencyStop); handled = true; break;
                case HkRemap: _vm.Host.Post(MacroCommandKind.ToggleRemapping); handled = true; break;
                case HkAnalog: _vm.Host.Post(MacroCommandKind.ToggleAnalog); handled = true; break;
            }
        }
        return IntPtr.Zero;
    }

    void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) Max_Click(sender, e);
        else if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Max_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>Verification helper: with ASTRO_AUTOSHOT=&lt;dir&gt; the app renders every page in both languages to PNG files and exits.</summary>
public partial class MainWindow
{
    public async Task RunAutoShots(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Task.Delay(2500);

        async Task Shot(string name)
        {
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            await Task.Delay(350);
            UpdateLayout();
            var root = (System.Windows.Media.Visual)Content;
            int w = (int)ActualWidth, h = (int)ActualHeight;
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var back = new System.Windows.Media.DrawingVisual();
            using (var dc = back.RenderOpen()) dc.DrawRectangle((System.Windows.Media.Brush)FindResource("BgBrush"), null, new System.Windows.Rect(0, 0, w, h));
            bmp.Render(back);
            bmp.Render(this);
            var sv = FindVisual<SimpleView>(this);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "layout.txt"), $"{name}: SimpleView {(sv == null ? "none" : $"{sv.ActualWidth:0}x{sv.ActualHeight:0} vis={sv.Visibility} children={System.Windows.Media.VisualTreeHelper.GetChildrenCount(sv)} extent={FindVisual<System.Windows.Controls.ScrollViewer>(sv)?.ExtentHeight:0} desc={CountDesc(sv)} opacity={sv.Opacity} clip={sv.ClipToBounds}")}" + Environment.NewLine);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, name + ".png"));
            enc.Save(fs);
        }

        foreach (var lang in new[] { Lang.Ar, Lang.En })
        {
            _vm.Language = lang;
            string p = lang == Lang.Ar ? "ar" : "en";
            _vm.WizardVisible = true;
            for (int s = 1; s <= 5; s++) { _vm.WizardStep = s; await Shot($"{p}_wizard{s}"); }
            _vm.WizardVisible = false;
            _vm.Page = AppPage.Home; await Shot($"{p}_home");
            {
                var hb = FindVisual<ASTRO.Polswid.App.Controls.HelpButton>(FindVisual<SimpleView>(this)!);
                if (hb != null && hb.Template.FindName("tb", hb) is System.Windows.Controls.Primitives.ToggleButton tb && hb.Template.FindName("pop", hb) is System.Windows.Controls.Primitives.Popup pop)
                {
                    hb.Topic = "analog"; tb.IsChecked = true;
                    await Task.Delay(600);
                    if (pop.Child is FrameworkElement fe)
                    {
                        var pb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)fe.ActualWidth, (int)fe.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        pb.Render(fe);
                        var pe = new System.Windows.Media.Imaging.PngBitmapEncoder(); pe.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(pb));
                        using var pfs = System.IO.File.Create(System.IO.Path.Combine(dir, $"{p}_helppopup.png")); pe.Save(pfs);
                    }
                    tb.IsChecked = false;
                }
            }
            _vm.Page = AppPage.Advanced;
            for (int t = 0; t < 5; t++)
            {
                FindVisual<AdvancedView>(this)?.SelectTab(t);
                await Shot($"{p}_adv{t}");
            }
            _vm.Page = AppPage.Settings; await Shot($"{p}_settings");
            _vm.Page = AppPage.Help; await Shot($"{p}_help");
            _vm.Host.Settings.DeveloperMode = true; _vm.Page = AppPage.Dev; await Shot($"{p}_dev");
            _vm.Host.Settings.DeveloperMode = false; _vm.Page = AppPage.Home;
        }
        Close();
    }

    static int CountDesc(System.Windows.DependencyObject o) { int n = 0; for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(o); i++) n += 1 + CountDesc(System.Windows.Media.VisualTreeHelper.GetChild(o, i)); return n; }

    static T? FindVisual<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        if (root is T t) return t;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var r = FindVisual<T>(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
            if (r != null) return r;
        }
        return null;
    }
}
