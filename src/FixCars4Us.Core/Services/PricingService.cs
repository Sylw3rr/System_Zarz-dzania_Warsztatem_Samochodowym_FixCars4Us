// Plik: PricingService.cs
// Rola: Usługa obliczania końcowej ceny naprawy dla klienta.
//       Łączy wzorzec Strategy (wybór metody wyceny robocizny) z wzorcem Decorator
//       (nakładanie warstw modyfikatorów ceny).
// Wzorzec: SERVICE LAYER + STRATEGY + DECORATOR.
//          Klasa serwisowa (Service Layer) orkiestruje wzorce z pliku Pricing.cs.

using FixCars4Us.Core.Models;    // RepairOrder — dane wejściowe dla wyceny
using FixCars4Us.Core.Patterns;  // ILaborCostStrategy, IPriceComponent, BaseCost, dekoratory

namespace FixCars4Us.Core.Services;

/// <summary>Opis pojedynczej warstwy modyfikującej cenę (dla budowy potoku Decoratorów).</summary>
/// <remarks>
/// Record (C# 9+) — immutowalny DTO (Data Transfer Object).
/// Kind określa typ dekoratora który zostanie użyty:
/// - "surcharge" -> SurchargeDecorator (kwota stała, np. 50 zł za utylizację)
/// - "percent"   -> PercentSurchargeDecorator (procent dopłaty, np. 20% za trudny dostęp)
/// - "discount"  -> DiscountDecorator (procent rabatu, np. 15% dla stałego klienta)
/// </remarks>
public record PriceModifier(string Kind, string Label, decimal Value);
// Kind: "surcharge" (kwotowa), "percent" (procentowa dopłata), "discount" (procentowy rabat)

/// <summary>
/// Silnik wyceny naprawy. Łączy Strategy (koszt robocizny) z Decoratorem
/// (nakładanie kolejnych warstw kosztów i rabatów na cenę bazową).
/// </summary>
/// <remarks>
/// PricingService jest "dyrektorem" (Director w terminologii Builder/Decorator):
/// wie w jakiej kolejności budować potok wyceny, ale deleguje każdy krok do
/// odpowiednich obiektów (strategii, dekoratorów).
///
/// Bezstanowość: PricingService nie przechowuje żadnego stanu między wywołaniami.
/// Możemy tworzyć jeden obiekt i używać dla wielu zleceń równolegle (thread-safe).
/// </remarks>
public class PricingService
{
    /// <summary>
    /// Buduje finalną wycenę: cena bazowa = części + robocizna (wg strategii),
    /// następnie kolejne modyfikatory nakładane jako Dekoratory.
    /// </summary>
    /// <param name="order">Zlecenie — źródło pozycji kosztorysu i szacowanych godzin.</param>
    /// <param name="laborStrategy">Strategia naliczania robocizny (wybrana przez użytkownika).</param>
    /// <param name="hourlyRate">Stawka godzinowa (zł/h) — wejście dla strategii.</param>
    /// <param name="customerDiscountPercent">Stały rabat klienta (0 = brak rabatu).</param>
    /// <param name="modifiers">Lista modyfikatorów do nałożenia jako Dekoratory.</param>
    /// <returns>IPriceComponent gotowy do wywołania GetPrice() i Breakdown().</returns>
    /// <remarks>
    /// Potok wyceny (pipeline):
    ///   BaseCost(partsTotal, laborCost)
    ///     -> [opcjonalnie] SurchargeDecorator / PercentSurchargeDecorator / DiscountDecorator (x N)
    ///     -> [opcjonalnie] DiscountDecorator("Rabat stały klienta", customerDiscountPercent)
    ///
    /// Kolejność ma znaczenie: rabat klienta jest nakładany OSTATNI aby dawał zniżkę
    /// od ceny już uwzględniającej wszystkie dopłaty (a nie od ceny bazowej).
    /// </remarks>
    public IPriceComponent BuildPrice(
        RepairOrder order,
        ILaborCostStrategy laborStrategy,
        decimal hourlyRate,
        decimal customerDiscountPercent,
        IEnumerable<PriceModifier> modifiers)
    {
        // KROK 1: Oblicz koszt części — suma wartości pozycji które mają PartId (to części, nie robocizna).
        var partsTotal = order.Items.Where(i => i.PartId.HasValue).Sum(i => i.LineTotal);

        // KROK 2: Oblicz koszt robocizny przez wybraną strategię.
        // Strategy decyduje jak użyć hourlyRate i EstimatedHours — bez if/switch tutaj.
        var laborCost = laborStrategy.CalculateLaborCost(order, hourlyRate);

        // KROK 3: Stwórz cenę bazową (ConcreteComponent w Decorator).
        // BaseCost rozbija cenę na dwie pozycje (części i robocizna) dla czytelnego rozbicia.
        IPriceComponent price = new BaseCost(partsTotal, laborCost, laborStrategy.Name);

        // KROK 4: Nakładaj modyfikatory jako Dekoratory (jeden po drugim).
        // "price" jest nadpisywane — każdy dekorator owija poprzednią warstwę.
        foreach (var m in modifiers)
        {
            // Switch expression (C# 8+) mapuje Kind na odpowiedni typ dekoratora.
            price = m.Kind switch
            {
                "surcharge" => new SurchargeDecorator(price, m.Label, m.Value),          // Dopłata kwotowa
                "percent"   => new PercentSurchargeDecorator(price, m.Label, m.Value),   // Dopłata procentowa
                "discount"  => new DiscountDecorator(price, m.Label, m.Value),           // Rabat procentowy
                _ => price // Nieznany typ — zignoruj (bezpieczne, bez modyfikacji ceny)
            };
        }

        // KROK 5: Rabat klienta nakładany OSTATNI — na cenę po wszystkich dopłatach.
        // Warunek > 0 zapobiega tworzeniu DiscountDecorator z rabatem 0% (bez efektu).
        if (customerDiscountPercent > 0)
            price = new DiscountDecorator(price, "Rabat stały klienta", customerDiscountPercent);

        // Zwróć gotowy potok — wywołujący (ComputePrice w VM) wołam GetPrice() i Breakdown().
        return price;
    }
}
