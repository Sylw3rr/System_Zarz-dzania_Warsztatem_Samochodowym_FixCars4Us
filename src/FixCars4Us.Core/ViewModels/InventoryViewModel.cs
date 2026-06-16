// Plik: InventoryViewModel.cs
// Rola: ViewModel dla zakładki "Magazyn" — ewidencja stanów magazynowych,
//       ręczne korekty (przychód/rozchód) i alert niskich stanów.
// Wzorzec: MVVM. Pośrednio Observer — Part.IsLowStock powiadamia UI przez
//          INotifyPropertyChanged (implementowane w klasie Part).

using System.Collections.ObjectModel; // ObservableCollection
using FixCars4Us.Core.Data;           // WorkshopContext
using FixCars4Us.Core.Enums;          // StockMovementType
using FixCars4Us.Core.Infrastructure; // ViewModelBase, RelayCommand
using FixCars4Us.Core.Models;         // Part

namespace FixCars4Us.Core.ViewModels;

/// <summary>Wiersz dziennika ruchów magazynowych (na potrzeby prezentacji w UI).</summary>
/// <remarks>
/// Prosta klasa danych bez INotifyPropertyChanged — wpisy dziennika są immutowalne
/// (raz wpisane nie zmieniają się). Lista Movements to ObservableCollection, więc
/// dodanie nowego wpisu automatycznie odświeża UI.
/// </remarks>
public class StockMovement
{
    public DateTime Time { get; set; } = DateTime.Now; // Czas operacji magazynowej
    public string Part { get; set; } = "";              // Nazwa części (string, nie referencja — snapshot)
    public StockMovementType Type { get; set; }         // Przychód lub Rozchód
    public int Quantity { get; set; }                   // Ilość sztuk w ruchu
    public int StockAfter { get; set; }                 // Stan magazynowy PO operacji (dla weryfikacji)
}

/// <summary>
/// Funkcja podstawowa: Zarządzanie magazynem.
/// Rejestruje przychody i rozchody części oraz pokazuje pozycje o niskim stanie.
/// (Automatyczny rozchód przy dodaniu części do zlecenia realizuje WorkshopFacade.)
/// </summary>
/// <remarks>
/// Automatyczny rozchód (WorkshopFacade.AddPartToOrder) NIE tworzy wpisu StockMovement —
/// ten dziennik dotyczy tylko ręcznych operacji. To celowe uproszczenie: ślad automatycznych
/// rozchodów jest w dzienniku audytowym zlecenia (RepairLogEntry).
///
/// LowStockParts jest właściwością obliczaną — nie posiada backing field.
/// Jest odświeżana ręcznie przez OnPropertyChanged(nameof(LowStockParts)) po każdej
/// zmianie stanu — bo WPF nie wie że LowStockParts zależy od StockQuantity.
/// </remarks>
public class InventoryViewModel : ViewModelBase
{
    // Współdzielony kontekst bazy — ten sam co w wszystkich ViewModelach.
    private readonly WorkshopContext _db;

    // Lista wszystkich części (do wyświetlenia i wyboru do operacji magazynowej).
    public ObservableCollection<Part> Parts { get; } = new();

    // Dziennik ręcznych ruchów magazynowych — Insert(0, ...) = najnowsze na górze.
    public ObservableCollection<StockMovement> Movements { get; } = new();

    // Pole zapasowe dla wybranej części.
    private Part? _selectedPart;

    /// <summary>
    /// Aktualnie wybrana część w liście magazynowej. Zmiana wyzwala aktualizację
    /// CanExecute dla StockIn/StockOutCommand (nie można operować bez wyboru).
    /// </summary>
    public Part? SelectedPart
    {
        get => _selectedPart;
        set
        {
            if (SetField(ref _selectedPart, value)) // Ustaw i powiadom tylko jeśli zmiana
            {
                // Po zmianie wyboru odśwież dostępność przycisków Przychód/Rozchód.
                StockInCommand.RaiseCanExecuteChanged();
                StockOutCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Ilość sztuk dla bieżącej operacji magazynowej (powiązana z polem numerycznym w UI).
    /// Domyślnie 1 — najczęstsza operacja to ruch o jedną sztukę.
    /// </summary>
    public int MovementQuantity { get; set; } = 1;

    /// <summary>Lista części z niskim stanem — alert dla managera (Observer/UI).</summary>
    /// <remarks>
    /// LINQ Where filtruje w pamięci (Parts to ObservableCollection, nie IQueryable).
    /// Właściwość obliczana — odświeżana przez OnPropertyChanged(nameof(LowStockParts))
    /// wywoływane w Load() i Move(). Binding w UI automatycznie przelicza listę.
    /// </remarks>
    public IEnumerable<Part> LowStockParts => Parts.Where(p => p.IsLowStock);

    // Komendy dla przycisków "Przychód" i "Rozchód".
    public RelayCommand StockInCommand { get; }   // Przychód towaru od dostawcy
    public RelayCommand StockOutCommand { get; }  // Rozchód (korekta ręczna)

    /// <summary>
    /// Konstruktor: tworzy komendy z warunkami CanExecute, wczytuje stan magazynu.
    /// </summary>
    public InventoryViewModel(WorkshopContext db)
    {
        _db = db;

        // Komendy z lambdą akcji i lambdą warunku.
        // "_ =>" — parametr object? ignorowany (nie używamy CommandParameter).
        // CanExecute: aktywne tylko gdy wybrano część.
        StockInCommand  = new RelayCommand(_ => Move(StockMovementType.Przychod), _ => SelectedPart != null);
        StockOutCommand = new RelayCommand(_ => Move(StockMovementType.Rozchod),  _ => SelectedPart != null);

        Load(); // Załaduj listę części z bazy
    }

    /// <summary>
    /// Wczytuje stan magazynu z bazy posortowany alfabetycznie.
    /// Wywołuje OnPropertyChanged(nameof(LowStockParts)) aby odświeżyć alert niskich stanów.
    /// </summary>
    public void Load()
    {
        Parts.Clear(); // Wyczyść — załadujesz świeże dane
        foreach (var p in _db.Parts.OrderBy(p => p.Name)) Parts.Add(p);
        OnPropertyChanged(nameof(LowStockParts)); // Powiadom UI że lista alertów mogła się zmienić
    }

    /// <summary>Odświeża alert niskich stanów (np. po zmianie zakładki gdy WorkshopFacade pobrał części).</summary>
    /// <remarks>
    /// Part.StockQuantity zmieniony przez WorkshopFacade aktualizuje Part.IsLowStock (przez INotifyPropertyChanged
    /// w Part), ale LowStockParts (właściwość obliczana w VM) wymaga osobnego odświeżenia.
    /// </remarks>
    public void Refresh() => OnPropertyChanged(nameof(LowStockParts));

    /// <summary>
    /// Wykonuje ruch magazynowy (Przychód lub Rozchód) dla wybranej części.
    /// Waliduje dane wejściowe, aktualizuje stan i zapisuje do bazy i dziennika.
    /// </summary>
    private void Move(StockMovementType type)
    {
        // Guard: wymagana wybrana część i dodatnia ilość.
        if (SelectedPart is null || MovementQuantity <= 0) return;

        // Dodatkowy guard dla Rozchodu: nie można wydać więcej niż jest na stanie.
        if (type == StockMovementType.Rozchod && SelectedPart.StockQuantity < MovementQuantity) return;

        // Aktualizuj stan magazynowy.
        // Operator trójkowy: Przychód = dodaj (+), Rozchód = odejmij (-).
        // StockQuantity.setter w Part powiadomi UI przez INotifyPropertyChanged.
        SelectedPart.StockQuantity += type == StockMovementType.Przychod ? MovementQuantity : -MovementQuantity;

        _db.SaveChanges(); // Utrwal zmianę stanu w bazie

        // Dodaj wpis do dziennika ruchów (Insert na pozycji 0 = najnowsze na górze).
        Movements.Insert(0, new StockMovement
        {
            Part = SelectedPart.Name,     // Snapshot nazwy (string — nie referencja)
            Type = type,                  // Przychód lub Rozchód
            Quantity = MovementQuantity,  // Ile sztuk
            StockAfter = SelectedPart.StockQuantity // Stan PO operacji (do weryfikacji bilansu)
        });

        // Odśwież listę alertów — stan mógł przekroczyć próg MinStock w dół (Rozchód).
        OnPropertyChanged(nameof(LowStockParts));
    }
}
