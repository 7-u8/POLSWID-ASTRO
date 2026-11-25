using System.Windows.Controls;
using ASTRO.Polswid.App.Controls;

namespace ASTRO.Polswid.App.Views;

public partial class SteeringView : UserControl
{
    public SteeringView()
    {
        InitializeComponent();
        EditHooks.Attach(this);
    }
}
