// Plik: Enums.cs
// Rola: Zbiór wyliczeń (enum) definiujących słowniki dziedziny biznesowej warsztatu.
//       Wyliczenia są używane zarówno w modelach domenowych, jak i we wzorcach projektowych
//       (State, Command, Strategy, Mediator). Przechowywanie ich w jednym pliku ułatwia
//       przeglądanie całego modelu pojęciowego aplikacji.
// Wzorzec: Brak konkretnego wzorca GoF — to warstwa domeny (Domain Layer).

namespace FixCars4Us.Core.Enums;

/// <summary>Typ klienta warsztatu.</summary>
/// <remarks>
/// Rozróżnienie Prywatny/Flota wpływa na logikę wyceny:
/// klienci flotowi często mają stały rabat procentowy (DiscountDecorator).
/// </remarks>
public enum CustomerType
{
    Prywatny, // Osoba fizyczna, jednorazowe wizyty
    Flota     // Firma z wieloma pojazdami, negocjowany rabat (DiscountPercent)
}

/// <summary>Specjalizacja mechanika (wykorzystywana przez Mediator do dopasowania zasobów).</summary>
/// <remarks>
/// WorkshopMediator używa tej wartości do filtrowania mechanika:
/// szuka pierwszego wolnego mechanika o wymaganej specjalizacji w danym oknie czasowym.
/// </remarks>
public enum MechanicSpecialization
{
    Mechanika,    // Silnik, zawieszenie, układ hamulcowy
    Elektryka,    // Instalacja elektryczna, alternatory, akumulatory
    Lakiernictwo, // Blacharstwo i lakierowanie nadwozia
    Diagnostyka   // Diagnostyka komputerowa, czytanie kodów błędów
}

/// <summary>Typ stanowiska / podnośnika.</summary>
/// <remarks>
/// Mediator filtruje podnośniki według typu i udźwigu (CapacityKg).
/// Np. ciężarówka wymaga podnośnika Ciezarowy z wystarczającym udźwigiem.
/// </remarks>
public enum LiftType
{
    Osobowy,    // Standardowy podnośnik do samochodów osobowych
    Ciezarowy,  // Podnośnik do pojazdów dostawczych i ciężarowych
    Lakierniczy // Stanowisko lakiernicze z filtracją powietrza
}

/// <summary>Kategoria pracy (wpływa na stawkę roboczogodziny).</summary>
/// <remarks>
/// Każdy typ usługi (ServiceType) ma przypisaną kategorię i stawkę.
/// Wzorzec Strategy używa stawki wraz z szacowanymi godzinami do wyliczenia robocizny.
/// </remarks>
public enum WorkCategory
{
    Mechanika,    // Prace mechaniczne — stawka standardowa
    Elektryka,    // Prace elektryczne — wyższa stawka (wymagane uprawnienia SEP)
    Lakiernictwo, // Prace lakiernicze — najwyższa stawka (materiały i kabina)
    Diagnostyka   // Diagnostyka komputerowa — stawka godzinowa za czas pracy komputera
}

/// <summary>
/// Statusy zlecenia naprawy (wzorzec State).
/// Przyjete -&gt; WDiagnostyce -&gt; OczekiwanieNaCzesci -&gt; WNaprawie -&gt; GotoweDoOdbioru.
/// </summary>
/// <remarks>
/// Każdemu statusowi odpowiada klasa stanu (IRepairState) w pliku RepairState.cs.
/// Klasa stanu definiuje dozwolone przejścia (AllowedNext) — nie można
/// "cofnąć" statusu ani przeskoczyć etapów bez jawnego zezwolenia.
/// Anulowane i Zakonczone to stany terminalne (AllowedNext = puste).
/// </remarks>
public enum RepairStatus
{
    Przyjete,               // Zlecenie właśnie przyjęte do warsztatu
    WDiagnostyce,           // Pojazd na stanowisku, trwa diagnostyka
    OczekiwanieNaCzesci,    // Diagnoza gotowa, zamówiono brakujące części
    WNaprawie,              // Części dostarczone, trwają właściwe prace
    GotoweDoOdbioru,        // Naprawa zakończona, pojazd gotowy dla klienta
    Anulowane,              // Zlecenie anulowane (stan terminalny)
    Zakonczone              // Pojazd odebrany przez klienta (stan terminalny)
}

/// <summary>Etapy procesu naprawy (Command + Memento, funkcja cofania).</summary>
/// <remarks>
/// Etap (RepairStage) jest bardziej granularny niż status (RepairStatus).
/// AdvanceStageCommand zapisuje etap w Memento przed zmianą, umożliwiając cofnięcie (Undo).
/// Mapowanie etap -&gt; status odbywa się w metodzie MapStageToStatus komendy.
/// </remarks>
public enum RepairStage
{
    Diagnostyka,        // Wstępne badanie usterki (odpowiada statusowi WDiagnostyce)
    ZamawianieCzesci,   // Zamówienie brakujących części (OczekiwanieNaCzesci)
    PraceWlasciwe,      // Właściwa naprawa (WNaprawie) — tutaj pobierane są części z magazynu
    KontrolaJakosci     // Sprawdzenie po naprawie (GotoweDoOdbioru)
}

/// <summary>Rodzaj ruchu magazynowego.</summary>
/// <remarks>
/// Używany w InventoryViewModel do ewidencji ruchów (przychód towaru / rozchód na zlecenie).
/// Automatyczny rozchód przy AddPartToOrder w WorkshopFacade nie generuje wpisu StockMovement
/// (ten jest tworzony tylko przez ręczne operacje w magazynie) — to celowe uproszczenie.
/// </remarks>
public enum StockMovementType
{
    Przychod, // Dostawa od dostawcy — zwiększa stan magazynowy
    Rozchod   // Wydanie na zlecenie lub korekta — zmniejsza stan magazynowy
}
