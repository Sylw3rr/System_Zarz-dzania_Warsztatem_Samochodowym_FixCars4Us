# FixCars4Us — System Zarządzania Warsztatem Samochodowym

Aplikacja desktopowa (**C# / WPF / .NET 8**) do obsługi warsztatu samochodowego:
naprawy bieżące, diagnostyka, magazyn części, kartoteka klientów i pojazdów oraz
zaawansowany moduł zleceń napraw z wyceną i historią operacji.

Projekt na przedmiot **Programowanie zaawansowane**. Temat: *System Zarządzania
Warsztatem Samochodowym (FixCar4Us)*. Realizacja: **grupa 3-osobowa**.

---

## 1. Wymagania

- **.NET 8 SDK** (https://dotnet.microsoft.com/download)
- **System Windows** — aplikacja używa WPF (Windows Presentation Foundation),
  który działa wyłącznie na Windows.
- Opcjonalnie: Visual Studio 2022 lub JetBrains Rider.

Baza danych: **SQLite** (plik `fixcars4us.db` tworzony automatycznie przy
pierwszym uruchomieniu — nie trzeba niczego instalować ani konfigurować).

---

## 2. Uruchomienie

### Z linii poleceń

```bash
cd src/FixCars4Us.App
dotnet run
```

### Z Visual Studio

1. Otwórz `FixCars4Us.sln`.
2. Ustaw `FixCars4Us.App` jako projekt startowy.
3. Naciśnij **F5**.

Przy pierwszym uruchomieniu baza zostanie utworzona i wypełniona danymi
przykładowymi (klienci, pojazdy, części, mechanicy, podnośniki).

> Aby zacząć od czystej bazy, usuń plik `fixcars4us.db` z katalogu wyjściowego
> (`src/FixCars4Us.App/bin/Debug/net8.0/`).

---

## 3. Architektura

Rozwiązanie składa się z dwóch projektów (szczegóły: [`docs/ARCHITEKTURA.md`](docs/ARCHITEKTURA.md)):

| Projekt | Typ | Opis |
|---|---|---|
| `FixCars4Us.Core` | biblioteka (net8.0) | logika domenowa, wzorce projektowe, warstwa danych (EF Core), ViewModele (MVVM) |
| `FixCars4Us.App`  | aplikacja WPF (net8.0-windows) | warstwa prezentacji (XAML), powiązania z ViewModelami |

Dzięki rozdzieleniu cała logika biznesowa jest niezależna od WPF
(można ją testować i kompilować również poza Windows).

```
FixCars4Us.sln
├── src/FixCars4Us.Core/
│   ├── Models/          – encje domeny (Customer, Vehicle, RepairOrder, Part, ...)
│   ├── Enums/           – typy wyliczeniowe (statusy, etapy, specjalizacje)
│   ├── Data/            – WorkshopContext (EF Core + SQLite), DbInitializer (seed)
│   ├── Patterns/        – Builder, Decorator, Strategy, State, Observer, Mediator, Command+Memento
│   ├── Services/        – WorkshopFacade (Facade), PricingService
│   ├── Infrastructure/  – ViewModelBase, RelayCommand (MVVM)
│   └── ViewModels/      – po jednym ViewModelu na moduł + MainViewModel
└── src/FixCars4Us.App/
    ├── App.xaml         – zasoby i style aplikacji
    ├── MainWindow.xaml  – okno główne z zakładkami
    └── Views/           – widoki modułów (UserControl)
```

---

## 4. Zrealizowana funkcjonalność

### Funkcje podstawowe (5/5)

1. **Baza pojazdów i historia serwisowa** — rejestracja pojazdów (nr rej., VIN,
   przebieg) wraz z pełną historią napraw.
2. **Kartoteka klientów** — klienci prywatni i floty, rabaty stałe.
3. **Katalog części i usług** — części (ceny zakupu/sprzedaży) oraz cennik
   roboczogodzin wg kategorii prac.
4. **Zarządzanie magazynem** — przychody/rozchody, automatyczna aktualizacja
   stanów przy pobieraniu części do zlecenia, alerty niskiego stanu.
5. **Kalendarz przyjęć** — planowanie wizyt z przypisaniem do stanowiska,
   z kontrolą kolizji terminów.

### Funkcje dodatkowe (3/3)

1. **Inteligentne Przypisywanie Zasobów Warsztatowych (Mediator)** — koordynacja
   dostępności mechanika, podnośnika i narzędzia w zazębiających się przedziałach
   czasowych; blokada planowania przy braku któregokolwiek zasobu.
2. **Dynamiczny System Wyceny Naprawy (Strategy + Decorator)** — wymienne algorytmy
   naliczania robocizny oraz nakładane warstwami dopłaty/rabaty.
3. **Zarządzanie Etapami Naprawy z Funkcją Cofania (Command + Memento)** —
   etapy naprawy z możliwością cofnięcia, automatycznym przywróceniem stanów
   magazynowych i pełnym śladem rewizyjnym.

### Wzorce projektowe (8 — przy wymaganych 5)

`Builder` · `Decorator` · `Facade` · `State` · `Observer` · `Strategy` ·
`Mediator` · `Command + Memento`

Szczegółowy opis wraz z fragmentami kodu znajduje się w raporcie (`report/`).

---

## 5. Struktura repozytorium

```
.
├── FixCars4Us.sln
├── README.md
├── .gitignore
├── docs/
│   └── ARCHITEKTURA.md
├── report/
│   └── Raport_FixCars4Us.docx   – raport projektu
└── src/
    ├── FixCars4Us.Core/
    └── FixCars4Us.App/
```

---

## 6. Uwaga o użyciu AI

Projekt powstał z użyciem asystenta AI (opis zakresu, zalet i wad znajduje się
w raporcie, w rozdziale poświęconym AI).
