using System.Drawing;
using System.Windows.Forms;

namespace ASTRO.Polswid.Setup;

/// <summary>One window: tick what you want, press Install. Arabic (RTL) by default, English with one click.</summary>
sealed class SetupForm : Form
{
    bool _ar = true;
    readonly Label _title = new() { AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(139, 92, 246) };
    readonly Label _intro = new() { AutoSize = false, Height = 80 };
    readonly CheckBox _vigem = new() { AutoSize = true, Checked = true }, _hid = new() { AutoSize = true, Checked = true },
                      _app = new() { AutoSize = true, Checked = true }, _desktop = new() { AutoSize = true, Checked = true };
    readonly Label _vigemSt = new() { AutoSize = true, ForeColor = Color.Gray }, _hidSt = new() { AutoSize = true, ForeColor = Color.Gray };
    readonly ProgressBar _bar = new() { Dock = DockStyle.Top, Height = 14 };
    readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
    readonly Button _install = new() { AutoSize = true, Padding = new Padding(18, 6, 18, 6) },
                    _lang = new() { AutoSize = true }, _close = new() { AutoSize = true };
    bool _busy;

    string T(string ar, string en) => _ar ? ar : en;

    public SetupForm()
    {
        Width = 640; Height = 600; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16, 12, 16, 4), WrapContents = false };
        _intro.Width = 575;
        top.Controls.AddRange(new Control[] { _title, _intro, Row(_vigem, _vigemSt), Row(_hid, _hidSt), _app, _desktop });

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 6, 12, 6) };
        buttons.Controls.AddRange(new Control[] { _install, _lang, _close });

        Controls.Add(_log); Controls.Add(_bar); Controls.Add(buttons); Controls.Add(top);

        _install.Click += async (_, _) => await RunAsync();
        _lang.Click += (_, _) => { _ar = !_ar; Apply(); };
        _close.Click += (_, _) => Close();
        Apply();
    }

    static FlowLayoutPanel Row(Control a, Control b)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        p.Controls.Add(a); p.Controls.Add(b);
        return p;
    }

    void Apply()
    {
        RightToLeft = _ar ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = _ar;
        Text = "ASTRO POLSWID – Setup";
        _title.Text = T("تثبيت ASTRO POLSWID", "Install ASTRO POLSWID");
        _intro.Text = T("اختر ما تريد تثبيته ثم اضغط «تثبيت». يتم تحميل ViGEmBus وHidHide من صفحات الإصدارات الرسمية للمطوّر nefarius على GitHub، ويتحقق البرنامج من توقيعها الرقمي قبل تشغيلها. تظهر نافذة كل مُثبّت أمامك.",
                        "Pick what to install, then press Install. ViGEmBus and HidHide are downloaded from the official nefarius GitHub releases; their digital signature is verified before they run. Each installer's own window is shown to you.");
        _vigem.Text = T("ViGEmBus – ينشئ وحدة التحكم الافتراضية (مطلوب)", "ViGEmBus – creates the virtual controller (required)");
        _hid.Text = T("HidHide – يخفي يدك الحقيقية عن اللعبة (موصى به)", "HidHide – hides your real controller from the game (recommended)");
        _app.Text = T("برنامج ASTRO POLSWID", "ASTRO POLSWID application");
        _desktop.Text = T("إنشاء اختصار على سطح المكتب", "Create a desktop shortcut");
        _install.Text = T("تثبيت", "Install");
        _lang.Text = T("English", "العربية");
        _close.Text = T("إغلاق", "Close");
        Refresh2();
    }

    void Refresh2()
    {
        _vigemSt.Text = Installer.IsInstalled(Installer.ViGEmBus) ? T("(مثبّت بالفعل)", "(already installed)") : "";
        _hidSt.Text = Installer.IsInstalled(Installer.HidHide) ? T("(مثبّت بالفعل)", "(already installed)") : "";
        if (Installer.IsInstalled(Installer.ViGEmBus)) _vigem.Checked = false;
        if (Installer.IsInstalled(Installer.HidHide)) _hid.Checked = false;
    }

    void Log(string s) => BeginInvoke(() => _log.AppendText(s + Environment.NewLine));

    async Task RunAsync()
    {
        if (_busy) return;
        _busy = true; _install.Enabled = false;
        var temp = Path.Combine(Path.GetTempPath(), "ASTRO-SETUP");
        Directory.CreateDirectory(temp);
        var progress = new Progress<int>(v => _bar.Value = Math.Clamp(v, 0, 100));
        bool reboot = false;
        try
        {
            foreach (var (d, box) in new[] { (Installer.ViGEmBus, _vigem), (Installer.HidHide, _hid) })
            {
                if (!box.Checked) continue;
                Log(T($"[{d.Name}] جارٍ البحث عن أحدث إصدار رسمي…", $"[{d.Name}] finding the latest official release…"));
                var url = await Installer.ResolveUrlAsync(d, Log);
                var path = Path.Combine(temp, Path.GetFileName(new Uri(url).LocalPath));
                Log(T($"[{d.Name}] تحميل من {url}", $"[{d.Name}] downloading {url}"));
                await Installer.DownloadAsync(url, path, progress);
                var signer = Installer.VerifySignature(path, d.SignerContains);
                Log(T($"[{d.Name}] التوقيع صحيح: {signer}", $"[{d.Name}] signature OK: {signer}"));
                Log(T($"[{d.Name}] تشغيل المُثبّت… أكمل خطواته في نافذته.", $"[{d.Name}] running the installer… finish its steps in its window."));
                int code = await Task.Run(() => Installer.RunInstaller(path));
                bool ok = Installer.IsInstalled(d);
                Log(ok ? T($"[{d.Name}] تم التثبيت.", $"[{d.Name}] installed.") : T($"[{d.Name}] لم يكتمل التثبيت (رمز {code}).", $"[{d.Name}] was not installed (exit code {code})."));
                if (ok && d == Installer.HidHide) reboot = true;
            }

            if (_app.Checked)
            {
                var exe = await Installer.InstallAppAsync(temp, _desktop.Checked, progress, Log);
                Log(T($"تم تثبيت البرنامج في: {exe}", $"Application installed to: {exe}"));
            }

            _bar.Value = 100;
            Log(reboot ? T("انتهى. أعد تشغيل الحاسوب ليعمل HidHide، ثم افتح ASTRO POLSWID.", "Done. Restart the PC so HidHide takes effect, then open ASTRO POLSWID.")
                       : T("انتهى. يمكنك فتح ASTRO POLSWID من قائمة ابدأ.", "Done. You can open ASTRO POLSWID from the Start menu."));
        }
        catch (Exception ex)
        {
            Log(T("خطأ: ", "Error: ") + ex.Message);
            MessageBox.Show(ex.Message, "ASTRO POLSWID Setup", MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, _ar ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0);
        }
        finally { _busy = false; _install.Enabled = true; Refresh2(); }
    }
}
