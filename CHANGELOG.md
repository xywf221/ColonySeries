# Changelog

All notable **series-level** changes. Per-mod deep notes may also live in that
pack’s README / DEVIATIONS.

## Unreleased

### Engineering

- Monorepo `ColonySeries/` with 14 loadable packs under `mods/`.
- Shared `Directory.Build.props` / `.targets` (TFM, RimWorldDir, Harmony, refs).
- Per-mod csproj reduced to AssemblyName identity.
- Scripts: `build-all`, `deploy`, `validate-mods`, `new-mod`, `sync-catalog`, `list-mods`.
- `catalog.json`, CONTRIBUTING, ENGINEERING handbook, editorconfig/gitattributes.

### Gameplay (carried from pre-monorepo work)

- Landworks, Fieldcraft, PersonalKit, Trait Extractor.
- Dead Drop, Artisan Mark, Workshop Wear, Rumor Mill.
- Ledger v2 with **Draw Credit** player verb (fixed dead trade-shortfall path).
- Apprentice Bond, Quarantine Line, Salvage Atlas, Watch Posts, Ford Rights.

## 2026-07-28

- Initial monorepo import and design-review freeze for standalone D-tier packs
  (Dream Debt, Logistics Cold not shipped).
