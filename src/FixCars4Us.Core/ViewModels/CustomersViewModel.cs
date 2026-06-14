using System.Collections.ObjectModel;
using FixCars4Us.Core.Data;
using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Infrastructure;
using FixCars4Us.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Funkcja podstawowa: Kartoteka klientów + Baza pojazdów i historia serwisowa.
/// Obsługuje klientów (prywatni/floty), ich pojazdy oraz historię napraw.
/// </summary>
public class CustomersViewModel : ViewModelBase
{
    private readonly WorkshopContext _db;

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    public ObservableCollection<ServiceHistoryEntry> History { get; } = new();
    public Array CustomerTypes => Enum.GetValues(typeof(CustomerType));

    private Customer? _selectedCustomer;
    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set { if (SetField(ref _selectedCustomer, value)) LoadVehicles(); }
    }

    private Vehicle? _selectedVehicle;
    public Vehicle? SelectedVehicle
    {
        get => _selectedVehicle;
        set { if (SetField(ref _selectedVehicle, value)) LoadHistory(); }
    }

    // --- Pola formularza nowego klienta ---
    public string NewCustomerName { get; set; } = "";
    public string NewCustomerPhone { get; set; } = "";
    public string NewCustomerEmail { get; set; } = "";
    public CustomerType NewCustomerType { get; set; } = CustomerType.Prywatny;
    public decimal NewCustomerDiscount { get; set; }

    // --- Pola formularza nowego pojazdu ---
    public string NewVehicleReg { get; set; } = "";
    public string NewVehicleVin { get; set; } = "";
    public string NewVehicleBrand { get; set; } = "";
    public string NewVehicleModel { get; set; } = "";
    public int NewVehicleMileage { get; set; }

    // --- Pola formularza wpisu historii ---
    public string NewHistoryDescription { get; set; } = "";
    public int NewHistoryMileage { get; set; }
    public decimal NewHistoryCost { get; set; }

    public RelayCommand AddCustomerCommand { get; }
    public RelayCommand AddVehicleCommand { get; }
    public RelayCommand AddHistoryCommand { get; }

    public CustomersViewModel(WorkshopContext db)
    {
        _db = db;
        AddCustomerCommand = new RelayCommand(AddCustomer);
        AddVehicleCommand = new RelayCommand(AddVehicle, _ => SelectedCustomer != null);
        AddHistoryCommand = new RelayCommand(AddHistory, _ => SelectedVehicle != null);
        Load();
    }

    public void Load()
    {
        Customers.Clear();
        foreach (var c in _db.Customers.OrderBy(c => c.Name)) Customers.Add(c);
    }

    private void LoadVehicles()
    {
        Vehicles.Clear();
        History.Clear();
        if (SelectedCustomer is null) return;
        foreach (var v in _db.Vehicles.Where(v => v.CustomerId == SelectedCustomer.Id))
            Vehicles.Add(v);
    }

    private void LoadHistory()
    {
        History.Clear();
        if (SelectedVehicle is null) return;
        foreach (var h in _db.ServiceHistory.Where(h => h.VehicleId == SelectedVehicle.Id).OrderByDescending(h => h.Date))
            History.Add(h);
    }

    private void AddCustomer(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName)) return;
        var c = new Customer
        {
            Name = NewCustomerName,
            Phone = NewCustomerPhone,
            Email = NewCustomerEmail,
            Type = NewCustomerType,
            DiscountPercent = NewCustomerDiscount
        };
        _db.Customers.Add(c);
        _db.SaveChanges();
        Customers.Add(c);
        SelectedCustomer = c;
    }

    private void AddVehicle(object? _)
    {
        if (SelectedCustomer is null || string.IsNullOrWhiteSpace(NewVehicleReg)) return;
        var v = new Vehicle
        {
            CustomerId = SelectedCustomer.Id,
            RegistrationNumber = NewVehicleReg,
            Vin = NewVehicleVin,
            Brand = NewVehicleBrand,
            Model = NewVehicleModel,
            Mileage = NewVehicleMileage
        };
        _db.Vehicles.Add(v);
        _db.SaveChanges();
        Vehicles.Add(v);
    }

    private void AddHistory(object? _)
    {
        if (SelectedVehicle is null || string.IsNullOrWhiteSpace(NewHistoryDescription)) return;
        var h = new ServiceHistoryEntry
        {
            VehicleId = SelectedVehicle.Id,
            Description = NewHistoryDescription,
            MileageAtService = NewHistoryMileage,
            Cost = NewHistoryCost,
            Date = DateTime.Now
        };
        _db.ServiceHistory.Add(h);
        _db.SaveChanges();
        History.Insert(0, h);
    }
}
