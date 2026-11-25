using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ASTRO.Polswid.App.Controls;
using ASTRO.Polswid.App.ViewModels;

namespace ASTRO.Polswid.App.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView()
    {
        InitializeComponent();
        EditHooks.Attach(this, () => { if (DataContext is MainViewModel vm) vm.Profile.Timing.Preset = "Custom"; });
    }

    MainViewModel? Vm => DataContext as MainViewModel;

    void Preset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm != null && Preset.SelectedItem is string name && (Preset.IsDropDownOpen || Preset.IsKeyboardFocusWithin))
            Vm.ApplyTimingPreset(name);
    }

    void NameBox_LostFocus(object sender, RoutedEventArgs e) { if (Vm != null) Vm.ProfileName = NameBox.Text; }

    void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { if (Vm != null) Vm.ProfileName = NameBox.Text; Keyboard.ClearFocus(); }
    }
}
