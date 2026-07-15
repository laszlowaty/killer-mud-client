---
name: mapa
description: Edytuje ilustracje lokacji KillerMudClient na podstawie najnowszych eksportów MapImageCalibrator. Używaj, gdy użytkownik wpisuje `/mapa nazwa-krainy`, `$mapa nazwa-krainy`, prosi o zastosowanie markerów lub roboczych przesunięć siatki do obrazu miasta albo chce poprawić grafikę mapy z najnowszego pakietu CalibrationExports.
---

# Mapa

Popraw obraz lokacji na podstawie najnowszego kompletnego eksportu kalibratora.

## Obowiązkowy styl ilustracji

Przed analizą eksportu przeczytaj w całości [references/manuscript-map-style.md](references/manuscript-map-style.md). Do każdego prompta generatora:

1. skopiuj bez zmian cały blok `Core prompt — use verbatim` z tej referencji;
2. dopisz wszystkie pasujące `KillerMud prompt overrides`;
3. dopiero potem dodaj dane roomów, markerów, ręcznych elementów, proporcje obrazu oraz instrukcje konkretnej edycji.

Nie tłumacz, nie streszczaj i nie zastępuj pełnego prompta krótszym opisem stylu. Dane roomów nadal decydują o treści i geografii. `generationPrompt` może zmieniać klimat, pogodę, epokę, akcenty kolorystyczne i porę dnia, ale nie może wyłączyć pergaminu, techniki tuszu, szrafowania, stipplingu, matowego druku ani manuskryptowo-kartograficznego języka ilustracji.

Obowiązkową wizualną referencją stylu jest `src/MudClient.App/Assets/Map/Locations/old-continent-overview.png`. Obejrzyj ją przed każdym generowaniem i przekaż generatorowi jako osobny obraz z rolą „style reference only”. Lokalne mapy muszą powtarzać jej kolor pergaminu, grubość i nieregularność kreski, proporcje czarnego oraz burgundowego tuszu, gęstość detalu, szrafowanie, stippling i efekt starego druku. Nie wystarczy zgodność z samym opisem tekstowym.

## Workflow

1. Ustal nazwę lokacji z tekstu po `/mapa` lub `$mapa`.
2. Ustal katalog główny repozytorium i uruchom:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "<repo>\.codex\skills\mapa\scripts\find-latest-export.ps1" -Name "<nazwa>" -RepoRoot "<repo>"
   ```

3. Odczytaj zwrócony JSON. Jeśli skrypt nie znalazł kompletnej pary eksportu PNG+JSON, poproś użytkownika o wykonanie „Eksportuj pakiet dla AI” w kalibratorze. `compositePng` jest opcjonalny dla starszych eksportów, ale wymagany do dokładnego zastosowania ręcznie ułożonych elementów.
4. Obejrzyj `targetImage`, `exportPng`, obowiązkowy atlas `old-continent-overview.png` oraz — jeśli istnieje — `compositePng`, po czym przeczytaj `exportJson`. Sprawdź `isBlankCanvas`, `layerName`, `generationPrompt`, `rooms`, `imageElements` i `hasManualComposition`.
5. Traktuj pliki następująco:
   - `targetImage`: jedyny plik docelowy do nadpisania oraz referencja geografii, kadru i semantycznej zawartości obszarów nieobjętych zmianą. Nie traktuj jego dotychczasowego stylu jako wzorca, jeśli różni się od obowiązkowego stylu pergaminowego atlasu.
   - `old-continent-overview.png`: nadrzędna referencja stylu dla wszystkich map, także lokalnych. Nigdy nie kopiuj z niej geografii, nazw, ramki ani kompasu do mapy lokalnej; kopiuj wyłącznie medium, paletę, kreskę, fakturę, gęstość detalu i charakter druku.
   - `exportPng`: referencja położenia siatki i numerowanych markerów; nie kopiuj z niego UI.
   - `compositePng`: czyste połączenie oryginalnego obrazu i ręcznie ułożonych elementów bez roomów, markerów i UI. Gdy `hasManualComposition` jest `true`, jest dokładną bazą kompozycji, a nie tylko luźną inspiracją.
   - `imageElements`: lista ręcznie ułożonych elementów z plikiem źródłowym, środkiem `imageX/imageY`, rozmiarem `width/height`, obrotem, kryciem i kolejnością `zIndex`. Przelicz położenie i rozmiar na procent wymiarów `targetImage` i wymień w promptcie. Zachowaj te parametry, chyba że marker jawnie nakazuje zmianę.
   - `generationPrompt`: wyłącznie ogólny opis klimatu i kontekstu wpisany przez użytkownika przy tworzeniu czarnej warstwy. Użyj go do nastroju, pory dnia, palety, epoki lub ogólnego charakteru miejsca. Nie wyprowadzaj z niego geografii ani obowiązkowych obiektów i nie pozwól mu zastąpić danych roomów. Jeśli nie określa pory dnia lub oświetlenia, zastosuj obowiązkowy wariant dzienny.
   - `markers`: instrukcje lokalnych zmian. Przelicz `imageX/imageY` na procent wymiarów obrazu i wymień je w promptcie.
   - `roomOffsets`: roboczy oczekiwany układ siatki. Użyj go oraz screena do skorygowania przebiegu dróg i położenia obiektów na obrazie; nie modyfikuj `world-map.json`.
   - `rooms`: wybrane roomy nowej warstwy wraz z vnumami, nazwami, sektorami i współrzędnymi. Są podstawowym źródłem treści i geografii. Sektory określają typ terenu lub wnętrza, nazwy określają funkcję i ważne obiekty, a współrzędne oraz połączenia widoczne na `exportPng` określają ich wzajemny układ.
6. Wybierz sposób generowania. W każdym wariancie przekaż `old-continent-overview.png` jako nadrzędną referencję stylu, umieść w instrukcji pełny, niezmieniony prompt z `references/manuscript-map-style.md`, pasujące nadpisania KillerMud oraz jawnie podaj wybraną porę dnia:
   - Gdy `isBlankCanvas` jest `false` i `hasManualComposition` jest `true`, użyj wbudowanego generatora obrazów w trybie precyzyjnej edycji. Podaj `compositePng` jako cel edycji, `targetImage` jako referencję geografii, kadru i niezmienionych obszarów, `old-continent-overview.png` jako referencję wyłącznie stylu, a `exportPng` jako pomocniczą referencję roomów i markerów. Wymagaj naturalnego przerysowania ręcznie dodanych elementów tuszem na pergaminie: usuń obce ramki lub tło assetu, dopasuj perspektywę, skalę kreski, szrafowanie i paletę do atlasu, ale zachowaj środek, rozmiar, obrót, kolejność oraz znaczenie każdego elementu. Przestylizuj całość do obowiązkowego kontraktu bez przebudowy geografii.
   - Gdy `isBlankCanvas` jest `false` i `hasManualComposition` jest `false`, użyj `targetImage` jako celu precyzyjnej edycji, `old-continent-overview.png` jako referencji wyłącznie stylu, a `exportPng` jako pomocniczej referencji geometrii. Wymagaj zachowania kadru, geografii i semantycznej zawartości wszystkich nieoznaczonych obszarów, ale przerysowania całego obrazu dokładnie w wizualnym stylu atlasu. Wymagaj braku tekstu, pseudo-pisma i elementów UI.
   - Gdy `isBlankCanvas` jest `true`, wygeneruj pełny obraz od zera, używając `old-continent-overview.png` jako referencji wyłącznie stylu. Najpierw zbuduj treść i kompozycję z sektorów, nazw oraz układu `rooms`: pogrupuj sąsiednie sektory w ciągłe obszary terenu lub wnętrza, wyznacz drogi i przejścia z połączeń widocznych na `exportPng`, a obiekty nazwane w roomach umieść w odpowiadających im rejonach. Traktuj węzły roomów jako niewidoczny szkielet, nie jako kształty do narysowania. Nie twórz technicznego rzutu, blueprintu, tilemapy, siatki prostokątnych pomieszczeń ani odseparowanych symboli na pustym pergaminie; wyrenderuj jeden organiczny, ciągły fragment świata o tej samej gęstości i sposobie ilustracji co atlas. Następnie zastosuj `generationPrompt` wyłącznie jako ogólną warstwę klimatu i kontekstu. Każdy marker oraz każdy `imageElement` musi zostać jawnie uwzględniony; wymień je w instrukcji dla generatora wraz z położeniem, skalą i znaczeniem. Jeżeli istnieje `compositePng`, użyj go jako dokładnej referencji położenia i skali elementów, ale nie traktuj czarnego tła jako części ilustracji. Użyj `exportPng` wyłącznie jako referencji geometrii roomów i markerów. Nie kopiuj siatki, markerów ani UI. W promptcie podaj stosunek boków `targetImage` i wymagaj, aby cała ważna geografia mieściła się w bezpiecznym kadrze tego prostokąta.
7. Sprawdź rezultat wizualnie względem `targetImage`, obowiązkowo względem `old-continent-overview.png` oraz — gdy istnieje ręczna kompozycja — `compositePng`. Zweryfikuj obecność i położenie każdego `imageElement`, brak przypadkowych liter oraz rzeczywistą zgodność z atlasem: ten sam ton i faktura pergaminu, zbliżona grubość kreski, udział burgundu, gęstość detalu, szrafowanie, stippling i efekt druku. Odrzuć rezultat wyglądający jak blueprint, techniczny plan lochu, tilemapa lub zestaw ikon, nawet jeśli spełnia tekstowy opis stylu. W razie zgubienia elementu, naruszenia nieoznaczonych obszarów, pseudo-tekstu albo niezgodnego stylu wykonaj jedną ukierunkowaną iterację.
8. Dopasuj wynik do pierwotnych wymiarów `targetImage` bez rozciągania, uruchamiając:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "<repo>\.codex\skills\mapa\scripts\fit-generated-image.ps1" -SourceImage "<wynik generatora>" -TargetImage "<targetImage>" -OutputImage "<plik tymczasowy PNG>"
   ```

   Skrypt skaluje oba wymiary jednym współczynnikiem i centralnie kadruje wyłącznie nadmiar (`cover`). Obejrzyj dopasowany plik tymczasowy i sprawdź ponownie wszystkie skrajne roomy, markery i `imageElements`. Jeśli kadr coś odciął, wykonaj jedną ukierunkowaną regenerację z większym marginesem bezpieczeństwa; nie przełączaj się na rozciąganie ani osobne współczynniki X/Y. Dopiero po tej kontroli nadpisz `targetImage`. Nie zmieniaj manifestu, chyba że eksport lub użytkownik jawnie żąda zmiany całej warstwy.
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
- Ręcznie ułożony `imageElement` jest silniejszą instrukcją kompozycji niż ogólny opis markera. Marker może zmienić lub usunąć element tylko wtedy, gdy mówi o tym wprost.
- Asset elementu jest wzorcem znaczenia, sylwetki i położenia. Nie zachowuj jego obcej ramki, jednolitego tła ani stylistycznych artefaktów, jeśli nie pasują do obrazu docelowego.
- Nie traktuj obiektów wspomnianych tylko w `generationPrompt` jako obowiązkowych. Obowiązkowa zawartość wynika z nazw i sektorów roomów, markerów oraz ręcznie ułożonych elementów.
- Nie pozwól, aby klimat pojedynczej lokacji zmienił wspólną perspektywę, pergaminowo-tuszowy styl starego atlasu, sposób szrafowania albo hierarchię detalu. Różnicuj temat, teren, architekturę i akcenty, zachowując ten sam język wizualny.
- Unikaj efektu przypadkowego kolażu efektownych obiektów. Najpierw buduj czytelną kompozycję z geografii roomów, potem wybierz kilka punktów skupienia, a pozostałe obszary pozostaw wizualnie spokojniejsze.
- Nie przyciemniaj całej mapy tylko dlatego, że miejsce jest mroczne, magiczne, podziemne lub niebezpieczne. Noc albo niedzienny wariant pergaminu wymaga wyraźnej wskazówki w `generationPrompt`.
- Nie generuj nazw roomów, regionów ani ozdobnego pseudo-pisma. Tekst jest dozwolony wyłącznie wtedy, gdy użytkownik lub marker podaje jego dokładną treść.
- Przed generowaniem sprawdź, czy instrukcja dla generatora obejmuje wszystkie unikalne sektory i istotne nazwy roomów oraz każdy marker i `imageElement`. Nie pomijaj ich na rzecz bardziej efektownej interpretacji ogólnego prompta.
- Nigdy nie rozciągaj obrazu do prostokąta docelowego. Zawsze zachowuj proporcje przez jednolitą skalę i kontrolowane kadrowanie.
- Przesunięte roomy są instrukcją dla grafiki, nie zmianą danych mapy.
- Gdy instrukcja jest niejednoznaczna, nie zgaduj znaczącej przebudowy; poproś o doprecyzowanie jednego markera.
- Zachowuj rozdzielczość, proporcje, nazwę pliku, kolejność warstw i aktualne ustawienia manifestu.
