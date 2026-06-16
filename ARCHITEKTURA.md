# Architektura systemu FixCars4Us

System Zarządzania Warsztatem Samochodowym — .NET 8 / WPF / SQLite / Entity Framework Core

---

## Spis plików

| Plik | Rola | Wzorzec projektowy |
|------|------|--------------------|
| `src/FixCars4Us.Core/Infrastructure/ViewModelBase.cs` | Bazowa klasa dla wszystkich ViewModeli; implementuje `INotifyPropertyChanged` i metodę `SetField<T>` eliminującą powtórzenia kodu powiadamiania | MVVM (Observer przez event) |
| `src/FixCars4Us.Core/Infrastructure/RelayCommand.cs` | Implementacja `ICommand` pozwalająca powiązać przyciski WPF z metodami ViewModel; klasa rozszerzenia `RelayCommandExtensions.RaiseAll()` | Command (GoF) |
| `src/FixCars4Us.Core/Enums/Enums.cs` | Słowniki dziedzinowe: typy klientów, specjalizacje mechaników, typy podnośników, kategorie prac, statusy i etapy zleceń, typy ruchów magazynowych | Domain Model (bez GoF) |
| `src/FixCars4Us.Core/Models/Models.cs` | Encje domenowe: `Customer`, `Vehicle`, `ServiceHistoryEntry`, `Part`, `ServiceType`, `Mechanic`, `Lift`, `SpecialTool`, `ReceptionStation`, `Appointment`, `RepairItem`, `RepairLogEntry`, `RepairOrder` | Domain Model; `Part` i `RepairOrder` implementują Observer przez `INotifyPropertyChanged` |
| `src/FixCars4Us.Core/Data/WorkshopContext.cs` | Kontekst EF Core; konfiguracja dostawcy SQLite, konfiguracja konwersji `decimal -> double` dla SQLite, DbSet dla każdej encji | Unit of Work + Repository (EF Core) |
| `src/FixCars4Us.Core/Data/DbInitializer.cs` | Idempotentna inicjalizacja bazy: `EnsureCreated()` + wypełnienie danymi przykładowymi (mechanicy, podnośniki, narzędzia, części, klienci, pojazdy, historia serwisowa) | Data Seeder (wzorzec aplikacyjny) |
| `src/FixCars4Us.Core/Patterns/RepairState.cs` | Cykl życia zlecenia: interfejs `IRepairState`, 7 klas stanów (`PrzyjeteState` ... `ZakonczoneState`), fabryka `RepairStateFactory` | **State** (GoF) + Factory Method |
| `src/FixCars4Us.Core/Patterns/CommandMemento.cs` | Funkcja cofania operacji etapowych: `RepairMemento` (migawka), `IRepairCommand`, `AdvanceStageCommand` (Execute/Undo), `RepairHistory` (stos Caretaker) | **Command** + **Memento** (GoF) |
| `src/FixCars4Us.Core/Patterns/Pricing.cs` | Silnik wyceny: 3 strategie robocizny (`ILaborCostStrategy`), komponent bazowy (`IPriceComponent`, `BaseCost`), 3 dekoratory (`SurchargeDecorator`, `PercentSurchargeDecorator`, `DiscountDecorator`) | **Strategy** + **Decorator** (GoF) |
| `src/FixCars4Us.Core/Patterns/RepairOrderBuilder.cs` | Fluent Builder do tworzenia `RepairOrder` krok po kroku; `Build()` ustawia status, etap i pierwszy wpis logu | **Builder** (GoF) |
| `src/FixCars4Us.Core/Patterns/Observer.cs` | Powiadamianie o zmianach statusu: `IRepairObserver`, `RepairNotifier` (Subject), `EmailCustomerObserver`, `ManagerAlertObserver` | **Observer** (GoF) |
| `src/FixCars4Us.Core/Patterns/WorkshopMediator.cs` | Inteligentne przypisywanie zasobów: zadanie `ResourceRequest`, wynik `ResourceAllocation`, `WorkshopMediator.TryAllocate()` -- atomowe przydzielenie mechanika, podniosnika i narzedzia | **Mediator** (GoF) |
| `src/FixCars4Us.Core/Services/WorkshopFacade.cs` | Panel Mechanika: `ChangeStatus()`, `AddPartToOrder()`, `LogWorkTime()`, `PersistNewOrder()`, `LoadOrders()` — jedno wywołanie = spójna operacja biznesowa | **Facade** (GoF) |
| `src/FixCars4Us.Core/Services/PricingService.cs` | Orkiestracja wyceny: `BuildPrice()` buduje potok `BaseCost -> Dekoratory -> Rabat klienta` i zwraca `IPriceComponent` | Service Layer; orchestruje Strategy + Decorator |
| `src/FixCars4Us.Core/ViewModels/MainViewModel.cs` | Korzeń drzewa ViewModeli: tworzy `WorkshopContext`, wywołuje `DbInitializer.EnsureSeeded()`, instancjonuje 5 ViewModeli zakładek z jednym współdzielonym DbContext | MVVM (Composite ViewModel) |
| `src/FixCars4Us.Core/ViewModels/CustomersViewModel.cs` | Zakładka "Klienci": zarządzanie kartoteką klientów, listą pojazdów i historią serwisową; komendy `AddCustomer`, `AddVehicle` | MVVM |
| `src/FixCars4Us.Core/ViewModels/CatalogViewModel.cs` | Zakładka "Katalog": zarządzanie częściami zamiennymi i cennikiem usług; komendy `AddPart`, `AddService` | MVVM |
| `src/FixCars4Us.Core/ViewModels/InventoryViewModel.cs` | Zakładka "Magazyn": ewidencja stanów magazynowych, ręczne korekty (przychód/rozchód), alert `LowStockParts`; komendy `StockInCommand`, `StockOutCommand` | MVVM; `StockMovement` — DTO |
| `src/FixCars4Us.Core/ViewModels/AppointmentsViewModel.cs` | Zakładka "Kalendarz przyjęć": planowanie wizyt z walidacją kolizji terminów; komenda `AddAppointmentCommand` | MVVM |
| `src/FixCars4Us.Core/ViewModels/RepairOrdersViewModel.cs` | Zakładka "Zlecenia napraw": centrum systemu łączące Builder, Mediator, State, Command, Memento, Strategy, Decorator, Observer, Facade | MVVM + wszystkie wzorce GoF |
| `src/FixCars4Us.App/MainWindow.xaml.cs` | Code-Behind okna głównego: `DataContext = new MainViewModel()`, obsługa `SelectionChanged` TabControl dla odświeżania danych między zakładkami | MVVM (minimalny Code-Behind) |

---

## Architektura MVVM

```
+----------------------------------------------------------+
|  VIEW (XAML)                                             |
|  MainWindow.xaml + zakładki TabControl                   |
|  Bindingi: {Binding Xxx} -> właściwości ViewModel        |
|  Command Binding: Command="{Binding AddCustomerCommand}" |
+------------------+---------------------------------------+
                   |  DataContext / INotifyPropertyChanged
                   |  ICommand (CanExecute / Execute)
+------------------v---------------------------------------+
|  VIEWMODEL                                               |
|  ViewModelBase (INotifyPropertyChanged + SetField)       |
|  RelayCommand (ICommand)                                 |
|  MainViewModel --+-- CustomersViewModel                  |
|                  +-- CatalogViewModel                    |
|                  +-- InventoryViewModel                  |
|                  +-- AppointmentsViewModel               |
|                  +-- RepairOrdersViewModel               |
+------------------+---------------------------------------+
                   |  wywołania metod (bez referencji na View)
+------------------v---------------------------------------+
|  MODEL (Domain + Services + Patterns)                    |
|  Models.cs — encje domenowe                              |
|  WorkshopContext — EF Core (Unit of Work)                |
|  WorkshopFacade — Facade                                 |
|  PricingService — Strategy + Decorator                   |
|  RepairOrderBuilder, WorkshopMediator, RepairNotifier,   |
|  RepairStateFactory, RepairHistory — wzorce GoF          |
+----------------------------------------------------------+
```

### Kluczowe zasady MVVM

1. **View nie zna ViewModelu** — View tylko wiąże się (Binding) z właściwościami i komendami.
2. **ViewModel nie zna View** — ViewModel nie ma referencji do żadnej kontrolki WPF. Powiadamia przez `INotifyPropertyChanged`.
3. **Model nie zna ViewModelu** — encje domenowe i serwisy są niezależne od warstwy prezentacji.
4. **Testability** — ViewModele można testować jednostkowo bez uruchamiania UI (brak zależności od WPF).

---

## Współdzielony DbContext (Shared DbContext)

### Problem który rozwiązuje

W aplikacji wielomodułowej każdy moduł może modyfikować dane (np. RepairOrdersViewModel obniża stan magazynowy).
Gdyby każdy ViewModel miał własny `WorkshopContext`, zmiany nie byłyby widoczne dla innych modułów bez jawnej synchronizacji.

### Rozwiązanie zastosowane w FixCars4Us

```
MainViewModel
|
+-- Db = new WorkshopContext()   <- jeden wspólny kontekst
|
+-- new CustomersViewModel(Db)   -+
+-- new CatalogViewModel(Db)     -+  wszystkie ViewModele
+-- new InventoryViewModel(Db)   -+  pracują na TYM SAMYM Db
+-- new AppointmentsViewModel(Db)-+
+-- new RepairOrdersViewModel(Db)-+
```

### Konsekwencje

| Zaleta | Wyjaśnienie |
|--------|-------------|
| Brak duplikacji danych | Zmiana stanu części (WorkshopFacade) jest natychmiast widoczna w InventoryViewModel |
| Brak synchronizacji | Nie potrzeba event-ów między modułami ani mechanizmu reload |
| Atomowe transakcje | `SaveChanges()` zatwierdza zmiany ze wszystkich modułów w jednej transakcji SQLite |

| Wada | Wyjaśnienie |
|------|-------------|
| Brak izolacji | Błąd w jednym module może wpłynąć na stan całego kontekstu |
| Thread-safety | DbContext nie jest thread-safe — aplikacja jednowątkowa (UI thread), więc to akceptowalne |

### Odświeżanie danych między zakładkami

Ponieważ ObservableCollection w każdym ViewModelu jest lokalną kopią danych z bazy, przy przełączeniu zakładki wywołujemy `Refresh()` aby dociągnąć zmiany dokonane przez inne moduły.
Logika odświeżania jest w `MainWindow.MainTabs_SelectionChanged()`.

---

## Wzorce projektowe — podsumowanie

| Wzorzec GoF | Gdzie zaimplementowany | Co rozwiązuje |
|-------------|------------------------|---------------|
| **MVVM** (architektoniczny) | Cała aplikacja | Separacja logiki od UI |
| **Command** | `RelayCommand`, `AdvanceStageCommand` | Przyciski WPF + cofanie operacji |
| **Memento** | `RepairMemento`, `RepairHistory` | Zapamiętywanie stanu dla Undo |
| **State** | `IRepairState`, klasy stanów, `RepairStateFactory` | Kontrola legalnych przejść statusów |
| **Builder** | `RepairOrderBuilder` | Czytelne tworzenie złożonego zlecenia |
| **Observer** | `RepairNotifier`, `IRepairObserver`, `EmailCustomerObserver`, `ManagerAlertObserver` | Powiadamianie o zmianach statusu |
| **Mediator** | `WorkshopMediator` | Inteligentne przypisywanie zasobów bez kolizji |
| **Strategy** | `ILaborCostStrategy`, 3 implementacje | Wymienne algorytmy wyceny robocizny |
| **Decorator** | `PriceDecorator`, 3 implementacje | Nakładanie warstw ceny bez modyfikacji istniejącego kodu |
| **Facade** | `WorkshopFacade` | Uproszczony interfejs dla złożonych operacji na zleceniu |
| **Factory Method** | `RepairStateFactory.Create()` | Tworzenie obiektów stanów bez ujawniania klas konkretnych |
