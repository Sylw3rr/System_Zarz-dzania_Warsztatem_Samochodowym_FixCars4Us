// Wzorzec: Command — enkapsulacja akcji jako obiektu do powiązania z UI (XAML Command Binding).

using System.Windows.Input;

namespace FixCars4Us.Core.Infrastructure;

/// <summary>Prosta implementacja ICommand dla MVVM.</summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Wygodny konstruktor dla komend bez parametru.</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    /// <summary>Wywołuj gdy zmienia się stan wpływający na dostępność komendy.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Pomocnik wywołujący RaiseCanExecuteChanged na wielu komendach jednocześnie.</summary>
public static class RelayCommandExtensions
{
    public static void RaiseAll(this IEnumerable<RelayCommand> commands)
    {
        foreach (var c in commands) c.RaiseCanExecuteChanged();
    }
}
