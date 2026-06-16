// Plik: CatalogViewModel.cs
// Rola: ViewModel dla zakładki "Katalog" — zarządzanie bazą części zamiennych
//       (kody, ceny zakupu/sprzedaży, stan magazynowy) oraz cennikiem usług
//       (typy robocizny ze stawkami godzinowymi).
// Wzorzec: MVVM — ViewModel dla modułu Katalogu. Bez dodatkowych wzorców GoF
//          (moduł katalogowy jest relatywnie prosty — CRUD bez złożonej logiki).

using System.Collections.ObjectModel; // ObservableCollection
using FixCars4Us.Core.Data;           // WorkshopContext
using FixCars4Us.Core.Enums;          // WorkCategory
using FixCars4Us.Core.Infrastructure; // ViewModelBase, RelayCommand
using FixCars4Us.Core.Models;         // Part, ServiceType

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Funkcja podstawowa: Katalog części i usług.
/// Zarządza bazą części zamiennych (ceny zakupu/sprzedaży) oraz cennikiem
/// roboczogodzin dla różnych typów prac.
/// </summary>
/// <remarks>
/// Katalog jest źródłem danych dla:
/// - InventoryViewModel: stan magazynowy (StockQuantity, MinStock)
/// - RepairOrdersViewModel: wybór części do kosztorysu (SalePrice)
/// - PricingService: stawki godzinowe (HourlyRate) dla ServiceType
///
/// Dwie niezależne listy (Parts i ServiceTypes) zarządzane jednocześnie —
/// formularz dla każdej jest oddzielny, ale oba w tej samej zakładce UI.
/// </remarks>
public class CatalogViewModel : ViewModelBase
{
    // Współdzielony kontekst bazy — ten sam co w wszystkich ViewModelach.
    private readonly WorkshopContext _db;

    // Listy obserwowalne — UI (DataGrid/ListBox) reaguje na dodanie nowej części/usługi.
    public ObservableCollection<Part> Parts { get; } = new();               // Katalog części
    public ObservableCollection<ServiceType> ServiceTypes { get; } = new(); // Cennik usług

    /// <summary>
    /// Lista kategorii pracy dla ComboBox "Kategoria" w formularzu nowej usługi.
    /// Enum.GetValues zwraca tablicę wszystkich wartości — WPF może bindować bezpośrednio.
    /// </summary>
    public Array WorkCategories => Enum.GetValues(typeof(WorkCategory));

    // --- Formularz nowej części ---
    // Pola bindowane do TextBox/CheckBox w XAML dla formularza dodawania części.
    public string NewPartCode { get; set; } = "";         // Kod katalogowy (np. "OL-5W30")
    public string NewPartName { get; set; } = "";         // Nazwa handlowa (np. "Olej silnikowy 5W30 (1L)")
    public decimal NewPartPurchase { get; set; }          // Cena zakupu od dostawcy (netto)
    public decimal NewPartSale { get; set; }              // Cena sprzedaży klientowi (netto)
    public int NewPartStock { get; set; }                 // Początkowy stan magazynowy
    public int NewPartMinStock { get; set; } = 2;         // Minimalny stan (alert gdy spada do tej wartości)
    public bool NewPartIsOriginal { get; set; } = true;   // Oryginał (true) lub zamiennik (false)

    // --- Formularz nowej usługi ---
    // Pola dla formularza dodawania nowego typu usługi z cennikiem.
    public string NewServiceName { get; set; } = "";              // Nazwa usługi (np. "Roboczogodzina elektryka")
    public WorkCategory NewServiceCategory { get; set; }          // Kategoria (Mechanika/Elektryka/...)
    public decimal NewServiceRate { get; set; }                   // Stawka za godzinę w złotych

    // Komendy przypisane do przycisków w XAML.
    public RelayCommand AddPartCommand { get; }     // Przycisk "Dodaj część"
    public RelayCommand AddServiceCommand { get; }  // Przycisk "Dodaj usługę"

    /// <summary>
    /// Konstruktor: inicjalizuje komendy, wczytuje katalog z bazy.
    /// </summary>
    public CatalogViewModel(WorkshopContext db)
    {
        _db = db;
        AddPartCommand = new RelayCommand(AddPart);       // Zawsze aktywna (brak CanExecute)
        AddServiceCommand = new RelayCommand(AddService); // Zawsze aktywna
        Load(); // Wczytaj katalog przy starcie
    }

    /// <summary>
    /// Wczytuje katalog części i cennik usług z bazy, posortowane alfabetycznie.
    /// Wywoływana przy inicjalizacji i przez Refresh() po przełączeniu zakładki.
    /// </summary>
    public void Load()
    {
        Parts.Clear(); // Wyczyść — załadujesz świeże dane z bazy
        // OrderBy transluje na SQL ORDER BY Name — sortowanie po stronie bazy (wydajniejsze).
        foreach (var p in _db.Parts.OrderBy(p => p.Name)) Parts.Add(p);

        ServiceTypes.Clear(); // Wyczyść cennik
        foreach (var s in _db.ServiceTypes.OrderBy(s => s.Name)) ServiceTypes.Add(s);
    }

    /// <summary>
    /// Tworzy nową część na podstawie danych formularza i zapisuje do bazy oraz katalogu.
    /// Guard: nazwa jest wymagana (kod może być pusty dla części niestandardowych).
    /// </summary>
    private void AddPart(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewPartName)) return; // Walidacja: wymagana nazwa

        // Utwórz encję Part z danych formularza.
        var p = new Part
        {
            Code = NewPartCode,           // Kod katalogowy (np. "FIL-OIL")
            Name = NewPartName,           // Nazwa handlowa
            PurchasePrice = NewPartPurchase, // Cena zakupu od dostawcy
            SalePrice = NewPartSale,      // Cena sprzedaży klientowi (PurchasePrice < SalePrice = marża)
            StockQuantity = NewPartStock, // Stan początkowy — ile mamy na stanie
            MinStock = NewPartMinStock,   // Próg alertu niskiego stanu
            IsOriginal = NewPartIsOriginal // Oryginał czy zamiennik
        };

        _db.Parts.Add(p); // Dodaj do kontekstu EF Core
        _db.SaveChanges(); // Zapisz do bazy (EF nada Id)
        Parts.Add(p);      // Dodaj do ObservableCollection — UI odświeży DataGrid
    }

    /// <summary>
    /// Tworzy nowy typ usługi (pozycję cennika) i zapisuje do bazy.
    /// Guard: nazwa wymagana.
    /// </summary>
    private void AddService(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewServiceName)) return; // Walidacja: wymagana nazwa

        // Utwórz encję ServiceType z danych formularza.
        var s = new ServiceType
        {
            Name = NewServiceName,         // Nazwa usługi (np. "Diagnostyka komputerowa")
            Category = NewServiceCategory, // Kategoria pracy (wpływa na grupowanie w raportach)
            HourlyRate = NewServiceRate    // Stawka za godzinę (wejście dla ILaborCostStrategy)
        };

        _db.ServiceTypes.Add(s); // Dodaj do kontekstu
        _db.SaveChanges();       // Zapisz do bazy
        ServiceTypes.Add(s);     // Odśwież UI
    }
}
