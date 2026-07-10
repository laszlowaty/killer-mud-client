# Zasady pracy z agentem AI

## Architektura

- `MudClient.Core` nie może zależeć od Avalonia ani innych elementów GUI.
- Dane sieciowe zawsze przechodzą kolejno: TCP -> Telnet -> GMCP/tekst -> linie -> automatyzacja -> UI.
- Nie parsuj Telnetu za pomocą wyrażeń regularnych.
- Nie wykonuj triggerów bezpośrednio na wątku UI.
- Nie udostępniaj komponentom UI obiektu `NetworkStream`.

## Sposób implementacji

- Jedna zmiana powinna obejmować jeden mały problem.
- Każda poprawka parsera Telnet musi mieć test odtwarzający problematyczne bajty.
- Nie używaj `Thread.Sleep`; używaj `Task.Delay` z `CancellationToken`.
- Wszystkie pętle odbioru i timery muszą poprawnie reagować na anulowanie.
- Każdy `SemaphoreSlim` musi być zwalniany w `finally`.
- Nie blokuj zadań przez `.Result` ani `.Wait()`.
- Nie ignoruj wyjątków bez komentarza i jawnego uzasadnienia.

## Protokół

- Traktuj Telnet jako protokół bajtowy, nie jako zwykły tekst TCP.
- Zachowuj stan parsera pomiędzy kolejnymi odczytami z sieci.
- `IAC IAC` oznacza pojedynczy bajt 255 w danych.
- GMCP jest UTF-8 niezależnie od kodowania zwykłego tekstu MUD-a.
- Nie odpowiadaj twierdząco na nieobsługiwaną opcję Telnet.
- MCCP2 musi zostać włączone dokładnie w miejscu wskazanym przez sekwencję subnegocjacji.

## Definicja ukończenia

Zmiana jest ukończona, gdy:

1. projekt się buduje,
2. testy przechodzą,
3. nowy kod ma anulowanie i obsługę błędów,
4. README lub komentarz architektoniczny został zaktualizowany, jeśli zmieniło się zachowanie.
