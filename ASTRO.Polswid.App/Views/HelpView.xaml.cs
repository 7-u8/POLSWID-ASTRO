using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ASTRO.Polswid.App.Localization;

namespace ASTRO.Polswid.App.Views;

/// <summary>Searchable help center: common questions plus the five-part explanation of every setting.</summary>
public partial class HelpView : UserControl
{
    readonly List<FaqItem> _faq = HelpContent.Faq().ToList();
    readonly List<HelpTopic> _topics = HelpContent.AllTopics().ToList();

    public HelpView()
    {
        InitializeComponent();
        Render("");
    }

    void Search_TextChanged(object sender, TextChangedEventArgs e) { Placeholder.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Render(Search.Text.Trim()); }
    void Clear_Click(object sender, RoutedEventArgs e) => Search.Clear();

    static Expander Card(string header, UIElement body)
    {
        var res = Application.Current.Resources;
        var title = new TextBlock { Text = header, FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)res["TextBrush"] };
        return new Expander
        {
            Header = title,
            Content = body,
            Foreground = (Brush)res["TextBrush"],
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12, 8, 12, 12),
            Background = (Brush)res["SurfaceBrush"],
            BorderBrush = (Brush)res["BorderBrush"],
            BorderThickness = new Thickness(1),
        };
    }

    static TextBlock Body(string text, bool muted = false) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 6),
        Foreground = (Brush)Application.Current.Resources[muted ? "MutedBrush" : "TextBrush"],
    };

    static TextBlock Head(string text) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0),
        Foreground = (Brush)Application.Current.Resources["NeonBrush"],
    };

    void Render(string query)
    {
        FaqPanel.Children.Clear();
        TopicPanel.Children.Clear();
        bool Match(string text) => query.Length == 0 || text.Contains(query, StringComparison.CurrentCultureIgnoreCase);

        foreach (var f in _faq.Where(f => Match(f.SearchText)))
            FaqPanel.Children.Add(Card(f.Question, Body(f.Answer)));

        foreach (var t in _topics.Where(t => Match(t.SearchText)))
        {
            var sp = new StackPanel();
            sp.Children.Add(Head(Loc.T("hp_what"))); sp.Children.Add(Body(t.What));
            sp.Children.Add(Head(Loc.T("hp_effect"))); sp.Children.Add(Body(t.Effect));
            sp.Children.Add(Head(Loc.T("hp_need"))); sp.Children.Add(Body(t.Need));
            sp.Children.Add(Head(Loc.T("hp_example"))); sp.Children.Add(Body(t.Example));
            sp.Children.Add(Head(Loc.T("hp_risk"))); sp.Children.Add(Body(t.Risk));
            TopicPanel.Children.Add(Card(t.Title, sp));
        }

        FaqHeader.Visibility = FaqPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TopicHeader.Visibility = TopicPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoResults.Visibility = FaqPanel.Children.Count + TopicPanel.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
