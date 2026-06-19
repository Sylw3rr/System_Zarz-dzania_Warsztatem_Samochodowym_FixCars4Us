// Wzorzec: Unit of Work + Repository (EF Core implementuje je wewnętrznie: DbContext = UoW, DbSet<T> = Repository<T>).

using FixCars4Us.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FixCars4Us.Core.Data;

/// <summary>Kontekst EF Core — SQLite, schemat tworzony automatycznie przez EnsureCreated().</summary>
public class WorkshopContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ServiceHistoryEntry> ServiceHistory => Set<ServiceHistoryEntry>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<Mechanic> Mechanics => Set<Mechanic>();
    public DbSet<Lift> Lifts => Set<Lift>();
    public DbSet<SpecialTool> SpecialTools => Set<SpecialTool>();
    public DbSet<ReceptionStation> ReceptionStations => Set<ReceptionStation>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<RepairOrder> RepairOrders => Set<RepairOrder>();
    public DbSet<RepairItem> RepairItems => Set<RepairItem>();
    public DbSet<RepairLogEntry> RepairLog => Set<RepairLogEntry>();

    private readonly string _dbPath;

    public WorkshopContext(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(AppContext.BaseDirectory, "fixcars4us.db");
    }

    /// <summary>Konstruktor dla testów / in-memory (opcje już skonfigurowane).</summary>
    public WorkshopContext(DbContextOptions<WorkshopContext> options) : base(options)
    {
        _dbPath = "";
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // SQLite nie wspiera natywnie decimal — konwersja na double przy zapisie/odczycie.
        b.Entity<Customer>().Property(c => c.DiscountPercent).HasConversion<double>();
        b.Entity<Part>().Property(p => p.PurchasePrice).HasConversion<double>();
        b.Entity<Part>().Property(p => p.SalePrice).HasConversion<double>();
        b.Entity<ServiceType>().Property(s => s.HourlyRate).HasConversion<double>();
        b.Entity<RepairItem>().Property(i => i.UnitPrice).HasConversion<double>();
        b.Entity<ServiceHistoryEntry>().Property(s => s.Cost).HasConversion<double>();
        b.Entity<RepairOrder>().Property(r => r.EstimatedHours).HasConversion<double>();
    }
}
