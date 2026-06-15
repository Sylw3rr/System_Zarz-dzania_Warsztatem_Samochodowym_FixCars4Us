using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: State.
/// Zarządza cyklem życia zlecenia: Przyjete -> WDiagnostyce -> OczekiwanieNaCzesci
/// -> WNaprawie -> GotoweDoOdbioru. Każdy stan decyduje, jakie przejścia są dozwolone.
/// </summary>
public interface IRepairState
{
    RepairStatus Status { get; }
    /// <summary>Dozwolone następne statusy z bieżącego stanu.</summary>
    IReadOnlyList<RepairStatus> AllowedNext { get; }
    bool CanTransitionTo(RepairStatus target) => AllowedNext.Contains(target);
}

public class PrzyjeteState : IRepairState
{
    public RepairStatus Status => RepairStatus.Przyjete;
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.WDiagnostyce, RepairStatus.Anulowane };
}

public class WDiagnostyceState : IRepairState
{
    public RepairStatus Status => RepairStatus.WDiagnostyce;
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.OczekiwanieNaCzesci, RepairStatus.WNaprawie, RepairStatus.Anulowane };
}

public class OczekiwanieNaCzesciState : IRepairState
{
    public RepairStatus Status => RepairStatus.OczekiwanieNaCzesci;
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.WNaprawie, RepairStatus.Anulowane };
}

public class WNaprawieState : IRepairState
{
    public RepairStatus Status => RepairStatus.WNaprawie;
    public IReadOnlyList<RepairStatus> AllowedNext { get; } =
        new[] { RepairStatus.GotoweDoOdbioru };
}

public class GotoweDoOdbioruState : IRepairState
{
    public RepairStatus Status => RepairStatus.GotoweDoOdbioru;
    public IReadOnlyList<RepairStatus> AllowedNext { get; } = new[] { RepairStatus.Zakonczone };
}

public class AnulowaneState : IRepairState
{
    public RepairStatus Status => RepairStatus.Anulowane;
    public IReadOnlyList<RepairStatus> AllowedNext { get; } = Array.Empty<RepairStatus>();
}

public class ZakonczoneState : IRepairState
{
    public RepairStatus Status => RepairStatus.Zakonczone;
    public IReadOnlyList<RepairStatus> AllowedNext { get; } = Array.Empty<RepairStatus>();
}

/// <summary>Fabryka stanów — mapuje wartość enum na obiekt stanu.</summary>
public static class RepairStateFactory
{
    public static IRepairState Create(RepairStatus status) => status switch
    {
        RepairStatus.Przyjete => new PrzyjeteState(),
        RepairStatus.WDiagnostyce => new WDiagnostyceState(),
        RepairStatus.OczekiwanieNaCzesci => new OczekiwanieNaCzesciState(),
        RepairStatus.WNaprawie => new WNaprawieState(),
        RepairStatus.GotoweDoOdbioru => new GotoweDoOdbioruState(),
        RepairStatus.Anulowane => new AnulowaneState(),
        RepairStatus.Zakonczone => new ZakonczoneState(),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
