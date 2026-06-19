using System.Collections.ObjectModel;
using FixCars4Us.Core.Data;
using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Infrastructure;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.ViewModels;

/// <summary>ViewModel zakładki Katalog — części zamienne i cennik usług.</summary>
public class CatalogViewModel : ViewModelBase
{
    private readonly WorkshopContext _db;

    public ObservableCollection<Part> Parts { get; } = new();
    public ObservableCollection<ServiceType> ServiceTypes { get; } = new();

    public Array WorkCategories => Enum.GetValues(typeof(WorkCategory));

    public string NewPartCode { get; set; } = "";
    public string NewPartName { get; set; } = "";
    public decimal NewPartPurchase { get; set; }
    public decimal NewPartSale { get; set; }
    public int NewPartStock { get; set; }
    public int NewPartMinStock { get; set; } = 2;
    public bool NewPartIsOriginal { get; set; } = true;

    public string NewServiceName { get; set; } = "";
    public WorkCategory NewServiceCategory { get; set; }
    public decimal NewServiceRate { get; set; }

    public RelayCommand AddPartCommand { get; }
    public RelayCommand AddServiceCommand { get; }

    public CatalogViewModel(WorkshopContext db)
    {
        _db = db;
        AddPartCommand = new RelayCommand(AddPart);
        AddServiceCommand = new RelayCommand(AddService);
        Load();
    }

    public void Load()
    {
        Parts.Clear();
        foreach (var p in _db.Parts.OrderBy(p => p.Name)) Parts.Add(p);

        ServiceTypes.Clear();
        foreach (var s in _db.ServiceTypes.OrderBy(s => s.Name)) ServiceTypes.Add(s);
    }

    private void AddPart(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewPartName)) return;

        var p = new Part
        {
            Code = NewPartCode,
            Name = NewPartName,
            PurchasePrice = NewPartPurchase,
            SalePrice = NewPartSale,
            StockQuantity = NewPartStock,
            MinStock = NewPartMinStock,
            IsOriginal = NewPartIsOriginal
        };

        _db.Parts.Add(p);
        _db.SaveChanges();
        Parts.Add(p);
    }

    private void AddService(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewServiceName)) return;

        var s = new ServiceType
        {
            Name = NewServiceName,
            Category = NewServiceCategory,
            HourlyRate = NewServiceRate
        };

        _db.ServiceTypes.Add(s);
        _db.SaveChanges();
        ServiceTypes.Add(s);
    }
}
