# Dane killeropedii

`teachers.json.gz` jest skompresowaną kopią pliku
[`MudletScripts/kbase/teachers.json`](https://github.com/laszlowaty/MudletScripts/blob/master/kbase/teachers.json).
Snapshot pobrano 2026-07-13 z gałęzi `master`, commit
`0ddd219829f72072f18d8aa98fc9975ea7b33b54`. Dodatkowe, ręcznie przekazane wpisy
są scalane w `Services/TeacherCatalogLoader.cs`; loader pomija identyczne oferty,
żeby nie dublować danych obecnych już w bazie.

`books.json` jest wbudowanym snapshotem katalogu ksiąg wygenerowanym komendami `booklist`.
Widok najpierw szuka `killeropedia-books.json` w katalogu ustawień aplikacji, a bez niego
czyta snapshot z paczki. Deweloperski proces odświeżania
zapisuje kompletny plik dopiero po poprawnym pobraniu list pięciu profesji i szczegółów
wszystkich unikalnych vnum, więc anulowanie, timeout lub rozłączenie nie nadpisuje
poprzedniego katalogu częściowymi danymi.
