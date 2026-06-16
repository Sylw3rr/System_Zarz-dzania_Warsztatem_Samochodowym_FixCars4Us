// Plik: DbInitializer.cs
// Rola: Inicjalizacja bazy danych — tworzenie schematu i wypełnianie danymi przykładowymi
//       (tzw. "seeding"). Wywoływany jednorazowo przy uruchomieniu aplikacji.
// Wzorzec: brak GoF — to prosty helper statyczny (Utility Class).
//          Ale idea jest zbliżona do wzorca "Data Seeder" z Entity Framework.

using FixCars4Us.Core.Enums;   // Wyliczenia specjalizacji, typów, kategorii
using FixCars4Us.Core.Models;  // Klasy encji (Mechanic, Part, Customer, ...)

namespace FixCars4Us.Core.Data;

/// <summary>Tworzy bazę (jeśli nie istnieje) i wypełnia ją danymi przykładowymi.</summary>
/// <remarks>
/// Klasa statyczna — brak stanu, nie potrzebuje instancji.
/// Wywołanie: DbInitializer.EnsureSeeded(db) w konstruktorze MainViewModel.
/// "Ensure" w nazwie to konwencja idempotentności: metoda jest bezpieczna
/// do wielokrotnego wywołania — wykrywa czy dane już istnieją i przerywa.
/// </remarks>
public static class DbInitializer
{
    /// <summary>
    /// Tworzy schemat bazy danych (jeśli nie istnieje) i wypełnia danymi startowymi.
    /// Idempotentna — jeśli dane już istnieją, kończy działanie natychmiast.
    /// </summary>
    public static void EnsureSeeded(WorkshopContext db)
    {
        // EnsureCreated: tworzy bazę SQLite i tabele jeśli plik db nie istnieje.
        // NIE używamy migracji (Migrate()) — prostsze uruchomienie bez CLI EF.
        db.Database.EnsureCreated();

        // Sprawdź czy dane startowe już zostały dodane (guard clause — wyjdź szybko).
        // Wystarczy sprawdzić jedną tabelę — jeśli jest klient, jest wszystko.
        if (db.Customers.Any()) return; // już zainicjowane — nie duplikuj danych

        // --- Zasoby warsztatu (mechanicy, stanowiska, narzędzia) ---
        // Tablice inicjalizatorów obiektów — wygodny sposób tworzenia wielu encji.

        var mechanics = new[]
        {
            // Czterech mechaników z różnymi specjalizacjami — pokrywają wszystkie WorkCategory.
            new Mechanic { Name = "Jan Kowalski",       Specialization = MechanicSpecialization.Mechanika },
            new Mechanic { Name = "Anna Nowak",         Specialization = MechanicSpecialization.Elektryka },
            new Mechanic { Name = "Piotr Wiśniewski",   Specialization = MechanicSpecialization.Diagnostyka },
            new Mechanic { Name = "Marek Lewandowski",  Specialization = MechanicSpecialization.Lakiernictwo },
        };
        var lifts = new[]
        {
            // Dwa podnośniki osobowe i jeden ciężarowy — różne typy dla Mediatora.
            new Lift { Name = "Podnośnik A", Type = LiftType.Osobowy,   CapacityKg = 2500 },
            new Lift { Name = "Podnośnik B", Type = LiftType.Osobowy,   CapacityKg = 3000 },
            new Lift { Name = "Podnośnik C", Type = LiftType.Ciezarowy, CapacityKg = 7000 },
        };
        var tools = new[]
        {
            // Narzędzia specjalistyczne — komputer diagnostyczny jest wymagany dla diagnostyki.
            new SpecialTool { Name = "Komputer diagnostyczny" },
            new SpecialTool { Name = "Ściągacz do amortyzatorów" },
            new SpecialTool { Name = "Klucz dynamometryczny" },
        };
        var stations = new[]
        {
            // Stanowiska przyjęć — kalendarz wizyt może rezerwować jedno z nich.
            new ReceptionStation { Name = "Stanowisko przyjęć 1" },
            new ReceptionStation { Name = "Stanowisko przyjęć 2" },
        };

        // AddRange dodaje całą tablicę do kontekstu (nie do bazy — dopiero SaveChanges).
        db.Mechanics.AddRange(mechanics);
        db.Lifts.AddRange(lifts);
        db.SpecialTools.AddRange(tools);
        db.ReceptionStations.AddRange(stations);

        // --- Katalog części i usług ---
        // Części reprezentują magazyn: PurchasePrice < SalePrice (marża warsztatu).
        db.Parts.AddRange(
            new Part { Code = "OL-5W30",  Name = "Olej silnikowy 5W30 (1L)",     PurchasePrice = 25,  SalePrice = 45,  StockQuantity = 20, MinStock = 5 },
            new Part { Code = "FIL-OIL",  Name = "Filtr oleju",                   PurchasePrice = 15,  SalePrice = 30,  StockQuantity = 12, MinStock = 4 },
            new Part { Code = "KLO-PRZ",  Name = "Klocki hamulcowe przód",        PurchasePrice = 90,  SalePrice = 160, StockQuantity = 6,  MinStock = 3 },
            new Part { Code = "AKU-60",   Name = "Akumulator 60Ah",               PurchasePrice = 220, SalePrice = 350, StockQuantity = 2,  MinStock = 2, IsOriginal = false },
            new Part { Code = "SWI-ZAP",  Name = "Świeca zapłonowa",              PurchasePrice = 18,  SalePrice = 35,  StockQuantity = 16, MinStock = 8 }
        );

        // Cennik usług — różne stawki dla różnych kategorii pracy.
        db.ServiceTypes.AddRange(
            new ServiceType { Name = "Roboczogodzina mechanika",   Category = WorkCategory.Mechanika,    HourlyRate = 150 },
            new ServiceType { Name = "Roboczogodzina elektryka",   Category = WorkCategory.Elektryka,    HourlyRate = 180 },
            new ServiceType { Name = "Roboczogodzina lakiernika",  Category = WorkCategory.Lakiernictwo, HourlyRate = 200 },
            new ServiceType { Name = "Diagnostyka komputerowa",    Category = WorkCategory.Diagnostyka,  HourlyRate = 170 }
        );

        // --- Klienci i pojazdy ---
        // Klient prywatny (bez rabatu) i klient flotowy (10% rabat — DiscountDecorator).
        var c1 = new Customer { Name = "Tomasz Zieliński",    Phone = "600100200", Email = "tomek@example.com",  Type = CustomerType.Prywatny };
        var c2 = new Customer { Name = "TransLog Sp. z o.o.", Phone = "224455667", Email = "biuro@translog.pl", Type = CustomerType.Flota, DiscountPercent = 10 };

        // Dodanie pojazdów przez kolekcję nawigacyjną — EF Core automatycznie ustawi CustomerId.
        c1.Vehicles.Add(new Vehicle { RegistrationNumber = "WX12345", Vin = "WVWZZZ1JZXW000001", Brand = "Volkswagen", Model = "Golf",    Mileage = 145000 });
        c2.Vehicles.Add(new Vehicle { RegistrationNumber = "GD99887", Vin = "WF0XXTTGXW0000002", Brand = "Ford",       Model = "Transit", Mileage = 280000 });
        c2.Vehicles.Add(new Vehicle { RegistrationNumber = "GD55443", Vin = "WF0XXTTGXW0000003", Brand = "Ford",       Model = "Focus",   Mileage = 95000 });

        db.Customers.AddRange(c1, c2); // Dodaj klientów z pojazdami (cascade insert EF Core)

        db.SaveChanges(); // Pierwszy zapis — EF Core nadaje klucze główne (Id) wszystkim encjom

        // Po SaveChanges encje mają nadane Id przez bazę — możemy tworzyć powiązania.
        // przykładowy wpis historii serwisowej dla Golfa (pokazuje historię serwisową w UI)
        var golf = db.Vehicles.First(v => v.RegistrationNumber == "WX12345"); // pobierz z bazy (ma już Id)
        db.ServiceHistory.Add(new ServiceHistoryEntry
        {
            VehicleId = golf.Id,                    // Powiąż z Golfem przez FK (Id nadane po SaveChanges)
            Date = DateTime.Now.AddMonths(-6),       // Wizyta sprzed 6 miesięcy — historyczny przykład
            Description = "Wymiana oleju i filtra",  // Opis wykonanej pracy
            MileageAtService = 138000,               // Przebieg w chwili serwisu (mniejszy niż aktualny 145000)
            Cost = 250                               // Koszt tej usługi
        });
        db.SaveChanges(); // Drugi zapis — utrwala wpis historii serwisowej
    }
}
