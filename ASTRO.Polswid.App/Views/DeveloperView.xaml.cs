using System.Collections.Specialized;
using System.Windows.Controls;
using ASTRO.Polswid.App.ViewModels;

namespace ASTRO.Polswid.App.Views;

public partial class DeveloperView : UserControl
{
    public DeveloperView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old) old.LogLines.CollectionChanged -= OnLogChanged;
            if (e.NewValue is MainViewModel vm) vm.LogLines.CollectionChanged += OnLogChanged;
        };
        Unloaded += (_, _) => { if (DataContext is MainViewModel vm) vm.LogLines.CollectionChanged -= OnLogChanged; };
    }

    void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && Log.Items.Count > 0 && IsVisible)
            Log.ScrollIntoView(Log.Items[^1]);
    }
}
