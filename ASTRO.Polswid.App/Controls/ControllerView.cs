using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.App.Controls;

/// <summary>DualShock 4 diagram drawn straight from a <see cref="GamepadState"/>. Optionally clickable (mapping page).</summary>
public sealed class ControllerView : FrameworkElement
{
    const double W = 420, H = 290;

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(GamepadState), typeof(ControllerView),
        new FrameworkPropertyMetadata(default(GamepadState), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.Register(nameof(Interactive), typeof(bool), typeof(ControllerView),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected), typeof(PadButton), typeof(ControllerView),
        new FrameworkPropertyMetadata(PadButton.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(ControllerView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public GamepadState State { get => (GamepadState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool Interactive { get => (bool)GetValue(InteractiveProperty); set => SetValue(InteractiveProperty, value); }
    public PadButton Selected { get => (PadButton)GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public event Action<PadButton>? ButtonClicked;

    // hit regions in diagram space
    static readonly (PadButton b, Rect r)[] Regions =
    {
        (PadButton.L2, new Rect(52, 8, 90, 30)), (PadButton.R2, new Rect(278, 8, 90, 30)),
        (PadButton.L1, new Rect(46, 42, 106, 18)), (PadButton.R1, new Rect(268, 42, 106, 18)),
        (PadButton.DpadUp, new Rect(98, 88, 24, 26)), (PadButton.DpadDown, new Rect(98, 130, 24, 26)),
        (PadButton.DpadLeft, new Rect(72, 112, 26, 24)), (PadButton.DpadRight, new Rect(122, 112, 26, 24)),
        (PadButton.Triangle, new Rect(297, 88, 26, 26)), (PadButton.Circle, new Rect(329, 112, 26, 26)),
        (PadButton.Cross, new Rect(297, 138, 26, 26)), (PadButton.Square, new Rect(265, 112, 26, 26)),
        (PadButton.Share, new Rect(162, 72, 18, 12)), (PadButton.Options, new Rect(240, 72, 18, 12)),
        (PadButton.Touchpad, new Rect(172, 88, 76, 42)), (PadButton.Guide, new Rect(200, 168, 20, 20)),
        (PadButton.L3, new Rect(118, 172, 64, 64)), (PadButton.R3, new Rect(238, 172, 64, 64)),
    };

    static Brush Res(string key, Brush fallback) => Application.Current?.TryFindResource(key) as Brush ?? fallback;

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? W : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? H : availableSize.Height;
        double s = Math.Min(w / W, h / (H + 24));
        return new Size(W * s, (H + 24) * s);
    }

    (double scale, double ox, double oy) Layout()
    {
        double s = Math.Min(ActualWidth / W, ActualHeight / (H + 24));
        if (s <= 0) s = 1;
        return (s, (ActualWidth - W * s) / 2, 0);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var (s, ox, oy) = Layout();
        dc.PushTransform(new TranslateTransform(ox, oy));
        dc.PushTransform(new ScaleTransform(s, s));
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var idle = Res("Surface2Brush", Brushes.DimGray);
        var active = Res("AccentBrush", Brushes.MediumPurple);
        var neon = Res("NeonBrush", Brushes.Violet);
        var line = Res("BorderBrush", Brushes.Gray);
        var text = Res("TextBrush", Brushes.White);
        var muted = Res("MutedBrush", Brushes.Gray);
        var body = Res("SurfaceBrush", Brushes.Black);
        var penLine = new Pen(line, 1.5);
        var penNeon = new Pen(neon, 2);
        var st = State;

        FormattedText Ft(string t, double size, Brush b, FontWeight? w = null) =>
            new(t, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI Symbol, Segoe UI"), FontStyles.Normal, w ?? FontWeights.Normal, FontStretches.Normal), size, b, ppd);

        if (!string.IsNullOrEmpty(Title))
        {
            var t = Ft(Title.ToUpperInvariant(), 11, muted, FontWeights.SemiBold);
            dc.DrawText(t, new Point(W / 2 - t.Width / 2, H + 4));
        }

        // body
        var bodyGeo = new StreamGeometry();
        using (var g = bodyGeo.Open())
        {
            g.BeginFigure(new Point(60, 62), true, true);
            g.BezierTo(new Point(120, 56), new Point(300, 56), new Point(360, 62), true, true);
            g.BezierTo(new Point(410, 70), new Point(420, 150), new Point(396, 232), true, true);
            g.BezierTo(new Point(388, 270), new Point(340, 276), new Point(316, 240), true, true);
            g.BezierTo(new Point(290, 205), new Point(130, 205), new Point(104, 240), true, true);
            g.BezierTo(new Point(80, 276), new Point(32, 270), new Point(24, 232), true, true);
            g.BezierTo(new Point(0, 150), new Point(10, 70), new Point(60, 62), true, true);
        }
        dc.DrawGeometry(body, penLine, bodyGeo);

        bool Down(PadButton b) => (st.Buttons & b) != 0;
        bool Sel(PadButton b) => Selected == b;
        Brush Fill(PadButton b) => Down(b) ? active : idle;
        Pen Edge(PadButton b) => Sel(b) ? new Pen(Res("WarnBrush", Brushes.Yellow), 2.5) : Down(b) ? penNeon : penLine;

        // triggers (analog fill)
        void Trigger(PadButton b, Rect r, float val, string label)
        {
            dc.DrawRoundedRectangle(idle, Edge(b), r, 6, 6);
            if (val > 0.001f)
            {
                double fh = r.Height * val;
                dc.PushClip(new RectangleGeometry(new Rect(r.X, r.Bottom - fh, r.Width, fh)));
                dc.DrawRoundedRectangle(active, null, r, 6, 6);
                dc.Pop();
            }
            var t = Ft($"{label} {val * 100:0}%", 10, text);
            dc.DrawText(t, new Point(r.X + r.Width / 2 - t.Width / 2, r.Y + r.Height / 2 - t.Height / 2));
        }
        Trigger(PadButton.L2, Regions[0].r, st.L2, "L2");
        Trigger(PadButton.R2, Regions[1].r, st.R2, "R2");

        // shoulders
        foreach (var (b, r) in Regions.Where(x => x.b is PadButton.L1 or PadButton.R1))
        {
            dc.DrawRoundedRectangle(Fill(b), Edge(b), r, 5, 5);
            var t = Ft(b == PadButton.L1 ? "L1" : "R1", 10, text);
            dc.DrawText(t, new Point(r.X + r.Width / 2 - t.Width / 2, r.Y + 2));
        }

        // dpad
        foreach (var (b, r) in Regions.Where(x => x.b is PadButton.DpadUp or PadButton.DpadDown or PadButton.DpadLeft or PadButton.DpadRight))
        {
            dc.DrawRoundedRectangle(Fill(b), Edge(b), r, 4, 4);
            string a = b switch { PadButton.DpadUp => "▲", PadButton.DpadDown => "▼", PadButton.DpadLeft => "◀", _ => "▶" };
            var t = Ft(a, 10, Down(b) ? Brushes.White : muted);
            dc.DrawText(t, new Point(r.X + r.Width / 2 - t.Width / 2, r.Y + r.Height / 2 - t.Height / 2));
        }

        // face buttons
        foreach (var (b, r, sym) in new[]
        {
            (PadButton.Triangle, Regions[8].r, "△"), (PadButton.Circle, Regions[9].r, "◯"),
            (PadButton.Cross, Regions[10].r, "✕"), (PadButton.Square, Regions[11].r, "□"),
        })
        {
            var c = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            dc.DrawEllipse(Fill(b), Edge(b), c, 13, 13);
            var t = Ft(sym, 13, Down(b) ? Brushes.White : muted);
            dc.DrawText(t, new Point(c.X - t.Width / 2, c.Y - t.Height / 2));
        }

        // share / options / touchpad / PS
        foreach (var (b, r) in Regions.Where(x => x.b is PadButton.Share or PadButton.Options))
            dc.DrawRoundedRectangle(Fill(b), Edge(b), r, 3, 3);
        dc.DrawRoundedRectangle(Fill(PadButton.Touchpad), Edge(PadButton.Touchpad), Regions[14].r, 6, 6);
        var tp = Ft("TOUCHPAD", 8, muted);
        dc.DrawText(tp, new Point(210 - tp.Width / 2, 104));
        dc.DrawEllipse(Fill(PadButton.Guide), Edge(PadButton.Guide), new Point(210, 178), 10, 10);

        // sticks
        void Stick(PadButton b, Point c, float x, float y)
        {
            dc.DrawEllipse(Down(b) ? active : body, Edge(b), c, 30, 30);
            dc.DrawEllipse(null, new Pen(line, 1), c, 20, 20);
            var p = new Point(c.X + x * 18, c.Y - y * 18);
            dc.DrawEllipse(Down(b) ? Brushes.White : neon, null, p, 9, 9);
        }
        Stick(PadButton.L3, new Point(150, 204), st.LX, st.LY);
        Stick(PadButton.R3, new Point(270, 204), st.RX, st.RY);

        dc.Pop();
        dc.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!Interactive) return;
        var (s, ox, oy) = Layout();
        var p = e.GetPosition(this);
        var d = new Point((p.X - ox) / s, (p.Y - oy) / s);
        foreach (var (b, r) in Regions)
        {
            var hit = b is PadButton.L3 or PadButton.R3 ? new Rect(r.X, r.Y, r.Width, r.Height) : r;
            hit.Inflate(3, 3);
            if (hit.Contains(d)) { ButtonClicked?.Invoke(b); return; }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = Interactive ? Cursors.Hand : Cursors.Arrow;
    }
}
