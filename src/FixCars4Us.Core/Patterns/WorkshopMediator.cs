// Plik: WorkshopMediator.cs
// Rola: Inteligentne przypisywanie zasobów warsztatowych (mechanik, podnośnik, narzędzie)
//       z uwzględnieniem ich dostępności czasowej. Eliminuje konflikty rezerwacji.
// Wzorzec: MEDIATOR (GoF) — obiekt pośredniczący koordynujący złożone interakcje
//          między wieloma typami obiektów (mechanicy, podnośniki, narzędzia).
//          Mediator = WorkshopMediator, Colleagues = Mechanic, Lift, SpecialTool.

using FixCars4Us.Core.Enums;   // MechanicSpecialization, LiftType
using FixCars4Us.Core.Models;  // Mechanic, Lift, SpecialTool

namespace FixCars4Us.Core.Patterns;

/// <summary>Żądanie zaplanowania naprawy w określonym oknie czasowym.</summary>
/// <remarks>
/// Record (C# 9+) — immutowalny typ wartościowy z automatycznym Equals i ToString.
/// Parametry żądania:
/// - RequiredSpecialization: jaki mechanik jest potrzebny (np. Elektryka)
/// - RequiredLiftType: jaki podnośnik (np. Ciezarowy)
/// - RequiredCapacityKg: minimalny udźwig podnośnika
/// - RequiresDiagnosticTool: czy potrzebny komputer diagnostyczny
/// - Start/End: okno czasowe rezerwacji
/// </remarks>
public record ResourceRequest(
    MechanicSpecialization RequiredSpecialization, // Wymagana specjalizacja mechanika
    LiftType RequiredLiftType,                     // Wymagany typ podnośnika
    int RequiredCapacityKg,                        // Minimalny udźwig podnośnika w kg
    bool RequiresDiagnosticTool,                   // Czy wymagane narzędzie specjalistyczne
    DateTime Start,                                // Początek rezerwacji
    DateTime End);                                 // Koniec rezerwacji

/// <summary>Wynik przydziału zasobów przez Mediatora.</summary>
/// <remarks>
/// Record z parametrami opcjonalnymi — Mechanic, Lift, Tool są null gdy Success == false.
/// Wywołujący (CreateOrder) sprawdza Success przed użyciem przydzielonych zasobów.
/// Message zawiera czytelny komunikat dla użytkownika (co się udało lub co blokuje).
/// </remarks>
public record ResourceAllocation(
    bool Success,           // Czy udało się przydzielić wszystkie zasoby
    string Message,         // Komunikat dla UI (sukces lub opis problemu)
    Mechanic? Mechanic = null,    // Przydzielony mechanik (null jeśli nie udało się)
    Lift? Lift = null,            // Przydzielony podnośnik (null jeśli nie udało się)
    SpecialTool? Tool = null);    // Przydzielone narzędzie (null jeśli nie wymagane lub nie udało się)

/// <summary>Zajętość zasobu w danym przedziale czasu.</summary>
/// <remarks>
/// Internal record — używany tylko wewnątrz Mediatora, niewidoczny na zewnątrz.
/// Przechowuje: Id zasobu (mechanika/podnośnika/narzędzia), start i koniec rezerwacji.
/// Bookings są trzymane w pamięci (nie persystowane) — przy restarcie aplikacji
/// Mediator zaczyna od czystego stanu. To celowe uproszczenie.
/// </remarks>
internal record Booking(int ResourceId, DateTime Start, DateTime End);

/// <summary>
/// WZORZEC: Mediator (INTELIGENTNE PRZYPISYWANIE ZASOBÓW WARSZTATOWYCH).
/// Koordynuje jednoczesną dostępność trzech typów zasobów — mechanika o właściwej
/// specjalizacji, wolnego podnośnika o wystarczającym udźwigu oraz narzędzia
/// specjalistycznego — w zazębiających się przedziałach czasowych. Uniemożliwia
/// zaplanowanie naprawy, jeśli choć jeden z wymaganych zasobów jest niedostępny.
/// </summary>
/// <remarks>
/// Bez Mediatora: ViewModel musiałby samodzielnie przeszukiwać mechaników,
/// podnośniki i narzędzia, sprawdzać ich dostępność i zarządzać rezerwacjami.
/// To byłby "spaghetti code" z ViewModelem wiedającym o szczegółach każdego zasobu.
///
/// Z Mediatorem: ViewModel wywołuje TryAllocate(request) i dostaje wynik.
/// Cała logika koordynacji jest w jednym miejscu — łatwa do testowania i zmiany.
///
/// Trzy oddzielne listy bookingów (mechanicy, podnośniki, narzędzia) zamiast jednej —
/// bo każdy typ zasobu ma inne atrybuty i reguły dostępności.
/// </remarks>
public class WorkshopMediator
{
    // Niezmienne listy dostępnych zasobów — tworzone raz w konstruktorze.
    private readonly IReadOnlyList<Mechanic> _mechanics;    // Wszyscy mechanicy w warsztacie
    private readonly IReadOnlyList<Lift> _lifts;            // Wszystkie podnośniki
    private readonly IReadOnlyList<SpecialTool> _tools;     // Wszystkie narzędzia specjalistyczne

    // Mutowalne listy aktywnych rezerwacji — modyfikowane przez TryAllocate i Release.
    private readonly List<Booking> _mechanicBookings = new(); // Rezerwacje mechaników
    private readonly List<Booking> _liftBookings = new();     // Rezerwacje podnośników
    private readonly List<Booking> _toolBookings = new();     // Rezerwacje narzędzi

    /// <summary>
    /// Konstruktor — wstrzykuje dostępne zasoby przez kolekcje.
    /// ToList() tworzy kopię — Mediator nie jest zależny od zmian oryginalnej kolekcji.
    /// </summary>
    public WorkshopMediator(IEnumerable<Mechanic> mechanics, IEnumerable<Lift> lifts, IEnumerable<SpecialTool> tools)
    {
        _mechanics = mechanics.ToList(); // Kopia listy mechaników
        _lifts = lifts.ToList();         // Kopia listy podnośników
        _tools = tools.ToList();         // Kopia listy narzędzi
    }

    /// <summary>
    /// Sprawdza czy rezerwacja b zachodzi w czasie z przedziałem [start, end).
    /// Warunek: start &lt; b.End AND b.Start &lt; end (klasyczny test zachodzenia przedziałów).
    /// Przykład: b=[10:00-12:00], [start=11:00, end=13:00] => zachodzi (11 &lt; 12 i 10 &lt; 13).
    /// </summary>
    private static bool Overlaps(Booking b, DateTime start, DateTime end)
        => start < b.End && b.Start < end; // Zachodzenie przedziałów otwartych

    /// <summary>
    /// Sprawdza czy zasób o podanym Id jest wolny w przedziale [start, end).
    /// Zwraca true gdy żadna istniejąca rezerwacja tego zasobu nie zachodzi z podanym przedziałem.
    /// </summary>
    private bool IsFree(List<Booking> bookings, int resourceId, DateTime start, DateTime end)
        // LINQ Any: szuka PIERWSZEJ rezerwacji która (a) dotyczy tego zasobu I (b) zachodzi.
        // Negacja Any() = "nie ma żadnej kolidującej rezerwacji" = zasób jest wolny.
        => !bookings.Any(b => b.ResourceId == resourceId && Overlaps(b, start, end));

    /// <summary>Próbuje przydzielić komplet zasobów. Rezerwuje je tylko gdy wszystkie są dostępne.</summary>
    /// <remarks>
    /// Algorytm działania:
    /// 1. Znajdź pierwszego wolnego mechanika o wymaganej specjalizacji.
    /// 2. Znajdź pierwszy wolny podnośnik o wymaganym typie i udźwigu.
    /// 3. Opcjonalnie: znajdź pierwsze wolne narzędzie.
    /// 4. Jeśli wszystko dostępne — zarezerwuj atomowo (wszystkie trzy naraz).
    /// 5. Jeśli cokolwiek niedostępne — zwróć błąd BEZ żadnej rezerwacji (brak częściowych rezerwacji).
    ///
    /// "Atomowość" jest ważna: nie chcemy sytuacji gdy mechanik jest zarezerwowany,
    /// ale podnośnik nie jest dostępny — wtedy mechanik byłby zablokowany na nic.
    /// </remarks>
    public ResourceAllocation TryAllocate(ResourceRequest req)
    {
        // KROK 1: Szukaj wolnego mechanika o wymaganej specjalizacji.
        var mechanic = _mechanics.FirstOrDefault(m =>
            m.Specialization == req.RequiredSpecialization &&        // Odpowiednia specjalizacja?
            IsFree(_mechanicBookings, m.Id, req.Start, req.End));    // Wolny w tym czasie?

        // Fail fast: jeśli mechanik niedostępny, nie szukaj dalej.
        if (mechanic is null)
            return new ResourceAllocation(false,
                $"Brak wolnego mechanika o specjalizacji {req.RequiredSpecialization} w wybranym terminie.");

        // KROK 2: Szukaj wolnego podnośnika o wymaganym typie i minimalnym udźwigu.
        var lift = _lifts.FirstOrDefault(l =>
            l.Type == req.RequiredLiftType &&                       // Wymagany typ?
            l.CapacityKg >= req.RequiredCapacityKg &&              // Wystarczający udźwig?
            IsFree(_liftBookings, l.Id, req.Start, req.End));      // Wolny w tym czasie?

        // Fail fast: podnośnik niedostępny.
        if (lift is null)
            return new ResourceAllocation(false,
                $"Wszystkie podnośniki typu {req.RequiredLiftType} o udźwigu >= {req.RequiredCapacityKg} kg są zajęte.");

        // KROK 3: Opcjonalnie szukaj narzędzia specjalistycznego.
        SpecialTool? tool = null; // Domyślnie: narzędzie nie jest wymagane
        if (req.RequiresDiagnosticTool) // Tylko jeśli żądanie wymaga narzędzia
        {
            // Znajdź pierwsze wolne narzędzie (nie sprawdzamy typu — każde narzędzie wystarczy).
            tool = _tools.FirstOrDefault(t => IsFree(_toolBookings, t.Id, req.Start, req.End));
            if (tool is null) // Wszystkie narzędzia zajęte
                return new ResourceAllocation(false, "Brak wolnego narzędzia specjalistycznego w wybranym terminie.");
        }

        // KROK 4: Wszystkie zasoby dostępne — rezerwujemy atomowo (wszystkie lub żaden).
        _mechanicBookings.Add(new Booking(mechanic.Id, req.Start, req.End)); // Zarezerwuj mechanika
        _liftBookings.Add(new Booking(lift.Id, req.Start, req.End));          // Zarezerwuj podnośnik
        if (tool is not null) _toolBookings.Add(new Booking(tool.Id, req.Start, req.End)); // Zarezerwuj narzędzie (jeśli przydzielone)

        // Zwróć sukces z referencjami do przydzielonych zasobów (ViewModelBuilder ich potrzebuje).
        return new ResourceAllocation(true,
            $"Przydzielono: {mechanic.Name}, {lift.Name}" + (tool is not null ? $", {tool.Name}." : "."),
            mechanic, lift, tool);
    }

    /// <summary>Zwalnia rezerwacje danego zasobu w przedziale (np. przy anulowaniu naprawy).</summary>
    /// <remarks>
    /// RemoveAll usuwa wszystkie pasujące elementy z listy w jednym przejściu (O(n)).
    /// Warunek dopasowania: ten sam Id zasobu + dokładnie te same czasy.
    /// Jeśli zasób był null (narzędzie niewymagane), RemoveAll po prostu nic nie usuwa.
    /// </remarks>
    public void Release(int? mechanicId, int? liftId, int? toolId, DateTime start, DateTime end)
    {
        // Usuń rezerwację mechanika (jeśli mechanicId nie jest null i pasuje do bookingu).
        _mechanicBookings.RemoveAll(b => b.ResourceId == mechanicId && b.Start == start && b.End == end);

        // Usuń rezerwację podnośnika.
        _liftBookings.RemoveAll(b => b.ResourceId == liftId && b.Start == start && b.End == end);

        // Usuń rezerwację narzędzia (bezpieczne gdy toolId == null — żaden booking nie pasuje).
        _toolBookings.RemoveAll(b => b.ResourceId == toolId && b.Start == start && b.End == end);
    }
}
