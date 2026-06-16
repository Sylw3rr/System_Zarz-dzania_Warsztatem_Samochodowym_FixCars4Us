// Plik: CustomersViewModel.cs
// Rola: ViewModel dla zakładki "Klienci" — zarządzanie kartoteką klientów,
//       ich pojazdami i historią serwisową.
// Wzorzec: MVVM — ViewModel warstwy prezentacji dla modułu Klientów.
//          INotifyPropertyChanged (dziedziczone z ViewModelBase) + RelayCommand.

using System.Collections.ObjectModel; // ObservableCollection — lista widoczna w UI przez binding
using FixCars4Us.Core.Data;           // WorkshopContext
using FixCars4Us.Core.Enums;          // CustomerType
using FixCars4Us.Core.Infrastructure; // ViewModelBase, RelayCommand
using FixCars4Us.Core.Models;         // Customer, Vehicle, ServiceHistoryEntry
using Microsoft.EntityFrameworkCore;  // Include(), OrderBy() — LINQ do EF

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Funkcja podstawowa: Kartoteka klientów + Baza pojazdów i historia serwisowa.
/// Obsługuje klientów (prywatni/floty), ich pojazdy oraz historię napraw.
/// </summary>
/// <remarks>
/// ObservableCollection&lt;T&gt; — specjalna lista która powiadamia UI (przez INotifyCollectionChanged)
/// o dodaniu, usunięciu i zmianie elementów. Zwykły List&lt;T&gt; nie odświeżałby UI automatycznie.
///
/// Trzy poziomy hierarchii: Customer -> Vehicle -> ServiceHistoryEntry.
/// Zmiana zaznaczenia na każdym poziomie wyzwala załadowanie elementów niższego poziomu.
/// </remarks>
public class CustomersViewModel : ViewModelBase
{
    // Współdzielony kontekst bazy — ten sam co w innych ViewModelach (Shared DbContext).
    private readonly WorkshopContext _db;

    // Kolekcje obserwowalne — WPF ItemsControl (ListBox, DataGrid) obserwuje te kolekcje.
    // Zmiana zawartości (Add/Remove) automatycznie odświeża UI bez jawnego powiadamiania.
    public ObservableCollection<Customer> Customers { get; } = new();             // Lista klientów
    public ObservableCollection<Vehicle> Vehicles { get; } = new();               // Pojazdy wybranego klienta
    public ObservableCollection<ServiceHistoryEntry> History { get; } = new();    // Historia wybranego pojazdu

    /// <summary>
    /// Lista wartości enum CustomerType dla ComboBox "Typ klienta" w formularzu.
    /// Enum.GetValues zwraca Array — WPF może bindować do Array bez konwersji.
    /// </summary>
    public Array CustomerTypes => Enum.GetValues(typeof(CustomerType));

    // Pole zapasowe dla wybranego klienta.
    private Customer? _selectedCustomer;

    /// <summary>
    /// Aktualnie zaznaczony klient w liście. Zmiana wyzwala:
    /// 1. Odświeżenie listy pojazdów (LoadVehicles).
    /// 2. Aktualizację CanExecute dla AddVehicleCommand (nie można dodać pojazdu bez klienta).
    /// </summary>
    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetField(ref _selectedCustomer, value)) // Ustaw i powiadom UI tylko jeśli zmiana
            {
                LoadVehicles();                             // Załaduj pojazdy nowego klienta
                AddVehicleCommand.RaiseCanExecuteChanged(); // Odśwież stan przycisku "Dodaj pojazd"
            }
        }
    }

    // Pole zapasowe dla wybranego pojazdu.
    private Vehicle? _selectedVehicle;

    /// <summary>
    /// Aktualnie zaznaczony pojazd w liście. Zmiana wyzwala załadowanie historii serwisowej.
    /// </summary>
    public Vehicle? SelectedVehicle
    {
        get => _selectedVehicle;
        set
        {
            if (SetField(ref _selectedVehicle, value)) // Ustaw i powiadom UI tylko jeśli zmiana
            {
                LoadHistory(); // Załaduj historię serwisową wybranego pojazdu
            }
        }
    }

    // --- Pola formularza nowego klienta ---
    // Właściwości bindowane do TextBox/ComboBox w XAML.
    // Brak INotifyPropertyChanged — dane tylko do zapisu (z UI do VM), nie odwrotnie.
    public string NewCustomerName { get; set; } = "";               // Pole "Imię i nazwisko / Firma"
    public string NewCustomerPhone { get; set; } = "";              // Pole "Telefon"
    public string NewCustomerEmail { get; set; } = "";              // Pole "E-mail"
    public CustomerType NewCustomerType { get; set; } = CustomerType.Prywatny; // ComboBox "Typ"
    public decimal NewCustomerDiscount { get; set; }               // Pole "Rabat %" (0 dla prywatnych)

    // --- Pola formularza nowego pojazdu ---
    public string NewVehicleReg { get; set; } = "";     // Pole "Numer rejestracyjny"
    public string NewVehicleVin { get; set; } = "";     // Pole "VIN"
    public string NewVehicleBrand { get; set; } = "";   // Pole "Marka"
    public string NewVehicleModel { get; set; } = "";   // Pole "Model"
    public int NewVehicleMileage { get; set; }          // Pole "Przebieg (km)"

    // Komendy — powiązane z przyciskami w XAML przez Command Binding.
    public RelayCommand AddCustomerCommand { get; }  // Przycisk "Dodaj klienta"
    public RelayCommand AddVehicleCommand { get; }   // Przycisk "Dodaj pojazd"

    /// <summary>
    /// Konstruktor: inicjalizuje komendy i wczytuje klientów z bazy.
    /// </summary>
    public CustomersViewModel(WorkshopContext db)
    {
        _db = db;

        // Komenda dodawania klienta — zawsze aktywna (brak warunku CanExecute).
        AddCustomerCommand = new RelayCommand(AddCustomer);

        // Komenda dodawania pojazdu — aktywna tylko gdy wybrany jest klient.
        // "_ =>" ignoruje parametr komendy (nie używamy CommandParameter z XAML).
        AddVehicleCommand = new RelayCommand(AddVehicle, _ => SelectedCustomer != null);

        Load(); // Wczytaj listę klientów przy starcie
    }

    /// <summary>
    /// Wczytuje wszystkich klientów z bazy posortowanych alfabetycznie.
    /// Wywoływana przy inicjalizacji i gdy zakładka wymaga odświeżenia.
    /// </summary>
    public void Load()
    {
        Customers.Clear(); // Wyczyść przed załadowaniem — unikamy duplikatów
        // OrderBy w EF Core transluje na ORDER BY w SQL (efektywniejsze niż sortowanie w pamięci).
        foreach (var c in _db.Customers.OrderBy(c => c.Name)) Customers.Add(c);
    }

    /// <summary>Odświeża pojazdy i historię serwisową bieżącego wyboru (np. po zmianie zakładki).</summary>
    /// <remarks>
    /// Wywoływana przez MainWindow.MainTabs_SelectionChanged gdy użytkownik przełącza na tę zakładkę.
    /// Konieczna bo inny moduł (np. RepairOrdersViewModel) mógł dodać wpis historii serwisowej.
    /// </remarks>
    public void Refresh()
    {
        LoadVehicles(); // Odśwież pojazdy (mogły zostać dodane przez inny moduł)
        LoadHistory();  // Odśwież historię (Facade.RecordCompletion mógł dodać wpis)
    }

    /// <summary>
    /// Wczytuje pojazdy aktualnie wybranego klienta z bazy.
    /// Czyści też historię — bo stara historia należy do poprzedniego pojazdu.
    /// </summary>
    private void LoadVehicles()
    {
        Vehicles.Clear(); // Usuń pojazdy poprzedniego klienta
        History.Clear();  // Usuń historię (stała się nieaktualna)

        if (SelectedCustomer is null) return; // Brak wybranego klienta — nic nie ładuj

        // Where w EF Core transluje na WHERE CustomerId = @id w SQL.
        foreach (var v in _db.Vehicles.Where(v => v.CustomerId == SelectedCustomer.Id))
            Vehicles.Add(v);
    }

    /// <summary>
    /// Wczytuje historię serwisową aktualnie wybranego pojazdu, posortowaną od najnowszej.
    /// </summary>
    private void LoadHistory()
    {
        History.Clear(); // Usuń starą historię

        if (SelectedVehicle is null) return; // Brak wybranego pojazdu — nic nie ładuj

        // OrderByDescending — najnowsze wpisy na górze listy (bardziej użyteczne dla mechanika).
        foreach (var h in _db.ServiceHistory
                     .Where(h => h.VehicleId == SelectedVehicle.Id)
                     .OrderByDescending(h => h.Date))
            History.Add(h);
    }

    /// <summary>
    /// Tworzy nowego klienta na podstawie danych z formularza i zapisuje do bazy.
    /// Guard: nazwa jest wymagana — pusta nazwa to błąd walidacji.
    /// </summary>
    private void AddCustomer(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName)) return; // Walidacja: wymagana nazwa

        // Utwórz encję klienta z wartości pól formularza.
        var c = new Customer
        {
            Name = NewCustomerName,              // Imię i nazwisko lub nazwa firmy
            Phone = NewCustomerPhone,            // Telefon kontaktowy
            Email = NewCustomerEmail,            // E-mail (używany przez EmailCustomerObserver)
            Type = NewCustomerType,              // Prywatny lub Flota
            DiscountPercent = NewCustomerDiscount // Stały rabat (0 dla prywatnych)
        };

        _db.Customers.Add(c); // Zarejestruj w kontekście (tracked entity)
        _db.SaveChanges();    // Zapisz do bazy — EF Core nada Id

        Customers.Add(c);      // Dodaj do ObservableCollection — UI odświeży listę natychmiast
        SelectedCustomer = c;  // Automatycznie zaznacz nowego klienta (UX: lepsze doświadczenie)
    }

    /// <summary>
    /// Dodaje pojazd do aktualnie wybranego klienta i zapisuje do bazy.
    /// Guard: wymagany wybrany klient i numer rejestracyjny.
    /// </summary>
    private void AddVehicle(object? _)
    {
        // Double guard: SelectedCustomer sprawdzany też przez CanExecute, ale defensywnie.
        if (SelectedCustomer is null || string.IsNullOrWhiteSpace(NewVehicleReg)) return;

        // Utwórz encję pojazdu z danych formularza i przypisz do klienta przez FK.
        var v = new Vehicle
        {
            CustomerId = SelectedCustomer.Id, // FK — powiąż z wybranym klientem
            RegistrationNumber = NewVehicleReg, // Numer rejestracyjny (identyfikator pojazdu)
            Vin = NewVehicleVin,                // 17-znakowy numer seryjny nadwozia
            Brand = NewVehicleBrand,            // Marka (np. "Toyota")
            Model = NewVehicleModel,            // Model (np. "Corolla")
            Mileage = NewVehicleMileage         // Aktualny przebieg w km
        };

        _db.Vehicles.Add(v); // Zarejestruj w kontekście
        _db.SaveChanges();   // Zapisz do bazy

        Vehicles.Add(v); // Dodaj do ObservableCollection — UI pokaże nowy pojazd natychmiast
    }
}
