// Plik: ViewModelBase.cs
// Rola: Bazowa klasa dla wszystkich ViewModeli w architekturze MVVM.
// Wzorzec: MVVM (Model-View-ViewModel) — klasa należy do warstwy ViewModel.

using System.ComponentModel;           // INotifyPropertyChanged — interfejs powiadamiania widoku o zmianach
using System.Runtime.CompilerServices; // CallerMemberName — automatyczne wyciąganie nazwy właściwości

namespace FixCars4Us.Core.Infrastructure;

/// <summary>
/// Bazowa klasa ViewModel z implementacją INotifyPropertyChanged (MVVM).
/// </summary>
/// <remarks>
/// Dlaczego abstrakcyjna? Nie istnieje "ogólny" ViewModel — każdy moduł tworzy
/// własną klasę pochodną (np. CustomersViewModel), dziedzicząc mechanizm
/// powiadamiania widoku o zmianach właściwości. Dzięki temu kod powiadamiający
/// jest napisany tylko raz i nie powtarza się w każdym ViewModelu (zasada DRY).
///
/// Wzorzec Observer leży u podstaw INotifyPropertyChanged: ViewModel (Subject)
/// wywołuje zdarzenie PropertyChanged, a WPF (Observer) odświeża powiązany
/// element UI (Binding). Całość bez referencji na View — to jest istota MVVM.
/// </remarks>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    // Zdarzenie wymagane przez INotifyPropertyChanged.
    // Subskrybentem jest silnik bindingów WPF — nie programista.
    // "?" oznacza, że zdarzenie może nie mieć subskrybentów (brak UI = brak wywołania).
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Wywołuje zdarzenie PropertyChanged dla podanej nazwy właściwości.
    /// </summary>
    /// <remarks>
    /// [CallerMemberName] sprawia, że kompilator automatycznie wstawia nazwę
    /// właściwości, z której metoda jest wywoływana — eliminuje ryzyko literówki
    /// w nazwie i usuwa potrzebę podawania stringa ręcznie (np. "Status").
    /// </remarks>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); // Wywołaj zdarzenie jeśli są subskrybenci

    /// <summary>
    /// Pomocnik ustawiający pole zapasowe właściwości i powiadamiający UI tylko wtedy,
    /// gdy wartość faktycznie się zmieniła. Zwraca true jeśli nastąpiła zmiana.
    /// </summary>
    /// <remarks>
    /// Dzięki tej metodzie właściwości ViewModelu wyglądają tak:
    ///   public string Foo { get => _foo; set => SetField(ref _foo, value); }
    /// zamiast ręcznego porównania + wywołania OnPropertyChanged.
    /// Generyczność (T) pozwala stosować metodę dla dowolnego typu danych.
    /// </remarks>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        // Jeśli wartość się nie zmieniła — nie powiadamiaj UI (optymalizacja wydajności).
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;           // Zaktualizuj pole zapasowe (backing field)
        OnPropertyChanged(name); // Powiadom WPF, żeby odświeżył binding
        return true;             // Poinformuj wywołującego, że zmiana miała miejsce
    }
}
