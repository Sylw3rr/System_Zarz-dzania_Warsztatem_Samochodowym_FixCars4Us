// Plik: Pricing.cs
// Rola: Dynamiczny silnik wyceny naprawy — oblicza końcową cenę dla klienta
//       uwzględniając metodę naliczania robocizny i dowolną liczbę modyfikatorów.
// Wzorce:
//   STRATEGY  — wymienny algorytm naliczania kosztu robocizny (ILaborCostStrategy)
//   DECORATOR — warstwy kosztów/zniżek nakładane na cenę bazową (PriceDecorator)

using FixCars4Us.Core.Models; // RepairOrder — źródło danych dla wyceny

namespace FixCars4Us.Core.Patterns;

// =====================================================================
//  DYNAMICZNY SYSTEM WYCENY NAPRAWY (Repair Pricing Engine)
//  Wzorce: Strategy (sposób naliczania robocizny) + Decorator (warstwy kosztów).
// =====================================================================

/// <summary>
/// WZORZEC: Strategy.
/// Wymienny algorytm naliczania kosztu robocizny dla zlecenia.
/// </summary>
/// <remarks>
/// Wzorzec Strategy definiuje rodzinę algorytmów, enkapsuluje każdy z nich
/// i sprawia, że są wymienne. Tu: trzy strategie naliczania kosztu pracy.
///
/// Zamiast if/switch w kodzie kalkulacji:
///   if (selectedMethod == "time") cost = hours * rate;
///   else if (selectedMethod == "norm") cost = hours * 1.15 * rate;
///   ...
/// mamy zamknięty interfejs ILaborCostStrategy i otwarte klasy strategii.
/// Dodanie nowej metody rozliczania = nowa klasa implementująca interfejs,
/// bez zmiany istniejącego kodu (zasada Open/Closed).
///
/// Context = PricingService, Strategy = ILaborCostStrategy,
/// ConcreteStrategy = TimeBasedLaborStrategy | ManufacturerNormLaborStrategy | FlatRateLaborStrategy
/// </remarks>
public interface ILaborCostStrategy
{
    /// <summary>Czytelna nazwa strategii — wyświetlana w UI do wyboru metody.</summary>
    string Name { get; }

    /// <summary>
    /// Oblicza koszt robocizny dla zlecenia przy podanej stawce godzinowej.
    /// Każda implementacja stosuje inny algorytm.
    /// </summary>
    decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate);
}

/// <summary>Rozliczenie według rzeczywistego, szacowanego czasu pracy.</summary>
/// <remarks>
/// Najprostsza i najczęstsza metoda: czas * stawka.
/// EstimatedHours pochodzi z formularza zlecenia lub logowania czasu mechanika.
/// </remarks>
public class TimeBasedLaborStrategy : ILaborCostStrategy
{
    public string Name => "Według czasu rzeczywistego";

    // Obliczenie: czas pracy * stawka za godzinę. Prosto i transparentnie dla klienta.
    public decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate)
        => order.EstimatedHours * hourlyRate;
}

/// <summary>Rozliczenie według norm producenta (np. narzut 15% do czasu).</summary>
/// <remarks>
/// W branży motoryzacyjnej producenci publikują "normy czasowe" dla typowych napraw
/// (np. wymiana sprzęgła = 3.5 roboczogodziny, niezależnie od rzeczywistego czasu).
/// Tu symulujemy to jako stały narzut 15% powyżej czasu szacowanego.
/// Math.Round(..., 2) — zaokrąglamy do groszy przed mnożeniem przez stawkę
/// aby uniknąć efektu kumulacji błędów zaokrąglenia.
/// </remarks>
public class ManufacturerNormLaborStrategy : ILaborCostStrategy
{
    public string Name => "Według norm producenta";

    // Narzut 15% (1.15m) na czas — "m" to sufiks literału decimal (nie double, nie float).
    public decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate)
        => Math.Round(order.EstimatedHours * 1.15m, 2) * hourlyRate;
}

/// <summary>Ryczałt za konkretną usługę — stała kwota niezależna od liczby godzin (podana w polu stawki).</summary>
/// <remarks>
/// Dla prostych, standardowych usług (np. "wymiana żarówki" = 50 zł ryczałt)
/// łatwiej podać stałą kwotę niż przeliczać godziny.
/// hourlyRate jest tu nadużyciem nazwy — de facto to "kwota ryczałtu".
/// Interfejs Strategy pozwala na taką elastyczność bez zmiany reszty kodu.
/// </remarks>
public class FlatRateLaborStrategy : ILaborCostStrategy
{
    public string Name => "Ryczałt";

    // Ignorujemy EstimatedHours — zwracamy stawkę jako stałą kwotę całkowitą.
    public decimal CalculateLaborCost(RepairOrder order, decimal hourlyRate) => hourlyRate;
}

// ---------------------------------------------------------------------
// WZORZEC: Decorator — nakładanie warstw kosztów na cenę bazową.
// Zaleta: łatwe dodawanie i usuwanie modyfikatorów bez zmiany istniejącego kodu.
// Klient (PricingService) nie wie ile warstw jest nałożonych — każda wygląda
// tak samo (IPriceComponent).
// ---------------------------------------------------------------------

/// <summary>Komponent wyceny — bazowy element łańcucha dekoratorów.</summary>
/// <remarks>
/// Wzorzec Decorator: Component = IPriceComponent.
/// Zarówno "czysta" cena bazowa (BasePrice, BaseCost) jak i każdy Decorator
/// implementują ten interfejs — dzięki czemu można je układać w dowolnej
/// kolejności i kombinacji (jak warstwy cebuli).
/// </remarks>
public interface IPriceComponent
{
    /// <summary>Zwraca całkowitą wycenę po zastosowaniu wszystkich modyfikatorów w łańcuchu.</summary>
    decimal GetPrice();

    /// <summary>Czytelne rozbicie składników wyceny (do podglądu w UI/raporcie).</summary>
    /// <remarks>
    /// Każdy element łańcucha dodaje swój wpis do rozbicia.
    /// Wynik to sekwencja (Label, Amount) od najtańszego do najdroższego składnika.
    /// IEnumerable + yield return = leniwe wyliczanie (dane generowane na żądanie).
    /// </remarks>
    IEnumerable<(string Label, decimal Amount)> Breakdown();
}

/// <summary>Cena bazowa = suma pozycji kosztorysu (części + robocizna).</summary>
/// <remarks>
/// Najprostszy ConcreteComponent w wzorcu Decorator — przechowuje jedną kwotę.
/// Używany gdy chcemy podać gotową kwotę bez rozróżnienia na części i robociznę.
/// </remarks>
public class BasePrice : IPriceComponent
{
    private readonly decimal _amount; // Łączna kwota kosztorysu bazowego
    private readonly string _label;   // Opis etykiety dla rozbicia

    /// <summary>
    /// Tworzy cenę bazową z podaną kwotą i etykietą opisu.
    /// </summary>
    public BasePrice(decimal amount, string label = "Kosztorys bazowy")
    {
        _amount = amount; // Przechowaj kwotę
        _label = label;   // Przechowaj etykietę dla rozbicia
    }

    // GetPrice zwraca kwotę bez modyfikacji — brak dekoratora nad tym elementem.
    public decimal GetPrice() => _amount;

    // Breakdown zwraca jedną pozycję — etykietę i kwotę.
    // "yield return" = iterator — element jest generowany leniwie gdy ktoś przetwarza sekwencję.
    public IEnumerable<(string, decimal)> Breakdown() { yield return (_label, _amount); }
}

/// <summary>Cena bazowa z rozbiciem na części i robociznę.</summary>
/// <remarks>
/// Bardziej szczegółowy ConcreteComponent — pokazuje oddzielnie koszt części
/// i koszt robocizny (z nazwą wybranej strategii). Używany przez PricingService.BuildPrice().
/// </remarks>
public class BaseCost : IPriceComponent
{
    private readonly decimal _partsTotal; // Suma wartości wszystkich części na kosztorysie
    private readonly decimal _laborCost;  // Koszt robocizny wyliczony przez wybraną strategię
    private readonly string _laborLabel;  // Nazwa strategii (np. "Według czasu rzeczywistego")

    /// <summary>Tworzy rozbity kosztorys bazowy z oddzielnymi kwotami dla części i robocizny.</summary>
    public BaseCost(decimal partsTotal, decimal laborCost, string laborLabel)
    {
        _partsTotal = partsTotal;  // Koszt części
        _laborCost = laborCost;    // Koszt robocizny (Strategy)
        _laborLabel = laborLabel;  // Nazwa strategii (dla UI)
    }

    // GetPrice = suma obu składowych (przed nałożeniem dekoratorów).
    public decimal GetPrice() => _partsTotal + _laborCost;

    // Breakdown zwraca dwie pozycje: osobno części i robocizna.
    public IEnumerable<(string, decimal)> Breakdown()
    {
        yield return ("Części", _partsTotal);                          // Wiersz: koszt części
        yield return ($"Robocizna ({_laborLabel})", _laborCost);      // Wiersz: koszt robocizny ze strategią
    }
}

/// <summary>
/// WZORZEC: Decorator.
/// Bazowy dekorator pozwalający "nakładać" kolejne warstwy kosztów/zniżek
/// na cenę bazową bez modyfikacji istniejącego kodu.
/// </summary>
/// <remarks>
/// Wzorzec Decorator: Decorator = PriceDecorator (abstrakcja),
/// ConcreteDecorator = SurchargeDecorator | PercentSurchargeDecorator | DiscountDecorator.
///
/// Struktura: Inner (owinięty komponent) + Modifier (co ten dekorator dodaje/odejmuje).
/// GetPrice() = Inner.GetPrice() ± modifikacja.
/// Breakdown() = rozbicie Inner-a + własny modifier.
///
/// Przykład łańcucha:
///   BaseCost(1000)
///   -> SurchargeDecorator("Utylizacja", 50)   => GetPrice = 1050
///   -> DiscountDecorator("Rabat flotowy", 10%) => GetPrice = 945
///   -> PercentSurchargeDecorator("Pilność",5%) => GetPrice = 992.25
/// </remarks>
public abstract class PriceDecorator : IPriceComponent
{
    // Inner — zawinięty komponent (cena bazowa lub poprzedni dekorator).
    // "protected" — dostępny w klasach dziedziczących dla ewentualnych nadpisań.
    protected readonly IPriceComponent Inner;

    // Konstruktor przyjmuje komponent do owinięcia — Dependency Injection przez konstruktor.
    protected PriceDecorator(IPriceComponent inner) => Inner = inner;

    /// <summary>Oblicza cenę po modyfikacji — każda podklasa definiuje swój wzór.</summary>
    public abstract decimal GetPrice();

    /// <summary>Zwraca wpis tego dekoratora do rozbicia wyceny (etykieta i kwota modyfikacji).</summary>
    protected abstract (string Label, decimal Amount) Modifier();

    /// <summary>
    /// Rozbicie wyceny: najpierw rozbicie wnętrza (Inner), potem własny modifier.
    /// "foreach + yield return" łączy dwie sekwencje bez tworzenia tymczasowych list.
    /// </summary>
    public IEnumerable<(string, decimal)> Breakdown()
    {
        foreach (var b in Inner.Breakdown()) yield return b; // Wszystkie poprzednie warstwy
        yield return Modifier(); // Własny wpis tego dekoratora
    }
}

/// <summary>Dopłata kwotowa (np. utylizacja płynów, szybki termin).</summary>
/// <remarks>
/// Dodaje stałą kwotę do ceny — prosta implementacja modyfikatora kwotowego.
/// Przykład: SurchargeDecorator(price, "Utylizacja płynów", 50) doda 50 zł do ceny.
/// </remarks>
public class SurchargeDecorator : PriceDecorator
{
    private readonly string _label;   // Opis dopłaty (wyświetlany w rozbicu)
    private readonly decimal _amount; // Kwota dopłaty w złotych

    /// <summary>Tworzy dekorator dopłaty kwotowej.</summary>
    public SurchargeDecorator(IPriceComponent inner, string label, decimal amount) : base(inner)
    {
        _label = label;   // Zapamiętaj opis
        _amount = amount; // Zapamiętaj kwotę dopłaty
    }

    // Cena = cena wnętrza + stała kwota dopłaty.
    public override decimal GetPrice() => Inner.GetPrice() + _amount;

    // Modifier zwraca etykietę i kwotę (dodatnią) dla rozbicia.
    protected override (string, decimal) Modifier() => ($"Dopłata: {_label}", _amount);
}

/// <summary>Dopłata procentowa (np. "trudny dostęp do śrub" +20%).</summary>
/// <remarks>
/// Dodaje procent od bieżącej ceny — kwota dopłaty zależy od wartości poprzednich warstw.
/// Kolejność dekoratorów ma znaczenie: 10% od 1000 = 100, ale 10% od 1050 (po dopłacie 50) = 105.
/// </remarks>
public class PercentSurchargeDecorator : PriceDecorator
{
    private readonly string _label;   // Opis dopłaty procentowej
    private readonly decimal _percent; // Procent dopłaty (np. 20 = 20%)

    /// <summary>Tworzy dekorator procentowej dopłaty.</summary>
    public PercentSurchargeDecorator(IPriceComponent inner, string label, decimal percent) : base(inner)
    {
        _label = label;     // Zapamiętaj opis
        _percent = percent; // Zapamiętaj wartość procentową
    }

    // Cena = cena wnętrza * (1 + percent/100). Math.Round do 2 miejsc = grosze.
    public override decimal GetPrice()
        => Math.Round(Inner.GetPrice() * (1 + _percent / 100m), 2);

    // Modifier: etykieta z procentem i kwota dopłaty (obliczona od ceny wnętrza).
    protected override (string, decimal) Modifier()
        => ($"Dopłata {_percent}% : {_label}", Math.Round(Inner.GetPrice() * _percent / 100m, 2));
}

/// <summary>Rabat procentowy (np. rabat flotowy / stały klient).</summary>
/// <remarks>
/// Odejmuje procent od ceny — wzór odwrotny do PercentSurchargeDecorator.
/// Używany gdy Customer.DiscountPercent > 0 (klient z rabatem stałym).
/// Kwota rabatu jest ujemna w rozbicu (dla wyraźności w UI).
/// </remarks>
public class DiscountDecorator : PriceDecorator
{
    private readonly string _label;    // Opis rabatu (np. "Rabat stały klienta")
    private readonly decimal _percent; // Procent rabatu (np. 10 = 10%)

    /// <summary>Tworzy dekorator rabatu procentowego.</summary>
    public DiscountDecorator(IPriceComponent inner, string label, decimal percent) : base(inner)
    {
        _label = label;     // Zapamiętaj opis
        _percent = percent; // Zapamiętaj procent rabatu
    }

    // Cena = cena wnętrza * (1 - percent/100). Przy rabacie 10%: cena * 0.9.
    public override decimal GetPrice()
        => Math.Round(Inner.GetPrice() * (1 - _percent / 100m), 2);

    // Modifier: kwota jest ujemna (odjęty rabat) — widoczne jako "-xxx zł" w rozbicu.
    protected override (string, decimal) Modifier()
        => ($"Rabat {_percent}% : {_label}", -Math.Round(Inner.GetPrice() * _percent / 100m, 2));
}
