using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: Memento.
/// Zapamiętuje pełny stan zlecenia naprawy (etap, status, stany magazynowe
/// powiązanych części) sprzed operacji, aby umożliwić bezpieczne cofnięcie.
/// </summary>
public class RepairMemento
{
    public RepairStage Stage { get; }
    public RepairStatus Status { get; }
    public decimal EstimatedHours { get; }
    /// <summary>Migawka stanów magazynowych: PartId -> ilość.</summary>
    public IReadOnlyDictionary<int, int> PartStockSnapshot { get; }
    public string Note { get; }

    public RepairMemento(RepairStage stage, RepairStatus status, decimal hours,
        IReadOnlyDictionary<int, int> partStock, string note)
    {
        Stage = stage;
        Status = status;
        EstimatedHours = hours;
        PartStockSnapshot = partStock;
        Note = note;
    }
}

/// <summary>
/// WZORZEC: Command.
/// Operacja wykonywana na zleceniu, którą można wykonać i cofnąć (undo).
/// </summary>
public interface IRepairCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Komenda przejścia do kolejnego etapu naprawy. Przy przejściu do etapu
/// "PraceWlasciwe" pobiera części z magazynu; Undo przywraca stany magazynowe
/// i poprzedni etap dzięki Memento.
/// </summary>
public class AdvanceStageCommand : IRepairCommand
{
    private readonly RepairOrder _order;
    private readonly RepairStage _targetStage;
    private readonly IReadOnlyList<Part> _allParts;
    private RepairMemento? _memento;

    public string Description => $"Etap -> {_targetStage}";

    public AdvanceStageCommand(RepairOrder order, RepairStage targetStage, IReadOnlyList<Part> allParts)
    {
        _order = order;
        _targetStage = targetStage;
        _allParts = allParts;
    }

    private RepairMemento Capture(string note)
    {
        var snapshot = _allParts.ToDictionary(p => p.Id, p => p.StockQuantity);
        return new RepairMemento(_order.Stage, _order.Status, _order.EstimatedHours, snapshot, note);
    }

    /// <summary>Mapowanie etapu naprawy na odpowiadający mu status zlecenia (State).</summary>
    private static RepairStatus? MapStageToStatus(RepairStage stage) => stage switch
    {
        RepairStage.Diagnostyka => RepairStatus.WDiagnostyce,
        RepairStage.ZamawianieCzesci => RepairStatus.OczekiwanieNaCzesci,
        RepairStage.PraceWlasciwe => RepairStatus.WNaprawie,
        RepairStage.KontrolaJakosci => RepairStatus.GotoweDoOdbioru,
        _ => null
    };

    public void Execute()
    {
        _memento = Capture($"Przed przejściem do {_targetStage}");

        _order.Stage = _targetStage;
        _order.Log.Add(new RepairLogEntry { Message = $"Przejście do etapu: {_targetStage}." });

        // Automatyczna zmiana statusu (State) na podstawie etapu, jeśli przejście jest dozwolone.
        var mappedStatus = MapStageToStatus(_targetStage);
        if (mappedStatus is not null && mappedStatus != _order.Status)
        {
            var state = RepairStateFactory.Create(_order.Status);
            if (state.CanTransitionTo(mappedStatus.Value))
            {
                _order.Status = mappedStatus.Value;
                _order.Log.Add(new RepairLogEntry { Message = $"Status automatycznie zmieniony na: {mappedStatus.Value}." });
            }
        }
    }

    public void Undo()
    {
        if (_memento is null) return;

        // Przywróć stany magazynowe ze zrzutu (Memento).
        foreach (var kv in _memento.PartStockSnapshot)
        {
            var part = _allParts.FirstOrDefault(p => p.Id == kv.Key);
            if (part is not null) part.StockQuantity = kv.Value;
        }

        _order.Stage = _memento.Stage;
        _order.Status = _memento.Status;
        _order.EstimatedHours = _memento.EstimatedHours;
        _order.Log.Add(new RepairLogEntry { Message = $"COFNIĘTO etap (przywrócono: {_memento.Note})." });
    }
}

/// <summary>
/// Caretaker — zarządza historią wykonanych komend i pozwala cofać je w poprawnej
/// kolejności (LIFO), zachowując pełny ślad rewizyjny.
/// </summary>
public class RepairHistory
{
    private readonly Stack<IRepairCommand> _executed = new();

    public bool CanUndo => _executed.Count > 0;
    public int Count => _executed.Count;

    public void Do(IRepairCommand command)
    {
        command.Execute();
        _executed.Push(command);
    }

    public string Undo()
    {
        if (!CanUndo) return "Brak operacji do cofnięcia.";
        var cmd = _executed.Pop();
        cmd.Undo();
        return $"Cofnięto: {cmd.Description}";
    }

    public IEnumerable<string> DescribeHistory() => _executed.Reverse().Select(c => c.Description);
}
