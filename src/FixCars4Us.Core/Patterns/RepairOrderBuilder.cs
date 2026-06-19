// Wzorzec: Builder — krok po kroku składanie zlecenia naprawy.

using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>Builder — składa zlecenie naprawy (usterka, części, zasoby) krok po kroku.</summary>
public class RepairOrderBuilder
{
    private readonly RepairOrder _order = new();

    /// <summary>Przypisuje pojazd do zlecenia.</summary>
    public RepairOrderBuilder ForVehicle(Vehicle vehicle)
    {
        _order.Vehicle = vehicle;
        _order.VehicleId = vehicle.Id;
        return this;
    }

    /// <summary>Ustawia opis usterki podany przez klienta.</summary>
    public RepairOrderBuilder WithFault(string description)
    {
        _order.FaultDescription = description;
        return this;
    }

    /// <summary>Przypisuje mechanika do zlecenia.</summary>
    public RepairOrderBuilder AssignMechanic(Mechanic mechanic)
    {
        _order.Mechanic = mechanic;
        _order.MechanicId = mechanic.Id;
        return this;
    }

    /// <summary>Przypisuje stanowisko / podnośnik.</summary>
    public RepairOrderBuilder UseLift(Lift lift)
    {
        _order.Lift = lift;
        _order.LiftId = lift.Id;
        return this;
    }

    /// <summary>Przypisuje narzędzie specjalistyczne (opcjonalne).</summary>
    public RepairOrderBuilder UseTool(SpecialTool tool)
    {
        _order.SpecialTool = tool;
        _order.SpecialToolId = tool.Id;
        return this;
    }

    /// <summary>Ustawia szacowany czas pracy w godzinach.</summary>
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
            // Quantity jest int, więc zaokrąglamy godziny w górę.
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

    /// <summary>Kończy budowanie: ustawia status początkowy, etap i wpis w logu.</summary>
    public RepairOrder Build()
    {
        _order.Status = RepairStatus.Przyjete;
        _order.Stage = RepairStage.Diagnostyka;
        _order.Log.Add(new RepairLogEntry
        {
            Message = "Zlecenie utworzone (Builder)."
        });
        return _order;
    }
}
