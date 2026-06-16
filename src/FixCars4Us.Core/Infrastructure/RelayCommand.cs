// Plik: RelayCommand.cs
// Rola: Implementacja wzorca Command dla WPF — pozwala powiązać przyciski/menu
//       z metodami ViewModelu bez pisania oddzielnych klas komend.
// Wzorzec: Command (GoF) — enkapsulacja akcji jako obiektu, który można wykonać
//          warunkowo (CanExecute) i przekazywać jako parametr.

using System.Windows.Input; // ICommand — interfejs rozpoznawany przez WPF dla Command Binding

namespace FixCars4Us.Core.Infrastructure;

/// <summary>
/// Prosta implementacja ICommand dla MVVM (przyciski w WPF wiążą się z komendami).
/// ICommand pochodzi z System.ObjectModel i jest dostępne także poza Windows.
/// </summary>
/// <remarks>
/// Dlaczego RelayCommand a nie EventHandler? W MVVM View nie powinien wywoływać
/// metod ViewModelu bezpośrednio przez zdarzenia (Code-Behind). Zamiast tego
/// przycisk deklaruje w XAML: Command="{Binding AddCustomerCommand}", a ViewModel
/// udostępnia właściwość będącą instancją RelayCommand — powiązanie bez referencji.
///
/// Wzorzec Command: Invoker = WPF Button, Command = RelayCommand,
/// Receiver = lambda przekazana do konstruktora (np. AddCustomer w ViewModelu).
/// </remarks>
public class RelayCommand : ICommand
{
    // Akcja do wykonania — może przyjmować opcjonalny parametr z XAML (CommandParameter).
    private readonly Action<object?> _execute;

    // Opcjonalna funkcja warunkowa — jeśli null, komenda jest zawsze aktywna.
    // WPF wywołuje ją automatycznie i blokuje przycisk gdy zwraca false.
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// Konstruktor przyjmujący akcję i opcjonalną funkcję warunkową (z parametrem object?).
    /// Używany gdy komenda potrzebuje parametru z XAML CommandParameter.
    /// </summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        // ?? throw — "fail fast": nieprawidłowe użycie wykrywamy w miejscu tworzenia, nie użycia.
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Wygodny konstruktor dla komend bez parametru (większość komend w MVVM).
    /// Deleguje do głównego konstruktora opakowując akcję w lambdę ignorującą parametr.
    /// </summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

    /// <summary>
    /// Sprawdza czy komenda może być wykonana. WPF wywołuje automatycznie
    /// i blokuje/odblokowuje przyciski. Jeśli brak warunku — zawsze true.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// Wykonuje właściwą akcję komendy — wywoływana przez WPF po kliknięciu.
    /// </summary>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Zdarzenie, na które WPF reaguje ponownym sprawdzeniem CanExecute.
    /// Musimy je ręcznie wywoływać przez RaiseCanExecuteChanged() gdy stan się zmienia.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Powiadamia WPF o konieczności ponownego sprawdzenia CanExecute.
    /// Wywołuj gdy zmienia się stan wpływający na dostępność komendy
    /// (np. po zmianie zaznaczenia w liście).
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Pomocnik wywołujący RaiseCanExecuteChanged na wielu komendach jednocześnie
/// (np. po zmianie zaznaczonego elementu, od którego zależy CanExecute).
/// </summary>
/// <remarks>
/// Metoda rozszerzająca (Extension Method) na IEnumerable&lt;RelayCommand&gt;.
/// Pozwala zapisać: _orderDependentCommands.RaiseAll() zamiast pętli foreach.
/// Wzorzec Iterator ukryty wewnątrz foreach — kolekcja może być dowolnego typu.
/// </remarks>
public static class RelayCommandExtensions
{
    /// <summary>
    /// Wywołuje RaiseCanExecuteChanged na każdej komendzie w kolekcji.
    /// Używane gdy jeden stan (np. SelectedOrder != null) wpływa na wiele komend.
    /// </summary>
    public static void RaiseAll(this IEnumerable<RelayCommand> commands)
    {
        foreach (var c in commands) c.RaiseCanExecuteChanged(); // Poinformuj WPF o każdej komendzie
    }
}
