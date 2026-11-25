using System.Windows.Controls;
using ASTRO.Polswid.App.Controls;

namespace ASTRO.Polswid.App.Views;

public partial class HelpersView : UserControl
{
    public HelpersView()
    {
        InitializeComponent();
        EditHooks.Attach(this);
    }
}
