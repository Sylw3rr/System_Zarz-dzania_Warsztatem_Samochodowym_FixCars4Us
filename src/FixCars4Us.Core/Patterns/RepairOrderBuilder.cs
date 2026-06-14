using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: Builder.
/// Pozwala czytelnie złożyć skomplikowane zlecenie naprawy (usterki, części,
/// przydzieleni pracownicy, zasoby, szacowany czas) krok po kroku.
/// </summary>
public class RepairOrderBuilder
{
    private readonly RepairOrder _order = new();

    public RepairOrderBuilder ForVehicle(Vehicle vehicle)
    {
        _order.Vehicle = vehicle;
        _order.VehicleId = vehicle.Id;
        return this;
    }

    public RepairOrderBuilder WithFault(string description)
    {
        _order.FaultDescription = description;
        return this;
    }

    public RepairOrderBuilder AssignMechanic(Mechanic mechanic)
    {
        _order.Mechanic = mechanic;
        _order.MechanicId = mechanic.Id;
        return this;
    }

    public RepairOrderBuilder UseLift(Lift lift)
    {
        _order.Lift = lift;
        _order.LiftId = lift.Id;
        return this;
    }

    public RepairOrderBuilder UseTool(SpecialTool tool)
    {
        _order.SpecialTool = tool;
        _order.SpecialToolId = tool.Id;
        return this;
    }

    public RepairOrderBuilder EstimateHours(decimal hours)
    {
        _order.EstimatedHours = hours;
        return this;
    }

    /// <summary>Dodaje pozycję robocizny do kosztorysu.</summary>
    public RepairOrderBuilder AddLabor(string description, decimal hourlyRate, decimal hours)
    {
        _order.Items.Add(new RepairItem
        {
            Description = $"Robocizna: {description}",
            UnitPrice = hourlyRate,
            Quantity = (int)Math.Ceiling(hours)
        });
        return this;
    }

    /// <summary>Dodaje część do kosztorysu (powiązanie z magazynem).</summary>
    public RepairOrderBuilder AddPart(Part part, int quantity)
    {
        _order.Items.Add(new RepairItem
        {
            Description = $"Część: {part.Name}",
            UnitPrice = part.SalePrice,
            Quantity = quantity,
            PartId = part.Id,
            Part = part
        });
        return this;
    }

    public RepairOrder Build()
    {
        _order.Status = RepairStatus.Przyjete;
        _order.Stage = RepairStage.Diagnostyka;
        _order.Log.Add(new RepairLogEntry { Message = "Zlecenie utworzone (Builder)." });
        return _order;
    }
}
