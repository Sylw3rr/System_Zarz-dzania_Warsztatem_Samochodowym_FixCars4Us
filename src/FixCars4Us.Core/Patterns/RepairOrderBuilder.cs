// Plik: RepairOrderBuilder.cs
// Rola: Stopniowe, czytelne budowanie zlecenia naprawy — krok po kroku,
//       bez wieloparametrowego konstruktora.
// Wzorzec: BUILDER (GoF) — oddziela proces tworzenia złożonego obiektu od jego reprezentacji.
//          Director = RepairOrdersViewModel.CreateOrder(),
//          Builder = RepairOrderBuilder,
//          Product = RepairOrder.

using FixCars4Us.Core.Enums;   // RepairStatus, RepairStage
using FixCars4Us.Core.Models;  // RepairOrder, Vehicle, Mechanic, Lift, Part itd.

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: Builder.
/// Pozwala czytelnie złożyć skomplikowane zlecenie naprawy (usterki, części,
/// przydzieleni pracownicy, zasoby, szacowany czas) krok po kroku.
/// </summary>
/// <remarks>
/// Problem bez Buildera:
///   new RepairOrder(vehicle, fault, mechanic, lift, tool, hours, items, ...)
///   — konstruktor z 8+ parametrami jest nieczytelny i trudny w utrzymaniu.
///
/// Z Builderem:
///   new RepairOrderBuilder()
///     .ForVehicle(v).WithFault("usterka").AssignMechanic(m).UseLift(l)
///     .EstimateHours(2).AddLabor("Diagnostyka", 150, 2).Build();
///
/// Każda metoda Builder-a zwraca "this" (fluent interface) umożliwiając
/// łańcuchowanie wywołań w jednym wyrażeniu.
///
/// Build() to "seal" — po jego wywołaniu zlecenie jest gotowe do użycia
/// (status Przyjete, etap Diagnostyka, pierwszy wpis w logu).
/// </remarks>
public class RepairOrderBuilder
{
    // Prywatne pole produktu — Builder stopniowo wypełnia ten obiekt.
    // Tworzone od razu w deklaracji — Builder zawsze zaczyna od pustego zlecenia.
    private readonly RepairOrder _order = new();

    /// <summary>
    /// Przypisuje pojazd do zlecenia (wymagane — bez pojazdu zlecenie nie ma sensu).
    /// Ustawia zarówno referencję nawigacyjną jak i klucz obcy (oba potrzebne przez EF Core).
    /// </summary>
    public RepairOrderBuilder ForVehicle(Vehicle vehicle)
    {
        _order.Vehicle = vehicle;       // Właściwość nawigacyjna — używana przez logikę (np. Mediator)
        _order.VehicleId = vehicle.Id; // Klucz obcy — wymagany przez EF Core do zapisu w bazie
        return this;                    // Fluent interface — zwróć this dla łańcuchowania
    }

    /// <summary>
    /// Ustawia opis usterki podany przez klienta.
    /// Zapisywany w logu serwisowym i używany jako tytuł w UI.
    /// </summary>
    public RepairOrderBuilder WithFault(string description)
    {
        _order.FaultDescription = description; // Opis usterki (np. "Silnik nie odpala")
        return this; // Fluent
    }

    /// <summary>
    /// Przypisuje mechanika do zlecenia (wybrany przez Mediator na podstawie specjalizacji).
    /// </summary>
    public RepairOrderBuilder AssignMechanic(Mechanic mechanic)
    {
        _order.Mechanic = mechanic;       // Nawigacja
        _order.MechanicId = mechanic.Id; // FK
        return this; // Fluent
    }

    /// <summary>
    /// Przypisuje stanowisko / podnośnik (wybrany przez Mediator na podstawie typu i udźwigu).
    /// </summary>
    public RepairOrderBuilder UseLift(Lift lift)
    {
        _order.Lift = lift;       // Nawigacja
        _order.LiftId = lift.Id; // FK
        return this; // Fluent
    }

    /// <summary>
    /// Przypisuje narzędzie specjalistyczne (opcjonalne — gdy diagnostyka wymaga komputera).
    /// </summary>
    public RepairOrderBuilder UseTool(SpecialTool tool)
    {
        _order.SpecialTool = tool;       // Nawigacja
        _order.SpecialToolId = tool.Id; // FK
        return this; // Fluent
    }

    /// <summary>
    /// Ustawia szacowany czas pracy w godzinach.
    /// Używany przez ILaborCostStrategy do obliczenia kosztu robocizny.
    /// </summary>
    public RepairOrderBuilder EstimateHours(decimal hours)
    {
        _order.EstimatedHours = hours; // Szacowany czas (np. 2.5 = 2h 30min)
        return this; // Fluent
    }

    /// <summary>Dodaje pozycję robocizny do kosztorysu.</summary>
    /// <remarks>
    /// Robocizna = praca mechanika wyceniona jako godziny * stawka.
    /// Math.Ceiling zaokrągla godziny w górę (0.5h = 1 "jednostka")
    /// bo Quantity w RepairItem jest int (nie decimal) — celowe uproszczenie modelu.
    /// PartId jest null dla pozycji robocizny (w odróżnieniu od części).
    /// </remarks>
    public RepairOrderBuilder AddLabor(string description, decimal hourlyRate, decimal hours)
    {
        _order.Items.Add(new RepairItem
        {
            Description = $"Robocizna: {description}", // Opis pozycji z prefiksem "Robocizna:"
            UnitPrice = hourlyRate,                     // Stawka godzinowa
            Quantity = (int)Math.Ceiling(hours)         // Zaokrąglone godziny (Ceiling = w górę)
            // PartId = null (domyślnie) — to nie jest część z magazynu
        });
        return this; // Fluent
    }

    /// <summary>Dodaje część do kosztorysu (powiązanie z magazynem).</summary>
    /// <remarks>
    /// Część jest powiązana z magazynem przez PartId — WorkshopFacade.AddPartToOrder()
    /// używa tego powiązania do aktualizacji stanów magazynowych.
    /// W Builderze tylko dodajemy pozycję kosztorysu; pobranie z magazynu
    /// odbywa się osobno przez Facade (separacja odpowiedzialności).
    /// </remarks>
    public RepairOrderBuilder AddPart(Part part, int quantity)
    {
        _order.Items.Add(new RepairItem
        {
            Description = $"Część: {part.Name}", // Opis z prefiksem "Część:"
            UnitPrice = part.SalePrice,           // Cena sprzedaży (nie zakupu!) — to co płaci klient
            Quantity = quantity,                  // Ile sztuk
            PartId = part.Id,                     // Powiąż z katalogiem — umożliwia rozchód z magazynu
            Part = part                           // Nawigacja (do wyświetlenia bez dodatkowego Include)
        });
        return this; // Fluent
    }

    /// <summary>
    /// Kończy budowanie zlecenia: ustawia początkowy status (Przyjete),
    /// etap (Diagnostyka) i dodaje pierwszy wpis do logu audytowego.
    /// Po wywołaniu Build() Builder nie powinien być używany ponownie
    /// (wewnętrzny _order jest już gotowym produktem).
    /// </summary>
    public RepairOrder Build()
    {
        _order.Status = RepairStatus.Przyjete;      // Każde nowe zlecenie zaczyna jako "Przyjęte"
        _order.Stage = RepairStage.Diagnostyka;     // Pierwszy etap procesu naprawy
        _order.Log.Add(new RepairLogEntry           // Pierwszy wpis w dzienniku audytowym
        {
            Message = "Zlecenie utworzone (Builder)." // Ślad że zlecenie powstało przez Builder
        });
        return _order; // Zwróć gotowy produkt
    }
}
