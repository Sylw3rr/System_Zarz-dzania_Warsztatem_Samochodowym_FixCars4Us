// Wzorce: Strategy (naliczanie robocizny) + Decorator (warstwy kosztów/zniżek).

using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>Strategy — wymienny algorytm naliczania kosztu robocizny.</summary>
public interface ILaborCostStrategy
{
    /// <summary>Czytelna nazwa strategii — wyświetlana w UI do wyboru metody.</summary>
    string Name { get; }

    decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate);
}

/// <summary>Rozliczenie według rzeczywistego, szacowanego czasu pracy.</summary>
public class TimeBasedLaborStrategy : ILaborCostStrategy
{
    public string Name => "Według czasu rzeczywistego";

    public decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate)
        => order.EstimatedHours * hourlyRate;
}

/// <summary>Rozliczenie według norm producenta (narzut 15% do czasu).</summary>
public class ManufacturerNormLaborStrategy : ILaborCostStrategy
{
    public string Name => "Według norm producenta";

    public decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate)
        => Math.Round(order.EstimatedHours * 1.15m, 2) * hourlyRate;
}

/// <summary>Ryczałt za usługę — stała kwota niezależna od liczby godzin.</summary>
public class FlatRateLaborStrategy : ILaborCostStrategy
{
    public string Name => "Ryczałt";

    // hourlyRate traktowane tu jako kwota ryczałtu, EstimatedHours ignorowane.
    public decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate) => hourlyRate;
}

/// <summary>Decorator — komponent wyceny, bazowy element łańcucha modyfikatorów.</summary>
public interface IPriceComponent
{
    decimal GetPrice();

    /// <summary>Rozbicie składników wyceny (od najstarszego do najnowszego w łańcuchu).</summary>
    IEnumerable<(string Label, decimal Amount)> Breakdown();
}

/// <summary>Cena bazowa = jedna gotowa kwota kosztorysu.</summary>
public class BasePrice : IPriceComponent
{
    private readonly decimal _amount;
    private readonly string _label;

    public BasePrice(decimal amount, string label = "Kosztorys bazowy")
    {
        _amount = amount;
        _label = label;
    }

    public decimal GetPrice() => _amount;

    public IEnumerable<(string, decimal)> Breakdown() { yield return (_label, _amount); }
}

/// <summary>Cena bazowa z rozbiciem na części i robociznę.</summary>
public class BaseCost : IPriceComponent
{
    private readonly decimal _partsTotal;
    private readonly decimal _laborCost;
    private readonly string _laborLabel;

    public BaseCost(decimal partsTotal, decimal laborCost, string laborLabel)
    {
        _partsTotal = partsTotal;
        _laborCost = laborCost;
        _laborLabel = laborLabel;
    }

    public decimal GetPrice() => _partsTotal + _laborCost;

    public IEnumerable<(string, decimal)> Breakdown()
    {
        yield return ("Części", _partsTotal);
        yield return ($"Robocizna ({_laborLabel})", _laborCost);
    }
}

/// <summary>Bazowy dekorator — nakłada kolejną warstwę kosztu/zniżki na cenę.</summary>
public abstract class PriceDecorator : IPriceComponent
{
    protected readonly IPriceComponent Inner;

    protected PriceDecorator(IPriceComponent inner) => Inner = inner;

    public abstract decimal GetPrice();

    protected abstract (string Label, decimal Amount) Modifier();

    public IEnumerable<(string, decimal)> Breakdown()
    {
        foreach (var b in Inner.Breakdown()) yield return b;
        yield return Modifier();
    }
}

/// <summary>Dopłata kwotowa (np. utylizacja płynów, szybki termin).</summary>
public class SurchargeDecorator : PriceDecorator
{
    private readonly string _label;
    private readonly decimal _amount;

    public SurchargeDecorator(IPriceComponent inner, string label, decimal amount) : base(inner)
    {
        _label = label;
        _amount = amount;
    }

    public override decimal GetPrice() => Inner.GetPrice() + _amount;

    protected override (string, decimal) Modifier() => ($"Dopłata: {_label}", _amount);
}

/// <summary>Dopłata procentowa — kwota zależy od ceny po poprzednich warstwach.</summary>
public class PercentSurchargeDecorator : PriceDecorator
{
    private readonly string _label;
    private readonly decimal _percent;

    public PercentSurchargeDecorator(IPriceComponent inner, string label, decimal percent) : base(inner)
    {
        _label = label;
        _percent = percent;
    }

    public override decimal GetPrice()
        => Math.Round(Inner.GetPrice() * (1 + _percent / 100m), 2);

    protected override (string, decimal) Modifier()
        => ($"Dopłata {_percent}% : {_label}", Math.Round(Inner.GetPrice() * _percent / 100m, 2));
}

/// <summary>Rabat procentowy (np. rabat flotowy / stały klient).</summary>
public class DiscountDecorator : PriceDecorator
{
    private readonly string _label;
    private readonly decimal _percent;

    public DiscountDecorator(IPriceComponent inner, string label, decimal percent) : base(inner)
    {
        _label = label;
        _percent = percent;
    }

    public override decimal GetPrice()
        => Math.Round(Inner.GetPrice() * (1 - _percent / 100m), 2);

    // Kwota ujemna — widoczna jako "-xxx zł" w rozbiciu.
    protected override (string, decimal) Modifier()
        => ($"Rabat {_percent}% : {_label}", -Math.Round(Inner.GetPrice() * _percent / 100m, 2));
}
