// Plik: Observer.cs
// Rola: Implementacja wzorca Observer dla powiadamiania zainteresowanych stron
//       o zmianach statusu zleceń naprawy (klient e-mailem, manager alertem).
// Wzorzec: OBSERVER (GoF) — definiuje zależność jeden-do-wielu między obiektami,
//          tak by zmiana stanu jednego obiektu powodowała powiadomienie i aktualizację
//          wszystkich zależnych obiektów.
//          Subject = RepairNotifier, Observer = IRepairObserver,
//          ConcreteObserver = EmailCustomerObserver | ManagerAlertObserver

using FixCars4Us.Core.Models; // RepairOrder — obiekt o którym powiadamiamy

namespace FixCars4Us.Core.Patterns;

/// <summary>
/// WZORZEC: Observer.
/// Obserwatorzy są powiadamiani o zmianie statusu zlecenia (np. klient e-mailem,
/// manager o konieczności akceptacji dodatkowych kosztów).
/// </summary>
/// <remarks>
/// Interfejs obserwatora definiuje "kontrakt" — Subject (RepairNotifier) wywołuje
/// OnStatusChanged() na każdym zarejestrowanym obserwatorze.
/// Obserwator nie wie nic o innych obserwatorach — całkowite odsprzężenie.
///
/// Dlaczego nie używamy event C#? Event byłby tu równoważny, ale jawny interfejs
/// jest bardziej czytelny edukacyjnie i ułatwia demonstrację wzorca.
/// W WPF INotifyPropertyChanged to przykład Observer przez event.
/// </remarks>
public interface IRepairObserver
{
    /// <summary>
    /// Wywoływane przez Subject gdy stan zlecenia się zmienia.
    /// Implementacja decyduje co zrobić z tą informacją.
    /// </summary>
    /// <param name="order">Zlecenie, którego status się zmienił.</param>
    /// <param name="message">Opis zmiany (np. "Status naprawy zmieniono na GotoweDoOdbioru").</param>
    void OnStatusChanged(RepairOrder order, string message);
}

/// <summary>Podmiot obserwowany — zarządza listą obserwatorów i rozsyła powiadomienia.</summary>
/// <remarks>
/// W terminologii GoF to "ConcreteSubject" lub "Observable".
/// Lista _observers jest prywatna — zewnętrzny kod może tylko subskrybować/odsubskrybować,
/// nie może bezpośrednio modyfikować listy (hermetyzacja).
///
/// Notify() wywołuje wszystkich obserwatorów synchronicznie — w aplikacji produkcyjnej
/// można by to zrównoleglić lub asynchronizować (async/await z IAsyncObserver).
/// </remarks>
public class RepairNotifier
{
    // Lista aktywnych obserwatorów — dodawanych przez Subscribe().
    private readonly List<IRepairObserver> _observers = new();

    /// <summary>
    /// Rejestruje obserwatora. Guard (!Contains) zapobiega podwójnej rejestracji,
    /// bo zduplikowany obserwator otrzymałby każde powiadomienie dwukrotnie.
    /// </summary>
    public void Subscribe(IRepairObserver observer)
    {
        if (!_observers.Contains(observer)) _observers.Add(observer); // Dodaj tylko jeśli nie ma
    }

    /// <summary>
    /// Usuwa obserwatora — np. gdy klient wyloguje się z systemu powiadomień.
    /// Remove() bezpiecznie pomija jeśli obiekt nie jest na liście.
    /// </summary>
    public void Unsubscribe(IRepairObserver observer) => _observers.Remove(observer);

    /// <summary>
    /// Rozsyła powiadomienie do wszystkich subskrybentów.
    /// Wywoływane przez WorkshopFacade po każdej operacji na zleceniu.
    /// </summary>
    public void Notify(RepairOrder order, string message)
    {
        // Iterujemy po kopii listy (nie ma tu kopii — założenie: Subscribe/Unsubscribe
        // nie są wywoływane z wnętrza OnStatusChanged, bo mogłoby to modyfikować listę
        // podczas iteracji i rzucić InvalidOperationException).
        foreach (var o in _observers) o.OnStatusChanged(order, message); // Powiadom każdego obserwatora
    }
}

/// <summary>Symulacja powiadomienia e-mail do klienta. Zbiera wysłane wiadomości (do podglądu w UI).</summary>
/// <remarks>
/// W rzeczywistej aplikacji ta klasa wysyłałaby prawdziwe e-maile przez SMTP.
/// Tu symulujemy przez zbieranie wiadomości w liście (do testów i podglądu w UI).
/// SentMessages jest publiczne — UI może wyświetlić historię wysłanych powiadomień.
///
/// Wzorzec Observer: ten obserwator "reaguje" na zmianę statusu przez "wysłanie e-maila".
/// Brak zniwelowania — jeśli mail nie dojdzie, zlecenie i tak zmienia status
/// (obserwator jest "fire and forget").
/// </remarks>
public class EmailCustomerObserver : IRepairObserver
{
    /// <summary>Historia wysłanych powiadomień e-mail — widoczna w zakładce UI.</summary>
    public List<string> SentMessages { get; } = new();

    /// <summary>
    /// Symuluje wysłanie e-maila do klienta. Adres e-mail pobierany
    /// z łańcucha nawigacyjnego: RepairOrder -> Vehicle -> Customer -> Email.
    /// Jeśli Customer nie jest załadowany (brak Include) — używamy tekstu zastępczego.
    /// </summary>
    public void OnStatusChanged(RepairOrder order, string message)
    {
        // Łańcuch ?. (null-conditional) bezpiecznie przechodzi przez potentially null referencje.
        // ?? "(brak e-mail)" — wartość domyślna gdy którykolwiek element w łańcuchu jest null.
        var email = order.Vehicle?.Customer?.Email ?? "(brak e-mail)";

        // Sformatuj wiadomość i dodaj do historii (symulacja wysyłki).
        SentMessages.Add($"[E-MAIL -> {email}] Zlecenie #{order.Id}: {message}");
    }
}

/// <summary>Powiadomienie dla managera (np. wymagana akceptacja dodatkowych kosztów).</summary>
/// <remarks>
/// Drugi ConcreteObserver — reaguje inaczej niż EmailCustomerObserver:
/// zamiast powiadamiać klienta, alertuje managera.
/// Oba obserwatory subskrybują ten sam Subject (RepairNotifier) i oba
/// otrzymują każde powiadomienie — to sedno wzorca Observer (broadcast).
///
/// Alerts jest publiczne — UI może wyświetlić alerty managera w osobnym panelu.
/// </remarks>
public class ManagerAlertObserver : IRepairObserver
{
    /// <summary>Lista alertów dla managera (np. niski stan magazynowy, zlecenie gotowe do odbioru).</summary>
    public List<string> Alerts { get; } = new();

    /// <summary>
    /// Rejestruje alert dla managera z informacją o zleceniu.
    /// Numer rejestracyjny pojazdu ułatwia szybką identyfikację klienta.
    /// </summary>
    public void OnStatusChanged(RepairOrder order, string message)
        => Alerts.Add($"[MANAGER] Zlecenie #{order.Id} ({order.Vehicle?.RegistrationNumber}): {message}");
}
