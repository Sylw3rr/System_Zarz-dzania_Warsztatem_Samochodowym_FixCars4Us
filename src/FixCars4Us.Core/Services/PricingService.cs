using FixCars4Us.Core.Models;
using FixCars4Us.Core.Patterns;

namespace FixCars4Us.Core.Services;

/// <summary>Opis pojedynczej warstwy modyfikującej cenę (dla budowy potoku Decoratorów).</summary>
public record PriceModifier(string Kind, string Label, decimal Value);
// Kind: "surcharge" (kwotowa), "percent" (procentowa dopłata), "discount" (procentowy rabat)

/// <summary>
/// Silnik wyceny naprawy. Łączy Strategy (koszt robocizny) z Decoratorem
/// (nakładanie kolejnych warstw kosztów i rabatów na cenę bazową).
/// </summary>
public class PricingService
{
    /// <summary>
    /// Buduje finalną wycenę: cena bazowa = części + robocizna (wg strategii),
    /// następnie kolejne modyfikatory nakładane jako Dekoratory.
    /// </summary>
    public IPriceComponent BuildPrice(
        RepairOrder order,
        ILaborCostStrategy laborStrategy,
        decimal hourlyRate,
        decimal customerDiscountPercent,
        IEnumerable<PriceModifier> modifiers)
    {
        var partsTotal = order.Items.Where(i => i.PartId.HasValue).Sum(i => i.LineTotal);
        var laborCost = laborStrategy.CalculateLaborCost(order, hourlyRate);

        IPriceComponent price = new BaseCost(partsTotal, laborCost, laborStrategy.Name);

        foreach (var m in modifiers)
        {
            price = m.Kind switch
            {
                "surcharge" => new SurchargeDecorator(price, m.Label, m.Value),
                "percent" => new PercentSurchargeDecorator(price, m.Label, m.Value),
                "discount" => new DiscountDecorator(price, m.Label, m.Value),
                _ => price
            };
        }

        if (customerDiscountPercent > 0)
            price = new DiscountDecorator(price, "Rabat stały klienta", customerDiscountPercent);

        return price;
    }
}
