// Plik: WorkshopContext.cs
// Rola: Kontekst bazy danych Entity Framework Core. Reprezentuje "sesję z bazą"
//       i udostępnia kolekcje (DbSet) dla każdej encji domenowej.
// Wzorzec: Unit of Work + Repository (EF Core implementuje oba wzorce wewnętrznie).
//          DbContext = Unit of Work, DbSet<T> = Repository<T>.

using FixCars4Us.Core.Models;       // Klasy domenowe (encje)
using Microsoft.EntityFrameworkCore; // EF Core — ORM mapujący obiekty na tabele

namespace FixCars4Us.Core.Data;

/// <summary>
/// Kontekst EF Core (warstwa zapisu danych). Używa SQLite z plikiem fixcars4us.db.
/// Schemat tworzony jest automatycznie metodą EnsureCreated() — bez migracji,
/// aby uruchomienie było maksymalnie proste.
/// </summary>
/// <remarks>
/// Dlaczego jeden kontekst dla całej aplikacji? To podejście "Shared DbContext"
/// — wszystkie ViewModele dostają tę samą instancję przez konstruktor MainViewModel.
/// Zalety: brak duplikacji danych, zmiany w jednym module widoczne w innym bez
/// synchronizacji. Wada: brak izolacji transakcji między modułami (akceptowalne
/// dla aplikacji desktopowej z jednym użytkownikiem).
///
/// Wzorzec Unit of Work: SaveChanges() zatwierdza wszystkie oczekujące zmiany
/// w jednej transakcji atomowej. Jeśli cokolwiek się nie powiedzie, cofnięte są wszystkie.
/// </remarks>
public class WorkshopContext : DbContext
{
    // DbSet<T> = tabela w bazie danych. Nazwa właściwości staje się nazwą tabeli.
    // "=> Set<T>()" to skróconą notacją zwracającą DbSet z wewnętrznej kolekcji kontekstu.

    public DbSet<Customer> Customers => Set<Customer>();               // Tabela klientów
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();                  // Tabela pojazdów
    public DbSet<ServiceHistoryEntry> ServiceHistory => Set<ServiceHistoryEntry>(); // Historia serwisowa
    public DbSet<Part> Parts => Set<Part>();                           // Katalog/magazyn części
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();      // Cennik usług
    public DbSet<Mechanic> Mechanics => Set<Mechanic>();               // Pracownicy warsztatu
    public DbSet<Lift> Lifts => Set<Lift>();                          // Stanowiska / podnośniki
    public DbSet<SpecialTool> SpecialTools => Set<SpecialTool>();      // Narzędzia specjalistyczne
    public DbSet<ReceptionStation> ReceptionStations => Set<ReceptionStation>(); // Stanowiska przyjęć
    public DbSet<Appointment> Appointments => Set<Appointment>();      // Kalendarz wizyt
    public DbSet<RepairOrder> RepairOrders => Set<RepairOrder>();      // Zlecenia napraw
    public DbSet<RepairItem> RepairItems => Set<RepairItem>();         // Pozycje kosztorysów
    public DbSet<RepairLogEntry> RepairLog => Set<RepairLogEntry>();   // Dzienniki audytowe

    // Ścieżka do pliku SQLite — ustalana w konstruktorze.
    private readonly string _dbPath;

    /// <summary>
    /// Główny konstruktor aplikacji. Gdy dbPath == null, baza tworzona jest
    /// obok pliku EXE (AppContext.BaseDirectory), co jest standardowym miejscem
    /// dla aplikacji desktopowej — dane nie giną po aktualizacji aplikacji.
    /// </summary>
    public WorkshopContext(string? dbPath = null)
    {
        // null-coalescing: użyj podanej ścieżki lub domyślnej obok EXE.
        _dbPath = dbPath ?? Path.Combine(AppContext.BaseDirectory, "fixcars4us.db");
    }

    /// <summary>Konstruktor dla testów / scenariuszy in-memory (przekazuje gotowe opcje).</summary>
    /// <remarks>
    /// Przy testach jednostkowych przekazujemy opcje skonfigurowane przez
    /// UseInMemoryDatabase() — baza żyje tylko przez czas testu, bez pliku na dysku.
    /// Pusty string dla _dbPath bo nie jest używany gdy options są już skonfigurowane.
    /// </remarks>
    public WorkshopContext(DbContextOptions<WorkshopContext> options) : base(options)
    {
        _dbPath = ""; // Nieużywane — opcje przekazane z zewnątrz (np. InMemory)
    }

    /// <summary>
    /// Konfiguracja połączenia z bazą. Wywoływana automatycznie przez EF Core
    /// jeśli opcje nie zostały podane w konstruktorze (standardowy scenariusz aplikacji).
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // IsConfigured == false gdy używamy konstruktora z _dbPath (nie z opcjami zewnętrznymi).
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite($"Data Source={_dbPath}"); // Skonfiguruj provider SQLite
    }

    /// <summary>
    /// Konfiguracja schematu bazy danych — mapowania typów, relacji, konwersji.
    /// EF Core wywołuje tę metodę przy pierwszym użyciu modelu (cache na czas życia aplikacji).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder b)
    {
        // SQLite nie obsługuje nativnie typu decimal — konwertujemy na double przy zapisie/odczycie.
        // HasConversion<double>() powoduje automatyczną konwersję bez straty dla kwot w PLN
        // (double ma ~15 cyfr znaczących, wystarczające dla cen i procentów).

        b.Entity<Customer>().Property(c => c.DiscountPercent).HasConversion<double>(); // Rabat 0-100%
        b.Entity<Part>().Property(p => p.PurchasePrice).HasConversion<double>();        // Cena zakupu
        b.Entity<Part>().Property(p => p.SalePrice).HasConversion<double>();            // Cena sprzedaży
        b.Entity<ServiceType>().Property(s => s.HourlyRate).HasConversion<double>();   // Stawka roboczogodziny
        b.Entity<RepairItem>().Property(i => i.UnitPrice).HasConversion<double>();     // Cena jednostkowa pozycji
        b.Entity<ServiceHistoryEntry>().Property(s => s.Cost).HasConversion<double>(); // Koszt w historii
        b.Entity<RepairOrder>().Property(r => r.EstimatedHours).HasConversion<double>(); // Szacowany czas
    }
}
