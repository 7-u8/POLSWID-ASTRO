using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ASTRO.Polswid.App.Localization;
using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.App.Controls;

public sealed class PadButtonNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is PadButton b ? Loc.ButtonName(b) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Enum value or timing-preset name -> localized text.</summary>
public sealed class LocEnumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            null => "",
            Enum e => Loc.EnumName(e),
            string s when parameter as string == "preset" => Loc.Preset(s),
            _ => value.ToString() ?? "",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && !b;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && !b;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class BrushKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Application.Current.TryFindResource(value as string ?? "TextBrush") ?? Brushes.White;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Small "?" button that opens a plain-language explanation (what / effect / need / example / risk).</summary>
public sealed class HelpButton : Control
{
    static HelpButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(HelpButton), new FrameworkPropertyMetadata(typeof(HelpButton)));

    public static readonly DependencyProperty TopicProperty = DependencyProperty.Register(nameof(Topic), typeof(string), typeof(HelpButton),
        new PropertyMetadata(null, (d, e) => ((HelpButton)d).Entry = e.NewValue is string id ? HelpContent.Get(id) : null));
    public static readonly DependencyProperty EntryProperty = DependencyProperty.Register(nameof(Entry), typeof(HelpTopic), typeof(HelpButton));

    public string? Topic { get => (string?)GetValue(TopicProperty); set => SetValue(TopicProperty, value); }
    public HelpTopic? Entry { get => (HelpTopic?)GetValue(EntryProperty); set => SetValue(EntryProperty, value); }
}

/// <summary>"Recommended" / "Optional" / "Advanced" tag.</summary>
public sealed class Badge : Control
{
    static Badge() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(typeof(Badge)));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(Badge),
        new PropertyMetadata("recommended", (d, e) => ((Badge)d).Apply()));
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(Badge));
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(nameof(Tint), typeof(Brush), typeof(Badge));

    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Brush Tint { get => (Brush)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public Badge() => Apply();

    void Apply()
    {
        Text = Loc.T("badge_" + Kind);
        string key = Kind switch { "recommended" => "GoodBrush", "advanced" => "WarnBrush", _ => "MutedBrush" };
        Tint = Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }
}

/// <summary>Label + help + slider + value readout + recommended value. Edits bubble up as the routed Slider.ValueChanged event.</summary>
public sealed class SliderRow : Control
{
    static SliderRow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SliderRow), new FrameworkPropertyMetadata(typeof(SliderRow)));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(SliderRow));
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(string), typeof(SliderRow));
    public static readonly DependencyProperty HelpProperty = DependencyProperty.Register(nameof(Help), typeof(string), typeof(SliderRow));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(SliderRow),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((SliderRow)d).Refresh()));
    public static readonly DependencyProperty DefaultProperty = DependencyProperty.Register(nameof(Default), typeof(double?), typeof(SliderRow),
        new PropertyMetadata(null, (d, e) => ((SliderRow)d).Refresh()));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderRow), new PropertyMetadata(0.0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderRow), new PropertyMetadata(1.0));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(SliderRow), new PropertyMetadata(0.01));
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(string), typeof(SliderRow),
        new PropertyMetadata("0.00", (d, e) => ((SliderRow)d).Refresh()));
    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(SliderRow), new PropertyMetadata(""));
    public static readonly DependencyProperty RecommendedTextProperty = DependencyProperty.Register(nameof(RecommendedText), typeof(string), typeof(SliderRow), new PropertyMetadata(""));
    public static readonly DependencyProperty CanResetProperty = DependencyProperty.Register(nameof(CanReset), typeof(bool), typeof(SliderRow), new PropertyMetadata(false));

    public string DisplayText { get => (string)GetValue(DisplayTextProperty); set => SetValue(DisplayTextProperty, value); }
    public string RecommendedText { get => (string)GetValue(RecommendedTextProperty); set => SetValue(RecommendedTextProperty, value); }
    public bool CanReset { get => (bool)GetValue(CanResetProperty); set => SetValue(CanResetProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public string Help { get => (string)GetValue(HelpProperty); set => SetValue(HelpProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double? Default { get => (double?)GetValue(DefaultProperty); set => SetValue(DefaultProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    void Refresh()
    {
        var f = Format ?? "0.00";
        DisplayText = Value.ToString(f, CultureInfo.InvariantCulture);
        RecommendedText = Default is { } d ? Loc.F("rec_value", d.ToString(f, CultureInfo.InvariantCulture)) : "";
        CanReset = Default is { } dd && Math.Abs(dd - Value) > 1e-9;
    }

    public static readonly RoutedEvent EditedEvent = EventManager.RegisterRoutedEvent("Edited", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SliderRow));
    public event RoutedEventHandler Edited { add => AddHandler(EditedEvent, value); remove => RemoveHandler(EditedEvent, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Slider") is Slider s)
            s.ValueChanged += (_, _) => { if (s.IsMouseCaptureWithin || s.IsKeyboardFocusWithin) RaiseEvent(new RoutedEventArgs(EditedEvent, this)); };
        if (GetTemplateChild("PART_Reset") is Button b)
            b.Click += (_, _) =>
            {
                if (Default is { } d) { Value = d; RaiseEvent(new RoutedEventArgs(EditedEvent, this)); }
            };
    }
}
