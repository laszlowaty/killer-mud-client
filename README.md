# MudClientStarter

Startowy, wieloplatformowy klient MUD napisany w C# i Avalonia.

## Co już działa

- połączenie TCP z MUD-em,
- stanowa obsługa podstawowego protokołu Telnet,
- negocjacja `GMCP`, `NAWS`, `TTYPE`, `EOR` i `SUPPRESS-GO-AHEAD`,
- odbiór i podgląd komunikatów GMCP,
- wysyłanie komend,
- podstawowe kolory ANSI SGR: 16 kolorów, 256 kolorów, RGB, bold, underline i reset,
- fundamenty aliasów, triggerów oraz timerów,
- testy parsera Telnet i mechanizmu aliasów,
- gotowe zadania i debugowanie w VS Code.

## Czego jeszcze nie ma

- MCCP2 / kompresji zlib,
- trwałego zapisu profili w SQLite,
- edytorów aliasów, triggerów i timerów,
- rozbudowanej historii komend,
- pełnego terminala z pozycjonowaniem kursora,
- pathfindingu i automatycznego chodzenia po mapie,
- TLS.

Renderer ANSI jest celowo liniowy: obsługuje kolory tekstu MUD, ale ignoruje terminalowe komendy przesuwania kursora. To jest odpowiedni model dla typowego klienta MUD.

## Wymagania

1. .NET 10 SDK.
2. VS Code.
3. Rozszerzenie **C# Dev Kit**.
4. Zalecane rozszerzenie **Avalonia for VS Code**.

Nie musisz instalować szablonów Avalonia, ponieważ projekt jest już utworzony.

## Pierwsze uruchomienie

W terminalu, w katalogu projektu:

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/MudClient.App
```

W VS Code możesz również nacisnąć `F5` albo uruchomić zadanie `run`.

Na Windows możesz użyć:

```powershell
.\run.ps1
```

Albo po prostu uruchomić:

```bat
.\run.bat
```

Na Linux/macOS:

```bash
chmod +x run.sh
./run.sh
```

## Gdzie wpisać adres MUD-a

Po uruchomieniu aplikacji wpisz host i port na górnym pasku, następnie kliknij **Połącz**.

Domyślne `localhost:4000` jest tylko przykładem.

## Struktura

```text
src/
├── MudClient.Core/       # Telnet, GMCP, TCP, aliasy, triggery, timery
└── MudClient.App/        # Avalonia, widoki i renderowanie ANSI
tests/
└── MudClient.Core.Tests/ # testy bez uruchamiania GUI
```

Najważniejsza granica architektoniczna: `MudClient.Core` nie zależy od Avalonia. Dzięki temu parser i silnik można testować bez GUI.

## Mapa świata

Zakładka **Mapa** obok **Gra** pokazuje mapę świata renderowaną własną kontrolką Avalonia (`WorldMapControl`), bez SkiaSharp ani innego ciężkiego silnika graficznego.

### Warstwy

- `MudClient.Core/Map/` — modele (`MapDocument`, `MapArea`, `MapRoom`, `MapExit`), `MapLoader` (asynchroniczne, tolerancyjne wczytywanie JSON-a), `MapIndex` (indeksy po id, vnum, obszarze/z, oraz siatka przestrzenna do renderowania tylko widocznych pokoi), `CollisionLayoutService` (deterministyczne rozkładanie pokoi o identycznych współrzędnych) — bez zależności od Avalonia.
- `MudClient.App/Controls/WorldMapControl.cs` — jedna kontrolka rysująca mapę przez `DrawingContext` (bez osobnych kontrolek per pokój), obsługa przeciągania, zoomu względem kursora, klawiatury oraz zaznaczania pokoi/grup kolizji.
- `MudClient.App/Services/SectorTextureCache.cs` — leniwe ładowanie i cache'owanie `Bitmap` per sektor, z fallbackiem gdy brakuje PNG.
- `MudClient.App/ViewModels/MapViewModel.cs` — ładowanie mapy poza wątkiem UI, śledzenie postaci, wybór obszaru/poziomu z.

### Pliki mapy

- Świat: `src/MudClient.App/Assets/Map/world-map.json`
- Grafiki sektorów: `src/MudClient.App/Assets/Map/Sectors/*.png`
- Opcjonalny manifest nazw sektorów: `src/MudClient.App/Assets/Map/Sectors/sectors.json`
- Konfiguracja mapy: `src/MudClient.App/Assets/Map/map-settings.json`

Wszystkie te pliki są kopiowane do katalogu wynikowego (`CopyToOutputDirectory=PreserveNewest`) i odnajdywane względem `AppContext.BaseDirectory`, więc aplikacja działa niezależnie od komputera, na którym została zbudowana. Brak `world-map.json` nie powoduje awarii — zakładka Mapa pokazuje czytelny komunikat z oczekiwaną ścieżką.

### Wykrywanie aktualnego pokoju z GMCP

Domyślnie `GmcpLocationResolver` nasłuchuje pakietu `Room.Info` i szuka vnum pod ścieżkami `vnum`, `num`, `room.vnum`, `room.num`, `location.vnum`, `location.num` (w tej kolejności). Aby dopasować inny serwer MUD, który wysyła lokalizację pod innym pakietem lub inną ścieżką, zmień `gmcpLocation.packages` i `gmcpLocation.vnumPaths` w `map-settings.json` — nie wymaga to zmian w kodzie.

## Następne sensowne kroki

1. Zapisać surowe sesje Telnet do pliku i dodać replay w testach.
2. Dodać MCCP2 jako warstwę strumienia przed parserem Telnet.
3. Dodać SQLite i profile połączeń.
4. Zbudować edytory aliasów, triggerów i timerów.
5. Dodać bufor wyjścia z wirtualizacją dla bardzo długich sesji.
6. Zbudować magazyn stanu GMCP i panele HP oraz grupy.
7. Dodać pathfinding i automatyczne chodzenie po mapie po kliknięciu pokoju.

## Ważne przy pracy z AI

Przeczytaj `AGENTS.md`. Zawiera zasady, które ograniczają mieszanie warstw i generowanie trudnego do utrzymania kodu.
