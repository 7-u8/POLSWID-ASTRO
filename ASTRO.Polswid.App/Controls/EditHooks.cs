using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ASTRO.Polswid.App.ViewModels;

namespace ASTRO.Polswid.App.Controls;

/// <summary>Any user edit (slider drag, checkbox, dropdown) is pushed to the live pipeline as a fresh profile clone.</summary>
public static class EditHooks
{
    public static void Attach(FrameworkElement root, Action? afterEdit = null)
    {
        void Commit()
        {
            (root.DataContext as MainViewModel)?.CommitProfile();
            afterEdit?.Invoke();
        }
        root.AddHandler(SliderRow.EditedEvent, new RoutedEventHandler((_, _) => Commit()));
        root.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) => { if (e.OriginalSource is CheckBox) Commit(); }));
        root.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, e) =>
        {
            if (e.OriginalSource is ComboBox c && c.Tag as string != "no-commit" && (c.IsDropDownOpen || c.IsKeyboardFocusWithin || c.IsMouseOver)) Commit();
        }));
    }
}
