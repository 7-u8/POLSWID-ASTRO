using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ASTRO.Polswid.App.Localization;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Profiles;
using Xunit;

namespace ASTRO.Polswid.Tests;

public class LocalizationTests
{
    static readonly Regex ArabicLetter = new(@"[؀-ۿ]");
    static string AppDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "ASTRO.Polswid.App"));

    /// <summary>Keys whose text is intentionally identical in both languages (brand / proper names / numbers only).</summary>
    static readonly HashSet<string> SameInBoth = new()
    {
        "lang_toggle", "lbl_none_dash", "dlg_title", "out_x360", "bat_pct", "set_lang_ar", "set_lang_en", "enum_VirtualPadType_Xbox360", "out_ds4", "enum_VirtualPadType_DualShock4",
    };

    [Fact]
    public void Every_string_exists_in_both_languages_and_Arabic_is_really_Arabic()
    {
        foreach (var (key, ar, en) in Strings.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(ar), $"missing Arabic for {key}");
            Assert.False(string.IsNullOrWhiteSpace(en), $"missing English for {key}");
            if (ar == en) Assert.True(SameInBoth.Contains(key), $"'{key}' is identical in both languages");
            else if (!SameInBoth.Contains(key))
                Assert.True(ArabicLetter.IsMatch(ar), $"Arabic text of '{key}' has no Arabic letters: {ar}");
        }
    }

    [Fact]
    public void Format_placeholders_match_between_languages()
    {
        foreach (var (key, ar, en) in Strings.All)
        {
            var a = Regex.Matches(ar, @"\{(\d+)\}").Select(m => m.Value).OrderBy(x => x).ToArray();
            var e = Regex.Matches(en, @"\{(\d+)\}").Select(m => m.Value).OrderBy(x => x).ToArray();
            Assert.True(a.SequenceEqual(e), $"placeholders differ for {key}");
        }
    }

    [Fact]
    public void Every_Tr_key_and_help_topic_used_in_XAML_exists()
    {
        var keys = new HashSet<string>(Strings.All.Select(x => x.key));
        var topics = new HashSet<string>(HelpContent.TopicIds);
        var files = Directory.GetFiles(AppDir(), "*.xaml", SearchOption.AllDirectories).Where(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\"));
        int checkedKeys = 0;
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @"\{l:Tr (\w+)\}")) { checkedKeys++; Assert.True(keys.Contains(m.Groups[1].Value), $"{Path.GetFileName(f)}: missing string '{m.Groups[1].Value}'"); }
            foreach (Match m in Regex.Matches(text, @"(?:Topic|Help)=""(\w+)"""))
                Assert.True(topics.Contains(m.Groups[1].Value), $"{Path.GetFileName(f)}: missing help topic '{m.Groups[1].Value}'");
        }
        Assert.True(checkedKeys > 100);
    }

    [Fact]
    public void Every_help_topic_has_the_five_explanations_in_both_languages()
    {
        foreach (var (id, ar, en) in HelpContent.RawTopics)
        {
            Assert.Equal(6, ar.Length); Assert.Equal(6, en.Length);
            for (int i = 0; i < 6; i++)
            {
                Assert.False(string.IsNullOrWhiteSpace(ar[i]), $"{id} Arabic field {i} empty");
                Assert.False(string.IsNullOrWhiteSpace(en[i]), $"{id} English field {i} empty");
                Assert.True(ArabicLetter.IsMatch(ar[i]), $"{id} Arabic field {i} has no Arabic");
            }
        }
        Assert.True(HelpContent.TopicIds.Count() >= 25);
    }

    [Fact]
    public void Faq_covers_the_required_questions_in_both_languages()
    {
        var ar = HelpContent.RawFaq(true); var en = HelpContent.RawFaq(false);
        Assert.Equal(ar.Length, en.Length);
        Assert.True(ar.Length >= 12);
        string all = string.Join("\n", ar.Select(x => x.q));
        foreach (var needle in new[] { "Physical Controller", "Virtual DS4", "R2", "Analog", "يتعرف على يد التحكم", "لا تظهر وحدة التحكم الافتراضية", "أستعيد الإعدادات الافتراضية" })
            Assert.Contains(needle, all);
    }

    [Fact]
    public void Every_button_and_enum_value_has_a_localized_name()
    {
        foreach (var b in PadButtons.All.Append(PadButton.None))
        {
            Assert.True(Strings.Has("btn_" + b), $"no name for {b}");
        }
        foreach (var v in Enum.GetValues<PriorityMode>()) Assert.True(Strings.Has($"enum_PriorityMode_{v}"));
        foreach (var v in Enum.GetValues<TriggerMode>()) Assert.True(Strings.Has($"enum_TriggerMode_{v}"));
        foreach (var v in Enum.GetValues<UTurnExecution>()) Assert.True(Strings.Has($"enum_UTurnExecution_{v}"));
        foreach (var v in Enum.GetValues<ASTRO.Polswid.Core.Output.VirtualPadType>()) Assert.True(Strings.Has($"enum_VirtualPadType_{v}"));
        foreach (var n in TimingPresets.Names) Assert.True(Strings.Has("preset_" + n.Replace(' ', '_').Replace("-", "_")), n);
    }

    [Fact]
    public void Arabic_is_the_default_language_and_simple_mode_the_default_level()
    {
        Assert.Equal("ar", new ASTRO.Polswid.Infrastructure.Configuration.AppSettings().Language);
        Assert.False(new ASTRO.Polswid.Infrastructure.Configuration.AppSettings().DeveloperMode);
        Assert.False(new ASTRO.Polswid.Infrastructure.Configuration.AppSettings().WizardDone);
        Assert.Equal(ASTRO.Polswid.App.ViewModels.AppPage.Home, default(ASTRO.Polswid.App.ViewModels.AppPage));
    }

    [Fact]
    public void Switching_language_changes_the_text_and_the_layout_direction()
    {
        var loc = Loc.Instance;
        loc.SetLanguage(Lang.Ar);
        Assert.Equal(System.Windows.FlowDirection.RightToLeft, loc.Flow);
        Assert.Equal("الرئيسية", Loc.T("nav_home"));
        Assert.Contains("الأنالوق", Loc.ButtonName(PadButton.L3));
        loc.SetLanguage(Lang.En);
        Assert.Equal(System.Windows.FlowDirection.LeftToRight, loc.Flow);
        Assert.Equal("Home", Loc.T("nav_home"));
        loc.SetLanguage(Lang.Ar);
    }
}

public class ContinuousSteeringTextTests
{
    [Fact]
    public void Required_Arabic_explanation_is_shown_word_for_word()
    {
        Assert.Equal(
            "ميزة التوجيه المستمر تعمل فقط أثناء ضغطك على R2. إذا كان الأنالوق ثابتاً، يرسل البرنامج السهم الأيسر تلقائياً. عند تحريك الأنالوق، يتغير الاتجاه حسب حركتك. بمجرد ترك R2، يعود التحكم إلى وضعه الطبيعي.",
            Strings.Get("cont_explain", Lang.Ar));
        Assert.Contains("continuous", HelpContent.TopicIds);
    }
}
