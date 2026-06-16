// Plik: RepairState.cs
// Rola: Implementacja wzorca State dla cyklu życia zlecenia naprawy.
//       Każdy status (enum RepairStatus) ma odpowiadający mu obiekt klasy stanu,
//       który przechowuje dozwolone przejścia — eliminując warunki if/switch w logice.
// Wzorzec: STATE (GoF) — obiekt zmienia swoje zachowanie gdy zmienia się jego wewnętrzny stan.
//          Tu: dozwolone przejścia zależą od bieżącego statusu.

using FixCars4Us.Core.Enums;   // RepairStatus — wartości enum identyfikujące stany
using FixCars4Us.Core.Models;  // (pośrednio — RepairOrder używa tych stanów)

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: State.
/// Zarządza cyklem życia zlecenia: Przyjete -&gt; WDiagnostyce -&gt; OczekiwanieNaCzesci
/// -&gt; WNaprawie -&gt; GotoweDoOdbioru. Każdy stan decyduje, jakie przejścia są dozwolone.
/// </summary>
/// <remarks>
/// Dlaczego wzorzec State zamiast switch/if?
/// Tradycyjne podejście wymagałoby rozbudowanego switch w ChangeStatus():
///   case Przyjete: if (target == WDiagnostyce || target == Anulowane) ...
/// Przy dodaniu nowego statusu trzeba by zmieniać centralną metodę.
/// Wzorzec State lokalizuje reguły przejścia w klasie stanu — zmiana reguł
/// dla jednego statusu nie dotyka pozostałych (zasada Open/Closed).
///
/// Context = RepairOrder (przechowuje Status), State = IRepairState,
/// ConcreteState = PrzyjeteState, WDiagnostyceState itd.
/// </remarks>
public interface IRepairState
{
    /// <summary>Wartość enum identyfikująca ten stan — dla porównania i raportowania.</summary>
    RepairStatus Status { get; }

    /// <summary>Dozwolone następne statusy z bieżącego stanu.</summary>
    /// <remarks>
    /// IReadOnlyList gwarantuje, że zewnętrzny kod nie doda ani nie usunie przejść
    /// z tej listy po stworzeniu obiektu stanu (immutability by design).
    /// </remarks>
    IReadOnlyList<RepairStatus> AllowedNext { get; }

    /// <summary>
    /// Sprawdza czy przejście do podanego statusu jest dozwolone.
    /// Domyślna implementacja metody interfejsu (C# 8+) — zawiera logikę, ale
    /// klasy implementujące mogą ją nadpisać (tu nie ma takiej potrzeby).
    /// </summary>
    bool CanTransitionTo(RepairStatus target) => AllowedNext.Contains(target);
}

// =====================================================================
// Konkretne klasy stanów — każda odpowiada jednemu enum RepairStatus.
// Wzorzec: "ConcreteState" w terminologii GoF.
// =====================================================================

/// <summary>
/// Stan "Przyjęte" — zlecenie właśnie trafiło do warsztatu.
/// Dozwolone przejścia: można rozpocząć diagnostykę lub anulować zlecenie.
/// </summary>
public class PrzyjeteState : IRepairState
{
    public RepairStatus Status => RepairStatus.Przyjete;

    // Zlecenie przyjęte może przejść tylko do diagnostyki lub być anulowane.
    // Nie można pominąć diagnostyki i od razu naprawiać.
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.WDiagnostyce, RepairStatus.Anulowane };
}

/// <summary>
/// Stan "W Diagnostyce" — trwa badanie usterki.
/// Można przejść do zamawiania części (jeśli brakuje), do naprawy (jeśli części są)
/// lub anulować (klient rezygnuje po diagnostyce).
/// </summary>
public class WDiagnostyceState : IRepairState
{
    public RepairStatus Status => RepairStatus.WDiagnostyce;

    // Elastyczność: można pominąć OczekiwanieNaCzesci jeśli części są na stanie.
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.OczekiwanieNaCzesci, RepairStatus.WNaprawie, RepairStatus.Anulowane };
}

/// <summary>
/// Stan "Oczekiwanie na Części" — diagnostyka zakończona, czekamy na dostawę.
/// Dozwolone: przejście do naprawy po dostawie lub anulowanie.
/// </summary>
public class OczekiwanieNaCzesciState : IRepairState
{
    public RepairStatus Status => RepairStatus.OczekiwanieNaCzesci;

    // W tym stanie nie można wrócić do diagnostyki — byłoby to cofnięcie procesu.
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.WNaprawie, RepairStatus.Anulowane };
}

/// <summary>
/// Stan "W Naprawie" — trwają właściwe prace mechaniczne.
/// Jedyne dozwolone przejście: gotowe do odbioru (nie ma drogi wstecz).
/// </summary>
public class WNaprawieState : IRepairState
{
    public RepairStatus Status => RepairStatus.WNaprawie;

    // Brak możliwości anulowania gdy naprawa trwa — można tylko dokończyć.
    // (W prawdziwym systemie można by dodać "WstrzymanaNaprawa" jako stan pośredni.)
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.GotoweDoOdbioru };
}

/// <summary>
/// Stan "Gotowe do Odbioru" — naprawa zakończona, klient informowany.
/// Jedyne dozwolone przejście: Zakonczone (klient odebrał pojazd).
/// </summary>
public class GotoweDoOdbioruState : IRepairState
{
    public RepairStatus Status => RepairStatus.GotoweDoOdbioru;

    // Po tym statusie zlecenie jest archiwizowane przez WorkshopFacade.RecordCompletion().
    public IReadOnlyList<RepairStatus> AllowedNext { get; } = new[] { RepairStatus.Zakonczone };
}

/// <summary>
/// Stan "Anulowane" — zlecenie zakończone bez wykonania naprawy.
/// Stan terminalny: brak możliwych przejść.
/// </summary>
public class AnulowaneState : IRepairState
{
    public RepairStatus Status => RepairStatus.Anulowane;

    // Array.Empty<T>() zamiast new T[0] — brak alokacji (optymalizacja, pusta tablica współdzielona).
    public IReadOnlyList<RepairStatus> AllowedNext { get; } = Array.Empty<RepairStatus>();
}

/// <summary>
/// Stan "Zakończone" — pojazd odebrany, zlecenie archiwalne.
/// Stan terminalny: brak możliwych przejść.
/// </summary>
public class ZakonczoneState : IRepairState
{
    public RepairStatus Status => RepairStatus.Zakonczone;

    // Stan terminalny — zlecenie znika z listy aktywnych (filtrowane w ReloadOrders).
    public IReadOnlyList<RepairStatus> AllowedNext { get; } = Array.Empty<RepairStatus>();
}

// =====================================================================
// Fabryka stanów — element uzupełniający wzorzec State.
// Wzorzec pomocniczy: FACTORY METHOD (GoF) — hermetyzuje tworzenie obiektów.
// =====================================================================

/// <summary>Fabryka stanów — mapuje wartość enum na obiekt stanu.</summary>
/// <remarks>
/// Dlaczego fabryka? Wywołujący (np. WorkshopFacade) nie powinien znać
/// klas stanów (PrzyjeteState itd.) — to szczegół implementacyjny.
/// Wystarczy przekazać enum RepairStatus i dostać gotowy IRepairState.
/// Wzorzec Factory Method: tworzy obiekt bez ujawniania klasy konkretnej.
///
/// "switch expression" (C# 8+) — czytelna alternatywa dla łańcucha if-else.
/// Kompilator ostrzeże o brakującym przypadku — bezpieczniejsze niż switch z default.
/// </remarks>
public static class RepairStateFactory
{
    /// <summary>
    /// Zwraca obiekt stanu odpowiadający podanej wartości enum.
    /// Rzuca wyjątek dla nieobsługiwanej wartości — fail fast.
    /// </summary>
    public static IRepairState Create(RepairStatus status) => status switch
    {
        RepairStatus.Przyjete              => new PrzyjeteState(),              // Nowe zlecenie
        RepairStatus.WDiagnostyce          => new WDiagnostyceState(),          // Diagnoza w toku
        RepairStatus.OczekiwanieNaCzesci   => new OczekiwanieNaCzesciState(),   // Czekamy na dostawę
        RepairStatus.WNaprawie             => new WNaprawieState(),             // Naprawa w toku
        RepairStatus.GotoweDoOdbioru       => new GotoweDoOdbioruState(),       // Gotowe, czeka na odbiór
        RepairStatus.Anulowane             => new AnulowaneState(),             // Stan terminalny
        RepairStatus.Zakonczone            => new ZakonczoneState(),            // Stan terminalny
        _ => throw new ArgumentOutOfRangeException(nameof(status))             // Nieznana wartość enum
    };
}
