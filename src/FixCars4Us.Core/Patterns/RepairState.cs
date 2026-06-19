// Wzorzec: State — kontrola przejść statusu zlecenia naprawy.

using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>Stan zlecenia (State) — decyduje jakie przejścia są dozwolone.</summary>
public interface IRepairState
{
    /// <summary>Wartość enum identyfikująca ten stan.</summary>
    RepairStatus Status { get; }

    /// <summary>Dozwolone następne statusy z bieżącego stanu.</summary>
    IReadOnlyList<RepairStatus> AllowedNext { get; }

    /// <summary>Sprawdza czy przejście do podanego statusu jest dozwolone.</summary>
    bool CanTransitionTo(RepairStatus target) => AllowedNext.Contains(target);
}

/// <summary>Stan "Przyjęte" — zlecenie właśnie trafiło do warsztatu.</summary>
public class PrzyjeteState : IRepairState
{
    public RepairStatus Status => RepairStatus.Przyjete;

    // Nie można pominąć diagnostyki i od razu naprawiać.
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.WDiagnostyce, RepairStatus.Anulowane };
}

/// <summary>Stan "W Diagnostyce" — trwa badanie usterki.</summary>
public class WDiagnostyceState : IRepairState
{
    public RepairStatus Status => RepairStatus.WDiagnostyce;

    // Można pominąć OczekiwanieNaCzesci, jeśli części są na stanie.
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.OczekiwanieNaCzesci, RepairStatus.WNaprawie, RepairStatus.Anulowane };
}

/// <summary>Stan "Oczekiwanie na Części" — diagnostyka zakończona, czekamy na dostawę.</summary>
public class OczekiwanieNaCzesciState : IRepairState
{
    public RepairStatus Status => RepairStatus.OczekiwanieNaCzesci;

    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.WNaprawie, RepairStatus.Anulowane };
}

/// <summary>Stan "W Naprawie" — trwają prace mechaniczne.</summary>
public class WNaprawieState : IRepairState
{
    public RepairStatus Status => RepairStatus.WNaprawie;

    // Brak możliwości anulowania w trakcie naprawy.
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.GotoweDoOdbioru };
}

/// <summary>Stan "Gotowe do Odbioru" — naprawa zakończona, klient informowany.</summary>
public class GotoweDoOdbioruState : IRepairState
{
    public RepairStatus Status => RepairStatus.GotoweDoOdbioru;

    public IReadOnlyList<RepairStatus> AllowedNext { get; } = new[] { RepairStatus.Zakonczone };
}

/// <summary>Stan "Anulowane" — zlecenie zakończone bez naprawy. Terminalny.</summary>
public class AnulowaneState : IRepairState
{
    public RepairStatus Status => RepairStatus.Anulowane;

    public IReadOnlyList<RepairStatus> AllowedNext { get; } = Array.Empty<RepairStatus>();
}

/// <summary>Stan "Zakończone" — pojazd odebrany. Terminalny.</summary>
public class ZakonczoneState : IRepairState
{
    public RepairStatus Status => RepairStatus.Zakonczone;

    public IReadOnlyList<RepairStatus> AllowedNext { get; } = Array.Empty<RepairStatus>();
}

// Fabryka stanów — Factory Method, mapuje enum na obiekt stanu.

/// <summary>Fabryka stanów — mapuje wartość enum na obiekt stanu.</summary>
public static class RepairStateFactory
{
    public static IRepairState Create(RepairStatus status) => status switch
    {
        RepairStatus.Przyjete              => new PrzyjeteState(),
        RepairStatus.WDiagnostyce          => new WDiagnostyceState(),
        RepairStatus.OczekiwanieNaCzesci   => new OczekiwanieNaCzesciState(),
        RepairStatus.WNaprawie             => new WNaprawieState(),
        RepairStatus.GotoweDoOdbioru       => new GotoweDoOdbioruState(),
        RepairStatus.Anulowane             => new AnulowaneState(),
        RepairStatus.Zakonczone            => new ZakonczoneState(),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
