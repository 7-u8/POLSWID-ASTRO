using System.Windows.Controls;

namespace ASTRO.Polswid.App.Views;

public partial class AdvancedView : UserControl
{
    public AdvancedView() => InitializeComponent();

    public void SelectTab(int i)
    {
        var tabs = new[] { TabButtons, TabSteering, TabHelpers, TabProfiles, TabDiag };
        tabs[Math.Clamp(i, 0, 4)].IsChecked = true;
    }
}
