# 工坊损耗 Workshop Wear (1.6)

Production benches wear out slowly. **Overwork** is the hero button: push a station for 24h speed at double wear, with a chance to **Jam**. Two repair tiers give real choices — slapdash vs proper.

Redesigned per `mod-design-review.md` §2.3 — not a second pipe-clog chore.

## Core loop

1. Bills complete → tiny wear (default **~1.0** toward Worn at **100** ≈ **80–120 bills**).
2. Inspect shows prose: **Good / Used / Worn / Jammed** (number is secondary).
3. Player hits **Overwork** on the building → +25% work speed for 24h, wear ×2; end may **Jam**.
4. Colonists repair:
   - **Slapdash** — 5 steel, fast, restores 40–60%, small fail chance to worsen. Does **not** clear jam.
   - **Proper** — 8 steel + 2 components, slow, full restore, **clears jam** and resumes bills.

## Default whitelist

| Included | Excluded by default |
|----------|---------------------|
| Electric/Fueled stove | Research benches |
| Electric/Fueled smithy | Sculpting tables |
| Machining table | |
| Fabrication bench | |
| Tailoring (hand/electric) | |
| Drug lab | |
| Brewery, smelter, biofuel | |
| Butcher table/spot, crafting spot | |
| Fuzzy: modded *Stove/Smithy/Machining/Tailor/Fabrication/…* with recipes | |

Settings can add research / sculpting.

## Numbers (defaults)

| Knob | Value |
|------|-------|
| Wear / bill | 1.0 (×0.5 easy) |
| Worn threshold | 100 |
| Speed at full Worn | 78% |
| Used band | ~100% → 92% |
| Jammed speed | 5% crawl (+ bills suspended) |
| Overwork | +25% speed, wear ×2, 24h |
| Base jam chance | 12% × wear scale (0.5–1.75×); **off in easy** |
| Slapdash | 5 steel, ~900 work, 40–60% restore, fail 4–18% |
| Proper | 8 steel + 2 components, ~2800 work, skill soft-gate 6 |

AI never auto-overworks. No jam explosions (dev fire spark flag only).

## Settings

- Master enable, easy mode, overwork toggle
- Wear rate / worn threshold / worn speed / overwork bonus / jam chance
- Include research / sculpting
- Overwork quality-risk flavor (see `DEVIATIONS.md`)
- Dev jam fire

## Build

```bash
dotnet build "Mods/WorkshopWear/1.6/Source/WorkshopWear/WorkshopWear.csproj" -c Release
```

Portable csproj (Fieldcraft style). Override: `-p:RimWorldDir="D:\Games\RimWorld"`.

## Requirements

- RimWorld 1.6
- Harmony (`brrainz.harmony`)
