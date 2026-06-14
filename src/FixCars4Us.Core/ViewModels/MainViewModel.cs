using FixCars4Us.Core.Data;
using FixCars4Us.Core.Infrastructure;

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Główny ViewModel aplikacji. Tworzy kontekst bazy, wypełnia danymi startowymi
/// i udostępnia ViewModele poszczególnych modułów (zakładek) dla okna WPF.
/// </summary>
public class MainViewModel : ViewModelBase
{
    public WorkshopContext Db { get; }

    public CustomersViewModel Customers { get; }
    public CatalogViewModel Catalog { get; }
    public InventoryViewModel Inventory { get; }
    public AppointmentsViewModel Appointments { get; }
    public RepairOrdersViewModel Repairs { get; }

    public MainViewModel(string? dbPath = null)
    {
        Db = new WorkshopContext(dbPath);
        DbInitializer.EnsureSeeded(Db);

        Customers = new CustomersViewModel(Db);
        Catalog = new CatalogViewModel(Db);
        Inventory = new InventoryViewModel(Db);
        Appointments = new AppointmentsViewModel(Db);
        Repairs = new RepairOrdersViewModel(Db);
    }
}
