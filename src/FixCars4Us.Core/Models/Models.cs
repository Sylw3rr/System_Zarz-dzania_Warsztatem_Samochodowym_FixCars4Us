// Plik: Models.cs
// Rola: Modele domenowe (Domain Models) — klasy reprezentujące rzeczywiste byty
//       warsztatu samochodowego. Są to tzw. POCO (Plain Old CLR Objects), bez
//       logiki biznesowej — przechowują dane i relacje między nimi.
// Wzorzec: Domain Model (P of EAA, Fowler). Niektóre klasy implementują
//          INotifyPropertyChanged aby zmiany ich pól były widoczne w UI (MVVM).

using System.ComponentModel;           // INotifyPropertyChanged
using System.Runtime.CompilerServices; // CallerMemberName
using FixCars4Us.Core.Enums;          // Wyliczenia dziedzinowe

namespace FixCars4Us.Core.Models;

/// <summary>Klient warsztatu (osoba prywatna lub flota).</summary>
/// <remarks>
/// Customer jest "korzeniem agregatu" w rozumieniu DDD: pojazdy należą do klienta
/// i są tworzone/usuwane razem z nim. DiscountPercent jest używany przez
/// DiscountDecorator w silniku wyceny (PricingService).
/// </remarks>
public class Customer
{
    // Klucz główny — EF Core automatycznie rozpoznaje właściwość "Id" jako PK.
    public int Id { get; set; }

    public string Name { get; set; } = ""; // Imię i nazwisko lub nazwa firmy

    public string Phone { get; set; } = ""; // Numer telefonu kontaktowego

    public string Email { get; set; } = ""; // Adres e-mail (używany przez EmailCustomerObserver)

    // Typ klienta wpływa na logikę wyceny — klienci Flota mają rabat.
    public CustomerType Type { get; set; } = CustomerType.Prywatny;

    /// <summary>Rabat stały dla klienta (np. flotowy) w procentach 0-100.</summary>
    /// <remarks>
    /// Wartość 0 = brak rabatu. PricingService przekazuje tę wartość do DiscountDecorator.
    /// Przechowywana jako decimal, ale SQLite używa typu double (konwersja w WorkshopContext).
    /// </remarks>
    public decimal DiscountPercent { get; set; }

    // Kolekcja nawigacyjna EF Core — nie musisz ręcznie łączyć klientów z pojazdami.
    // "= new()" zapewnia, że lista nigdy nie jest null (bezpieczniejszy kod).
    public List<Vehicle> Vehicles { get; set; } = new();

    // ToString jest używany przez ListBox/ComboBox w UI do wyświetlenia klienta.
    public override string ToString() => $"{Name} ({Type})";
}

/// <summary>Pojazd przypisany do klienta wraz z historią serwisową.</summary>
/// <remarks>
/// Vehicle jest węzłem pośrednim między Customer a RepairOrder/Appointment.
/// Klucz obcy CustomerId + właściwość nawigacyjna Customer? to standardowy
/// wzorzec EF Core dla relacji jeden-do-wielu.
/// </remarks>
public class Vehicle
{
    public int Id { get; set; }

    public string RegistrationNumber { get; set; } = ""; // Numer rejestracyjny (unikatowy identyfikator dla klienta)

    public string Vin { get; set; } = ""; // Vehicle Identification Number — 17-znakowy numer seryjny nadwozia

    public string Brand { get; set; } = ""; // Marka pojazdu (np. "Volkswagen")

    public string Model { get; set; } = ""; // Model pojazdu (np. "Golf")

    public int Mileage { get; set; } // Aktualny przebieg w km (aktualizowany ręcznie)

    // Klucz obcy do Customer — EF Core wymaga explicite podania FK gdy relacja jest nullable.
    public int CustomerId { get; set; }

    // Właściwość nawigacyjna — EF Core wypełnia ją po Include() lub lazy loading.
    // "?" oznacza że może być null gdy nie załadowano relacji (brak Include).
    public Customer? Customer { get; set; }

    // Historia serwisowa tego pojazdu — agregat wpisów z dat wcześniejszych napraw.
    public List<ServiceHistoryEntry> History { get; set; } = new();

    // ToString wyświetlany w listach wyboru pojazdu w UI.
    public override string ToString() => $"{Brand} {Model} [{RegistrationNumber}]";
}

/// <summary>Pojedynczy wpis historii serwisowej pojazdu.</summary>
/// <remarks>
/// Wpis jest tworzony automatycznie przez WorkshopFacade.RecordCompletion()
/// gdy zlecenie przechodzi do statusu GotoweDoOdbioru. Stanowi trwały zapis
/// wykonanej usługi niezależny od zlecenia (zlecenia są archiwizowane/usuwane,
/// historia zostaje na zawsze).
/// </remarks>
public class ServiceHistoryEntry
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.Now; // Data wizyty serwisowej

    public string Description { get; set; } = ""; // Opis wykonanej pracy

    public int MileageAtService { get; set; } // Przebieg w chwili serwisu (dla analizy zużycia)

    public decimal Cost { get; set; } // Łączny koszt usługi (pobierany z ItemsTotal zlecenia)

    public int VehicleId { get; set; } // FK do Vehicle

    public Vehicle? Vehicle { get; set; } // Właściwość nawigacyjna
}

/// <summary>Część zamienna w katalogu i magazynie.</summary>
/// <remarks>
/// Part implementuje INotifyPropertyChanged ponieważ StockQuantity jest modyfikowane
/// bezpośrednio przez WorkshopFacade i AdvanceStageCommand — zmiany mają być
/// natychmiast widoczne w UI bez przeładowania całej kolekcji.
/// Wzorzec Observer (INotifyPropertyChanged) na poziomie modelu.
/// </remarks>
public class Part : INotifyPropertyChanged
{
    // Zdarzenie wymagane przez INotifyPropertyChanged — subskrybentem jest UI przez binding.
    public event PropertyChangedEventHandler? PropertyChanged;

    // Prywatna metoda pomocnicza — powiadamia UI o zmianie podanej właściwości.
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public int Id { get; set; }

    public string Code { get; set; } = ""; // Kod katalogowy (np. "OL-5W30")

    public string Name { get; set; } = ""; // Nazwa handlowa części

    public decimal PurchasePrice { get; set; } // Cena zakupu od dostawcy (netto)

    public decimal SalePrice { get; set; } // Cena sprzedaży klientowi (netto)

    // Pole zapasowe dla właściwości z powiadamianiem.
    private int _stockQuantity;

    /// <summary>
    /// Aktualny stan magazynowy. Zmiana tej właściwości automatycznie
    /// odświeża w UI zarówno liczbę sztuk jak i flagę IsLowStock.
    /// </summary>
    public int StockQuantity
    {
        get => _stockQuantity;
        set
        {
            if (_stockQuantity == value) return; // Nie powiadamiaj jeśli wartość bez zmian (optymalizacja)
            _stockQuantity = value;
            OnPropertyChanged(); // Powiadom binding o zmianie StockQuantity
            OnPropertyChanged(nameof(IsLowStock)); // Powiadom osobno — IsLowStock zależy od StockQuantity
        }
    }

    // Minimalny wymagany stan magazynowy — alert gdy stan spadnie do tej wartości lub poniżej.
    public int MinStock { get; set; } = 2;

    /// <summary>Czy część jest oryginałem (true) czy zamiennikiem (false).</summary>
    public bool IsOriginal { get; set; } = true;

    /// <summary>
    /// Flaga informująca czy stan magazynowy jest krytycznie niski.
    /// Właściwość obliczana (computed property) — nie posiada backing field.
    /// WPF odświeża ją dzięki ręcznemu wywołaniu OnPropertyChanged(nameof(IsLowStock))
    /// w seterze StockQuantity.
    /// </summary>
    public bool IsLowStock => StockQuantity <= MinStock;

    // ToString wyświetlany w listach wyboru części w UI.
    public override string ToString() => $"{Name} [{Code}]";
}

/// <summary>Typ usługi / robocizny z cennikiem roboczogodziny.</summary>
/// <remarks>
/// ServiceType definiuje cennik pracy dla różnych kategorii.
/// Właściwość HourlyRate jest przekazywana do ILaborCostStrategy.CalculateLaborCost()
/// jako stawka bazowa — wybrany wzorzec Strategy decyduje jak jej użyć.
/// </remarks>
public class ServiceType
{
    public int Id { get; set; }

    public string Name { get; set; } = ""; // Opis usługi (np. "Roboczogodzina mechanika")

    public WorkCategory Category { get; set; } // Kategoria pracy (wpływa na stawkę)

    public decimal HourlyRate { get; set; } // Stawka za roboczogodzinę w złotych

    // ToString wyświetlany w listach w UI.
    public override string ToString() => $"{Name} ({Category})";
}

/// <summary>Mechanik / pracownik warsztatu.</summary>
/// <remarks>
/// Mechanic jest przydzielany do zlecenia przez WorkshopMediator na podstawie
/// Specialization — szuka wolnego mechanika pasującego do wymaganej specjalizacji.
/// </remarks>
public class Mechanic
{
    public int Id { get; set; }

    public string Name { get; set; } = ""; // Imię i nazwisko mechanika

    public MechanicSpecialization Specialization { get; set; } // Specjalizacja — klucz dopasowania w Mediatorze

    // ToString wyświetlany w listach i raportach.
    public override string ToString() => $"{Name} ({Specialization})";
}

/// <summary>Stanowisko / podnośnik.</summary>
/// <remarks>
/// Lift jest przydzielany przez WorkshopMediator razem z mechanikiem.
/// Mediator sprawdza zarówno Type jak i CapacityKg — jeśli pojazd waży
/// więcej niż udźwig podnośnika, ten podnośnik jest pomijany.
/// </remarks>
public class Lift
{
    public int Id { get; set; }

    public string Name { get; set; } = ""; // Nazwa stanowiska (np. "Podnośnik A")

    public LiftType Type { get; set; } // Typ — Osobowy / Ciezarowy / Lakierniczy

    /// <summary>Maksymalny udźwig w kg.</summary>
    public int CapacityKg { get; set; }

    // ToString wyświetlany w UI.
    public override string ToString() => $"{Name} ({Type})";
}

/// <summary>Narzędzie specjalistyczne (np. komputer diagnostyczny).</summary>
/// <remarks>
/// SpecialTool jest opcjonalnym zasobem przydzielanym przez Mediator
/// gdy ResourceRequest.RequiresDiagnosticTool == true.
/// W przeciwieństwie do mechanika i podnośnika — wymaganie narzędzia jest opcjonalne.
/// </remarks>
public class SpecialTool
{
    public int Id { get; set; }

    public string Name { get; set; } = ""; // Nazwa narzędzia (np. "Komputer diagnostyczny")

    // ToString wyświetlany w listach.
    public override string ToString() => Name;
}

/// <summary>Stanowisko przyjęć dla kalendarza wizyt.</summary>
/// <remarks>
/// ReceptionStation to fizyczne stanowisko obsługi klienta przy przyjęciu pojazdu.
/// AppointmentsViewModel waliduje kolizje: to samo stanowisko nie może obsługiwać
/// dwóch pojazdów jednocześnie (walidacja zachodzenia przedziałów czasowych).
/// </remarks>
public class ReceptionStation
{
    public int Id { get; set; }

    public string Name { get; set; } = ""; // Nazwa stanowiska (np. "Stanowisko przyjęć 1")

    // ToString wyświetlany w comboboxach kalendarza.
    public override string ToString() => Name;
}

/// <summary>Wizyta w kalendarzu przyjęć.</summary>
/// <remarks>
/// Appointment jest tworzony w dwóch sytuacjach:
/// 1. Ręcznie przez pracownika w zakładce "Kalendarz" (AppointmentsViewModel).
/// 2. Automatycznie przez RepairOrdersViewModel przy tworzeniu zlecenia,
///    gdy jest wolne stanowisko — integracja modułów przez wspólny DbContext.
/// </remarks>
public class Appointment
{
    public int Id { get; set; }

    public DateTime Start { get; set; } // Godzina rozpoczęcia wizyty

    public DateTime End { get; set; } // Godzina zakończenia wizyty (Start + DurationHours)

    public string Reason { get; set; } = ""; // Powód wizyty / opis usterki

    public int VehicleId { get; set; } // FK — który pojazd

    public Vehicle? Vehicle { get; set; } // Właściwość nawigacyjna (ładowana przez Include)

    public int ReceptionStationId { get; set; } // FK — które stanowisko

    public ReceptionStation? ReceptionStation { get; set; } // Właściwość nawigacyjna
}

/// <summary>Pozycja kosztorysu naprawy (część albo robocizna).</summary>
/// <remarks>
/// RepairItem może reprezentować:
/// - Część z magazynu (PartId != null) — wyceniana po SalePrice * Quantity
/// - Robociznę (PartId == null) — wyceniana jako UnitPrice * Quantity (godziny)
/// Suma LineTotal wszystkich pozycji daje ItemsTotal zlecenia.
/// </remarks>
public class RepairItem
{
    public int Id { get; set; }

    public string Description { get; set; } = ""; // Opis pozycji (np. "Część: Filtr oleju")

    public decimal UnitPrice { get; set; } // Cena jednostkowa (za sztukę lub roboczogodzinę)

    public int Quantity { get; set; } = 1; // Ilość sztuk lub liczba godzin

    /// <summary>Powiązanie z częścią (jeśli pozycja to część z magazynu).</summary>
    public int? PartId { get; set; } // null = pozycja robocizny, nie null = część z magazynu

    public Part? Part { get; set; } // Właściwość nawigacyjna do katalogu części

    public int RepairOrderId { get; set; } // FK do zlecenia nadrzędnego

    public RepairOrder? RepairOrder { get; set; } // Właściwość nawigacyjna

    /// <summary>
    /// Wartość wiersza kosztorysu (ilość * cena jednostkowa).
    /// Właściwość obliczana — nie jest zapisywana w bazie danych.
    /// </summary>
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>Wpis dziennika audytowego zlecenia (kto, co, kiedy).</summary>
/// <remarks>
/// RepairLogEntry jest tworzony przez wiele miejsc w kodzie:
/// - WorkshopFacade przy każdej operacji (zmiana statusu, dodanie części)
/// - AdvanceStageCommand przy przejściu etapu i cofnięciu (Undo)
/// - RepairOrderBuilder przy tworzeniu zlecenia
/// Dziennik pozwala odtworzyć pełną historię decyzji dla danego zlecenia.
/// </remarks>
public class RepairLogEntry
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now; // Czas zdarzenia (automatycznie przy tworzeniu)

    public string Message { get; set; } = ""; // Opis zdarzenia (co się stało i dlaczego)

    public int RepairOrderId { get; set; } // FK do zlecenia

    public RepairOrder? RepairOrder { get; set; } // Właściwość nawigacyjna
}

/// <summary>
/// Zlecenie naprawy — centralny agregat systemu.
/// Tworzone Builderem, posiada stan (State), etap (Command+Memento) i kosztorys (Decorator/Strategy).
/// </summary>
/// <remarks>
/// RepairOrder jest najważniejszą klasą w systemie — łączy wszystkie wzorce:
/// - Builder: tworzony przez RepairOrderBuilder krok po kroku
/// - State: Status zarządzany przez IRepairState / RepairStateFactory
/// - Command+Memento: Stage zmieniony przez AdvanceStageCommand z możliwością Undo
/// - Strategy+Decorator: Items i EstimatedHours używane przez PricingService
/// - Observer: zmiany Status powiadamiają subskrybentów przez RepairNotifier
/// - Facade: WorkshopFacade enkapsuluje operacje na zleceniu
///
/// Implementuje INotifyPropertyChanged ponieważ Status i Stage są zmieniane
/// przez wzorce i muszą być natychmiast widoczne w UI bez przeładowania.
/// </remarks>
public class RepairOrder : INotifyPropertyChanged
{
    // Zdarzenie powiadamiania UI — subskrybowane przez WPF Binding Engine.
    public event PropertyChangedEventHandler? PropertyChanged;

    // Prywatna metoda pomocnicza wywołująca zdarzenie.
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public int Id { get; set; } // Klucz główny (nadawany przez EF Core przy zapisie)

    public DateTime CreatedAt { get; set; } = DateTime.Now; // Data i czas przyjęcia zlecenia

    public string FaultDescription { get; set; } = ""; // Opis usterki zgłoszonej przez klienta

    public int VehicleId { get; set; } // FK do pojazdu

    public Vehicle? Vehicle { get; set; } // Właściwość nawigacyjna (z Customer wewnątrz)

    public int? MechanicId { get; set; } // FK do mechanika (nullable — przydzielany przez Mediator)

    public Mechanic? Mechanic { get; set; } // Nawigacja do mechanika

    public int? LiftId { get; set; } // FK do podnośnika (nullable — przydzielany przez Mediator)

    public Lift? Lift { get; set; } // Nawigacja do podnośnika

    public int? SpecialToolId { get; set; } // FK do narzędzia specjalnego (nullable — opcjonalne)

    public SpecialTool? SpecialTool { get; set; } // Nawigacja do narzędzia

    // Pole zapasowe dla Status — zmiana wymaga powiadamiania UI.
    private RepairStatus _status = RepairStatus.Przyjete;

    /// <summary>
    /// Bieżący status zlecenia (wzorzec State). Zmiana tylko przez WorkshopFacade
    /// lub AdvanceStageCommand — które sprawdzają dozwolone przejścia przez RepairStateFactory.
    /// Bezpośrednie ustawianie tego pola z pominięciem Factory jest błędem projektowym.
    /// </summary>
    public RepairStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return; // Guard — bez zbędnych powiadomień
            _status = value;
            OnPropertyChanged(); // Powiadom WPF — lista zleceń odświeży kolumnę "Status"
        }
    }

    // Pole zapasowe dla Stage — wzorzec identyczny jak Status.
    private RepairStage _stage = RepairStage.Diagnostyka;

    /// <summary>
    /// Bieżący etap naprawy (Command + Memento). Zmieniany przez AdvanceStageCommand,
    /// który przed zmianą zapisuje stan w RepairMemento umożliwiając Undo.
    /// </summary>
    public RepairStage Stage
    {
        get => _stage;
        set
        {
            if (_stage == value) return;
            _stage = value;
            OnPropertyChanged();
        }
    }

    // Szacowany czas pracy w godzinach — używany przez ILaborCostStrategy.
    public decimal EstimatedHours { get; set; }

    // Pozycje kosztorysu (części i robocizna) — dodawane przez Builder i WorkshopFacade.
    public List<RepairItem> Items { get; set; } = new();

    // Dziennik audytowy — każda operacja dopisuje wpis (WorkshopFacade, AdvanceStageCommand).
    public List<RepairLogEntry> Log { get; set; } = new();

    /// <summary>Suma pozycji kosztorysu (przed dopłatami Decoratora).</summary>
    /// <remarks>
    /// Właściwość obliczana — nie jest persystowana w bazie.
    /// PricingService używa jej jako podstawy do budowy potoku Decoratorów.
    /// </remarks>
    public decimal ItemsTotal => Items.Sum(i => i.LineTotal);

    // ToString wyświetlany w liście zleceń w UI.
    public override string ToString() => $"Zlecenie #{Id} – {Vehicle?.RegistrationNumber}";
}
