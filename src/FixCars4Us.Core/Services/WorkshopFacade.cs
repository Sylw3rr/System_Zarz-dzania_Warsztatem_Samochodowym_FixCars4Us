using FixCars4Us.Core.Data;
using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;
using FixCars4Us.Core.Patterns;
using Microsoft.EntityFrameworkCore;

namespace FixCars4Us.Core.Services;

/// <summary>
/// WZORZEC: Facade ("Panel Mechanika").
/// Udostępnia proste API dla UI, ukrywając współpracę wielu podsystemów:
/// magazynu (pobieranie części), maszyny stanów (State), powiadomień (Observer)
/// oraz dziennika audytowego. Jedno wywołanie = spójna operacja biznesowa.
/// </summary>
public class WorkshopFacade
{
    private readonly WorkshopContext _db;
    private readonly RepairNotifier _notifier;

    public WorkshopFacade(WorkshopContext db, RepairNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    /// <summary>
    /// Zmienia status zlecenia z walidacją przez wzorzec State, zapisuje log
    /// i rozsyła powiadomienia (Observer). Zwraca komunikat dla UI.
    /// </summary>
    public (bool Ok, string Message) ChangeStatus(RepairOrder order, RepairStatus target)
    {
        var state = RepairStateFactory.Create(order.Status);
        if (!state.CanTransitionTo(target))
            return (false, $"Niedozwolone przejście: {order.Status} -> {target}.");

        order.Status = target;
        order.Log.Add(new RepairLogEntry { Message = $"Zmiana statusu na: {target}." });
        _db.SaveChanges();

        _notifier.Notify(order, $"Status naprawy zmieniono na „{target}”.");
        return (true, $"Status zmieniony na {target}.");
    }

    /// <summary>
    /// Dodaje część z magazynu do zlecenia: aktualizuje stan magazynowy (rozchód),
    /// dopisuje pozycję kosztorysu i wpis do logu — wszystko za jednym wywołaniem.
    /// </summary>
    public (bool Ok, string Message) AddPartToOrder(RepairOrder order, Part part, int quantity)
    {
        if (quantity <= 0) return (false, "Ilość musi być dodatnia.");
        if (part.StockQuantity < quantity)
            return (false, $"Za mało części „{part.Name}” w magazynie (dostępne: {part.StockQuantity}).");

        part.StockQuantity -= quantity;
        order.Items.Add(new RepairItem
        {
            Description = $"Część: {part.Name}",
            UnitPrice = part.SalePrice,
            Quantity = quantity,
            PartId = part.Id
        });
        order.Log.Add(new RepairLogEntry { Message = $"Pobrano z magazynu: {quantity} x {part.Name}." });

        if (part.IsLowStock)
            _notifier.Notify(order, $"UWAGA: niski stan magazynowy części „{part.Name}” ({part.StockQuantity}).");

        _db.SaveChanges();
        return (true, $"Dodano {quantity} x {part.Name} do zlecenia.");
    }

    /// <summary>Rejestruje czas pracy mechanika na zleceniu (logowanie roboczogodzin).</summary>
    public void LogWorkTime(RepairOrder order, decimal hours)
    {
        order.EstimatedHours += hours;
        order.Log.Add(new RepairLogEntry { Message = $"Zalogowano {hours} h pracy mechanika." });
        _db.SaveChanges();
    }

    /// <summary>Zapisuje nowe zlecenie (zbudowane Builderem) do bazy.</summary>
    public RepairOrder PersistNewOrder(RepairOrder order)
    {
        _db.RepairOrders.Add(order);
        _db.SaveChanges();
        return order;
    }

    public List<RepairOrder> LoadOrders() =>
        _db.RepairOrders
           .Include(o => o.Vehicle).ThenInclude(v => v!.Customer)
           .Include(o => o.Items)
           .Include(o => o.Log)
           .Include(o => o.Mechanic)
           .Include(o => o.Lift)
           .ToList();
}
