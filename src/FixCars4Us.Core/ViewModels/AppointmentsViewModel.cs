// Plik: AppointmentsViewModel.cs
// Rola: ViewModel dla zakładki "Kalendarz przyjęć" — planowanie wizyt klientów
//       z przypisaniem pojazdu do stanowiska przyjęć i walidacją kolizji terminów.
// Wzorzec: MVVM. Walidacja kolizji terminów to prosta reguła biznesowa bez dodatkowego wzorca GoF.

using System.Collections.ObjectModel; // ObservableCollection
using FixCars4Us.Core.Data;           // WorkshopContext
using FixCars4Us.Core.Infrastructure; // ViewModelBase, RelayCommand
using FixCars4Us.Core.Models;         // Appointment, Vehicle, ReceptionStation
using Microsoft.EntityFrameworkCore;  // Include() — eager loading

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Funkcja podstawowa: Kalendarz przyjęć.
/// Planowanie terminów wizyt z przypisaniem auta do stanowiska przyjęć.
/// Waliduje kolizje terminów na tym samym stanowisku.
/// </summary>
/// <remarks>
/// Wizyta (Appointment) jest tworzona w dwóch scenariuszach:
/// 1. Ręcznie przez pracownika w tej zakładce (AddAppointment).
/// 2. Automatycznie przez RepairOrdersViewModel.CreateOrder() gdy tworzone jest zlecenie
///    — system stara się zarezerwować wolne stanowisko na czas naprawy.
///
/// Walidacja kolizji terminów: algorytm zachodzenia przedziałów czasowych.
/// Dwa terminy kolidują jeśli: start1 &lt; end2 AND start2 &lt; end1.
/// Sprawdzane przez LINQ Any() w AddAppointment — transluje na SQL EXISTS().
///
/// Status (string) to komunikat zwrotny dla użytkownika — sukces lub opis błędu.
/// Wzorzec: "Status Message" — prosta alternatywa dla dialogów modalnycg.
/// </remarks>
public class AppointmentsViewModel : ViewModelBase
{
    // Współdzielony kontekst bazy — Ten sam co we wszystkich ViewModelach.
    private readonly WorkshopContext _db;

    // Kolekcje obserwowalne dla list w UI.
    public ObservableCollection<Appointment> Appointments { get; } = new(); // Wszystkie zaplanowane wizyty
    public ObservableCollection<Vehicle> Vehicles { get; } = new();          // Lista pojazdów do wyboru
    public ObservableCollection<ReceptionStation> Stations { get; } = new(); // Stanowiska przyjęć

    // Pola formularza nowej wizyty — bindowane do kontrolek UI.
    public Vehicle? SelectedVehicle { get; set; }          // Wybrany pojazd (ComboBox)
    public ReceptionStation? SelectedStation { get; set; } // Wybrane stanowisko (ComboBox)
    public DateTime NewDate { get; set; } = DateTime.Today.AddDays(1); // Domyślnie jutro (DatePicker)
    public int StartHour { get; set; } = 9;                // Godzina rozpoczęcia (0-23)
    public int DurationHours { get; set; } = 2;            // Czas trwania wizyty w godzinach
    public string Reason { get; set; } = "";               // Powód wizyty / opis usterki

    // Pole zapasowe dla właściwości Status.
    private string _status = "";

    /// <summary>
    /// Komunikat statusu dla użytkownika — sukces ("Dodano wizytę...") lub błąd ("Kolizja!...").
    /// INotifyPropertyChanged przez SetField — UI (TextBlock) odświeża się automatycznie.
    /// </summary>
    public string Status { get => _status; set => SetField(ref _status, value); }

    // Komenda dla przycisku "Zaplanuj wizytę".
    public RelayCommand AddAppointmentCommand { get; }

    /// <summary>
    /// Konstruktor: inicjalizuje komendę i wczytuje dane z bazy.
    /// </summary>
    public AppointmentsViewModel(WorkshopContext db)
    {
        _db = db;
        AddAppointmentCommand = new RelayCommand(AddAppointment); // Zawsze aktywna (brak CanExecute)
        Load(); // Wczytaj pojazdy, stanowiska i wizyty przy starcie
    }

    /// <summary>
    /// Wczytuje pojazdy (z klientami), stanowiska przyjęć i listę wizyt z bazy.
    /// Include(v => v.Customer) potrzebne do wyświetlenia "Ford Transit [GD99887] (TransLog...)" w UI.
    /// </summary>
    public void Load()
    {
        Vehicles.Clear();
        // Include(v => v.Customer) — eager loading klienta razem z pojazdem (dla etykiety ComboBox).
        foreach (var v in _db.Vehicles.Include(v => v.Customer)) Vehicles.Add(v);

        Stations.Clear();
        foreach (var s in _db.ReceptionStations) Stations.Add(s); // Stanowiska przyjęć

        ReloadAppointments(); // Wczytaj listę zaplanowanych wizyt
    }

    /// <summary>Odświeża wizyty oraz pojazdy/stanowiska (np. po dodaniu zlecenia lub pojazdu w innej zakładce).</summary>
    /// <remarks>
    /// Wywoływana przez MainWindow.MainTabs_SelectionChanged gdy użytkownik przełącza na tę zakładkę.
    /// CreateOrder() w RepairOrdersViewModel może tworzyć wizyty automatycznie, a CustomersViewModel
    /// może dodać nowy pojazd — Load() przeładowuje wszystko, nie tylko listę wizyt.
    /// </remarks>
    public void Refresh() => Load();

    /// <summary>
    /// Przeładowuje listę wizyt z bazy — posortowane chronologicznie od najwcześniejszej.
    /// Include(a.Vehicle) i Include(a.ReceptionStation) potrzebne do wyświetlenia szczegółów.
    /// </summary>
    private void ReloadAppointments()
    {
        Appointments.Clear();
        foreach (var a in _db.Appointments
                     .Include(a => a.Vehicle)          // Załaduj pojazd (dla numeru rejestracyjnego)
                     .Include(a => a.ReceptionStation) // Załaduj stanowisko (dla nazwy w liście)
                     .OrderBy(a => a.Start))           // Sortuj chronologicznie (najwcześniejsze pierwsze)
            Appointments.Add(a);
    }

    /// <summary>
    /// Dodaje nową wizytę po walidacji danych i sprawdzeniu kolizji.
    /// Walidacje: wybrany pojazd i stanowisko, brak kolizji terminów.
    /// </summary>
    private void AddAppointment(object? _)
    {
        // Walidacja: wymagany pojazd i stanowisko.
        if (SelectedVehicle is null || SelectedStation is null)
        {
            Status = "Wybierz pojazd i stanowisko."; // Komunikat dla użytkownika
            return; // Fail fast — brak zmian w bazie
        }

        // Oblicz przedział czasowy wizyty.
        var start = NewDate.Date.AddHours(StartHour);              // Data + godzina (np. 2024-01-15 09:00)
        var end = start.AddHours(Math.Max(1, DurationHours));      // Koniec (minimum 1 godzina)

        // Sprawdź kolizje na tym samym stanowisku.
        // Any() w EF Core transluje na SQL EXISTS() — wydajne, nie ładuje danych do pamięci.
        bool conflict = _db.Appointments.Any(a =>
            a.ReceptionStationId == SelectedStation.Id &&  // To samo stanowisko
            start < a.End && a.Start < end);               // Zachodzące przedziały (klasyczny test)

        if (conflict)
        {
            // Stanowisko zajęte w wybranym terminie — poinformuj użytkownika.
            Status = $"Kolizja! Stanowisko {SelectedStation.Name} jest zajęte w tym czasie.";
            return; // Fail fast — nie twórz wizyty
        }

        // Brak kolizji — utwórz i zapisz wizytę.
        var appt = new Appointment
        {
            VehicleId = SelectedVehicle.Id,           // FK do pojazdu
            ReceptionStationId = SelectedStation.Id,  // FK do stanowiska
            Start = start,                             // Czas rozpoczęcia
            End = end,                                 // Czas zakończenia
            Reason = Reason                            // Opis powodu wizyty
        };

        _db.Appointments.Add(appt); // Zarejestruj w kontekście
        _db.SaveChanges();          // Zapisz do bazy

        ReloadAppointments(); // Odśwież listę (nowa wizyta pojawi się w odpowiednim miejscu)

        // Komunikat sukcesu z datą i rejestracją dla potwierdzenia.
        Status = $"Dodano wizytę: {start:g} – {SelectedVehicle.RegistrationNumber}.";
    }
}
