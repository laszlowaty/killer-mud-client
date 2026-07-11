# MudClientStarter

Startowy, wieloplatformowy klient MUD napisany w C# i Avalonia.

## Co już działa

- połączenie TCP z MUD-em,
- stanowa obsługa podstawowego protokołu Telnet,
- negocjacja `GMCP`, `NAWS`, `TTYPE`, `EOR` i `SUPPRESS-GO-AHEAD`,
- MCCP2 (kompresja zlib): dekompresja włączana dokładnie na granicy `IAC SB 86 IAC SE`; bajty odebrane po znaczniku w tym samym odczycie TCP trafiają do dekompresora, a zakończenie strumienia zlib przez serwer przywraca odczyt bez kompresji,
- odbiór i podgląd komunikatów GMCP,
- wysyłanie komend,
- podstawowe kolory ANSI SGR: 16 kolorów, 256 kolorów, RGB, bold, underline i reset,
- fundamenty aliasów, triggerów oraz timerów,
- testy parsera Telnet i mechanizmu aliasów,
- gotowe zadania i debugowanie w VS Code.

## Czego jeszcze nie ma

- trwałego zapisu profili w SQLite,
- edytorów aliasów, triggerów i timerów,
- rozbudowanej historii komend,
- pełnego terminala z pozycjonowaniem kursora,
- pathfindingu i automatycznego chodzenia po mapie,
- TLS.

Renderer ANSI jest celowo liniowy: obsługuje kolory tekstu MUD, ale ignoruje terminalowe komendy przesuwania kursora. To jest odpowiedni model dla typowego klienta MUD.

Okno wyjścia MUD-a jest zwirtualizowane: tekst trafia do bufora pierścieniowego (do 10 000 linii), a rysowane są wyłącznie linie widoczne w viewporcie (`OutputPaneControl`, własny `ILogicalScrollable`). Dzięki temu koszt dopisania tekstu nie zależy od wielkości scrollbacka i wielogodzinne sesje (np. długi autowalk) nie spowalniają UI. Zaznaczanie i kopiowanie tekstu jest zaimplementowane w kontrolce (przeciąganie myszą + menu kontekstowe).

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

## Publikacja

Skrypty `publish.bat` oraz `publish-mac.bat` przyjmują wariant `beta` albo `release`. Przed uruchomieniem `dotnet publish` usuwają cały katalog docelowy wybranego wariantu i tworzą go ponownie, dzięki czemu paczka nie zawiera plików pozostałych po starszej wersji. Sąsiedni wariant i pozostałe platformy nie są czyszczone.

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
- `MudClient.App/Controls/WorldMapControl.cs` — jedna kontrolka rysująca mapę przez `DrawingContext` (bez osobnych kontrolek per pokój), obsługa przeciągania, zoomu względem kursora, klawiatury oraz zaznaczania pokoi/grup kolizji. Renderer najpierw buduje z sektorów i połączeń widoczną warstwę krajobrazu (biomy, linie brzegowe, drogi i delikatne tekstury), a następnie nakłada techniczną mapę pokoi, trasę i bieżącą pozycję. Poniżej zoomu `0.45` kontrolka przechodzi w prekomponowany widok strategiczny: dwie bitmapy zawierają interpolowane biomy oraz wszystkie pokoje i połączenia, a runtime dokłada tylko trasę, zaznaczenie i pozycję gracza. Repaint podczas przeciągania jest scalany przez kolejkę UI.
- `MudClient.App/Services/SectorTextureCache.cs` — leniwe ładowanie i cache'owanie `Bitmap` per sektor, z fallbackiem gdy brakuje PNG.
- `MudClient.App/ViewModels/MapViewModel.cs` — ładowanie mapy poza wątkiem UI, śledzenie postaci, wybór obszaru/poziomu z.

### Pliki mapy

- Świat: `src/MudClient.App/Assets/Map/world-map.json`
- Grafiki sektorów: `src/MudClient.App/Assets/Map/Sectors/*.png`
- Neutralne tło atlasowe dla obszarów bez pokojów: `src/MudClient.App/Assets/Map/Sectors/world-background.png`
- Prekomponowane tła biomów i warstwy pokojów: `src/MudClient.App/Assets/Map/Backdrops/`
- Opcjonalny manifest nazw sektorów: `src/MudClient.App/Assets/Map/Sectors/sectors.json`
- Konfiguracja mapy: `src/MudClient.App/Assets/Map/map-settings.json`

Wszystkie te pliki są kopiowane do katalogu wynikowego (`CopyToOutputDirectory=PreserveNewest`) i odnajdywane względem `AppContext.BaseDirectory`, więc aplikacja działa niezależnie od komputera, na którym została zbudowana. Brak `world-map.json` nie powoduje awarii — zakładka Mapa pokazuje czytelny komunikat z oczekiwaną ścieżką.

Backdropy są deterministycznie generowane z sektorów, nazw, współrzędnych i wyjść pokojów. Po zmianie `world-map.json` należy je odtworzyć poleceniem:

```powershell
dotnet run --project tools/MudClient.MapBackdropGenerator -- src/MudClient.App/Assets/Map/world-map.json src/MudClient.App/Assets/Map/Backdrops
```

### Wykrywanie aktualnego pokoju z GMCP

Domyślnie `GmcpLocationResolver` nasłuchuje pakietu `Room.Info` i szuka vnum pod ścieżkami `vnum`, `num`, `room.vnum`, `room.num`, `location.vnum`, `location.num` (w tej kolejności). Aby dopasować inny serwer MUD, który wysyła lokalizację pod innym pakietem lub inną ścieżką, zmień `gmcpLocation.packages` i `gmcpLocation.vnumPaths` w `map-settings.json` — nie wymaga to zmian w kodzie.

### Odzyskiwanie ruchu i zamknięte bramy w autowalku

Przed każdym krokiem autowalk sprawdza ostatnie `mv/max_mv` z `Char.Vitals`. Przy poziomie 10% lub niższym rzuca `refresh` na siebie, jeśli gotowy czar znajduje się w `Char.MemSpell`; w przeciwnym razie wysyła `rest`, czeka 30 sekund i wznawia trasę. Zatrzymanie autowalku anuluje oczekiwanie.

Jeżeli próba otwarcia bramy kończy się komunikatem o zamknięciu na klucz, klient wysyła kolejno `zapukaj`, `pull` i `uderz`. Ruch jest wznawiany dopiero po wysłaniu całej sekwencji i potwierdzeniu przez `Room.Info`, że wyjście używane przez bieżący krok nie jest już zamknięte.

## Następne sensowne kroki

1. Zapisać surowe sesje Telnet do pliku i dodać replay w testach.
2. Dodać SQLite i profile połączeń.
3. Zbudować edytory aliasów, triggerów i timerów.
4. Dodać bufor wyjścia z wirtualizacją dla bardzo długich sesji.
6. Zbudować magazyn stanu GMCP i panele HP oraz grupy.
7. Dodać pathfinding i automatyczne chodzenie po mapie po kliknięciu pokoju.

## Ważne przy pracy z AI

Przeczytaj `AGENTS.md`. Zawiera zasady, które ograniczają mieszanie warstw i generowanie trudnego do utrzymania kodu.
