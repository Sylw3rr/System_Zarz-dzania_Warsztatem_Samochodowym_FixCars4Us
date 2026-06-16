// Plik: MainWindow.xaml.cs (Code-Behind okna głównego)
// Rola: Minimalny kod zarządzający oknem WPF — inicjalizacja DataContext
//       i odświeżanie danych przy przełączaniu zakładek.
// Wzorzec: MVVM — Code-Behind jest celowo ograniczony do minimum.
//          Żadna logika biznesowa nie powinna trafiać do Code-Behind.
//          Jedyna dozwolona logika: obsługa zdarzeń UI które nie mają
//          naturalnego odpowiednika w MVVM (np. SelectionChanged TabControl).

using System.Windows;            // Window, RoutedEventArgs
using System.Windows.Controls;   // TabControl, SelectionChangedEventArgs
using FixCars4Us.Core.ViewModels; // MainViewModel — korzeń drzewa ViewModeli

namespace FixCars4Us.App;

/// <summary>
/// Okno główne aplikacji FixCars4Us.
/// Code-Behind jest celowo minimalny — zgodnie z zasadami MVVM
/// logika biznesowa i logika prezentacji są w ViewModelach.
/// </summary>
/// <remarks>
/// "partial class" — klasa jest podzielona między dwa pliki:
/// - MainWindow.xaml: deklaracja UI w XAML (kontrolki, layout, bindingi)
/// - MainWindow.xaml.cs: obsługa zdarzeń (Code-Behind, minimalny)
///
/// Dlaczego DataContext = new MainViewModel() a nie Dependency Injection?
/// Dla prostoty aplikacji desktopowej "desktop-first". W architekturze
/// produkcyjnej kontener IoC (np. Microsoft.Extensions.DependencyInjection)
/// wstrzykiwałby MainViewModel z zewnątrz, co ułatwia testy i konfigurację.
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>
    /// Konstruktor okna głównego. Inicjalizuje komponenty XAML i ustawia DataContext.
    /// InitializeComponent() — generowane przez kompilator XAML, tworzy kontrolki z XAML.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent(); // Załaduj i zbuduj drzewo wizualne z MainWindow.xaml

        // Ustaw DataContext — to "klej" MVVM. Wszystkie bindingi w XAML ({Binding Xxx})
        // szukają właściwości Xxx w tym obiekcie.
        // MainViewModel: tworzy bazę (SQLite, EnsureCreated), wczytuje dane startowe
        // i inicjalizuje wszystkie ViewModele zakładek.
        DataContext = new MainViewModel();
    }

    /// <summary>
    /// Odświeża dane aktywnej zakładki przy przełączeniu — ViewModele dzielą jeden
    /// WorkshopContext, więc zmiany dokonane w innym module (np. nowy wpis historii
    /// serwisowej, rezerwacja terminu, zmiana stanu magazynu) muszą zostać dociągnięte.
    /// </summary>
    /// <remarks>
    /// Dlaczego odświeżamy ręcznie a nie przez reaktywny data binding?
    /// Ponieważ używamy "Shared DbContext" a nie reaktywnych strumieni (RxUI, LiveCharts).
    /// ObservableCollection nie "wie" że baza danych się zmieniła — trzeba ją jawnie
    /// przeładować. Alternatywa: IObservable + reaktywny ViewModel (bardziej złożone).
    ///
    /// Tylko 4 zakładki wymagają odświeżenia (indeksy 0, 2, 3, 4):
    /// - 0: Klienci — historia serwisowa mogła się zmienić po zakończeniu naprawy
    /// - 2: Magazyn — stany mogły zmienić się przez AddPartToOrder w Facade
    /// - 3: Kalendarz — nowe wizyty mogły zostać utworzone przez CreateOrder
    /// - 4: Zlecenia — zawsze odświeżane (to centrum systemu)
    /// Zakładka 1 (Katalog) nie wymaga odświeżenia — katalog zmienia tylko użytkownik tej zakładki.
    ///
    /// e.OriginalSource is TabControl tabs — sprawdza czy zdarzenie pochodzi z głównego
    /// TabControl (nie z zagnieżdżonego TabControl wewnątrz zakładki — jeśli taki istnieje).
    /// </remarks>
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: sprawdź czy DataContext to MainViewModel (bezpieczne castowanie przez "is not").
        if (DataContext is not MainViewModel vm || e.OriginalSource is not TabControl tabs) return;

        // Wywołaj Refresh() dla aktualnie wybranej zakładki.
        // switch expression na indeksie zakładki — indeks odpowiada kolejności w XAML.
        switch (tabs.SelectedIndex)
        {
            case 0: vm.Customers.Refresh();    break; // Zakładka "Klienci" — odśwież pojazdy i historię
            case 2: vm.Inventory.Refresh();    break; // Zakładka "Magazyn" — odśwież alerty niskich stanów
            case 3: vm.Appointments.Refresh(); break; // Zakładka "Kalendarz" — odśwież listę wizyt
            case 4: vm.Repairs.Refresh();      break; // Zakładka "Zlecenia" — odśwież listę zleceń
            // case 1 (Katalog) — brak Refresh, katalog jest edytowany tylko z tej zakładki
        }
    }
}
