// Wzorzec: Facade — uproszczony interfejs do operacji na zleceniach ("Panel Mechanika").

using FixCars4Us.Core.Data;
using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;
using FixCars4Us.Core.Patterns;
using Microsoft.EntityFrameworkCore;

namespace FixCars4Us.Core.Services;

/// <summary>Udostępnia proste API dla UI, ukrywając magazyn, maszynę stanów i powiadomienia.</summary>
public class WorkshopFacade
{
    private readonly WorkshopContext _db;
    private readonly RepairNotifier _notifier;

    public WorkshopFacade(WorkshopContext db, RepairNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    /// <summary>Zmienia status zlecenia (walidacja przez State), loguje i powiadamia obserwatorów.</summary>
    public (bool Ok, string Message) ChangeStatus(RepairOrder order, RepairStatus target)
    {
        var state = RepairStateFactory.Create(order.Status);
        if (!state.CanTransitionTo(target))
            return (false, $"Niedozwolone przejście: {order.Status} -> {target}.");

        order.Status = target;
        order.Log.Add(new RepairLogEntry { Message = $"Zmiana statusu na: {target}." });

        if (target == RepairStatus.GotoweDoOdbioru)
            RecordCompletion(order);

        _db.SaveChanges();
        _notifier.Notify(order, $"Status naprawy zmieniono na '{target}'.");

        return (true, $"Status zmieniony na {target}.");
    }

    /// <summary>Dopisuje wpis do historii serwisowej pojazdu po zakończeniu naprawy.</summary>
    public void RecordCompletion(RepairOrder order)
    {
        if (order.Vehicle is null) return; // relacja Vehicle nie załadowana

        _db.ServiceHistory.Add(new ServiceHistoryEntry
        {
            VehicleId = order.VehicleId,
            Date = DateTime.Now,
            Description = $"Naprawa: {order.FaultDescription}",
            MileageAtService = order.Vehicle.Mileage,
            Cost = order.ItemsTotal
        });
        // SaveChanges wywołuje wywołujący (ChangeStatus) — tylko raz na operację.
    }

    /// <summary>Pobiera część z magazynu, dopisuje pozycję kosztorysu i wpis do logu.</summary>
    public (bool Ok, string Message) AddPartToOrder(RepairOrder order, Part part, int quantity)
    {
        if (quantity <= 0) return (false, "Ilość musi być dodatnia.");
        if (part.StockQuantity < quantity)
            return (false, $"Za malo czesci '{part.Name}' w magazynie (dostepne: {part.StockQuantity}).");

        part.StockQuantity -= quantity;

        order.Items.Add(new RepairItem
        {
            Description = $"Część: {part.Name}",
            UnitPrice = part.SalePrice,
            Quantity = quantity,
            PartId = part.Id
        });

        order.Log.Add(new RepairLogEntry { Message = $"Pobrano z magazynu: {quantity} x {part.Name}." });

        // Sprawdzane po pobraniu — dopiero wtedy wiadomo czy stan spadł poniżej MinStock.
        if (part.IsLowStock)
            _notifier.Notify(order, $"UWAGA: niski stan magazynowy czesci '{part.Name}' ({part.StockQuantity}).");

        _db.SaveChanges();

        return (true, $"Dodano {quantity} x {part.Name} do zlecenia.");
    }

    /// <summary>Rejestruje czas pracy mechanika na zleceniu.</summary>
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

    /// <summary>Wczytuje aktywne zlecenia z pełnym zestawem relacji (eager loading, bez N+1).</summary>
    public List<RepairOrder> LoadOrders() =>
        _db.RepairOrders
           .Include(o => o.Vehicle).ThenInclude(v => v!.Customer)
           .Include(o => o.Items)
           .Include(o => o.Log)
           .Include(o => o.Mechanic)
           .Include(o => o.Lift)
           .ToList();
}
