using System.Collections.ObjectModel;
using FixCars4Us.Core.Data;
using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Infrastructure;
using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.ViewModels;

/// <summary>Wiersz dziennika ruchów magazynowych (na potrzeby prezentacji w UI).</summary>
public class StockMovement
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Part { get; set; } = "";
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public int StockAfter { get; set; }
}

/// <summary>
/// Funkcja podstawowa: Zarządzanie magazynem.
/// Rejestruje przychody i rozchody części oraz pokazuje pozycje o niskim stanie.
/// (Automatyczny rozchód przy dodaniu części do zlecenia realizuje WorkshopFacade.)
/// </summary>
public class InventoryViewModel : ViewModelBase
{
    private readonly WorkshopContext _db;

    public ObservableCollection<Part> Parts { get; } = new();
    public ObservableCollection<StockMovement> Movements { get; } = new();

    private Part? _selectedPart;
    public Part? SelectedPart
    {
        get => _selectedPart;
        set
        {
            if (SetField(ref _selectedPart, value))
            {
                StockInCommand.RaiseCanExecuteChanged();
                StockOutCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int MovementQuantity { get; set; } = 1;

    /// <summary>Lista części z niskim stanem — alert dla managera (Observer/UI).</summary>
    public IEnumerable<Part> LowStockParts => Parts.Where(p => p.IsLowStock);

    public RelayCommand StockInCommand { get; }
    public RelayCommand StockOutCommand { get; }

    public InventoryViewModel(WorkshopContext db)
    {
        _db = db;
        StockInCommand = new RelayCommand(_ => Move(StockMovementType.Przychod), _ => SelectedPart != null);
        StockOutCommand = new RelayCommand(_ => Move(StockMovementType.Rozchod), _ => SelectedPart != null);
        Load();
    }

    public void Load()
    {
        Parts.Clear();
        foreach (var p in _db.Parts.OrderBy(p => p.Name)) Parts.Add(p);
        OnPropertyChanged(nameof(LowStockParts));
    }

    private void Move(StockMovementType type)
    {
        if (SelectedPart is null || MovementQuantity <= 0) return;
        if (type == StockMovementType.Rozchod && SelectedPart.StockQuantity < MovementQuantity) return;

        SelectedPart.StockQuantity += type == StockMovementType.Przychod ? MovementQuantity : -MovementQuantity;
        _db.SaveChanges();

        Movements.Insert(0, new StockMovement
        {
            Part = SelectedPart.Name,
            Type = type,
            Quantity = MovementQuantity,
            StockAfter = SelectedPart.StockQuantity
        });
        OnPropertyChanged(nameof(LowStockParts));
    }
}
