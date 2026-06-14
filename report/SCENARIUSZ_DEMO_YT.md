# Scenariusz nagrania filmiku demonstracyjnego (YouTube – niepubliczny)

Czas docelowy: **5–8 minut**. Nagrywać np. OBS Studio / wbudowanym rejestratorem
Windows (Win+G). Po nagraniu wgrać na YouTube jako **„Niepubliczny”** i wkleić
link do raportu.

## Plan nagrania

1. **Wstęp (15 s)**
   - Przedstawienie: imiona i nazwiska członków grupy, temat projektu (FixCars4Us),
     technologia (C# / WPF / .NET 8 / SQLite).

2. **Uruchomienie aplikacji (15 s)**
   - Pokazać start aplikacji i okno główne z zakładkami.

3. **Funkcje podstawowe (2–3 min)**
   - **Klienci i pojazdy**: dodanie nowego klienta, dodanie pojazdu, dopisanie
     wpisu historii serwisowej.
   - **Katalog części i usług**: dodanie części i usługi.
   - **Magazyn**: przychód/rozchód części, pokazanie alertu niskiego stanu.
   - **Kalendarz przyjęć**: zaplanowanie wizyty + pokazanie kolizji terminu.

4. **Funkcje dodatkowe (3–4 min)** — zakładka *Zlecenia napraw*:
   - **Mediator**: utworzenie zlecenia z doborem zasobów; pokazać próbę, która się
     nie uda (np. zajęty jedyny mechanik danej specjalizacji lub brak podnośnika
     o udźwigu) → komunikat Mediatora.
   - **Builder + Facade**: utworzone zlecenie, dodanie części z magazynu (rozchód
     widoczny w zakładce Magazyn), wpisy w dzienniku audytowym.
   - **State + Observer**: zmiana statusu zgodnie z cyklem życia; próba
     niedozwolonego przejścia → blokada; powiadomienia (e-mail) w panelu.
   - **Strategy + Decorator**: wybór strategii robocizny, dodanie kilku warstw
     (dopłata kwotowa, dopłata %, rabat %), przeliczenie i pokazanie rozbicia ceny.
   - **Command + Memento**: wykonanie etapu „Prace właściwe” (zużycie części),
     następnie **Cofnij** → przywrócenie stanu magazynu i etapu, wpis w logu.

5. **Architektura i kod (1 min)**
   - Krótko pokazać strukturę rozwiązania w VS i jeden–dwa pliki wzorców.

6. **Zakończenie (15 s)**
   - Podsumowanie, deklarowana ocena.

## Checklist przed nagraniem
- [ ] Usunięty stary plik `fixcars4us.db` (czysty start z danymi seed).
- [ ] Powiększona czcionka w razie potrzeby (czytelność).
- [ ] Przygotowane przykładowe dane do wpisania.
- [ ] Film ustawiony jako **Niepubliczny** na YouTube, link wklejony do raportu.
