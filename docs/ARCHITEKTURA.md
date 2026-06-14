# Architektura systemu FixCars4Us

## 1. Przegląd

System zbudowano w architekturze **warstwowej** z wzorcem prezentacji **MVVM**
(Model–View–ViewModel). Rozwiązanie dzieli się na dwa projekty:

- **FixCars4Us.Core** — logika domenowa, wzorce projektowe, warstwa dostępu do
  danych (EF Core) oraz ViewModele. Projekt typu `net8.0` (niezależny od systemu
  operacyjnego).
- **FixCars4Us.App** — warstwa prezentacji w WPF (`net8.0-windows`). Zawiera
  wyłącznie pliki XAML i minimalny kod opakowujący; cała logika delegowana jest do
  ViewModeli z projektu Core.

```
┌──────────────────────────────────────────────────────────┐
│                  FixCars4Us.App (WPF)                      │
│   MainWindow.xaml  +  Views/*.xaml  (warstwa View)         │
└───────────────▲───────────────────────────────────────────┘
                │  DataBinding (WPF Binding / RelayCommand)
┌───────────────┴───────────────────────────────────────────┐
│                 FixCars4Us.Core                            │
│                                                            │
│   ViewModels/   ── MainViewModel, CustomersViewModel, ...  │
│        │                                                   │
│   Services/     ── WorkshopFacade (Facade), PricingService │
│        │                                                   │
│   Patterns/     ── Builder, Decorator, Strategy, State,    │
│        │            Observer, Mediator, Command+Memento    │
│        │                                                   │
│   Data/         ── WorkshopContext (EF Core), DbInitializer│
│        │                                                   │
│   Models/ Enums/ ── encje domeny                           │
└───────────────▲────────────────────────────────────────────┘
                │  EF Core
        ┌───────┴────────┐
        │   SQLite (.db) │
        └────────────────┘
```

## 2. Warstwy

### 2.1. Model (domena) — `Models/`, `Enums/`
Encje opisujące dziedzinę warsztatu: `Customer`, `Vehicle`,
`ServiceHistoryEntry`, `Part`, `ServiceType`, `Mechanic`, `Lift`, `SpecialTool`,
`ReceptionStation`, `Appointment`, `RepairOrder`, `RepairItem`, `RepairLogEntry`.

### 2.2. Dostęp do danych — `Data/`
- `WorkshopContext` — kontekst EF Core skonfigurowany na SQLite. Schemat tworzony
  metodą `EnsureCreated()` (bez migracji, dla maksymalnej prostoty uruchomienia).
- `DbInitializer` — tworzy bazę i wypełnia danymi przykładowymi.

### 2.3. Logika i wzorce — `Patterns/`, `Services/`
Implementacje wzorców projektowych oraz serwisy aplikacyjne (m.in. Fasada
`WorkshopFacade`, silnik wyceny `PricingService`).

### 2.4. ViewModel — `ViewModels/`, `Infrastructure/`
ViewModele eksponują dane jako `ObservableCollection<T>` i operacje jako
`RelayCommand` (ICommand). Bazują na `ViewModelBase` (INotifyPropertyChanged).

### 2.5. View — `FixCars4Us.App`
Widoki WPF (`UserControl`) powiązane z ViewModelami przez DataBinding.
Okno główne (`MainWindow`) zawiera `TabControl` z pięcioma modułami.

## 3. Mapowanie wzorców na elementy systemu

| Wzorzec | Plik | Zastosowanie |
|---|---|---|
| Builder | `Patterns/RepairOrderBuilder.cs` | składanie zlecenia naprawy |
| Strategy | `Patterns/Pricing.cs` (`ILaborCostStrategy`) | algorytmy naliczania robocizny |
| Decorator | `Patterns/Pricing.cs` (`PriceDecorator`) | warstwy dopłat/rabatów |
| State | `Patterns/RepairState.cs` | cykl życia zlecenia |
| Observer | `Patterns/Observer.cs` | powiadomienia o zmianie statusu |
| Mediator | `Patterns/WorkshopMediator.cs` | przydział zasobów warsztatu |
| Command + Memento | `Patterns/CommandMemento.cs` | etapy naprawy z cofaniem |
| Facade | `Services/WorkshopFacade.cs` | „Panel Mechanika” |

## 4. Przepływ tworzenia zlecenia (przykład)

1. Użytkownik wypełnia formularz w `RepairsView` → dane trafiają do
   `RepairOrdersViewModel`.
2. ViewModel buduje `ResourceRequest` i pyta `WorkshopMediator` o dostępność
   zasobów (mechanik + podnośnik + ewentualne narzędzie).
3. Jeśli zasoby są dostępne — `RepairOrderBuilder` składa obiekt `RepairOrder`.
4. `WorkshopFacade` zapisuje zlecenie przez `WorkshopContext` (EF Core → SQLite).
5. Zmiany statusu przechodzą przez maszynę stanów (`State`) i wyzwalają
   powiadomienia (`Observer`).
6. Wycena liczona jest przez `PricingService` (Strategy + Decorator).
7. Operacje na etapach rejestrowane są jako komendy (`Command`) z możliwością
   cofnięcia dzięki migawkom (`Memento`).
