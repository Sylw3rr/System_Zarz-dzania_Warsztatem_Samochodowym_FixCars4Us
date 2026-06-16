// Plik: MainViewModel.cs
// Rola: Korzeń drzewa ViewModeli — tworzy bazę, inicjalizuje dane startowe
//       i udostępnia ViewModele poszczególnych zakładek okna głównego.
// Wzorzec: MVVM (Model-View-ViewModel) — wastwa ViewModel, kompozycja ViewModeli modułów.
//          Pośrednio: Kompozyt (Composite) — jeden ViewModel agreguje inne ViewModele.

using FixCars4Us.Core.Data;           // WorkshopContext, DbInitializer
using FixCars4Us.Core.Infrastructure; // ViewModelBase

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Główny ViewModel aplikacji. Tworzy kontekst bazy, wypełnia danymi startowymi
/// i udostępnia ViewModele poszczególnych modułów (zakładek) dla okna WPF.
/// </summary>
/// <remarks>
/// Dlaczego MainViewModel tworzy WorkshopContext (a nie App.xaml.cs)?
/// ViewModele są odpowiedzialne za dane — tworzenie DB w ViewModel jest bardziej
/// zgodne z MVVM niż umieszczanie logiki inicjalizacji w Code-Behind okna.
///
/// "Shared DbContext" — jeden kontekst EF Core współdzielony przez wszystkie
/// ViewModele modułów (przez konstruktor). To kluczowa decyzja architektoniczna:
/// zmiany dokonane w jednym ViewModelu (np. dodanie pojazdu) są natychmiast
/// widoczne dla innego (np. lista pojazdów do wyboru w zleceniu) bez synchronizacji.
///
/// Właściwość Db jest publiczna — dostęp w razie potrzeby (np. testy integracyjne),
/// ale ViewModele powinny korzystać z Db przez siebie, nie przez MainViewModel.
/// </remarks>
public class MainViewModel : ViewModelBase
{
    /// <summary>
    /// Współdzielony kontekst bazy danych — jedna sesja EF Core dla całej aplikacji.
    /// Publiczny dla dostępu testowego i debugowania — w produkcji dostęp pośredni.
    /// </summary>
    public WorkshopContext Db { get; }

    // ViewModele zakładek — każdy odpowiada jednemu modułowi aplikacji.
    // Inicjowane raz w konstruktorze — cykl życia równy cyklowi życia MainViewModel.
    public CustomersViewModel Customers { get; }       // Zakładka: Klienci i pojazdy
    public CatalogViewModel Catalog { get; }           // Zakładka: Katalog części i usług
    public InventoryViewModel Inventory { get; }       // Zakładka: Magazyn
    public AppointmentsViewModel Appointments { get; } // Zakładka: Kalendarz przyjęć
    public RepairOrdersViewModel Repairs { get; }      // Zakładka: Zlecenia napraw

    /// <summary>
    /// Inicjalizuje całą aplikację: tworzy bazę danych, sieje dane startowe
    /// i tworzy ViewModele dla wszystkich zakładek ze współdzielonym kontekstem.
    /// </summary>
    /// <param name="dbPath">
    /// Opcjonalna ścieżka do bazy danych. null = domyślna ścieżka obok EXE.
    /// Parametr umożliwia testowanie z bazą in-memory lub w tymczasowym pliku.
    /// </param>
    public MainViewModel(string? dbPath = null)
    {
        // Krok 1: Utwórz kontekst bazy danych (provider SQLite, plik fixcars4us.db).
        Db = new WorkshopContext(dbPath);

        // Krok 2: Zapewnij istnienie schematu i danych startowych (idempotentne).
        // EnsureCreated() — tworzy tabele jeśli nie istnieją.
        // EnsureSeeded()  — wypełnia przykładowymi danymi jeśli tabele są puste.
        DbInitializer.EnsureSeeded(Db);

        // Krok 3: Stwórz ViewModele zakładek przekazując JEDEN wspólny kontekst.
        // Dzięki temu wszystkie moduły widzą te same dane i tę samą sesję EF Core.
        Customers = new CustomersViewModel(Db);           // Kartoteka klientów
        Catalog = new CatalogViewModel(Db);               // Katalog części i cennik usług
        Inventory = new InventoryViewModel(Db);           // Ewidencja stanów magazynowych
        Appointments = new AppointmentsViewModel(Db);     // Kalendarz wizyt
        Repairs = new RepairOrdersViewModel(Db);          // Zlecenia napraw (centrum systemu)
    }
}
