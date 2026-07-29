# Contributing

This repository is an experimental fork of
[laszlowaty/killer-mud-client](https://github.com/laszlowaty/killer-mud-client)
(the "upstream" repo). It develops extra features that aren't (yet, or ever)
merged upstream, while trying to stay compatible with it. That goal shapes the
branch model below.

## Branch model

| Branch | Purpose |
| --- | --- |
| `main` | Mirrors `upstream/main` exactly. Never receives direct commits or feature PRs — only updated by syncing from upstream (see below). |
| `develop` | Integration branch for this fork. All fork-specific work lands here. Releases are cut from this branch. |
| `feature/*` | One branch per feature or fix, created off `develop`, merged back into `develop` via PR. |

```
feature/chat-panel        ┐
feature/transparency-mode ├─► develop ──► dev release (vX.Y.Z-dev.N)
feature/map-markers       ┘

upstream/main ──sync──► main   (no feature work ever happens here)
```

### Starting new work

```bash
git checkout develop
git pull
git checkout -b feature/my-thing
```

Open the PR against `develop`, not `main`.

### Syncing `main` from upstream

`main` should only ever move to match `upstream/main`:

```bash
git checkout main
git fetch upstream
git reset --hard upstream/main
git push origin main --force-with-lease
```

Do this periodically so `develop` can be rebased/merged against a current
`main` when picking up upstream changes.

## Commit messages

This repo uses [Conventional Commits](https://www.conventionalcommits.org/).
The release notes generator groups commits by prefix, so sticking to these
types keeps changelogs and GitHub Releases readable:

- `feat: ...` — a new feature (listed under **Added** in release notes)
- `fix: ...` — a bug fix (listed under **Fixed**)
- `refactor: ...` — code change that neither fixes a bug nor adds a feature
- `docs: ...` — documentation only
- `chore: ...` — maintenance (tooling, deps, version bumps)
- `test: ...` — adding or correcting tests
- `style: ...` — formatting, whitespace, no code meaning change
- `perf: ...` — performance improvement
- `build: ...` — build system or dependency changes
- `ci: ...` — CI/CD configuration changes
- `revert: ...` — reverts a previous commit

Optional scope in parentheses, e.g. `feat(chat): add chat panel`,
`fix(ui): transparency rendering`.

## Cutting a dev release

Releases are only ever cut from `develop`, via the **Release** workflow
(`Actions` → `Release` → `Run workflow`, on branch `develop`).

Each release is versioned relative to upstream's *current* version, not the
fork's own last release: the workflow reads `Directory.Build.props` from
`upstream/main`, applies the chosen bump (`patch`/`minor`/`major`/`none`), and
appends the next free `-dev.N` suffix for that target version. For example,
if upstream is at `v0.6.3`:

- First dev release → `v0.6.4-dev.1`
- Next one (same upstream base) → `v0.6.4-dev.2`, `v0.6.4-dev.3`, ...
- Once upstream ships `v0.6.4` → the next dev release automatically becomes
  `v0.6.5-dev.1`

Every fork release is a GitHub prerelease — the fork never claims ownership
of a stable version number, since that number belongs to upstream. The
generated release notes always include a "Based on upstream" line stating
which upstream version the release was built against, followed by
Added/Changed/Fixed sections derived from Conventional Commit messages since
the previous tag.
