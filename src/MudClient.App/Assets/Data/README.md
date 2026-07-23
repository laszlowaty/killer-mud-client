# Dane killeropedii

`teachers.json.gz` jest skompresowaną kopią pliku
[`MudletScripts/kbase/teachers.json`](https://github.com/laszlowaty/MudletScripts/blob/master/kbase/teachers.json).
Snapshot pobrano 2026-07-13 z gałęzi `master`, commit
`0ddd219829f72072f18d8aa98fc9975ea7b33b54`. Dodatkowe, ręcznie przekazane wpisy
są scalane w `Services/TeacherCatalogLoader.cs`; loader pomija identyczne oferty,
żeby nie dublować danych obecnych już w bazie. Ręcznie przekazane triki nauczycieli
są przechowywane osobno od umiejętności razem z szansą i ceną nauki oraz scalane
z nauczycielami po vnum moba.

`books.json` jest wbudowanym snapshotem katalogu ksiąg wygenerowanym komendami `booklist`.
Widok najpierw szuka `killeropedia-books.json` w katalogu ustawień aplikacji, a bez niego
czyta snapshot z paczki. Deweloperski proces odświeżania
zapisuje kompletny plik dopiero po poprawnym pobraniu list pięciu profesji i szczegółów
wszystkich unikalnych vnum, więc anulowanie, timeout lub rozłączenie nie nadpisuje
poprzedniego katalogu częściowymi danymi.

`lore-catalog.json.gz` jest generowany z kanonicznych rekordów w katalogu `lore/`
poleceniem `python .codex/skills/build-killermud-lore/scripts/build_lore_outputs.py`.
Killeropedia używa tego katalogu do wyszukiwania i nawigacji między miejscami,
postaciami, organizacjami, bóstwami, artefaktami, wydarzeniami i legendami. Plik
`%AppData%/KillerMudClient/Data/lore-catalog.json.gz` zastępuje kopię wbudowaną,
dzięki czemu aktualizacja lore nie wymaga ponownej kompilacji klienta.

`quests.json` zawiera skróconą listę zadań graczy opracowaną na podstawie
`docs/questy-area-lore.md`. Zakładka „Zadania” pokazuje nazwę zadania, krainę
oraz moba zlecającego; katalog aplikacji celowo nie zawiera VNUM-ów.

## Aktualizacje danych aplikacji

Aplikacja sprawdza publiczny `docs/content/manifest.json` i może pobrać osobne
paczki mapy oraz Killeropedii bez aktualizacji pliku wykonywalnego. Zweryfikowane
paczki trafiają do `%AppData%/KillerMudClient/Content/<komponent>/<wersja-hash>`,
a `Content/active.json` wskazuje aktywną wersję. Przełączenie następuje dopiero po
sprawdzeniu rozmiaru, SHA-256, bezpiecznym rozpakowaniu i sparsowaniu danych.
Brak albo uszkodzenie pobranej wersji powoduje użycie danych dostarczonych z aplikacją.

Lokalny `killeropedia-books.json`, utworzony przez odświeżenie książek z MUD-a,
ma pierwszeństwo przed pobranym bazowym `books.json` i nie jest nadpisywany.
Publikowanie paczek wykonuje workflow `.github/workflows/content-release.yml`.
