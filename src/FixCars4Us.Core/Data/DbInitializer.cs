using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Data;

/// <summary>Tworzy bazę (jeśli nie istnieje) i wypełnia ją danymi przykładowymi.</summary>
public static class DbInitializer
{
    public static void EnsureSeeded(WorkshopContext db)
    {
        db.Database.EnsureCreated();
        if (db.Customers.Any()) return; // już zainicjowane

        // --- Zasoby warsztatu ---
        var mechanics = new[]
        {
            new Mechanic { Name = "Jan Kowalski", Specialization = MechanicSpecialization.Mechanika },
            new Mechanic { Name = "Anna Nowak", Specialization = MechanicSpecialization.Elektryka },
            new Mechanic { Name = "Piotr Wiśniewski", Specialization = MechanicSpecialization.Diagnostyka },
            new Mechanic { Name = "Marek Lewandowski", Specialization = MechanicSpecialization.Lakiernictwo },
        };
        var lifts = new[]
        {
            new Lift { Name = "Podnośnik A", Type = LiftType.Osobowy, CapacityKg = 2500 },
            new Lift { Name = "Podnośnik B", Type = LiftType.Osobowy, CapacityKg = 3000 },
            new Lift { Name = "Podnośnik C", Type = LiftType.Ciezarowy, CapacityKg = 7000 },
        };
        var tools = new[]
        {
            new SpecialTool { Name = "Komputer diagnostyczny" },
            new SpecialTool { Name = "Ściągacz do amortyzatorów" },
            new SpecialTool { Name = "Klucz dynamometryczny" },
        };
        var stations = new[]
        {
            new ReceptionStation { Name = "Stanowisko przyjęć 1" },
            new ReceptionStation { Name = "Stanowisko przyjęć 2" },
        };

        db.Mechanics.AddRange(mechanics);
        db.Lifts.AddRange(lifts);
        db.SpecialTools.AddRange(tools);
        db.ReceptionStations.AddRange(stations);

        // --- Katalog części i usług ---
        db.Parts.AddRange(
            new Part { Code = "OL-5W30", Name = "Olej silnikowy 5W30 (1L)", PurchasePrice = 25, SalePrice = 45, StockQuantity = 20, MinStock = 5 },
            new Part { Code = "FIL-OIL", Name = "Filtr oleju", PurchasePrice = 15, SalePrice = 30, StockQuantity = 12, MinStock = 4 },
            new Part { Code = "KLO-PRZ", Name = "Klocki hamulcowe przód", PurchasePrice = 90, SalePrice = 160, StockQuantity = 6, MinStock = 3 },
            new Part { Code = "AKU-60", Name = "Akumulator 60Ah", PurchasePrice = 220, SalePrice = 350, StockQuantity = 2, MinStock = 2, IsOriginal = false },
            new Part { Code = "SWI-ZAP", Name = "Świeca zapłonowa", PurchasePrice = 18, SalePrice = 35, StockQuantity = 16, MinStock = 8 }
        );
        db.ServiceTypes.AddRange(
            new ServiceType { Name = "Roboczogodzina mechanika", Category = WorkCategory.Mechanika, HourlyRate = 150 },
            new ServiceType { Name = "Roboczogodzina elektryka", Category = WorkCategory.Elektryka, HourlyRate = 180 },
            new ServiceType { Name = "Roboczogodzina lakiernika", Category = WorkCategory.Lakiernictwo, HourlyRate = 200 },
            new ServiceType { Name = "Diagnostyka komputerowa", Category = WorkCategory.Diagnostyka, HourlyRate = 170 }
        );

        // --- Klienci i pojazdy ---
        var c1 = new Customer { Name = "Tomasz Zieliński", Phone = "600100200", Email = "tomek@example.com", Type = CustomerType.Prywatny };
        var c2 = new Customer { Name = "TransLog Sp. z o.o.", Phone = "224455667", Email = "biuro@translog.pl", Type = CustomerType.Flota, DiscountPercent = 10 };
        c1.Vehicles.Add(new Vehicle { RegistrationNumber = "WX12345", Vin = "WVWZZZ1JZXW000001", Brand = "Volkswagen", Model = "Golf", Mileage = 145000 });
        c2.Vehicles.Add(new Vehicle { RegistrationNumber = "GD99887", Vin = "WF0XXTTGXW0000002", Brand = "Ford", Model = "Transit", Mileage = 280000 });
        c2.Vehicles.Add(new Vehicle { RegistrationNumber = "GD55443", Vin = "WF0XXTTGXW0000003", Brand = "Ford", Model = "Focus", Mileage = 95000 });
        db.Customers.AddRange(c1, c2);

        db.SaveChanges();

        // przykładowy wpis historii serwisowej
        var golf = db.Vehicles.First(v => v.RegistrationNumber == "WX12345");
        db.ServiceHistory.Add(new ServiceHistoryEntry
        {
            VehicleId = golf.Id,
            Date = DateTime.Now.AddMonths(-6),
            Description = "Wymiana oleju i filtra",
            MileageAtService = 138000,
            Cost = 250
        });
        db.SaveChanges();
    }
}
