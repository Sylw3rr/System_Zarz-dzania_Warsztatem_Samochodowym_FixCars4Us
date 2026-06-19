namespace FixCars4Us.Core.Enums;

/// <summary>Typ klienta warsztatu.</summary>
public enum CustomerType
{
    Prywatny,
    Flota // może mieć stały rabat (DiscountPercent)
}

/// <summary>Specjalizacja mechanika (używana przez Mediator do dopasowania zasobów).</summary>
public enum MechanicSpecialization
{
    Mechanika,
    Elektryka,
    Lakiernictwo,
    Diagnostyka
}

/// <summary>Typ stanowiska / podnośnika.</summary>
public enum LiftType
{
    Osobowy,
    Ciezarowy,
    Lakierniczy
}

/// <summary>Kategoria pracy (wpływa na stawkę roboczogodziny).</summary>
public enum WorkCategory
{
    Mechanika,
    Elektryka,
    Lakiernictwo,
    Diagnostyka
}

/// <summary>
/// Statusy zlecenia naprawy (wzorzec State).
/// Przyjete -&gt; WDiagnostyce -&gt; OczekiwanieNaCzesci -&gt; WNaprawie -&gt; GotoweDoOdbioru.
/// </summary>
public enum RepairStatus
{
    Przyjete,
    WDiagnostyce,
    OczekiwanieNaCzesci,
    WNaprawie,
    GotoweDoOdbioru,
    Anulowane,   // stan terminalny
    Zakonczone   // stan terminalny
}

/// <summary>Etapy procesu naprawy — bardziej granularne niż status (Command + Memento, Undo).</summary>
public enum RepairStage
{
    Diagnostyka,
    ZamawianieCzesci,
    PraceWlasciwe,
    KontrolaJakosci
}

/// <summary>Rodzaj ruchu magazynowego.</summary>
public enum StockMovementType
{
    Przychod,
    Rozchod
}
