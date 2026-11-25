using System.Windows;
using System.Windows.Controls;
using ASTRO.Polswid.App.Localization;
using ASTRO.Polswid.App.ViewModels;

namespace ASTRO.Polswid.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    void Ar_Click(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel vm) vm.Language = Lang.Ar; }
    void En_Click(object sender, RoutedEventArgs e) { if (DataContext is MainViewModel vm) vm.Language = Lang.En; }
}
