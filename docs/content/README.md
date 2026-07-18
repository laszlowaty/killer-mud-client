# Aktualizacje danych KillerMudClient

Plik `manifest.json` jest publicznym indeksem paczek pobieranym przez aplikację.
Nie należy edytować wpisów komponentów ręcznie. Workflow
`Aktualizacja mapy i Killeropedii` buduje i testuje wybraną paczkę, publikuje ją
jako asset GitHub Release, oblicza SHA-256 i aktualizuje manifest na GitHub Pages.
Przy starcie aplikacja porównuje ten manifest ze stanem zainstalowanych danych
i pokazuje banner z nazwą oraz wersją każdej dostępnej paczki.

Paczka `map` zawiera zawartość `src/MudClient.App/Assets/Map`. Paczka
`killeropedia` zawiera `lore-catalog.json.gz`, `teachers.json.gz` i `books.json`.
Wersję komponentu należy zmienić przy każdym opublikowaniu nowych danych.

Wersja samej aplikacji jest publikowana oddzielnie w `docs/app-version.json`.
Workflow `Release` aktualizuje ten plik automatycznie; aplikacja nie korzysta
przy tym z limitowanego GitHub Releases API.
