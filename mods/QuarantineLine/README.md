# 疫线检疫 Quarantine Line (1.6)

Spatial quarantine when disease hits. **Fully asleep** with no infectious disease on player maps.

Per `mod-design-review.md` §2.7 — space decisions (who enters the ward, isolation policy, visit risk), not a second medicine system.

## Core loop

1. Infectious disease appears (vanilla incident, tend infection, or rare QL event) → map component **wakes**.
2. **Architect → Quarantine**: mark ward cells / release cells / toggle **strict isolation**.
3. Sick in the ward: contained. Healthy **visitors** take higher exposure risk.
4. **Strict isolation**: outbreak exposure down; patient mood worse; doctors feel workload.
5. Disease clears → component **sleeps** (no exposure jobs, no meaningful tick cost). Zone paint may remain.

Vanilla tend / immunity / medicine unchanged. QL only seeds light contact infections when spread modifiers are on.

## Player verbs

| Verb | Where |
|------|--------|
| Mark quarantine zone | Architect → Quarantine → Mark |
| Release zone | Architect → Quarantine → Release |
| Force isolation policy | Architect tool **or** colonist gizmo **Strict isolation** |
| Seed ward on patient | Sick colonist gizmo (when outbreak awake) |

## Sleep conditions

- No infectious hediff (`isInfection` or `HediffComp_Immunizable`) on:
  - free colonists, colony prisoners, colony animals
- Then: rare tick only re-checks presence lists; **no** exposure pulse, **no** carrier jobs.

Wake triggers: presence check finds disease, or Harmony `AddHediff` on player-relevant pawn.

## Balance knobs (defaults)

| Knob | Default | Easy |
|------|---------|------|
| Visit risk / pulse | 12% | ×0.45 |
| Casual exposure / pulse | 4% | ×0.45 |
| Strict containment | 65% cut | same cut, lower base |
| Patient mood (open / strict) | −4 / −8 | ×0.6 |
| Doctor workload (strict) | −2 | ×0.6 |
| Pulse interval | 2500 ticks | same |
| **Spread modifiers master** | ON | can disable entirely |
| Caravan / animal events | ON, low chance | plague weighted out |

## Settings

Options → Mod settings → Quarantine Line:

- Master enable, **spread modifiers off**, easy mode
- Visit / base exposure / isolation %
- Mood magnitudes
- Rare events toggles
- Pulse interval

## Build

```bash
dotnet build "E:\RimWorld-v1.6.4850\Mods\QuarantineLine\1.6\Source\QuarantineLine\QuarantineLine.csproj" -c Release
```

Portable csproj (Fieldcraft style). Override: `-p:RimWorldDir="D:\Games\RimWorld"` or `RIMWORLD_DIR`.

Output: `1.6/Assemblies/QuarantineLine.dll`

## Requirements

- RimWorld 1.6
- Harmony (`brrainz.harmony`)
