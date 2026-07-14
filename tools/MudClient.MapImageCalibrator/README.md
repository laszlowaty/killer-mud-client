# MudClient.MapImageCalibrator

Lokalne narzędzie Avalonia do dopasowywania ilustracji lokacji do współrzędnych mapy oraz ręcznego składania obrazu z edytowalnych elementów.

## Edycja elementów obrazu

- Lewa paleta zawiera domyślne tekstury pobrane z `Assets/Map/Sectors`.
- Dodatkowe pliki PNG można umieszczać w `Assets/Map/EditorAssets`. Nazwy podkatalogów tworzą kategorie palety.
- Elementy przeciąga się z palety na obraz. Tryb `Edytuj elementy` pozwala je wybierać i przesuwać.
- Panel właściwości zmienia położenie, szerokość, wysokość, obrót, krycie i kolejność nakładania.
- `Ctrl+D` duplikuje element, `Delete` usuwa, `Ctrl+Z` cofa, a `Ctrl+Y` ponawia.

Elementy są zapisywane w pliku `*.calibration.json`; źródłowy PNG nie jest nadpisywany. `Eksportuj gotowy PNG` tworzy spłaszczony obraz w `Locations/CalibrationExports`, bez siatki roomów, zaznaczenia i markerów. Klient nadal używa zwykłych plików PNG z `Locations/manifest.json`.

`Eksportuj pakiet dla AI` zapisuje trzy powiązane pliki: JSON z markerami i elementami, PNG roboczy z roomami oraz `*-composite.png` bez nakładek. Skill `/mapa` używa composite jako dokładnej bazy kompozycji, oryginalnego obrazu jako wzorca stylu i PNG roboczego wyłącznie do interpretacji roomów oraz markerów.

Uruchomienie z katalogu repozytorium:

```powershell
tools\MudClient.MapImageCalibrator\run.bat
```
