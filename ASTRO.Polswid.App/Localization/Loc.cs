using System.Windows;
using System.Windows.Markup;
using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.App.Localization;

public enum Lang { Ar, En }

/// <summary>Current UI language. The whole shell is rebuilt when it changes, so strings can be resolved statically.</summary>
public sealed class Loc
{
    public static Loc Instance { get; } = new();

    public Lang Language { get; private set; } = Lang.Ar;
    public bool IsArabic => Language == Lang.Ar;
    public FlowDirection Flow => IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public event Action? LanguageChanged;

    public void SetLanguage(Lang l)
    {
        if (l == Language) return;
        Language = l;
        LanguageChanged?.Invoke();
    }

    public static string T(string key) => Strings.Get(key, Instance.Language);
    public static string F(string key, params object?[] args) => string.Format(T(key), args);

    public static string ButtonName(PadButton b)
    {
        string k = "btn_" + b;
        var s = T(k);
        return s == k ? PadButtons.Display(b) : s;
    }

    public static string EnumName(object value)
    {
        string k = $"enum_{value.GetType().Name}_{value}";
        var s = T(k);
        return s == k ? value.ToString() ?? "" : s;
    }

    public static string Preset(string name) => T("preset_" + name.Replace(' ', '_').Replace("-", "_"));

    /// <summary>Controller family labels. Brand names stay in English on purpose.</summary>
    public static string Family(string f) => Instance.IsArabic ? "يد " + f : f;

    public static string Direction(SteeringDirection d) => d switch
    {
        SteeringDirection.Left => T("dir_left"),
        SteeringDirection.Right => T("dir_right"),
        SteeringDirection.Both => T("dir_both"),
        _ => T("dir_neutral"),
    };
}

/// <summary>XAML: Text="{loc:Tr home_title}".</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension(string key) => Key = key;
    public string Key { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
