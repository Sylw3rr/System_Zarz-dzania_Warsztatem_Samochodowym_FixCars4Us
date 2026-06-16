// Plik: RepairOrdersViewModel.cs
// Rola: Centralny ViewModel modułu zleceń napraw — integruje wszystkie wzorce projektowe.
//       Jest "dyrektorem orkiestry" który wie kiedy i jak użyć każdego wzorca.
// Wzorce: Builder, Mediator, State, Command, Memento, Strategy, Decorator, Observer, Facade.
//         To serce systemu — każda operacja na zleceniu przechodzi przez ten ViewModel.

using System.Collections.ObjectModel; // ObservableCollection — lista powiadamiająca UI
using FixCars4Us.Core.Data;           // WorkshopContext — baza danych
using FixCars4Us.Core.Enums;          // RepairStatus, RepairStage, MechanicSpecialization, LiftType
using FixCars4Us.Core.Infrastructure; // ViewModelBase, RelayCommand
using FixCars4Us.Core.Models;         // RepairOrder, Vehicle, Part, Mechanic itd.
using FixCars4Us.Core.Patterns;       // Builder, Mediator, State, Command, Observer, Strategy
using FixCars4Us.Core.Services;       // WorkshopFacade, PricingService
using Microsoft.EntityFrameworkCore;  // Include() — eager loading

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Centralny moduł zleceń napraw — łączy WSZYSTKIE funkcje dodatkowe i większość wzorców:
///  • Builder            – składanie zlecenia,
///  • Mediator           – inteligentne przypisywanie zasobów (funkcja dodatkowa 1),
///  • Strategy+Decorator – dynamiczna wycena naprawy (funkcja dodatkowa 2),
///  • Command+Memento    – etapy naprawy z cofaniem (funkcja dodatkowa 3),
///  • State              – cykl życia zlecenia,
///  • Observer           – powiadomienia,
///  • Facade             – Panel Mechanika (operacje na zleceniu).
/// </summary>
public class RepairOrdersViewModel : ViewModelBase
{
    private readonly WorkshopContext _db;             // Kontekst EF Core (Shared DbContext)
    private readonly WorkshopFacade _facade;          // Fasada — ukrywa szczegóły operacji na zleceniach
    private readonly PricingService _pricing = new(); // Serwis wyceny (Strategy + Decorator)
    private readonly RepairNotifier _notifier;        // Subject Observer — rozsyła powiadomienia
    private readonly WorkshopMediator _mediator;      // Mediator — przydziela zasoby warsztatowe
    private readonly Dictionary<int, RepairHistory> _histories = new(); // Caretaker — historia komend per zlecenie
    private readonly HashSet<int> _completionRecorded = new();          // Zapobiega duplikatom ServiceHistoryEntry po Undo+Redo
    private readonly List<RelayCommand> _orderDependentCommands;        // Komendy zależne od SelectedOrder (dla RaiseAll)

    // --- Kolekcje danych dla formularzy ---
    public ObservableCollection<RepairOrder> Orders { get; } = new();          // Lista aktywnych zleceń (bez Zakonczone)
    public ObservableCollection<Vehicle> Vehicles { get; } = new();            // Pojazdy do wyboru przy tworzeniu zlecenia
    public ObservableCollection<Mechanic> Mechanics { get; } = new();          // Mechanicy (informacyjnie)
    public ObservableCollection<Lift> Lifts { get; } = new();                  // Podnośniki (informacyjnie)
    public ObservableCollection<Part> Parts { get; } = new();                  // Części do dodania do kosztorysu
    public ObservableCollection<ReceptionStation> Stations { get; } = new();  // Stanowiska przyjęć (dla kalendarza)
    public ObservableCollection<string> Notifications { get; } = new();       // Dziennik powiadomień Observer
    public ObservableCollection<string> PriceBreakdown { get; } = new();      // Rozbicie wyceny (Decorator.Breakdown)
    public ObservableCollection<PriceModifier> Modifiers { get; } = new();    // Modyfikatory do dodania do potoku Decorator
    public ObservableCollection<ILaborCostStrategy> LaborStrategies { get; } = new(); // Dostępne strategie wyceny robocizny

    // Tablice enum do ComboBox — Enum.GetValues() zwraca wszystkie wartości enum jako Array.
    public Array Specializations => Enum.GetValues(typeof(MechanicSpecialization)); // Dla ComboBox specjalizacji
    public Array LiftTypes => Enum.GetValues(typeof(LiftType));                    // Dla ComboBox typu podnośnika
    public Array Stages => Enum.GetValues(typeof(RepairStage));                    // Dla ComboBox docelowego etapu

    // --- Formularz nowego zlecenia (Builder + Mediator) ---
    public Vehicle? NewVehicle { get; set; }
    public string NewFault { get; set; } = "";
    public MechanicSpecialization NewSpecialization { get; set; }
    public LiftType NewLiftType { get; set; }
    public int NewCapacityKg { get; set; } = 2000;
    public bool NewRequiresTool { get; set; }
    public decimal NewEstimatedHours { get; set; } = 2;
    public DateTime NewDate { get; set; } = DateTime.Today.AddDays(1);
    public int NewStartHour { get; set; } = 9;

    // --- Wycena ---
    public decimal HourlyRate { get; set; } = 150;
    private ILaborCostStrategy? _selectedStrategy;
    public ILaborCostStrategy? SelectedStrategy
    {
        get => _selectedStrategy;
        set
        {
            if (SetField(ref _selectedStrategy, value))
                OnPropertyChanged(nameof(LaborInputLabel));
        }
    }

    /// <summary>Etykieta pola stawki — zależna od wybranej strategii robocizny (Strategy).</summary>
    public string LaborInputLabel => SelectedStrategy is FlatRateLaborStrategy
        ? "Kwota ryczałtu (zł, np. 500):"
        : "Stawka za godzinę (zł, np. 150):";

    private string _modifierKind = "surcharge"; // surcharge|percent|discount
    public string ModifierKind
    {
        get => _modifierKind;
        set
        {
            if (SetField(ref _modifierKind, value))
                OnPropertyChanged(nameof(ModifierValueLabel));
        }
    }

    /// <summary>Etykieta pola wartości modyfikatora — zł dla dopłaty kwotowej, % dla procentowych.</summary>
    public string ModifierValueLabel => ModifierKind == "surcharge"
        ? "Wartość (zł, np. 50):"
        : "Wartość (%, np. 10):";

    public string ModifierLabel { get; set; } = "";
    public decimal ModifierValue { get; set; }
    private decimal _finalPrice;
    public decimal FinalPrice { get => _finalPrice; set => SetField(ref _finalPrice, value); }

    // --- Wybór ---
    private RepairOrder? _selectedOrder;
    public RepairOrder? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetField(ref _selectedOrder, value))
            {
                OnPropertyChanged(nameof(SelectedOrderLog));
                _orderDependentCommands.RaiseAll();
            }
        }
    }
    private Part? _selectedPart;
    public Part? SelectedPart
    {
        get => _selectedPart;
        set { if (SetField(ref _selectedPart, value)) AddPartCommand.RaiseCanExecuteChanged(); }
    }
    public int PartQuantity { get; set; } = 1;
    public RepairStage TargetStage { get; set; }

    public IEnumerable<RepairLogEntry> SelectedOrderLog => SelectedOrder?.Log.ToList() ?? Enumerable.Empty<RepairLogEntry>();

    private string _status = "";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- Komendy ---
    public RelayCommand CreateOrderCommand { get; }
    public RelayCommand AddPartCommand { get; }
    public RelayCommand AdvanceStageCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand FinishOrderCommand { get; }
    public RelayCommand AddModifierCommand { get; }
    public RelayCommand ComputePriceCommand { get; }

    /// <summary>
    /// Konstruktor — inicjalizuje wszystkie wzorce projektowe i wczytuje dane.
    /// Tworzy: Observer, Facade, Mediator, strategie wyceny, komendy WPF.
    /// </summary>
    public RepairOrdersViewModel(WorkshopContext db)
    {
        _db = db; // Zapamiętaj współdzielony kontekst (ten sam co w innych ViewModelach)

        // Observer: stwórz notifiera i zarejestruj obserwatorów (inline — bez pól klasy).
        _notifier = new RepairNotifier();
        _notifier.Subscribe(new EmailCustomerObserver()); // Powiadomienia e-mail do klienta
        _notifier.Subscribe(new ManagerAlertObserver());  // Alerty dla managera (niski stan, zakończenie)

        // Facade: jeden punkt dostępu do operacji na zleceniach.
        _facade = new WorkshopFacade(_db, _notifier);

        // Mediator: wczytaj katalogi zasobów (raz, przy starcie) — Mediator zarządza rezerwacjami w pamięci.
        _mediator = new WorkshopMediator(_db.Mechanics.ToList(), _db.Lifts.ToList(), _db.SpecialTools.ToList());

        // Strategy: dodaj wszystkie dostępne strategie wyceny robocizny do listy w UI.
        LaborStrategies.Add(new TimeBasedLaborStrategy());       // Czas × stawka
        LaborStrategies.Add(new ManufacturerNormLaborStrategy()); // Czas × 1.15 × stawka (normy prod.)
        LaborStrategies.Add(new FlatRateLaborStrategy());         // Ryczałt (stawka = kwota całkowita)
        SelectedStrategy = LaborStrategies[0]; // Domyślna strategia: czas rzeczywisty

        // Command: stwórz komendy WPF powiązane z metodami prywatymi i warunkami CanExecute.
        CreateOrderCommand = new RelayCommand(CreateOrder); // Bez warunku — zawsze aktywna
        AddPartCommand = new RelayCommand(AddPart, _ => SelectedOrder != null && SelectedPart != null); // Oba muszą być wybrane
        AdvanceStageCommand = new RelayCommand(AdvanceStage, _ => SelectedOrder != null); // Wymaga wybranego zlecenia
        UndoCommand = new RelayCommand(Undo, _ => SelectedOrder != null);                 // Wymaga wybranego zlecenia
        AddModifierCommand = new RelayCommand(AddModifier);  // Dodaj modyfikator do listy Decoratorów
        ComputePriceCommand = new RelayCommand(ComputePrice, _ => SelectedOrder != null); // Wymaga zlecenia
        FinishOrderCommand = new RelayCommand(FinishOrder,  // Aktywna tylko gdy State pozwala na Zakonczone
            _ => SelectedOrder != null && RepairStateFactory.Create(SelectedOrder.Status).CanTransitionTo(RepairStatus.Zakonczone));

        // Lista komend zależnych od SelectedOrder — przy zmianie zaznaczenia odświeżamy wszystkie.
        _orderDependentCommands = new List<RelayCommand>
        {
            AddPartCommand, AdvanceStageCommand, UndoCommand, ComputePriceCommand, FinishOrderCommand
        };

        Load(); // Wczytaj dane z bazy
    }

    /// <summary>Wczytuje wszystkie dane potrzebne do formularzy i listy zleceń.</summary>
    public void Load()
    {
        // Załaduj dane referencyjne do ComboBoxów w formularzach.
        Vehicles.Clear();
        foreach (var v in _db.Vehicles.Include(v => v.Customer)) Vehicles.Add(v); // Include(Customer) dla wyświetlenia imienia
        Mechanics.Clear();
        foreach (var m in _db.Mechanics) Mechanics.Add(m);           // Lista mechaników (informacyjnie)
        Lifts.Clear();
        foreach (var l in _db.Lifts) Lifts.Add(l);                   // Lista podnośników (informacyjnie)
        Parts.Clear();
        foreach (var p in _db.Parts) Parts.Add(p);                   // Części do wyboru w AddPartCommand
        Stations.Clear();
        foreach (var s in _db.ReceptionStations) Stations.Add(s);    // Stanowiska (dla kalendarza)
        ReloadOrders(); // Załaduj zlecenia (osobna metoda bo wywoływana też przez Refresh)
    }

    /// <summary>Odświeża listę zleceń bez resetowania pozostałych danych (np. po zmianie zakładki).</summary>
    public void Refresh() => ReloadOrders();

    /// <summary>
    /// Przeładowuje listę zleceń, pomijając zakończone (terminal state).
    /// Przywraca poprzednie zaznaczenie na podstawie Id — DataGrid nie traci zaznaczenia.
    /// </summary>
    private void ReloadOrders()
    {
        var selectedId = SelectedOrder?.Id; // Zapamiętaj Id zaznaczonego zlecenia przed czyszczeniem
        Orders.Clear(); // Wyczyść kolekcję ObservableCollection (powiadomi DataGrid)
        // Załaduj przez Facade (eager loading) i odfiltruj Zakonczone — nie pojawiają się w aktywnej liście.
        foreach (var o in _facade.LoadOrders().Where(o => o.Status != RepairStatus.Zakonczone)) Orders.Add(o);
        // Przywróć zaznaczenie — FirstOrDefault zwraca null jeśli zlecenie nie jest już w liście.
        SelectedOrder = selectedId is null ? null : Orders.FirstOrDefault(o => o.Id == selectedId);
    }

    /// <summary>
    /// Wyświetla komunikat w polu Status i dodaje do listy powiadomień z timestamp.
    /// Insert(0,...) = dodaj na POCZĄTKU (najnowsze na górze).
    /// </summary>
    private void Log(string msg)
    {
        Status = msg; // Pole statusu na dole okna (binding przez INotifyPropertyChanged)
        Notifications.Insert(0, $"{DateTime.Now:HH:mm:ss}  {msg}"); // Dodaj z godziną na początku listy
    }

    /// <summary>Tworzy zlecenie: Mediator przydziela zasoby, Builder składa obiekt, Facade zapisuje.</summary>
    /// <remarks>
    /// Sekwencja wzorców:
    /// 1. Mediator.TryAllocate()    — znajdź wolnego mechanika, podnośnik i narzędzie
    /// 2. RepairOrderBuilder.Build() — złóż zlecenie krok po kroku (Fluent API)
    /// 3. Facade.PersistNewOrder()  — zapisz do bazy (EF Core nadaje Id)
    /// 4. Opcjonalnie: Appointments.Add() — zarezerwuj stanowisko przyjęć na ten czas
    /// </remarks>
    private void CreateOrder(object? _)
    {
        // Guard: pojazd musi być wybrany (formularz jest niekompletny bez pojazdu).
        if (NewVehicle is null) { Log("Wybierz pojazd."); return; }

        // Oblicz okno czasowe naprawy: data + godzina startu, plus szacowany czas.
        var start = NewDate.Date.AddHours(NewStartHour);
        var end = start.AddHours((double)Math.Max(1, NewEstimatedHours)); // Min. 1 godzina

        // MEDIATOR: stwórz żądanie i poproś o przydział zasobów.
        var req = new ResourceRequest(NewSpecialization, NewLiftType, NewCapacityKg, NewRequiresTool, start, end);
        var alloc = _mediator.TryAllocate(req); // Atomowe przydzielenie: mechanik + podnośnik [+ narzędzie]
        if (!alloc.Success) { Log("Mediator: " + alloc.Message); return; } // Fail fast: brak zasobów

        // BUILDER: złóż zlecenie krok po kroku (Fluent API).
        var builder = new RepairOrderBuilder()
            .ForVehicle(NewVehicle)              // Przypisz pojazd (z FK)
            .WithFault(NewFault)                 // Opis usterki z formularza
            .AssignMechanic(alloc.Mechanic!)     // Mechanik przydzielony przez Mediator (! = nie null po Success)
            .UseLift(alloc.Lift!)                // Podnośnik przydzielony przez Mediator
            .EstimateHours(NewEstimatedHours)    // Szacowany czas (używany przez Strategy)
            .AddLabor("Diagnostyka i naprawa", HourlyRate, NewEstimatedHours); // Pierwsza pozycja kosztorysu
        if (alloc.Tool is not null) builder.UseTool(alloc.Tool); // Narzędzie tylko gdy wymagane i dostępne

        var order = builder.Build(); // Finalizuj: Status=Przyjete, Stage=Diagnostyka, pierwszy log

        // FACADE: zapisz zlecenie do bazy (EF Core nada Id).
        _facade.PersistNewOrder(order);

        // Automatyczna rezerwacja stanowiska przyjęć gdy jest wolne w tym czasie.
        var freeStation = _db.ReceptionStations.FirstOrDefault(s => !_db.Appointments.Any(a =>
            a.ReceptionStationId == s.Id && start < a.End && a.Start < end)); // Klasyczny test nakładania
        if (freeStation is not null)
        {
            _db.Appointments.Add(new Appointment // Dodaj wizytę powiązaną z nowym zleceniem
            {
                VehicleId = NewVehicle.Id,
                ReceptionStationId = freeStation.Id,
                Start = start,
                End = end,
                Reason = NewFault // Opis z formularza zlecenia
            });
            _db.SaveChanges(); // Utrwal wizytę w bazie
        }

        // Odśwież listę i zaznacz nowe zlecenie.
        ReloadOrders();
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == order.Id); // Zaznacz nowe zlecenie
        Log($"Utworzono zlecenie #{order.Id}. Mediator: {alloc.Message}"); // Komunikat z wynikiem Mediatora
    }

    /// <summary>Dodaje wybraną część do kosztorysu zlecenia przez Facade (rozchód z magazynu).</summary>
    private void AddPart(object? _)
    {
        if (SelectedOrder is null || SelectedPart is null) return; // Guard — oba wymagane
        var (_, msg) = _facade.AddPartToOrder(SelectedOrder, SelectedPart, PartQuantity); // Facade: walidacja + rozchód + kosztorys
        // Part.StockQuantity : INotifyPropertyChanged — DataGrid Inventory odświeży się samo.
        OnPropertyChanged(nameof(SelectedOrderLog)); // Odśwież log zlecenia (nowy wpis z rozchodem)
        Log(msg); // Wyświetl wynik operacji w StatusBar
    }

    /// <summary>Przejście do kolejnego etapu jako Command (z możliwością Undo dzięki Memento).</summary>
    private void AdvanceStage(object? _)
    {
        if (SelectedOrder is null) return;
        var history = GetHistory(SelectedOrder.Id);
        var statusBefore = SelectedOrder.Status;
        var cmd = new AdvanceStageCommand(SelectedOrder, TargetStage, _db.Parts.Local.Any() ? _db.Parts.Local.ToList() : _db.Parts.ToList());
        history.Do(cmd);

        if (statusBefore != RepairStatus.GotoweDoOdbioru && SelectedOrder.Status == RepairStatus.GotoweDoOdbioru
            && _completionRecorded.Add(SelectedOrder.Id))
            _facade.RecordCompletion(SelectedOrder);

        _db.SaveChanges();
        OnPropertyChanged(nameof(SelectedOrderLog));
        Log($"Wykonano komendę: {cmd.Description}. W historii: {history.Count} operacji.");
    }

    /// <summary>
    /// Cofa ostatnią operację etapową (wzorzec Command + Memento, LIFO).
    /// RepairHistory.Undo() zdejmuje komendę ze stosu i wywołuje jej Undo(),
    /// przywracając stan Stage, Status i stanów magazynowych z Memento.
    /// </summary>
    private void Undo(object? _)
    {
        if (SelectedOrder is null) return; // Guard
        var history = GetHistory(SelectedOrder.Id); // Caretaker — historia komend tego zlecenia
        var msg = history.Undo(); // Zdejmij ze stosu, cofnij skutki i pobierz komunikat
        _db.SaveChanges(); // Utrwal przywrócony stan (Stage, Status, stany magazynowe)
        OnPropertyChanged(nameof(SelectedOrderLog)); // Odśwież dziennik (Undo dodało wpis "COFNIĘTO")
        Log(msg); // Wyświetl np. "Cofnięto: Etap -> PraceWlasciwe"
    }

    /// <summary>
    /// Pobiera (lub tworzy) RepairHistory (Caretaker Memento) dla podanego Id zlecenia.
    /// Każde zlecenie ma swoją niezależną historię — Dictionary zapewnia izolację.
    /// </summary>
    private RepairHistory GetHistory(int orderId)
    {
        // TryGetValue: bezpieczne pobranie bez wyjątku gdy klucz nie istnieje.
        if (!_histories.TryGetValue(orderId, out var h))
        {
            h = new RepairHistory(); // Nowe zlecenie = nowy stos komend
            _histories[orderId] = h; // Zarejestruj w słowniku dla przyszłych wywołań
        }
        return h; // Zwróć istniejącą lub nowo stworzoną historię
    }

    /// <summary>Kończy zlecenie (wydanie pojazdu klientowi) i usuwa je z listy aktywnych zleceń.</summary>
    private void FinishOrder(object? _)
    {
        if (SelectedOrder is null) return;
        var (ok, msg) = _facade.ChangeStatus(SelectedOrder, RepairStatus.Zakonczone);
        Log(msg);
        if (ok)
        {
            Orders.Remove(SelectedOrder);
            SelectedOrder = null;
        }
    }

    /// <summary>
    /// Dodaje modyfikator ceny do listy Modifiers.
    /// Lista jest używana przez ComputePrice do budowania potoku Decoratorów.
    /// Guard: wymagana niepusta etykieta i niezerowa wartość.
    /// </summary>
    private void AddModifier(object? _)
    {
        if (string.IsNullOrWhiteSpace(ModifierLabel) || ModifierValue == 0) return; // Walidacja
        // Dodaj do ObservableCollection — UI (ItemsControl) odświeży listę modyfikatorów.
        Modifiers.Add(new PriceModifier(ModifierKind, ModifierLabel, ModifierValue));
    }

    /// <summary>Buduje finalną wycenę (Strategy + Decorator) i prezentuje rozbicie składników.</summary>
    private void ComputePrice(object? _)
    {
        if (SelectedOrder is null || SelectedStrategy is null) return;

        var discount = SelectedOrder.Vehicle?.Customer?.DiscountPercent ?? 0;
        var price = _pricing.BuildPrice(SelectedOrder, SelectedStrategy, HourlyRate, discount, Modifiers);

        PriceBreakdown.Clear();
        foreach (var (label, amount) in price.Breakdown())
            PriceBreakdown.Add($"{label}: {amount:0.00} zł");
        FinalPrice = price.GetPrice();
        Log($"Wyceniono zlecenie #{SelectedOrder.Id}: {FinalPrice:0.00} zł ({SelectedStrategy.Name}).");
    }
}
