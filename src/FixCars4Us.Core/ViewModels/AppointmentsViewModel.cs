using System.Collections.ObjectModel;
using FixCars4Us.Core.Data;
using FixCars4Us.Core.Infrastructure;
using FixCars4Us.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FixCars4Us.Core.ViewModels;

/// <summary>
/// Funkcja podstawowa: Kalendarz przyjęć.
/// Planowanie terminów wizyt z przypisaniem auta do stanowiska przyjęć.
/// Waliduje kolizje terminów na tym samym stanowisku.
/// </summary>
public class AppointmentsViewModel : ViewModelBase
{
    private readonly WorkshopContext _db;

    public ObservableCollection<Appointment> Appointments { get; } = new();
    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    public ObservableCollection<ReceptionStation> Stations { get; } = new();

    public Vehicle? SelectedVehicle { get; set; }
    public ReceptionStation? SelectedStation { get; set; }
    public DateTime NewDate { get; set; } = DateTime.Today.AddDays(1);
    public int StartHour { get; set; } = 9;
    public int DurationHours { get; set; } = 2;
    public string Reason { get; set; } = "";

    private string _status = "";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public RelayCommand AddAppointmentCommand { get; }

    public AppointmentsViewModel(WorkshopContext db)
    {
        _db = db;
        AddAppointmentCommand = new RelayCommand(AddAppointment);
        Load();
    }

    public void Load()
    {
        Vehicles.Clear();
        foreach (var v in _db.Vehicles.Include(v => v.Customer)) Vehicles.Add(v);
        Stations.Clear();
        foreach (var s in _db.ReceptionStations) Stations.Add(s);
        ReloadAppointments();
    }

    private void ReloadAppointments()
    {
        Appointments.Clear();
        foreach (var a in _db.Appointments
                     .Include(a => a.Vehicle)
                     .Include(a => a.ReceptionStation)
                     .OrderBy(a => a.Start))
            Appointments.Add(a);
    }

    private void AddAppointment(object? _)
    {
        if (SelectedVehicle is null || SelectedStation is null)
        {
            Status = "Wybierz pojazd i stanowisko.";
            return;
        }
        var start = NewDate.Date.AddHours(StartHour);
        var end = start.AddHours(Math.Max(1, DurationHours));

        bool conflict = _db.Appointments.Any(a =>
            a.ReceptionStationId == SelectedStation.Id &&
            start < a.End && a.Start < end);
        if (conflict)
        {
            Status = $"Kolizja! Stanowisko {SelectedStation.Name} jest zajęte w tym czasie.";
            return;
        }

        var appt = new Appointment
        {
            VehicleId = SelectedVehicle.Id,
            ReceptionStationId = SelectedStation.Id,
            Start = start,
            End = end,
            Reason = Reason
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();
        ReloadAppointments();
        Status = $"Dodano wizytę: {start:g} – {SelectedVehicle.RegistrationNumber}.";
    }
}
