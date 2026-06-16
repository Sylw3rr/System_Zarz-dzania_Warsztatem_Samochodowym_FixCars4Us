// Plik: WorkshopFacade.cs
// Rola: Uproszczony interfejs dostępu do operacji biznesowych warsztatu ("Panel Mechanika").
//       Ukrywa złożoną współpracę między podsystemami: bazą danych, maszyną stanów,
//       powiadomieniami i dziennikiem audytowym.
// Wzorzec: FACADE (GoF) — udostępnia uproszczony interfejs dla złożonego podsystemu.
//          Zamiast: db.SaveChanges() + State.Check() + Notifier.Notify() + Log.Add()
//          jedno wywołanie: facade.ChangeStatus(order, target).

using FixCars4Us.Core.Data;      // WorkshopContext — dostęp do bazy danych
using FixCars4Us.Core.Enums;     // RepairStatus — cel zmiany statusu
using FixCars4Us.Core.Models;    // RepairOrder, Part, RepairItem, ServiceHistoryEntry
using FixCars4Us.Core.Patterns;  // RepairStateFactory, RepairNotifier, RepairLogEntry
using Microsoft.EntityFrameworkCore; // Include() — eager loading relacji

namespace FixCars4Us.Core.Services;

/// <summary>
/// WZORZEC: Facade ("Panel Mechanika").
/// Udostępnia proste API dla UI, ukrywając współpracę wielu podsystemów:
/// magazynu (pobieranie części), maszyny stanów (State), powiadomień (Observer)
/// oraz dziennika audytowego. Jedno wywołanie = spójna operacja biznesowa.
/// </summary>
/// <remarks>
/// Dlaczego Facade zamiast wywoływania podsystemów bezpośrednio z ViewModelu?
/// Gdyby ViewModel samodzielnie zarządzał wszystkimi podsystemami, stałby się
/// "God Object" — klasą wiedzącą o wszystkim i niczym nieograniczoną.
/// Facade grupuje powiązane operacje, ułatwia testowanie (mockuje jeden obiekt)
/// i ukrywa szczegóły transakcji (SaveChanges() jest tu, nie w ViewModelu).
///
/// Pola:
/// - _db: WorkshopContext — "brama" do bazy danych (Unit of Work)
/// - _notifier: RepairNotifier — Subject w wzorcu Observer (rozsyła powiadomienia)
/// </remarks>
public class WorkshopFacade
{
    private readonly WorkshopContext _db;       // Kontekst bazy danych (wszystkie tabele)
    private readonly RepairNotifier _notifier;  // Zarządca obserwatorów (e-mail, manager)

    /// <summary>
    /// Konstruktor z Dependency Injection — Facade nie tworzy zależności samodzielnie,
    /// dostaje je z zewnątrz (od ViewModelu który zarządza ich cyklem życia).
    /// </summary>
    public WorkshopFacade(WorkshopContext db, RepairNotifier notifier)
    {
        _db = db;           // Zapamiętaj kontekst (shared z innymi ViewModelami)
        _notifier = notifier; // Zapamiętaj notifier (subskrybenci już dodani w ViewModel)
    }

    /// <summary>
    /// Zmienia status zlecenia z walidacją przez wzorzec State, zapisuje log
    /// i rozsyła powiadomienia (Observer). Zwraca komunikat dla UI.
    /// </summary>
    /// <remarks>
    /// Zwracanie krotki (bool Ok, string Message) zamiast wyjątku to celowy wybór:
    /// niepowodzenie zmiany statusu to scenariusz spodziewany (np. użytkownik kliknął
    /// "zakończ" gdy status nie pozwala), a nie wyjątkowy błąd systemu.
    /// ViewModel sprawdza Ok i wyświetla Message w polu Status.
    /// </remarks>
    public (bool Ok, string Message) ChangeStatus(RepairOrder order, RepairStatus target)
    {
        // KROK 1: Walidacja przez wzorzec State — czy przejście jest dozwolone?
        var state = RepairStateFactory.Create(order.Status); // Pobierz obiekt bieżącego stanu
        if (!state.CanTransitionTo(target)) // Sprawdź czy przejście jest legalne
            return (false, $"Niedozwolone przejście: {order.Status} -> {target}."); // Fail fast, brak zmian w DB

        // KROK 2: Wykonaj zmianę statusu.
        order.Status = target; // Zmień status w obiekcie (INotifyPropertyChanged powiadomi UI)
        order.Log.Add(new RepairLogEntry { Message = $"Zmiana statusu na: {target}." }); // Wpis audytowy

        // KROK 3: Jeśli zlecenie jest gotowe — zapisz w historii serwisowej pojazdu.
        if (target == RepairStatus.GotoweDoOdbioru)
            RecordCompletion(order); // Stwórz wpis w ServiceHistory (trwały zapis naprawy)

        // KROK 4: Zapisz wszystkie zmiany do bazy (Unit of Work — jedna transakcja).
        _db.SaveChanges();

        // KROK 5: Powiadom obserwatorów (e-mail do klienta, alert dla managera).
        _notifier.Notify(order, $"Status naprawy zmieniono na '{target}'.");

        return (true, $"Status zmieniony na {target}."); // Sukces — ViewModel wyświetli komunikat
    }

    /// <summary>
    /// Dopisuje wpis do historii serwisowej pojazdu po zakończeniu naprawy
    /// (status "Gotowe do odbioru"). Wywoływane zarówno przy ręcznej zmianie
    /// statusu, jak i przy automatycznym przejściu statusu wynikającym z etapu.
    /// </summary>
    /// <remarks>
    /// Historia serwisowa (ServiceHistory) to trwały, niezmienny zapis dla pojazdu.
    /// Nawet gdy zlecenie zostanie zarchiwizowane lub usunięte z systemu, historia
    /// pozostaje i jest widoczna w kartotece pojazdu.
    /// Guard: if (order.Vehicle is null) — zabezpieczenie gdy relacja nie załadowana.
    /// </remarks>
    public void RecordCompletion(RepairOrder order)
    {
        if (order.Vehicle is null) return; // Guard: brak pojazdu = brak historii do zapisania

        // Dodaj wpis historii serwisowej — trwały zapis naprawy dla kartoteki pojazdu.
        _db.ServiceHistory.Add(new ServiceHistoryEntry
        {
            VehicleId = order.VehicleId,                        // FK do pojazdu
            Date = DateTime.Now,                                 // Data zakończenia naprawy
            Description = $"Naprawa: {order.FaultDescription}", // Opis z zlecenia
            MileageAtService = order.Vehicle.Mileage,           // Aktualny przebieg pojazdu
            Cost = order.ItemsTotal                             // Suma kosztorysu (przed rabatami Decoratora)
        });
        // Uwaga: SaveChanges() nie jest tu wywoływany — robi to wywołujący (ChangeStatus lub ViewModel).
        // Zasada: każda metoda odpowiada za swoje zmiany, SaveChanges tylko raz.
    }

    /// <summary>
    /// Dodaje część z magazynu do zlecenia: aktualizuje stan magazynowy (rozchód),
    /// dopisuje pozycję kosztorysu i wpis do logu — wszystko za jednym wywołaniem.
    /// </summary>
    /// <remarks>
    /// Walidacje (quantity &lt;= 0, brak stanu) są sprawdzane przed jakąkolwiek modyfikacją.
    /// Dzięki temu albo wszystkie zmiany zostaną zastosowane, albo żadna (atomowość).
    /// Powiadomienie o niskim stanie (IsLowStock) jest wysyłane PO zmniejszeniu stanu —
    /// dopiero wtedy wiemy czy przekroczono próg MinStock.
    /// </remarks>
    public (bool Ok, string Message) AddPartToOrder(RepairOrder order, Part part, int quantity)
    {
        // Walidacja wejściowa — fail fast przed modyfikacją.
        if (quantity <= 0) return (false, "Ilość musi być dodatnia.");
        if (part.StockQuantity < quantity)
            return (false, $"Za malo czesci '{part.Name}' w magazynie (dostepne: {part.StockQuantity}).");

        // Pobierz z magazynu — StockQuantity.setter powiadomi UI przez INotifyPropertyChanged.
        part.StockQuantity -= quantity;

        // Dodaj pozycję do kosztorysu zlecenia.
        order.Items.Add(new RepairItem
        {
            Description = $"Część: {part.Name}", // Opis dla faktury/raportu
            UnitPrice = part.SalePrice,           // Cena sprzedaży (marża warsztatu)
            Quantity = quantity,                  // Ilość pobranych sztuk
            PartId = part.Id                      // Powiąż z katalogiem (FK)
        });

        // Dodaj wpis audytowy — kto, co, ile.
        order.Log.Add(new RepairLogEntry { Message = $"Pobrano z magazynu: {quantity} x {part.Name}." });

        // Sprawdź niski stan po pobraniu — może trzeba zamówić uzupełnienie.
        if (part.IsLowStock)
            _notifier.Notify(order, $"UWAGA: niski stan magazynowy czesci '{part.Name}' ({part.StockQuantity})."); // Alert dla managera

        _db.SaveChanges(); // Zatwierdź rozchód i pozycję kosztorysu w jednej transakcji

        return (true, $"Dodano {quantity} x {part.Name} do zlecenia."); // Komunikat dla UI
    }

    /// <summary>Rejestruje czas pracy mechanika na zleceniu (logowanie roboczogodzin).</summary>
    /// <remarks>
    /// EstimatedHours jest kumulowany — każde wywołanie dodaje do istniejącej wartości.
    /// W bardziej rozbudowanym systemie byłoby tu ID mechanika i szczegółowy czas pracy.
    /// </remarks>
    public void LogWorkTime(RepairOrder order, decimal hours)
    {
        order.EstimatedHours += hours; // Skumuluj godziny
        order.Log.Add(new RepairLogEntry { Message = $"Zalogowano {hours} h pracy mechanika." }); // Audyt
        _db.SaveChanges(); // Utrwal zmianę godzin
    }

    /// <summary>Zapisuje nowe zlecenie (zbudowane Builderem) do bazy.</summary>
    /// <remarks>
    /// Oddzielna metoda zamiast SaveChanges() bezpośrednio w ViewModelu —
    /// Facade kontroluje kiedy i co jest zapisywane (enkapsulacja dostępu do DB).
    /// Zwraca to samo zlecenie z nadanym Id (EF Core wypełnia Id po SaveChanges).
    /// </remarks>
    public RepairOrder PersistNewOrder(RepairOrder order)
    {
        _db.RepairOrders.Add(order); // Zarejestruj zlecenie w kontekście (tracked entity)
        _db.SaveChanges();           // Zapisz do SQLite — EF Core nada Id
        return order;                // Zwróć ze zaktualizowanym Id (do SelectedOrder)
    }

    /// <summary>
    /// Wczytuje aktywne zlecenia z bazy z pełnym zestawem relacji (Include).
    /// Eager loading (Include/ThenInclude) eliminuje problem N+1 zapytań.
    /// </summary>
    /// <remarks>
    /// Include() ładuje relacje w JEDNYM zapytaniu SQL (JOIN) zamiast osobnych zapytań
    /// dla każdego zlecenia — to crucial dla wydajności przy wielu zleceniach.
    /// ThenInclude() zagłębia się w relacje: RepairOrder -> Vehicle -> Customer.
    /// </remarks>
    public List<RepairOrder> LoadOrders() =>
        _db.RepairOrders
           .Include(o => o.Vehicle).ThenInclude(v => v!.Customer) // Załaduj pojazd i klienta (dla e-maila i rabatu)
           .Include(o => o.Items)     // Załaduj pozycje kosztorysu (do ItemsTotal i UI)
           .Include(o => o.Log)       // Załaduj dziennik audytowy (do podglądu w UI)
           .Include(o => o.Mechanic)  // Załaduj mechanika (do wyświetlenia w tabeli)
           .Include(o => o.Lift)      // Załaduj podnośnik (do wyświetlenia)
           .ToList();                 // Materializuj wynik (wykonaj zapytanie SQL)
}
