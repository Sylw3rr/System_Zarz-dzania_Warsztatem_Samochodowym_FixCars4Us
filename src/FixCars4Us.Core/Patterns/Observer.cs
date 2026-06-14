using FixCars4Us.Core.Models;

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: Observer.
/// Obserwatorzy są powiadamiani o zmianie statusu zlecenia (np. klient e-mailem,
/// manager o konieczności akceptacji dodatkowych kosztów).
/// </summary>
public interface IRepairObserver
{
    void OnStatusChanged(RepairOrder order, string message);
}

/// <summary>Podmiot obserwowany — zarządza listą obserwatorów i rozsyła powiadomienia.</summary>
public class RepairNotifier
{
    private readonly List<IRepairObserver> _observers = new();

    public void Subscribe(IRepairObserver observer)
    {
        if (!_observers.Contains(observer)) _observers.Add(observer);
    }

    public void Unsubscribe(IRepairObserver observer) => _observers.Remove(observer);

    public void Notify(RepairOrder order, string message)
    {
        foreach (var o in _observers) o.OnStatusChanged(order, message);
    }
}

/// <summary>Symulacja powiadomienia e-mail do klienta. Zbiera wysłane wiadomości (do podglądu w UI).</summary>
public class EmailCustomerObserver : IRepairObserver
{
    public List<string> SentMessages { get; } = new();

    public void OnStatusChanged(RepairOrder order, string message)
    {
        var email = order.Vehicle?.Customer?.Email ?? "(brak e-mail)";
        SentMessages.Add($"[E-MAIL -> {email}] Zlecenie #{order.Id}: {message}");
    }
}

/// <summary>Powiadomienie dla managera (np. wymagana akceptacja dodatkowych kosztów).</summary>
public class ManagerAlertObserver : IRepairObserver
{
    public List<string> Alerts { get; } = new();

    public void OnStatusChanged(RepairOrder order, string message)
        => Alerts.Add($"[MANAGER] Zlecenie #{order.Id} ({order.Vehicle?.RegistrationNumber}): {message}");
}
