// Plik: CommandMemento.cs
// Rola: Implementacja funkcji cofania operacji etapowych (Undo) na zleceniu naprawy.
//       Łączy dwa wzorce GoF:
//       - COMMAND: enkapsuluje operację zmiany etapu jako obiekt z metodami Execute/Undo
//       - MEMENTO: zapamiętuje stan zlecenia przed operacją aby umożliwić cofnięcie
// Wzorzec: COMMAND + MEMENTO (GoF).
//          Command: Invoker = RepairHistory, Command = AdvanceStageCommand,
//                   Receiver = RepairOrder + lista Part (stany magazynowe)
//          Memento: Originator = RepairOrder, Memento = RepairMemento,
//                   Caretaker = RepairHistory

using FixCars4Us.Core.Enums;   // RepairStage, RepairStatus
using FixCars4Us.Core.Models;  // RepairOrder, Part, RepairLogEntry

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: Memento.
/// Zapamiętuje pełny stan zlecenia naprawy (etap, status, stany magazynowe
/// powiązanych części) sprzed operacji, aby umożliwić bezpieczne cofnięcie.
/// </summary>
/// <remarks>
/// Memento jest "zrzutem ekranu" stanu obiektu w danym momencie.
/// Tylko klasa Originator (tu: AdvanceStageCommand w imieniu RepairOrder)
/// powinna tworzyć i odczytywać Memento — Caretaker (RepairHistory) tylko je przechowuje.
///
/// Dlaczego przechowujemy stany magazynowe? Przejście do etapu PraceWlasciwe
/// może automatycznie pobierać części z magazynu. Undo musi przywrócić stany
/// sprzed pobrania — inaczej cofnięcie zmieniłoby etap ale nie oddałoby części.
///
/// Właściwości są readonly — Memento jest immutowalne (nie można go zmodyfikować po stworzeniu).
/// </remarks>
public class RepairMemento
{
    /// <summary>Etap naprawy w chwili zrzutu (przed operacją).</summary>
    public RepairStage Stage { get; }

    /// <summary>Status zlecenia w chwili zrzutu (przed automatyczną zmianą przez State).</summary>
    public RepairStatus Status { get; }

    /// <summary>Szacowany czas pracy w chwili zrzutu.</summary>
    public decimal EstimatedHours { get; }

    /// <summary>Migawka stanów magazynowych: PartId -&gt; ilość.</summary>
    /// <remarks>
    /// Dictionary zapewnia O(1) dostęp do stanu danej części przy przywracaniu.
    /// IReadOnlyDictionary — Caretaker nie może modyfikować migawki.
    /// </remarks>
    public IReadOnlyDictionary<int, int> PartStockSnapshot { get; }

    /// <summary>Opis kontekstu zrzutu (np. "Przed przejściem do PraceWlasciwe").</summary>
    public string Note { get; }

    /// <summary>
    /// Konstruktor — jedyne miejsce gdzie Memento może być wypełnione danymi.
    /// Po stworzeniu obiekt jest immutowalny.
    /// </summary>
    public RepairMemento(RepairStage stage, RepairStatus status, decimal hours,
        IReadOnlyDictionary<int, int> partStock, string note)
    {
        Stage = stage;                    // Zapamiętaj etap
        Status = status;                  // Zapamiętaj status
        EstimatedHours = hours;           // Zapamiętaj szacowany czas
        PartStockSnapshot = partStock;    // Zapamiętaj stany magazynowe (kopia!)
        Note = note;                      // Zapamiętaj opis dla logu
    }
}

/// <summary>
/// WZORZEC: Command.
/// Operacja wykonywana na zleceniu, którą można wykonać i cofnąć (undo).
/// </summary>
/// <remarks>
/// Każda operacja musi mieć opis (Description) czytelny dla użytkownika,
/// metodę Execute (akcja) i metodę Undo (cofnięcie). Historia komend
/// (RepairHistory) trzyma listę wykonanych IRepairCommand — można cofać
/// w kolejności LIFO (jak stos operacji Ctrl+Z w edytorze tekstu).
/// </remarks>
public interface IRepairCommand
{
    /// <summary>Krótki opis operacji dla UI i logu audytowego.</summary>
    string Description { get; }

    /// <summary>Wykonaj operację.</summary>
    void Execute();

    /// <summary>Cofnij operację (przywróć stan sprzed Execute).</summary>
    void Undo();
}

/// <summary>
/// Komenda przejścia do kolejnego etapu naprawy. Przy przejściu do etapu
/// "PraceWlasciwe" pobiera części z magazynu; Undo przywraca stany magazynowe
/// i poprzedni etap dzięki Memento.
/// </summary>
/// <remarks>
/// Klasa realizuje jednocześnie wzorzec Command (Execute/Undo) i korzysta
/// z Memento (Capture/Undo przez migawkę) oraz State (mapowanie etap->status
/// i sprawdzanie dozwolonych przejść przez RepairStateFactory).
///
/// Pola:
/// - _order: Receiver — obiekt na którym operacja jest wykonywana
/// - _targetStage: cel przejścia
/// - _allParts: potrzebne do migawki stanów magazynowych
/// - _memento: zrzut stanu (wypełniany przez Execute, używany przez Undo)
/// </remarks>
public class AdvanceStageCommand : IRepairCommand
{
    private readonly RepairOrder _order;           // Zlecenie — odbiorca operacji
    private readonly RepairStage _targetStage;     // Docelowy etap
    private readonly IReadOnlyList<Part> _allParts; // Wszystkie części (potrzebne do migawki)
    private RepairMemento? _memento;               // Migawka stanu — tworzona przy Execute, używana przy Undo

    /// <summary>Opis komendy wyświetlany w logu i historii operacji.</summary>
    public string Description => $"Etap -> {_targetStage}";

    /// <summary>
    /// Konstruktor komendy — DI (Dependency Injection) przez parametry.
    /// Wszystkie zależności są wstrzykiwane, komenda nie tworzy nic samodzielnie.
    /// </summary>
    public AdvanceStageCommand(RepairOrder order, RepairStage targetStage, IReadOnlyList<Part> allParts)
    {
        _order = order;            // Zapamietaj odbiorcę
        _targetStage = targetStage; // Zapamietaj cel
        _allParts = allParts;      // Zapamietaj listę części dla migawki
    }

    /// <summary>
    /// Tworzy migawkę bieżącego stanu zlecenia i magazynu (Memento).
    /// Wywoływana przez Execute przed wprowadzeniem jakichkolwiek zmian.
    /// </summary>
    private RepairMemento Capture(string note)
    {
        // ToDictionary tworzy kopię słownika Id -> StockQuantity dla WSZYSTKICH części.
        // Kopia jest ważna — gdybyśmy trzymali referencję, migawka zmieniałaby się razem z danymi.
        var snapshot = _allParts.ToDictionary(p => p.Id, p => p.StockQuantity);
        return new RepairMemento(_order.Stage, _order.Status, _order.EstimatedHours, snapshot, note);
    }

    /// <summary>Mapowanie etapu naprawy na odpowiadający mu status zlecenia (State).</summary>
    /// <remarks>
    /// Powiązanie między granularnym etapem (Stage) a statusem (Status) widocznym dla klienta.
    /// Zwraca null jeśli etap nie ma odpowiadającego statusu (nie powinno się zdarzyć).
    /// </remarks>
    private static RepairStatus? MapStageToStatus(RepairStage stage) => stage switch
    {
        RepairStage.Diagnostyka       => RepairStatus.WDiagnostyce,         // Rozpoczęto diagnostykę
        RepairStage.ZamawianieCzesci  => RepairStatus.OczekiwanieNaCzesci,  // Czekamy na dostawę
        RepairStage.PraceWlasciwe     => RepairStatus.WNaprawie,            // Trwa naprawa
        RepairStage.KontrolaJakosci   => RepairStatus.GotoweDoOdbioru,      // Gotowe, kontrola zakończona
        _ => null // Nieznany etap — brak automatycznej zmiany statusu
    };

    /// <summary>
    /// Wykonuje zmianę etapu, aktualizuje status (przez State) i dopisuje wpisy do logu.
    /// Zapamiętuje stan przez Memento przed wprowadzeniem zmian.
    /// </summary>
    public void Execute()
    {
        // KROK 1: Zapamiętaj stan PRZED zmianą (Memento) — jeśli Undo będzie potrzebne.
        _memento = Capture($"Przed przejściem do {_targetStage}");

        // KROK 2: Zmień etap zlecenia.
        _order.Stage = _targetStage;
        _order.Log.Add(new RepairLogEntry { Message = $"Przejście do etapu: {_targetStage}." });

        // KROK 3: Automatyczna zmiana statusu (State) na podstawie etapu.
        // Mapujemy nowy etap na odpowiedni status, sprawdzamy czy przejście jest dozwolone
        // przez bieżący stan (RepairStateFactory.Create()) — nie każde przejście jest legalne.
        var mappedStatus = MapStageToStatus(_targetStage); // Jaki status odpowiada temu etapowi?
        if (mappedStatus is not null && mappedStatus != _order.Status) // Czy status musi się zmienić?
        {
            var state = RepairStateFactory.Create(_order.Status); // Pobierz obiekt bieżącego stanu
            if (state.CanTransitionTo(mappedStatus.Value)) // Czy przejście jest dozwolone przez State?
            {
                _order.Status = mappedStatus.Value; // Zmień status
                _order.Log.Add(new RepairLogEntry { Message = $"Status automatycznie zmieniony na: {mappedStatus.Value}." });
            }
        }
    }

    /// <summary>
    /// Cofa operację: przywraca stany magazynowe (ze zrzutu Memento),
    /// poprzedni etap, status i szacowany czas. Dodaje wpis do logu.
    /// </summary>
    public void Undo()
    {
        // Guard: jeśli Execute nie zostało wywołane, Memento nie istnieje — nic do cofnięcia.
        if (_memento is null) return;

        // Przywróć stany magazynowe ze zrzutu (Memento).
        // Pętla po migawce, nie po _allParts — tylko te części które były w migawce.
        foreach (var kv in _memento.PartStockSnapshot)
        {
            // kv.Key = PartId, kv.Value = StockQuantity sprzed Execute
            var part = _allParts.FirstOrDefault(p => p.Id == kv.Key); // Znajdź część po Id
            if (part is not null) part.StockQuantity = kv.Value; // Przywróć ilość z migawki
        }

        // Przywróć etap, status i czas ze zrzutu — to te wartości które były PRZED Execute.
        _order.Stage = _memento.Stage;
        _order.Status = _memento.Status;
        _order.EstimatedHours = _memento.EstimatedHours;

        // Zaloguj cofnięcie — ważne dla dziennika audytowego (kto, co i kiedy cofnął).
        _order.Log.Add(new RepairLogEntry { Message = $"COFNIĘTO etap (przywrócono: {_memento.Note})." });
    }
}

/// <summary>
/// Caretaker — zarządza historią wykonanych komend i pozwala cofać je w poprawnej
/// kolejności (LIFO), zachowując pełny ślad rewizyjny.
/// </summary>
/// <remarks>
/// Wzorzec Memento — rola Caretaker:
/// - Nie zna struktury Memento (nie odczytuje jego pól)
/// - Przechowuje listę komend (Stack — stos LIFO)
/// - Odpowiada za wywołanie Execute i Undo we właściwej kolejności
///
/// Stack (LIFO) jest idealny dla Undo: ostatnio wykonana operacja
/// jest cofana jako pierwsza — jak Ctrl+Z w edytorze.
///
/// Każdy RepairOrder ma swoją instancję RepairHistory (Dictionary w ViewModelu).
/// </remarks>
public class RepairHistory
{
    // Stos wykonanych komend — LIFO (last in, first out).
    private readonly Stack<IRepairCommand> _executed = new();

    /// <summary>Czy jest co cofnąć? Używane przez CanExecute komendy Undo w UI.</summary>
    public bool CanUndo => _executed.Count > 0;

    /// <summary>Liczba operacji w historii (do wyświetlenia w UI).</summary>
    public int Count => _executed.Count;

    /// <summary>
    /// Wykonuje komendę i zapisuje ją na stosie historii.
    /// Każda operacja musi przejść przez tę metodę — nie wolno wywoływać Execute bezpośrednio.
    /// </summary>
    public void Do(IRepairCommand command)
    {
        command.Execute();       // Wykonaj operację
        _executed.Push(command); // Zapamiętaj na stosie (do cofnięcia)
    }

    /// <summary>
    /// Cofa ostatnią operację (LIFO) i zwraca komunikat dla UI.
    /// Jeśli historia jest pusta — informuje o tym.
    /// </summary>
    public string Undo()
    {
        if (!CanUndo) return "Brak operacji do cofnięcia."; // Guard — pusty stos
        var cmd = _executed.Pop(); // Zdejmij ostatnią komendę ze stosu
        cmd.Undo();                // Cofnij jej skutki
        return $"Cofnięto: {cmd.Description}"; // Zwróć komunikat dla UI (Status)
    }

    /// <summary>
    /// Zwraca czytelny opis historii operacji (od najstarszej do najnowszej).
    /// Reverse() bo stos trzyma od najnowszej — odwracamy dla chronologicznego widoku.
    /// </summary>
    public IEnumerable<string> DescribeHistory() => _executed.Reverse().Select(c => c.Description);
}
