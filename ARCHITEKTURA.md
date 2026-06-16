# Architektura systemu FixCars4Us

## Cel dokumentu

Opis struktury projektu dla celów edukacyjnych — każdy plik, jego rola i zastosowany wzorzec projektowy.
Projekt realizuje **System Zarządzania Warsztatem Samochodowym** na zaliczenie przedmiotu *Programowanie zaawansowane*.

---

## Struktura projektu

```
FixCars4Us/
├── src/
│   ├── FixCars4Us.Core/          ← Logika biznesowa (bez WPF)
│   │   ├── Enums/
│   │   │   └── Enums.cs
│   │   ├── Models/
│   │   │   └── Models.cs
│   │   ├── Data/
│   │   │   ├── WorkshopContext.cs
│   │   │   └── DbInitializer.cs
│   │   ├── Infrastructure/
│   │   │   ├── ViewModelBase.cs
│   │   │   └── RelayCommand.cs
│   │   ├── Patterns/
│   │   │   ├── RepairState.cs
│   │   │   ├── CommandMemento.cs
│   │   │   ├── Pricing.cs
│   │   │   ├── RepairOrderBuilder.cs
│   │   │   ├── Observer.cs
│   │   │   └── WorkshopMediator.cs
│   │   ├── Services/
│   │   │   ├── WorkshopFacade.cs
│   │   │   └── PricingService.cs
│   │   └── ViewModels/
│   │       ├── MainViewModel.cs
│   │       ├── CustomersViewModel.cs
│   │       ├── CatalogViewModel.cs
│   │       ├── InventoryViewModel.cs
│   │       ├── AppointmentsViewModel.cs
│   │       └── RepairOrdersViewModel.cs
│   └── FixCars4Us.App/           ← Warstwa prezentacji (WPF XAML)
│       ├── MainWindow.xaml
│       ├── MainWindow.xaml.cs
│       └── Views/                ← UserControls dla każdej zakładki
└── tests/
    └── FixCars4Us.Tests/         ← Testy jednostkowe (xUnit)
```

---

## Opis plików

### Enums/Enums.cs
**Rola:** Słowniki dziedziny biznesowej — wyliczenia używane w modelach i wzorcach.

| Enum | Wartości | Użycie |
|------|----------|--------|
| `CustomerType` | Prywatny, Flota | Typ klienta — Flota ma rabat (`DiscountDecorator`) |
| `MechanicSpecialization` | Mechanika, Elektryka, Lakiernictwo, Diagnostyka | Filtrowanie mechaników przez `WorkshopMediator` |
| `LiftType` | Osobowy, Ciezarowy, Lakierniczy | Dobór podnośnika przez `WorkshopMediator` |
| `WorkCategory` | Mechanika, Elektryka, Lakiernictwo, Diagnostyka | Stawka w `ServiceType` |
| `RepairStatus` | Przyjete → WDiagnostyce → OczekiwanieNaCzesci → WNaprawie → GotoweDoOdbioru → Zakonczone/Anulowane | Wzorzec **State** |
| `RepairStage` | Diagnostyka, ZamawianieCzesci, PraceWlasciwe, KontrolaJakosci | Wzorzec **Command+Memento** |
| `StockMovementType` | Przychod, Rozchod | Ewidencja magazynowa |

---

### Models/Models.cs
**Rola:** Klasy encji domenowych — mapowane przez EF Core na tabele SQLite.

| Klasa | Tabela | Opis |
|-------|--------|------|
| `Customer` | Customers | Klient (prywatny lub flotowy), ma pojazdy |
| `Vehicle` | Vehicles | Pojazd klienta, ma historię serwisową |
| `ServiceHistoryEntry` | ServiceHistory | Trwały zapis zakończonej naprawy |
| `Part` | Parts | Część z magazynu — implementuje `INotifyPropertyChanged` dla auto-odświeżania UI |
| `ServiceType` | ServiceTypes | Cennik usług (stawka godzinowa per kategoria) |
| `Mechanic` | Mechanics | Pracownik warsztatu ze specjalizacją |
| `Lift` | Lifts | Podnośnik / stanowisko — typ i udźwig |
| `SpecialTool` | SpecialTools | Narzędzie specjalistyczne (np. komputer diagnostyczny) |
| `ReceptionStation` | ReceptionStations | Stanowisko przyjęć klientów |
| `Appointment` | Appointments | Zaplanowana wizyta w kalendarzu |
| `RepairOrder` | RepairOrders | Zlecenie naprawy — implementuje `INotifyPropertyChanged` dla StatusBadge |
| `RepairItem` | RepairItems | Pozycja kosztorysu (część lub robocizna) |
| `RepairLogEntry` | RepairLog | Wpis dziennika audytowego zlecenia |

**Kluczowe:** `Part` i `RepairOrder` implementują `INotifyPropertyChanged` — zmiana stanu (`StockQuantity`, `Status`, `Stage`) natychmiast odświeża kolumny DataGrid bez wywołania `Refresh()`.

---

### Data/WorkshopContext.cs
**Rola:** Kontekst EF Core — "brama" do bazy danych SQLite.  
**Wzorzec:** Unit of Work + Repository (wbudowane w EF Core).

- `DbSet<T>` = tabela w bazie (Repository)
- `SaveChanges()` = zatwierdź wszystkie zmiany atomowo (Unit of Work)
- `OnModelCreating()` — konfiguracja schematu: konwersja `decimal → double` dla SQLite
- **Shared DbContext** — jeden kontekst dla całej aplikacji, wstrzykiwany do wszystkich ViewModeli

---

### Data/DbInitializer.cs
**Rola:** Seed danych startowych — wywoływany raz przy uruchomieniu.  
**Wzorzec:** Data Seeder (pomocniczy, nie GoF).

- `EnsureSeeded()` — idempotentna: sprawdza `db.Customers.Any()` i wychodzi jeśli dane już są
- Tworzy mechaników, podnośniki, narzędzia, stanowiska, katalog części, cennik, 2 klientów z pojazdami
- Dwa wywołania `SaveChanges()` — pierwsze nadaje `Id` encjom, drugie zapisuje powiązaną historię

---

### Infrastructure/ViewModelBase.cs
**Rola:** Bazowa klasa ViewModeli implementująca `INotifyPropertyChanged`.  
**Wzorzec:** Observer (C# events) — WPF binding nasłuchuje na `PropertyChanged`.

- `SetField<T>()` — ustaw pole jeśli wartość różna i wywołaj `PropertyChanged`
- `[CallerMemberName]` — automatycznie przekazuje nazwę właściwości (bez hardcodowania `"Status"`)

---

### Infrastructure/RelayCommand.cs
**Rola:** Implementacja `ICommand` dla WPF MVVM.  
**Wzorzec:** Command (GoF).

- Invoker = WPF Button (Command="{Binding ...Command}")
- Command = `RelayCommand` — przechowuje akcję i warunek
- Receiver = lambda przekazana do konstruktora
- `RaiseCanExecuteChanged()` — powiadamia WPF o zmianie dostępności przycisku
- `RelayCommandExtensions.RaiseAll()` — odświeża wiele komend naraz (extension method)

---

### Patterns/RepairState.cs
**Rola:** Maszyna stanów cyklu życia zlecenia naprawy.  
**Wzorzec:** State (GoF).

- `IRepairState` — interfejs stanu z listą dozwolonych przejść i metodą `CanTransitionTo()`
- Konkretne stany: `PrzyjeteState`, `WDiagnostyceState`, `OczekiwanieNaCzesciState`, `WNaprawieState`, `GotoweDoOdbioruState`, `AnulowaneState`, `ZakonczoneState`
- `RepairStateFactory` — mapuje `RepairStatus` (enum) na obiekt stanu (Factory Method)
- Stany terminalne (`Anulowane`, `Zakonczone`) mają `AllowedNext = Array.Empty<RepairStatus>()`

**Diagram przejść:**
```
Przyjete → WDiagnostyce → OczekiwanieNaCzesci → WNaprawie → GotoweDoOdbioru → Zakonczone
    ↘ Anulowane         ↘ Anulowane              ↘ Anulowane
```

---

### Patterns/CommandMemento.cs
**Rola:** Operacje z cofaniem (Undo) na etapach naprawy.  
**Wzorzec:** Command + Memento (GoF) — oba wzorce razem.

- `RepairMemento` — zrzut stanu (etap, status, godziny, stany magazynowe) przed operacją
- `IRepairCommand` — interfejs z `Execute()` i `Undo()`
- `AdvanceStageCommand` — zmienia etap zlecenia, tworzy Memento przed zmianą, przywraca przy Undo
- `RepairHistory` — Caretaker/Invoker: stos (Stack LIFO) wykonanych komend, `Do()` / `Undo()`
- Cofnięcie przywraca stany magazynowe (atomowo z etapem i statusem)

---

### Patterns/Pricing.cs
**Rola:** Dynamiczny silnik wyceny naprawy.  
**Wzorzec:** Strategy + Decorator (GoF).

**Strategy — metoda naliczania robocizny:**
- `ILaborCostStrategy` — interfejs strategii
- `TimeBasedLaborStrategy` — czas × stawka
- `ManufacturerNormLaborStrategy` — czas × 1.15 × stawka (narzut norm)
- `FlatRateLaborStrategy` — stała kwota (stawka jako ryczałt)

**Decorator — warstwy modyfikatorów ceny:**
- `IPriceComponent` — `GetPrice()` + `Breakdown()`
- `BasePrice` / `BaseCost` — cena bazowa (ConcreteComponent)
- `PriceDecorator` — abstrakcja dekoratora
- `SurchargeDecorator` — dopłata kwotowa (+50 zł)
- `PercentSurchargeDecorator` — dopłata procentowa (+20%)
- `DiscountDecorator` — rabat procentowy (−10%)

---

### Patterns/RepairOrderBuilder.cs
**Rola:** Krokowe, czytelne tworzenie zlecenia naprawy.  
**Wzorzec:** Builder (GoF) z Fluent API.

- Każda metoda zwraca `this` → łańcuchowanie: `.ForVehicle(v).WithFault("...").AssignMechanic(m).Build()`
- `Build()` ustawia `Status=Przyjete`, `Stage=Diagnostyka` i dodaje pierwszy wpis logu
- Director = `RepairOrdersViewModel.CreateOrder()`

---

### Patterns/Observer.cs
**Rola:** Powiadomienia o zmianach statusu zleceń.  
**Wzorzec:** Observer (GoF).

- `IRepairObserver` — interfejs obserwatora (`OnStatusChanged()`)
- `RepairNotifier` — Subject: rejestruje obserwatorów (`Subscribe/Unsubscribe`) i rozsyła (`Notify`)
- `EmailCustomerObserver` — symuluje wysyłkę e-mail do klienta
- `ManagerAlertObserver` — generuje alert dla managera (niski stan, gotowe do odbioru)

---

### Patterns/WorkshopMediator.cs
**Rola:** Atomowe przydzielanie zasobów warsztatowych z detekcją konfliktów czasowych.  
**Wzorzec:** Mediator (GoF).

- `ResourceRequest` — żądanie (specjalizacja, typ podnośnika, udźwig, narzędzie, okno czasu)
- `ResourceAllocation` — wynik: sukces + przydzielone zasoby lub komunikat o konflikcie
- `WorkshopMediator.TryAllocate()` — atomowo: albo wszystkie zasoby (mechanik + podnośnik + narzędzie), albo żaden
- Detekcja nakładania terminów: `start < b.End && b.Start < end` (klasyczny test zachodzenia przedziałów)

---

### Services/WorkshopFacade.cs
**Rola:** Uproszczony interfejs do operacji na zleceniach (jedno wywołanie = cała operacja biznesowa).  
**Wzorzec:** Facade (GoF).

| Metoda | Co ukrywa |
|--------|-----------|
| `ChangeStatus()` | State.CanTransitionTo + Status.set + Log.Add + RecordCompletion + SaveChanges + Notify |
| `RecordCompletion()` | Tworzenie ServiceHistoryEntry w _db |
| `AddPartToOrder()` | Walidacja magazynu + StockQuantity-- + Items.Add + Log.Add + Notify (niski stan) + SaveChanges |
| `LogWorkTime()` | EstimatedHours+= + Log.Add + SaveChanges |
| `PersistNewOrder()` | _db.RepairOrders.Add + SaveChanges |
| `LoadOrders()` | Include (Vehicle, Customer, Items, Log, Mechanic, Lift) + ToList |

---

### Services/PricingService.cs
**Rola:** Orkiestracja silnika wyceny — łączy Strategy z potoku Decoratorów.  
**Wzorzec:** Service Layer (orkiestrator Strategy + Decorator).

- `PriceModifier` — DTO opisujący jeden modyfikator (kind, label, value)
- `BuildPrice()` — buduje potok: BaseCost → [modyfikatory] → [rabat klienta]
- Kolejność ważna: rabat klienta nakładany ostatni (na cenę po dopłatach)

---

### ViewModels/MainViewModel.cs
**Rola:** Korzeń drzewa ViewModeli — inicjalizuje aplikację i współdzieli kontekst DB.  
**Wzorzec:** MVVM, Composite (agregacja ViewModeli modułów).

- Tworzy jeden `WorkshopContext` i przekazuje do WSZYSTKICH 5 ViewModeli modułów
- `DbInitializer.EnsureSeeded(Db)` — idempotentne, bezpieczne do wielokrotnego wywołania
- `DataContext = new MainViewModel()` w `MainWindow.xaml.cs` — punkt wejścia

---

### ViewModels/CustomersViewModel.cs
**Rola:** Kartoteka klientów → pojazdy → historia serwisowa (trzy poziomy hierarchii).  
**Wzorzec:** MVVM + Observer (INotifyPropertyChanged).

- `SelectedCustomer.set` → `LoadVehicles()` → `AddVehicleCommand.RaiseCanExecuteChanged()`
- `SelectedVehicle.set` → `LoadHistory()`
- `Refresh()` — wywoływane przez `MainWindow` przy przełączeniu na tę zakładkę
- Brak formularza ręcznego dodawania historii — historia powstaje wyłącznie przez `RecordCompletion`

---

### ViewModels/CatalogViewModel.cs
**Rola:** Katalog części zamiennych i cennik usług.  
**Wzorzec:** MVVM.

- Zarządza dwoma kolekcjami: `Parts` (magazyn) i `ServiceTypes` (cennik)
- `AddPart()` — dodaje część z formularzem kodu, nazwy, cen zakupu/sprzedaży, stanu, minimum
- `AddService()` — dodaje pozycję cennika (kategoria + stawka godzinowa)

---

### ViewModels/InventoryViewModel.cs
**Rola:** Ewidencja stanów magazynowych — przychody i rozchody.  
**Wzorzec:** MVVM + Observer (INotifyPropertyChanged na `Part.StockQuantity`).

- `StockInCommand` / `StockOutCommand` — zwiększ/zmniejsz stan wybranej części
- `LowStockParts` — widok obliczany: `Parts.Where(p => p.IsLowStock)` — alert dla managera
- `Refresh()` — `OnPropertyChanged(nameof(LowStockParts))` — odśwież alert po zmianie zakładki
- Automatyczny rozchód przy zleceniu naprawy realizuje `WorkshopFacade.AddPartToOrder()`

---

### ViewModels/AppointmentsViewModel.cs
**Rola:** Kalendarz przyjęć pojazdów z detekcją kolizji terminów.  
**Wzorzec:** MVVM.

- `AddAppointment()` — sprawdza kolizje: `_db.Appointments.Any(a => a.ReceptionStationId == id && start < a.End && a.Start < end)`
- `Refresh()` — przeładowuje wizyty przy przełączeniu zakładki
- Wizyty tworzone są też automatycznie przez `RepairOrdersViewModel.CreateOrder()`

---

### ViewModels/RepairOrdersViewModel.cs
**Rola:** Centralny moduł systemu — integruje wszystkie wzorce projektowe.  
**Wzorzec:** MVVM + Builder + Mediator + State + Command + Memento + Strategy + Decorator + Observer + Facade.

**Kluczowe mechanizmy:**

| Mechanizm | Kod |
|-----------|-----|
| Tworzenie zlecenia | `Mediator.TryAllocate()` → `Builder.Build()` → `Facade.PersistNewOrder()` |
| Etapy z cofaniem | `RepairHistory.Do(new AdvanceStageCommand(...))` / `.Undo()` |
| Wycena | `PricingService.BuildPrice(order, strategy, rate, discount, modifiers)` |
| Zakończenie | `Facade.ChangeStatus(order, Zakonczone)` → znika z listy |
| Auto-odświeżanie | `RepairOrder : INotifyPropertyChanged` — kolumny DataGrid same się aktualizują |
| Ochrona przed duplikatem historii | `HashSet<int> _completionRecorded` — `Add()` zwraca false przy drugim dodaniu |
| Dynamiczne etykiety formularza | `LaborInputLabel`, `ModifierValueLabel` — computed properties zależne od wyboru |

---

### App/MainWindow.xaml.cs
**Rola:** Code-Behind głównego okna — minimalna logika (odświeżanie zakładek przy przełączeniu).  
**Wzorzec:** MVVM — Code-Behind powinien być możliwie pusty.

- `DataContext = new MainViewModel()` — punkt podpięcia ViewModelu do widoku
- `MainTabs_SelectionChanged` — wywołuje `Refresh()` aktywnej zakładki przy przełączeniu:
  - Tab 0 (Klienci): `vm.Customers.Refresh()`
  - Tab 2 (Magazyn): `vm.Inventory.Refresh()`
  - Tab 3 (Kalendarz): `vm.Appointments.Refresh()`
  - Tab 4 (Zlecenia): `vm.Repairs.Refresh()`

---

## Wzorce projektowe — podsumowanie

| Wzorzec | Klasy | Kiedy działa |
|---------|-------|--------------|
| **State** | `IRepairState`, `PrzyjeteState`, ..., `RepairStateFactory` | `Facade.ChangeStatus()`, `AdvanceStageCommand.Execute()` |
| **Command** | `IRepairCommand`, `AdvanceStageCommand`, `RepairHistory` | Przycisk "Zmień etap", przycisk "Cofnij" |
| **Memento** | `RepairMemento`, `RepairHistory` | Przy Execute (zapis) i Undo (odczyt) |
| **Strategy** | `ILaborCostStrategy`, 3 implementacje | `PricingService.BuildPrice()` |
| **Decorator** | `IPriceComponent`, `PriceDecorator`, 3 dekoratory | `PricingService.BuildPrice()` |
| **Builder** | `RepairOrderBuilder` | `RepairOrdersViewModel.CreateOrder()` |
| **Observer** | `IRepairObserver`, `RepairNotifier`, 2 obserwatory | `Facade.ChangeStatus()`, `Facade.AddPartToOrder()` |
| **Mediator** | `WorkshopMediator`, `ResourceRequest`, `ResourceAllocation` | `RepairOrdersViewModel.CreateOrder()` |
| **Facade** | `WorkshopFacade` | Każda operacja na zleceniu |
| **Factory Method** | `RepairStateFactory` | `Facade.ChangeStatus()`, `AdvanceStageCommand.Execute()` |

---

## Architektura MVVM i Shared DbContext

### Warstwa podziału

```
FixCars4Us.App (WPF)          FixCars4Us.Core (logika)
─────────────────────         ──────────────────────────────
MainWindow.xaml               MainViewModel
Views/*.xaml  ──DataContext──► CustomersViewModel
                               CatalogViewModel
                               InventoryViewModel
                               AppointmentsViewModel
                               RepairOrdersViewModel
                                    │
                               WorkshopFacade
                                    │
                               WorkshopContext (SQLite)
```

### Shared DbContext — dlaczego jeden kontekst?

Wszystkie 5 ViewModeli dostaje **tę samą instancję** `WorkshopContext` od `MainViewModel`.

**Zaleta:** Zmiany w jednym module są natychmiast widoczne w innym:
- `RepairOrdersViewModel.AddPartToOrder()` zmniejsza `Part.StockQuantity`
- `InventoryViewModel` natychmiast widzi nową wartość — bo to **ten sam obiekt** w pamięci
- `INotifyPropertyChanged` na `Part.StockQuantity` odświeża kolumnę DataGrid bez `Refresh()`

**Wada:** Brak izolacji transakcji między modułami (akceptowalne dla aplikacji desktopowej z jednym użytkownikiem).

### Refresh() przy zmianie zakładki

`MainWindow.MainTabs_SelectionChanged` wywołuje `Refresh()` na aktywnym ViewModelu.  
Konieczne dla danych które nie mają `INotifyPropertyChanged` na poziomie kolekcji (np. `Appointments`):
- Nowa wizyta dodana przez `RepairOrdersViewModel` nie powiadomi `AppointmentsViewModel.Appointments`
- Dlatego `Refresh()` przeładowuje listę przy każdym wejściu na zakładkę

---

## Baza danych

**Provider:** SQLite (plik `fixcars4us.db` obok EXE)  
**ORM:** Entity Framework Core 8 bez migracji (`EnsureCreated()`)  
**Konwersja typów:** `decimal → double` dla wszystkich pól cenowych (SQLite nie obsługuje DECIMAL natywnie)

---

## Technologie

| Technologia | Wersja | Użycie |
|-------------|--------|--------|
| .NET | 8.0 | Target framework |
| C# | 12 | Język (record, switch expression, pattern matching, default interface methods) |
| WPF | .NET 8 | UI (XAML + DataBinding + Command) |
| Entity Framework Core | 8.x | ORM, SQLite provider |
| SQLite | 3.x | Baza danych (plik lokalny) |
| xUnit | 2.x | Testy jednostkowe (InMemory database) |
