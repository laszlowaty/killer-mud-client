# KillerMudClient

[![CI](https://github.com/laszlowaty/killer-mud-client/actions/workflows/ci.yml/badge.svg)](https://github.com/laszlowaty/killer-mud-client/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/laszlowaty/killer-mud-client?include_prereleases&label=release)](https://github.com/laszlowaty/killer-mud-client/releases)
[![Strona projektu](https://img.shields.io/badge/www-killer--mud--client-d9b970)](https://laszlowaty.github.io/killer-mud-client/)

Wieloplatformowy klient MUD napisany w C# i Avalonia, tworzony z myślą o [killer-mud.pl](http://killer-mud.pl).

**Strona projektu i pobieranie:** https://laszlowaty.github.io/killer-mud-client/

![Zrzut ekranu klienta: terminal, mapa świata, panele buffów i automatów](docs/assets/screenshot.png)

## Funkcje

### Połączenie i protokoły

- połączenie TCP z MUD-em, stanowa obsługa protokołu Telnet,
- negocjacja `GMCP`, `NAWS`, `TTYPE`, `EOR` i `SUPPRESS-GO-AHEAD`,
- MCCP2 (kompresja zlib): dekompresja włączana dokładnie na granicy `IAC SB 86 IAC SE`; bajty odebrane po znaczniku w tym samym odczycie TCP trafiają do dekompresora, a zakończenie strumienia zlib przez serwer przywraca odczyt bez kompresji,
- konta z hasłem szyfrowanym DPAPI (Windows, per użytkownik) i automatycznym logowaniem; profil JSON nigdy nie zawiera hasła w postaci jawnej.

### Terminal

- kolory ANSI SGR: 16 kolorów z wybieralnymi schematami (ciepły, colorblind w skali szarości i mocno nasycony jaskrawy), 256 kolorów, RGB, bold, underline i reset,
- filtry kanałów nad terminalem: Wszystko / Walka / Czaty / System,
- szybkie przyciski komend pod terminalem,
- opcjonalne zawijanie długich linii (word wrap), przełączane w ustawieniach systemowych i zapamiętywane między uruchomieniami,
- zwirtualizowany bufor wyjścia: tekst trafia do bufora pierścieniowego (do 10 000 linii), a rysowane są wyłącznie linie widoczne w viewporcie (`OutputPaneControl`, własny `ILogicalScrollable`) — koszt dopisania tekstu nie zależy od wielkości scrollbacka, więc wielogodzinne sesje nie spowalniają UI,
- zaznaczanie i kopiowanie tekstu lub kolorowego fragmentu terminala jako obrazu do schowka systemowego (przeciąganie myszą + menu kontekstowe).

Renderer ANSI jest celowo liniowy: obsługuje kolory tekstu MUD, ale ignoruje terminalowe komendy przesuwania kursora. To jest odpowiedni model dla typowego klienta MUD.

### Mapa świata i autowalk

- interaktywna mapa świata (17 obszarów, ~25 000 pokoi) renderowana własną kontrolką Avalonia, z biomowymi podkładami graficznymi, zoomem względem kursora i widokiem strategicznym przy dużym oddaleniu,
- śledzenie pozycji postaci przez GMCP (`Room.Info`); każda zmiana lokacji ponownie włącza tryb śledzenia i centruje mapę na aktualnym pokoju,
- pathfinding i automatyczne chodzenie po kliknięciu pokoju, z politykami odzyskiwania: odpoczynek/`refresh` przy niskim `mv` oraz obsługa zamkniętych bram (szczegóły w sekcji [Mapa świata](#mapa-świata)).

### Panele postaci (GMCP)

Dokowalne, konfigurowalne panele (układ można przestawiać, przycisk **Resetuj UI** przywraca domyślny):

- **Postać** — statystyki postaci,
- **Kondycja** — HP/MV i stan witalny,
- **Efekty** — aktywne efekty,
- **Buffy** — lista wymaganych buffów z podświetleniem brakujących; komenda `/recast` jednym ruchem rzuca wszystkie brakujące,
- **Pokój** — szczegóły bieżącego pokoju (id, vnum, sektor, grafika),
- **Drużyna** — skład i stan grupy,
- **Mem** — zapamiętane czary,
- **GMCP** — surowy podgląd pakietów GMCP.

### Automatyzacja

- **Automaty** — aliasy i triggery z wzorcami oraz timery powtarzające komendy,
- **Autoassist** — opcjonalne wysłanie `as`, gdy GMCP wskaże walczącego członka drużyny w bieżącym pokoju; komenda jest ponawiana, jeśli postać przestanie walczyć, a członek drużyny nadal walczy,
- **Ordery** — opcjonalne wykonywanie komendy z komunikatu `Gracz rozkazuje ci 'komenda'.`, wyłącznie gdy nadawca jest członkiem aktualnej grupy GMCP,
- **Notatki** — panel na własne zapiski.

## Pobieranie

Gotowe paczki (self-contained, jeden plik wykonywalny, bez instalacji) są na [stronie projektu](https://laszlowaty.github.io/killer-mud-client/) oraz w [GitHub Releases](https://github.com/laszlowaty/killer-mud-client/releases): `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`.

Na macOS binarka nie jest podpisana — po rozpakowaniu:

```bash
chmod +x KillerMudClient-*
xattr -dr com.apple.quarantine .
```

## Budowanie ze źródeł

### Wymagania

1. .NET 10 SDK.
2. Opcjonalnie VS Code z rozszerzeniami **C# Dev Kit** i **Avalonia for VS Code** (projekt ma gotowe zadania i konfigurację debugowania).

### Budowanie i uruchomienie

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/MudClient.App
```

W VS Code możesz również nacisnąć `F5` albo uruchomić zadanie `run`. Skróty: `.\run.ps1` / `.\run.bat` (Windows), `./run.sh` (Linux/macOS).

### Publikacja lokalna

Skrypty przyjmują wariant `beta` albo `release` (domyślnie `release`) i czytają wersję z `Directory.Build.props`:

- `publish.bat [beta|release]` — Windows (win-x64, single-file, self-contained),
- `publish.sh [beta|release]` — Linux/macOS (RID wykrywany automatycznie),
- `publish-mac.bat [beta|release]` — cross-kompilacja macOS (arm64 + x64) z Windows.

Przed `dotnet publish` skrypty czyszczą katalog docelowy wybranego wariantu, dzięki czemu paczka nie zawiera plików po starszej wersji.

### Wydania (GitHub Actions)

Oficjalne wydania buduje workflow **Release** (zakładka Actions → Release → *Run workflow*):

- wybierasz kanał: `beta` → pre-release z tagiem `vX.Y.Z-beta.N` (numer bety nadawany automatycznie), `release` → pełne wydanie z tagiem `vX.Y.Z`,
- wybierasz podbicie wersji: `patch` / `minor` / `major` / `none` — workflow aktualizuje `Directory.Build.props`, commituje i taguje,
- po przejściu testów budowane są paczki `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64` i publikowane jako GitHub Release z automatycznymi notatkami.

Poza tym workflow **CI** buduje projekt i odpala testy przy każdym pushu i pull requeście do `main`, a workflow **Deploy GitHub Pages** publikuje stronę projektu z katalogu `docs/`.

## Gdzie wpisać adres MUD-a

Po uruchomieniu aplikacji wpisz host i port na górnym pasku, następnie kliknij **Połącz**. Możesz też utworzyć konto (host, port, login, hasło) — klient zaloguje się automatycznie.

## Struktura

```text
src/
├── MudClient.Core/       # Telnet, GMCP, TCP, mapa, aliasy, triggery, timery
└── MudClient.App/        # Avalonia, panele, widoki i renderowanie ANSI
tests/
├── MudClient.Core.Tests/ # testy silnika bez uruchamiania GUI
└── MudClient.App.Tests/  # testy warstwy aplikacji
tools/
└── MudClient.MapBackdropGenerator/ # generator podkładów mapy
docs/                     # strona GitHub Pages
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

## Czego jeszcze nie ma

- trwałego zapisu profili w SQLite (profile są w plikach JSON),
- rozbudowanej historii komend,
- pełnego terminala z pozycjonowaniem kursora,
- TLS.

## Następne sensowne kroki

1. Zapisać surowe sesje Telnet do pliku i dodać replay w testach.
2. Rozbudować historię komend.
3. Dodać TLS.

## Ważne przy pracy z AI

Przeczytaj `AGENTS.md`. Zawiera zasady, które ograniczają mieszanie warstw i generowanie trudnego do utrzymania kodu.
