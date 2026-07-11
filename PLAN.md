# Plan

## Problem
- Po powrocie focusu do okna/terminala pierwsza wpisana litera czasem dopisuje się do istniejącej komendy zamiast zaznaczyć/nadpisać cały input.
- Aktualna poprawka w `TerminalPanelView.RedirectTextInput` wybiera cały tekst przy przekierowaniu inputu, ale nie obsługuje przypadku, gdy Avalonia nadal uważa `CommandBox` za fokusowany po reaktywacji okna.
- Testy `MudClient.App.Tests` mają 12 faili. Większość wygląda na stare testy po refaktorze: odwołują się przez reflection do pól/metod w `MainWindow`, które obecnie są w `TerminalPanelView`, oraz szukają kontrolek bezpośrednio w `MainWindow`, choć terminal siedzi w dockowanym panelu.

## Implementation
1. W `TerminalPanelView` dodać publiczne/metody pomocnicze umożliwiające:
   - sprawdzenie, czy `CommandBox` ma focus,
   - zaznaczenie całego inputu przed następnym wpisanym tekstem, jeśli focus wrócił z zewnątrz.
2. W `MainWindow` śledzić reaktywację/focus okna:
   - po `Deactivated` lub utracie focusu ustawić flagę, że następny tekst w terminalowym `CommandBox` ma wybrać cały input,
   - po pierwszym `TextInput` z tą flagą, jeśli fokusowany jest terminalowy `CommandBox`, wykonać `SelectAll` i wyczyścić flagę,
   - zachować istniejące zachowanie: jeśli focus nie jest w żadnym `TextBox`, przekierować input do terminala i tam zaznaczyć cały tekst przed wstawieniem znaku.
3. Nie zmieniać zachowania innych pól tekstowych (`Host`, `Port`, nazwa profilu itd.) — zwykły typing w nich nie może być przechwytywany przez terminal.
4. Upewnić się, że kliknięcie w nieinteraktywny obszar nadal wykonuje `FocusCommandBoxAndSelectAll`.

## Tests
1. Zaktualizować stare testy focus/click do obecnej architektury: `CommandBox`, `MudOutput` i helpery są w `TerminalPanelView`, nie w `MainWindow`.
2. Dodać test/regresję dla scenariusza: po powrocie focusu/reaktywacji okna i przy fokusowanym `CommandBox`, pierwszy wpisany znak zastępuje zaznaczony cały input zamiast dopisywać na końcu.
3. Sprawdzić osobny failure `EditRuleClickTests.ClickingEditRule_DoesNotThrow` i ustalić, czy to też test po refaktorze układu/dockingu; naprawić test jeśli jest nieaktualny, albo zgłosić produkcyjny problem jeśli klik edit faktycznie nie działa.
4. Uruchomić:
   - `dotnet build src\\MudClient.App\\MudClient.App.csproj --no-restore`
   - `dotnet test tests\\MudClient.App.Tests\\MudClient.App.Tests.csproj --no-restore`

## Notes
- Nie używać `Thread.Sleep`.
- Nie zmieniać parserów/protokołów.
- Jeśli testy wymagają produkcyjnych hooków pod testowalność, coder robi minimalne zmiany produkcyjne, a tester aktualizuje same testy.
