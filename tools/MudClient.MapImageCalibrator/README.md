# MudClient.MapImageCalibrator

Lokalne narzędzie Avalonia do przygotowywania projektów Nortantis na podstawie roomów z `world-map.json`.

## Workflow

1. Wybierz atlas/obszar i poziom Z.
2. Wybierz roomy pojedynczo, prostokątem, lassem albo przez wyszukiwanie vnum/nazwy.
3. Podaj nazwę projektu i kliknij `Utwórz pusty .nort i overlay`.

Narzędzie zapisuje:

- `tools/Nortantis/Projects/<nazwa>.nort` — nowy projekt bez ręcznych ikon, tekstów i dróg,
- `tools/Nortantis/Overlays/<nazwa>-rooms.png` — przezroczysty overlay zawierający wyłącznie wybrane roomy i połączenia między nimi.

Projekt ma overlay od razu włączony. Zaznaczona sieć zajmuje 90% dostępnego obszaru, pozostawiając co najmniej 5% marginesu z każdej strony.

Eksporter używa istniejącego, poprawnego pliku `.nort` z `tools/Nortantis/Projects` jako bazowych ustawień zgodnych z aktualną wersją Nortantis. Preferowanym szablonem jest `old-continent.nort`.

Uruchomienie z katalogu repozytorium:

```powershell
tools\MudClient.MapImageCalibrator\run.bat
```

Narzędzie nie modyfikuje `world-map.json` ani assetów aplikacji.
