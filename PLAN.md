# Plan: poprawki autowalk

## Kontekst
- Autowalk jest obsługiwany głównie w `src/MudClient.App/ViewModels/MainWindowViewModel.cs`.
- Podwójny klik mapy trafia do `OnMapRoomDoubleClicked`. Obecnie, gdy `IsAutowalking == true`, metoda tylko zapamiętuje nowy cel tymczasowy i zwraca bez czyszczenia aktywnej trasy ani wyznaczania podglądu nowej.
- Panel UI autowalk jest w `src/MudClient.App/Views/Panels/AutowalkPanelView.axaml`; istnieje przycisk `IDŹ DO CELU` widoczny tylko dla celu tymczasowego, ale brakuje ogólnej opcji UI robiącej dokładnie to samo co komenda `/idz` bez argumentu.

## Implementacja produkcyjna dla agenta coder
1. W `MainWindowViewModel` zmienić obsługę podwójnego kliknięcia mapy tak, aby wybranie nowego celu podczas aktywnego autowalk:
   - przerwało/wyczyściło aktualny autowalk i zaznaczoną trasę,
   - zachowało nowo wybrany cel tymczasowy,
   - od razu wyznaczyło i namalowało podgląd nowej trasy z bieżącego `Map.CurrentVnum`, tak jak dziś dzieje się poza autowalk,
   - nie usuwało celu tymczasowego przez przypadkowe użycie `StopAutowalk`, które obecnie czyści `_temporaryTarget`.
2. Dodać publiczny `RelayCommand` (np. `GoCommand` / `GoToSelectedTargetCommand`) wykonujący semantycznie to samo co wpisanie `/idz` bez argumentu:
   - jeśli istnieje `_temporaryTarget`, uruchamia `StartAutowalk(_temporaryTarget)`,
   - jeśli go nie ma, pokazuje ten sam komunikat użycia co `/idz`, najlepiej przez współdzielenie logiki z `TryHandleAutowalkCommand("/idz")` lub małą metodę pomocniczą.
3. Dodać do `AutowalkPanelView.axaml` widoczny przycisk UI opisany jako `IDŹ`/`Idź`, podpięty do nowego commandu i umieszczony w sekcji statusu/celu tak, aby użytkownik mógł kliknąć UI zamiast wpisywać `/idz`.
   - Istniejący przycisk `IDŹ DO CELU` może pozostać, ale nie powinien być jedyną drogą; jeśli zostanie zastąpiony, zachować jasną etykietę i binding.
4. Zachować istniejące zasady architektury: zmiany tylko w warstwie App/UI, bez zależności Core od UI, bez blokowania `.Wait()`/`.Result` i bez `Thread.Sleep`.

## Testy/monitoring dla agenta tester
1. Dodać lub zaktualizować testy jednostkowe `MudClient.App.Tests`, jeśli da się sensownie skonstruować `MainWindowViewModel`, dla scenariuszy:
   - podwójny klik podczas aktywnego autowalk zastępuje starą trasę nowym celem i aktualizuje `Map.RouteRooms`, zamiast zostawiać starą trasę,
   - nowy command UI `IDŹ` zachowuje się jak `/idz` bez argumentu (uruchamia marsz do celu tymczasowego albo pokazuje komunikat użycia bez celu).
2. Uruchomić co najmniej `dotnet test MudClientStarter.sln` i zgłosić wynik.

## Kryteria akceptacji
- Podczas zablokowanego/stojącego autowalk podwójny klik na mapie czyści starą drogę i pokazuje nową trasę do klikniętego pokoju.
- UI ma opcję `IDŹ`, która zachowuje się jak `/idz`.
- Projekt buduje się i testy przechodzą.
