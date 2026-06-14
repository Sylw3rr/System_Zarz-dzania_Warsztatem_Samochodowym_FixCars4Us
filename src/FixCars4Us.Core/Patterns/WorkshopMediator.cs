using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>Żądanie zaplanowania naprawy w określonym oknie czasowym.</summary>
public record ResourceRequest(
    MechanicSpecialization RequiredSpecialization,
    LiftType RequiredLiftType,
    int RequiredCapacityKg,
    bool RequiresDiagnosticTool,
    DateTime Start,
    DateTime End);

/// <summary>Wynik przydziału zasobów przez Mediatora.</summary>
public record ResourceAllocation(bool Success, string Message, Mechanic? Mechanic = null, Lift? Lift = null, SpecialTool? Tool = null);

/// <summary>Zajętość zasobu w danym przedziale czasu.</summary>
internal record Booking(int ResourceId, DateTime Start, DateTime End);

/// <summary>
/// WZORZEC: Mediator (INTELIGENTNE PRZYPISYWANIE ZASOBÓW WARSZTATOWYCH).
/// Koordynuje jednoczesną dostępność trzech typów zasobów — mechanika o właściwej
/// specjalizacji, wolnego podnośnika o wystarczającym udźwigu oraz narzędzia
/// specjalistycznego — w zazębiających się przedziałach czasowych. Uniemożliwia
/// zaplanowanie naprawy, jeśli choć jeden z wymaganych zasobów jest niedostępny.
/// </summary>
public class WorkshopMediator
{
    private readonly IReadOnlyList<Mechanic> _mechanics;
    private readonly IReadOnlyList<Lift> _lifts;
    private readonly IReadOnlyList<SpecialTool> _tools;

    private readonly List<Booking> _mechanicBookings = new();
    private readonly List<Booking> _liftBookings = new();
    private readonly List<Booking> _toolBookings = new();

    public WorkshopMediator(IEnumerable<Mechanic> mechanics, IEnumerable<Lift> lifts, IEnumerable<SpecialTool> tools)
    {
        _mechanics = mechanics.ToList();
        _lifts = lifts.ToList();
        _tools = tools.ToList();
    }

    private static bool Overlaps(Booking b, DateTime start, DateTime end)
        => start < b.End && b.Start < end;

    private bool IsFree(List<Booking> bookings, int resourceId, DateTime start, DateTime end)
        => !bookings.Any(b => b.ResourceId == resourceId && Overlaps(b, start, end));

    /// <summary>Próbuje przydzielić komplet zasobów. Rezerwuje je tylko gdy wszystkie są dostępne.</summary>
    public ResourceAllocation TryAllocate(ResourceRequest req)
    {
        var mechanic = _mechanics.FirstOrDefault(m =>
            m.Specialization == req.RequiredSpecialization &&
            IsFree(_mechanicBookings, m.Id, req.Start, req.End));
        if (mechanic is null)
            return new ResourceAllocation(false,
                $"Brak wolnego mechanika o specjalizacji {req.RequiredSpecialization} w wybranym terminie.");

        var lift = _lifts.FirstOrDefault(l =>
            l.Type == req.RequiredLiftType &&
            l.CapacityKg >= req.RequiredCapacityKg &&
            IsFree(_liftBookings, l.Id, req.Start, req.End));
        if (lift is null)
            return new ResourceAllocation(false,
                $"Wszystkie podnośniki typu {req.RequiredLiftType} o udźwigu >= {req.RequiredCapacityKg} kg są zajęte.");

        SpecialTool? tool = null;
        if (req.RequiresDiagnosticTool)
        {
            tool = _tools.FirstOrDefault(t => IsFree(_toolBookings, t.Id, req.Start, req.End));
            if (tool is null)
                return new ResourceAllocation(false, "Brak wolnego narzędzia specjalistycznego w wybranym terminie.");
        }

        // Wszystkie zasoby dostępne — rezerwujemy atomowo.
        _mechanicBookings.Add(new Booking(mechanic.Id, req.Start, req.End));
        _liftBookings.Add(new Booking(lift.Id, req.Start, req.End));
        if (tool is not null) _toolBookings.Add(new Booking(tool.Id, req.Start, req.End));

        return new ResourceAllocation(true,
            $"Przydzielono: {mechanic.Name}, {lift.Name}" + (tool is not null ? $", {tool.Name}." : "."),
            mechanic, lift, tool);
    }

    /// <summary>Zwalnia rezerwacje danego zasobu w przedziale (np. przy anulowaniu naprawy).</summary>
    public void Release(int? mechanicId, int? liftId, int? toolId, DateTime start, DateTime end)
    {
        _mechanicBookings.RemoveAll(b => b.ResourceId == mechanicId && b.Start == start && b.End == end);
        _liftBookings.RemoveAll(b => b.ResourceId == liftId && b.Start == start && b.End == end);
        _toolBookings.RemoveAll(b => b.ResourceId == toolId && b.Start == start && b.End == end);
    }
}
