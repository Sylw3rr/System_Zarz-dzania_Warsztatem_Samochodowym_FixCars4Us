using FixCars4Us.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FixCars4Us.Core.Data;

/// <summary>
/// Kontekst EF Core (warstwa zapisu danych). Używa SQLite z plikiem fixcars4us.db.
/// Schemat tworzony jest automatycznie metodą EnsureCreated() — bez migracji,
/// aby uruchomienie było maksymalnie proste.
/// </summary>
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

    /// <summary>Konstruktor dla testów / scenariuszy in-memory (przekazuje gotowe opcje).</summary>
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
        b.Entity<Customer>().Property(c => c.DiscountPercent).HasConversion<double>();
        b.Entity<Part>().Property(p => p.PurchasePrice).HasConversion<double>();
        b.Entity<Part>().Property(p => p.SalePrice).HasConversion<double>();
        b.Entity<ServiceType>().Property(s => s.HourlyRate).HasConversion<double>();
        b.Entity<RepairItem>().Property(i => i.UnitPrice).HasConversion<double>();
        b.Entity<ServiceHistoryEntry>().Property(s => s.Cost).HasConversion<double>();
        b.Entity<RepairOrder>().Property(r => r.EstimatedHours).HasConversion<double>();
    }
}
