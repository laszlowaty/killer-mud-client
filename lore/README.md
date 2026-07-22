# Lore KillerMUD

Źródłem prawdy są rekordy JSONL wymienione w `manifest.json`. Plików w `dist/`
nie należy edytować ręcznie: są odtwarzane z rekordów kanonicznych.

Kanoniczny zbiór jest odbudowywany wyłącznie ze źródeł wymienionych
w `tools/KillerMUD/area-lore/area.lst`.

Po zmianie świata albo rekordów lore uruchom z katalogu repozytorium:

```powershell
python .codex/skills/build-killermud-lore/scripts/build_lore_outputs.py --lore-root lore
```

Jedna komenda waliduje dane i aktualizuje trzy widoki:

- `dist/lore-catalog.json.gz` — katalog dla Killeropedii i przyszłych narzędzi AI,
- `dist/markdown/` — czytelne artykuły z odnośnikami dla ludzi,
- `dist/build-manifest.json` — wersję źródeł, skróty i liczebność artefaktów.

Po zbudowaniu aplikacji katalog gzip jest jej zasobem awaryjnym. Aby dostarczyć
samą aktualizację lore bez rekompilacji, skopiuj go do
`%AppData%\KillerMudClient\Data\lore-catalog.json.gz` albo użyj:

```powershell
python .codex/skills/build-killermud-lore/scripts/build_lore_outputs.py `
  --lore-root lore `
  --install-dir "$env:APPDATA\KillerMudClient\Data"
```

Killeropedia nie parsuje Markdown ani kanonicznego JSONL. Czyta wyłącznie
skompilowany katalog, dzięki czemu zachowuje relacje, typy, status prawdziwości
i źródła bez ładowania całego kontekstu lore.
