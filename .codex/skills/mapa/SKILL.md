---
name: mapa
description: Edytuje ilustracje lokacji KillerMudClient na podstawie najnowszych eksportów MapImageCalibrator. Używaj, gdy użytkownik wpisuje `/mapa nazwa-krainy`, `$mapa nazwa-krainy`, prosi o zastosowanie markerów lub roboczych przesunięć siatki do obrazu miasta albo chce poprawić grafikę mapy z najnowszego pakietu CalibrationExports.
---

# Mapa

Popraw obraz lokacji na podstawie najnowszego kompletnego eksportu kalibratora.

## Workflow

1. Ustal nazwę lokacji z tekstu po `/mapa` lub `$mapa`.
2. Ustal katalog główny repozytorium i uruchom:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "<repo>\.codex\skills\mapa\scripts\find-latest-export.ps1" -Name "<nazwa>" -RepoRoot "<repo>"
   ```

3. Odczytaj zwrócony JSON. Jeśli skrypt nie znalazł kompletnej pary eksportu PNG+JSON, poproś użytkownika o wykonanie „Eksportuj pakiet dla AI” w kalibratorze.
4. Obejrzyj `exportPng` oraz `targetImage` i przeczytaj `exportJson`. Sprawdź `isBlankCanvas`, `layerName` i `rooms`.
5. Traktuj pliki następująco:
   - `targetImage`: jedyny cel edycji i źródło stylu.
   - `exportPng`: referencja położenia siatki i numerowanych markerów; nie kopiuj z niego UI.
   - `markers`: instrukcje lokalnych zmian. Przelicz `imageX/imageY` na procent wymiarów obrazu i wymień je w promptcie.
   - `roomOffsets`: roboczy oczekiwany układ siatki. Użyj go oraz screena do skorygowania przebiegu dróg i położenia obiektów na obrazie; nie modyfikuj `world-map.json`.
   - `rooms`: wybrane roomy nowej warstwy wraz z vnumami, nazwami i współrzędnymi. Traktuj je jako opis geografii i ważnych obiektów.
6. Wybierz sposób generowania:
   - Gdy `isBlankCanvas` jest `false`, użyj wbudowanego generatora obrazów w trybie precyzyjnej edycji. Podaj `targetImage` jako cel, a `exportPng` jako pomocniczą referencję. Wymagaj zachowania kadru, stylu, wszystkich nieoznaczonych obszarów, braku tekstu i braku elementów UI.
   - Gdy `isBlankCanvas` jest `true`, wygeneruj pełny obraz od zera. Oprzyj temat na `layerName`, nazwy i układ `rooms` oraz opisy markerów. Użyj `exportPng` jako referencji kompozycji i siatki, ale nie używaj czarnego `targetImage` jako źródła stylu. Dopasuj drogi i kluczowe obiekty do roomów. Nie kopiuj siatki, markerów ani UI. Zachowaj dokładne proporcje i docelowy rozmiar `targetImage`.
7. Sprawdź rezultat wizualnie. W razie naruszenia nieoznaczonych obszarów wykonaj jedną ukierunkowaną iterację.
8. Przeskaluj wynik dokładnie do pierwotnych wymiarów `targetImage` i nadpisz ten plik. Nie zmieniaj manifestu, chyba że eksport lub użytkownik jawnie żąda zmiany całej warstwy.
9. Zbuduj `MudClient.App` do katalogu tymczasowego, jeśli uruchomiony klient blokuje normalny output.
10. Dopiero po udanym nadpisaniu obrazu, kontroli wizualnej i buildzie usuń wykorzystane eksporty PNG/JSON oraz plik `.calibration.json` aktualnej krainy:

    ```powershell
    powershell -NoProfile -ExecutionPolicy Bypass -File "<repo>\.codex\skills\mapa\scripts\clear-calibration-exports.ps1" -Name "<exportName>" -RepoRoot "<repo>"
    ```

    Jeśli edycja lub build nie powiedzie się, pozostaw wszystkie pliki do ponownej próby.
11. Podaj ścieżkę zmienionego obrazu, krótką listę zastosowanych markerów oraz liczbę usuniętych plików roboczych.

## Zasady interpretacji

- Marker z opisem „usuń” oznacza lokalne usunięcie i naturalne uzupełnienie otoczenia.
- Marker nazwany obiektem, np. „park” lub „świątynia”, oznacza umieszczenie tego obiektu w punkcie markera.
- Przesunięte roomy są instrukcją dla grafiki, nie zmianą danych mapy.
- Gdy instrukcja jest niejednoznaczna, nie zgaduj znaczącej przebudowy; poproś o doprecyzowanie jednego markera.
- Zachowuj rozdzielczość, proporcje, nazwę pliku, kolejność warstw i aktualne ustawienia manifestu.
