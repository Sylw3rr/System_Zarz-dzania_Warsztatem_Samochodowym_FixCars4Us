using System.ComponentModel;
using System.Runtime.CompilerServices;
using FixCars4Us.Core.Enums;

namespace FixCars4Us.Core.Models;

/// <summary>Klient warsztatu (osoba prywatna lub flota).</summary>
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public CustomerType Type { get; set; } = CustomerType.Prywatny;
    /// <summary>Rabat stały dla klienta (np. flotowy) w procentach 0-100.</summary>
    public decimal DiscountPercent { get; set; }

    public List<Vehicle> Vehicles { get; set; } = new();

    public override string ToString() => $"{Name} ({Type})";
}

/// <summary>Pojazd przypisany do klienta wraz z historią serwisową.</summary>
public class Vehicle
{
    public int Id { get; set; }
    public string RegistrationNumber { get; set; } = "";
    public string Vin { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public int Mileage { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public List<ServiceHistoryEntry> History { get; set; } = new();

    public override string ToString() => $"{Brand} {Model} [{RegistrationNumber}]";
}

/// <summary>Pojedynczy wpis historii serwisowej pojazdu.</summary>
public class ServiceHistoryEntry
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string Description { get; set; } = "";
    public int MileageAtService { get; set; }
    public decimal Cost { get; set; }

    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
}

/// <summary>Część zamienna w katalogu i magazynie.</summary>
public class Part : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }

    private int _stockQuantity;
    public int StockQuantity
    {
        get => _stockQuantity;
        set
        {
            if (_stockQuantity == value) return;
            _stockQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLowStock));
        }
    }

    public int MinStock { get; set; } = 2;
    /// <summary>Czy część jest oryginałem (true) czy zamiennikiem (false).</summary>
    public bool IsOriginal { get; set; } = true;

    public bool IsLowStock => StockQuantity <= MinStock;

    public override string ToString() => $"{Name} [{Code}]";
}

/// <summary>Typ usługi / robocizny z cennikiem roboczogodziny.</summary>
public class ServiceType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public WorkCategory Category { get; set; }
    public decimal HourlyRate { get; set; }
    public override string ToString() => $"{Name} ({Category})";
}

/// <summary>Mechanik / pracownik warsztatu.</summary>
public class Mechanic
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public MechanicSpecialization Specialization { get; set; }
    public override string ToString() => $"{Name} ({Specialization})";
}

/// <summary>Stanowisko / podnośnik.</summary>
public class Lift
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public LiftType Type { get; set; }
    /// <summary>Maksymalny udźwig w kg.</summary>
    public int CapacityKg { get; set; }
    public override string ToString() => $"{Name} ({Type})";
}

/// <summary>Narzędzie specjalistyczne (np. komputer diagnostyczny).</summary>
public class SpecialTool
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

/// <summary>Stanowisko przyjęć dla kalendarza wizyt.</summary>
public class ReceptionStation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

/// <summary>Wizyta w kalendarzu przyjęć.</summary>
public class Appointment
{
    public int Id { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Reason { get; set; } = "";

    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public int ReceptionStationId { get; set; }
    public ReceptionStation? ReceptionStation { get; set; }
}

/// <summary>Pozycja kosztorysu naprawy (część albo robocizna).</summary>
public class RepairItem
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    /// <summary>Powiązanie z częścią (jeśli pozycja to część z magazynu).</summary>
    public int? PartId { get; set; }
    public Part? Part { get; set; }

    public int RepairOrderId { get; set; }
    public RepairOrder? RepairOrder { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>Wpis dziennika audytowego zlecenia (kto, co, kiedy).</summary>
public class RepairLogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Message { get; set; } = "";

    public int RepairOrderId { get; set; }
    public RepairOrder? RepairOrder { get; set; }
}

/// <summary>
/// Zlecenie naprawy — centralny agregat systemu.
/// Tworzone Builderem, posiada stan (State), etap (Command+Memento) i kosztorys (Decorator/Strategy).
/// </summary>
public class RepairOrder : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string FaultDescription { get; set; } = "";

    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public int? MechanicId { get; set; }
    public Mechanic? Mechanic { get; set; }

    public int? LiftId { get; set; }
    public Lift? Lift { get; set; }

    public int? SpecialToolId { get; set; }
    public SpecialTool? SpecialTool { get; set; }

    private RepairStatus _status = RepairStatus.Przyjete;
    public RepairStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    private RepairStage _stage = RepairStage.Diagnostyka;
    public RepairStage Stage
    {
        get => _stage;
        set
        {
            if (_stage == value) return;
            _stage = value;
            OnPropertyChanged();
        }
    }

    public decimal EstimatedHours { get; set; }

    public List<RepairItem> Items { get; set; } = new();
    public List<RepairLogEntry> Log { get; set; } = new();

    /// <summary>Suma pozycji kosztorysu (przed dopłatami Decoratora).</summary>
    public decimal ItemsTotal => Items.Sum(i => i.LineTotal);

    public override string ToString() => $"Zlecenie #{Id} – {Vehicle?.RegistrationNumber}";
}
