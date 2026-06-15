namespace FixCars4Us.Core.Enums;

/// <summary>Typ klienta warsztatu.</summary>
public enum CustomerType
{
    Prywatny,
    Flota
}

/// <summary>Specjalizacja mechanika (wykorzystywana przez Mediator do dopasowania zasobów).</summary>
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
/// Przyjete -> WDiagnostyce -> OczekiwanieNaCzesci -> WNaprawie -> GotoweDoOdbioru.
/// </summary>
public enum RepairStatus
{
    Przyjete,
    WDiagnostyce,
    OczekiwanieNaCzesci,
    WNaprawie,
    GotoweDoOdbioru,
    Anulowane,
    Zakonczone
}

/// <summary>Etapy procesu naprawy (Command + Memento, funkcja cofania).</summary>
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
