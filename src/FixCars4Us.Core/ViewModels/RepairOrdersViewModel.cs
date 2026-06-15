using System.Collections.ObjectModel;
using FixCars4Us.Core.Data;
using FixCars4Us.Core.Enums;
using FixCars4Us.Core.Infrastructure;
using FixCars4Us.Core.Models;
using FixCars4Us.Core.Patterns;
using FixCars4Us.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Centralny moduł zleceń napraw — łączy WSZYSTKIE funkcje dodatkowe i większość wzorców:
///  • Builder            – składanie zlecenia,
///  • Mediator           – inteligentne przypisywanie zasobów (funkcja dodatkowa 1),
///  • Strategy+Decorator – dynamiczna wycena naprawy (funkcja dodatkowa 2),
///  • Command+Memento    – etapy naprawy z cofaniem (funkcja dodatkowa 3),
///  • State              – cykl życia zlecenia,
///  • Observer           – powiadomienia,
///  • Facade             – Panel Mechanika (operacje na zleceniu).
/// </summary>
public class RepairOrdersViewModel : ViewModelBase
{
    private readonly WorkshopContext _db;
    private readonly WorkshopFacade _facade;
    private readonly PricingService _pricing = new();
    private readonly RepairNotifier _notifier;
    private readonly EmailCustomerObserver _emailObserver;
    private readonly ManagerAlertObserver _managerObserver;
    private readonly WorkshopMediator _mediator;
    private readonly Dictionary<int, RepairHistory> _histories = new();
    private readonly List<RelayCommand> _orderDependentCommands;

    // --- Kolekcje ---
    public ObservableCollection<RepairOrder> Orders { get; } = new();
    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    public ObservableCollection<Mechanic> Mechanics { get; } = new();
    public ObservableCollection<Lift> Lifts { get; } = new();
    public ObservableCollection<Part> Parts { get; } = new();
    public ObservableCollection<string> Notifications { get; } = new();
    public ObservableCollection<string> PriceBreakdown { get; } = new();
    public ObservableCollection<PriceModifier> Modifiers { get; } = new();
    public ObservableCollection<ILaborCostStrategy> LaborStrategies { get; } = new();

    public Array Specializations => Enum.GetValues(typeof(MechanicSpecialization));
    public Array LiftTypes => Enum.GetValues(typeof(LiftType));
    public Array Statuses => Enum.GetValues(typeof(RepairStatus));
    public Array Stages => Enum.GetValues(typeof(RepairStage));

    // --- Formularz nowego zlecenia (Builder + Mediator) ---
    public Vehicle? NewVehicle { get; set; }
    public string NewFault { get; set; } = "";
    public MechanicSpecialization NewSpecialization { get; set; }
    public LiftType NewLiftType { get; set; }
    public int NewCapacityKg { get; set; } = 2000;
    public bool NewRequiresTool { get; set; }
    public decimal NewEstimatedHours { get; set; } = 2;
    public DateTime NewDate { get; set; } = DateTime.Today.AddDays(1);
    public int NewStartHour { get; set; } = 9;

    // --- Wycena ---
    public decimal HourlyRate { get; set; } = 150;
    private ILaborCostStrategy? _selectedStrategy;
    public ILaborCostStrategy? SelectedStrategy { get => _selectedStrategy; set => SetField(ref _selectedStrategy, value); }
    public string ModifierKind { get; set; } = "surcharge"; // surcharge|percent|discount
    public string ModifierLabel { get; set; } = "";
    public decimal ModifierValue { get; set; }
    private decimal _finalPrice;
    public decimal FinalPrice { get => _finalPrice; set => SetField(ref _finalPrice, value); }

    // --- Wybór ---
    private RepairOrder? _selectedOrder;
    public RepairOrder? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetField(ref _selectedOrder, value))
            {
                OnPropertyChanged(nameof(SelectedOrderLog));
                _orderDependentCommands.RaiseAll();
            }
        }
    }
    private Part? _selectedPart;
    public Part? SelectedPart
    {
        get => _selectedPart;
        set { if (SetField(ref _selectedPart, value)) AddPartCommand.RaiseCanExecuteChanged(); }
    }
    public int PartQuantity { get; set; } = 1;
    public RepairStatus TargetStatus { get; set; }
    public RepairStage TargetStage { get; set; }

    public IEnumerable<RepairLogEntry> SelectedOrderLog => SelectedOrder?.Log.ToList() ?? Enumerable.Empty<RepairLogEntry>();

    private string _status = "";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- Komendy ---
    public RelayCommand CreateOrderCommand { get; }
    public RelayCommand ChangeStatusCommand { get; }
    public RelayCommand AddPartCommand { get; }
    public RelayCommand AdvanceStageCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand AddModifierCommand { get; }
    public RelayCommand ComputePriceCommand { get; }

    public RepairOrdersViewModel(WorkshopContext db)
    {
        _db = db;

        _notifier = new RepairNotifier();
        _emailObserver = new EmailCustomerObserver();
        _managerObserver = new ManagerAlertObserver();
        _notifier.Subscribe(_emailObserver);
        _notifier.Subscribe(_managerObserver);
        _facade = new WorkshopFacade(_db, _notifier);

        _mediator = new WorkshopMediator(_db.Mechanics.ToList(), _db.Lifts.ToList(), _db.SpecialTools.ToList());

        LaborStrategies.Add(new TimeBasedLaborStrategy());
        LaborStrategies.Add(new ManufacturerNormLaborStrategy());
        LaborStrategies.Add(new FlatRateLaborStrategy(500));
        SelectedStrategy = LaborStrategies[0];

        CreateOrderCommand = new RelayCommand(CreateOrder);
        ChangeStatusCommand = new RelayCommand(ChangeStatus, _ => SelectedOrder != null);
        AddPartCommand = new RelayCommand(AddPart, _ => SelectedOrder != null && SelectedPart != null);
        AdvanceStageCommand = new RelayCommand(AdvanceStage, _ => SelectedOrder != null);
        UndoCommand = new RelayCommand(Undo, _ => SelectedOrder != null);
        AddModifierCommand = new RelayCommand(AddModifier);
        ComputePriceCommand = new RelayCommand(ComputePrice, _ => SelectedOrder != null);

        _orderDependentCommands = new List<RelayCommand>
        {
            ChangeStatusCommand, AddPartCommand, AdvanceStageCommand, UndoCommand, ComputePriceCommand
        };

        Load();
    }

    public void Load()
    {
        Vehicles.Clear();
        foreach (var v in _db.Vehicles.Include(v => v.Customer)) Vehicles.Add(v);
        Mechanics.Clear();
        foreach (var m in _db.Mechanics) Mechanics.Add(m);
        Lifts.Clear();
        foreach (var l in _db.Lifts) Lifts.Add(l);
        Parts.Clear();
        foreach (var p in _db.Parts) Parts.Add(p);
        ReloadOrders();
    }

    private void ReloadOrders()
    {
        Orders.Clear();
        foreach (var o in _facade.LoadOrders()) Orders.Add(o);
    }

    private void Log(string msg)
    {
        Status = msg;
        Notifications.Insert(0, $"{DateTime.Now:HH:mm:ss}  {msg}");
    }

    /// <summary>Tworzy zlecenie: Mediator przydziela zasoby, Builder składa obiekt, Facade zapisuje.</summary>
    private void CreateOrder(object? _)
    {
        if (NewVehicle is null) { Log("Wybierz pojazd."); return; }

        var start = NewDate.Date.AddHours(NewStartHour);
        var end = start.AddHours((double)Math.Max(1, NewEstimatedHours));
        var req = new ResourceRequest(NewSpecialization, NewLiftType, NewCapacityKg, NewRequiresTool, start, end);

        var alloc = _mediator.TryAllocate(req);
        if (!alloc.Success) { Log("Mediator: " + alloc.Message); return; }

        var builder = new RepairOrderBuilder()
            .ForVehicle(NewVehicle)
            .WithFault(NewFault)
            .AssignMechanic(alloc.Mechanic!)
            .UseLift(alloc.Lift!)
            .EstimateHours(NewEstimatedHours)
            .AddLabor("Diagnostyka i naprawa", HourlyRate, NewEstimatedHours);
        if (alloc.Tool is not null) builder.UseTool(alloc.Tool);

        var order = builder.Build();
        _facade.PersistNewOrder(order);
        ReloadOrders();
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == order.Id);
        Log($"Utworzono zlecenie #{order.Id}. Mediator: {alloc.Message}");
    }

    /// <summary>Wymusza odświeżenie wiersza zaznaczonego zlecenia w DataGridzie (model nie wysyła PropertyChanged).</summary>
    private void RefreshSelectedOrderRow()
    {
        if (SelectedOrder is null) return;
        var idx = Orders.IndexOf(SelectedOrder);
        if (idx >= 0) Orders[idx] = SelectedOrder;
    }

    private void ChangeStatus(object? _)
    {
        if (SelectedOrder is null) return;
        var (ok, msg) = _facade.ChangeStatus(SelectedOrder, TargetStatus);
        Log(msg);
        if (ok)
        {
            foreach (var m in _emailObserver.SentMessages.AsEnumerable().Reverse().Take(1))
                Notifications.Insert(0, m);
            OnPropertyChanged(nameof(SelectedOrderLog));
            RefreshSelectedOrderRow();
        }
    }

    private void AddPart(object? _)
    {
        if (SelectedOrder is null || SelectedPart is null) return;
        var (_, msg) = _facade.AddPartToOrder(SelectedOrder, SelectedPart, PartQuantity);
        // odśwież stan magazynu w lokalnej kolekcji
        OnPropertyChanged(nameof(SelectedOrderLog));
        Log(msg);
    }

    /// <summary>Przejście do kolejnego etapu jako Command (z możliwością Undo dzięki Memento).</summary>
    private void AdvanceStage(object? _)
    {
        if (SelectedOrder is null) return;
        var history = GetHistory(SelectedOrder.Id);
        var statusBefore = SelectedOrder.Status;
        var cmd = new AdvanceStageCommand(SelectedOrder, TargetStage, _db.Parts.Local.Any() ? _db.Parts.Local.ToList() : _db.Parts.ToList());
        history.Do(cmd);

        if (statusBefore != RepairStatus.GotoweDoOdbioru && SelectedOrder.Status == RepairStatus.GotoweDoOdbioru)
            _facade.RecordCompletion(SelectedOrder);

        _db.SaveChanges();
        OnPropertyChanged(nameof(SelectedOrderLog));
        RefreshSelectedOrderRow();
        Log($"Wykonano komendę: {cmd.Description}. W historii: {history.Count} operacji.");
    }

    private void Undo(object? _)
    {
        if (SelectedOrder is null) return;
        var history = GetHistory(SelectedOrder.Id);
        var msg = history.Undo();
        _db.SaveChanges();
        OnPropertyChanged(nameof(SelectedOrderLog));
        RefreshSelectedOrderRow();
        Log(msg);
    }

    private RepairHistory GetHistory(int orderId)
    {
        if (!_histories.TryGetValue(orderId, out var h))
        {
            h = new RepairHistory();
            _histories[orderId] = h;
        }
        return h;
    }

    private void AddModifier(object? _)
    {
        if (string.IsNullOrWhiteSpace(ModifierLabel) || ModifierValue == 0) return;
        Modifiers.Add(new PriceModifier(ModifierKind, ModifierLabel, ModifierValue));
    }

    /// <summary>Buduje finalną wycenę (Strategy + Decorator) i prezentuje rozbicie składników.</summary>
    private void ComputePrice(object? _)
    {
        if (SelectedOrder is null || SelectedStrategy is null) return;

        var discount = SelectedOrder.Vehicle?.Customer?.DiscountPercent ?? 0;
        var price = _pricing.BuildPrice(SelectedOrder, SelectedStrategy, HourlyRate, discount, Modifiers);

        PriceBreakdown.Clear();
        foreach (var (label, amount) in price.Breakdown())
            PriceBreakdown.Add($"{label}: {amount:0.00} zł");
        FinalPrice = price.GetPrice();
        Log($"Wyceniono zlecenie #{SelectedOrder.Id}: {FinalPrice:0.00} zł ({SelectedStrategy.Name}).");
    }
}
