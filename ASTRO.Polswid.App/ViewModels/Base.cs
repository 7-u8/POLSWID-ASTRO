using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ASTRO.Polswid.App.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    readonly Action<object?> _run;
    readonly Func<object?, bool>? _can;
    public RelayCommand(Action run, Func<bool>? can = null) : this(_ => run(), can == null ? null : _ => can()) { }
    public RelayCommand(Action<object?> run, Func<object?, bool>? can = null) { _run = run; _can = can; }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _run(parameter);
}

public sealed class KeyValueRow : ObservableObject
{
    string _value = "";
    string _brushKey = "TextBrush";
    public KeyValueRow(string label) => Label = label;
    public string Label { get; }
    public string Value { get => _value; set => Set(ref _value, value); }
    /// <summary>Resource key of the brush used for the value (TextBrush, GoodBrush, WarnBrush, BadBrush).</summary>
    public string BrushKey { get => _brushKey; set => Set(ref _brushKey, value); }
}
