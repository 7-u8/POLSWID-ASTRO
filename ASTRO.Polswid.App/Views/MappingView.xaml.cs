using System.Windows.Controls;
using ASTRO.Polswid.App.ViewModels;
using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.App.Views;

public partial class MappingView : UserControl
{
    public MappingView() => InitializeComponent();

    void Diagram_ButtonClicked(PadButton b)
    {
        Diagram.Selected = b;
        if (DataContext is MainViewModel vm) vm.SelectRow(b);
    }
}
