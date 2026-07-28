# Contributing to ColonySeries

## Before you write code

1. Read [`CLAUDE.md`](CLAUDE.md) — short engineering charter (humans + agents).
2. Read [`docs/design/mod-design-review.md`](docs/design/mod-design-review.md) — playability is the product.
3. Read [`docs/ENGINEERING.md`](docs/ENGINEERING.md) — build system contract.
4. Pass the design five gates for any new feature:
   - Player agency
   - Meaningful stakes
   - Readable failure
   - No double-tax with vanilla (default settings)
   - Master switch + easy mode

## Dev loop

```bash
# one-time: clone next to RimWorld install, or set RIMWORLD_DIR
export RIMWORLD_DIR="/path/to/RimWorld"   # optional if sibling layout

./scripts/validate-mods.sh
./scripts/build-all.sh
./scripts/deploy.sh
# → launch RimWorld, enable the pack, test
```

Edit under `mods/<Name>/` only. Do not invent a second copy under the game
`Mods/` tree as source of truth.

## Code conventions

- C# for RimWorld 1.6 / `net472`, `LangVersion` 12 (shared props).
- Event-driven or bounded pulses — **no** `map.AllCells` on hot paths.
- Soft-link other series packs (`GetNamedSilentFail`, optional thought/hediff names).
- Settings: master enable + easy mode for systems with cost/risk.
- Player verbs beat passive debuffs.
- EN + CN Keyed (and DefInject where labels live in Defs).
- Intentional design drift → `DEVIATIONS.md` in that pack.

### csproj

Identity only — see ENGINEERING. Never paste a full portable csproj from an
old template into a new pack.

### About.xml

- `author`: **ColonySeries**
- `packageId`: stable reverse-DNS, unique in `catalog.json`
- `supportedVersions`: include `1.6`

## PR / commit hygiene

- One logical change per commit when practical.
- Run `validate-mods` + build the touched packs before pushing.
- Update `docs/design/todo.md` / CHANGELOG when shipping player-visible behavior.
- Do not commit `obj/`, `_decomp/`, or Steam workshop trees.

## Out of scope packs

See `docs/archive/NOT_SHIPPED.md`. Do not re-add DreamDebt / LogisticsCold as
standalone packages without a design review amendment.
