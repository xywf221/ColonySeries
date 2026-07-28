# Ford Rights — Phase 1 deviations

Core from `mod-design-review.md` §2.10 preserved: water-gated ford, small capped tolls, slow fishing, rare readable flood, idle without water, settings + easy mode. Landworks river soft-link deferred (doc: Phase 2).

## Intentional Phase 1 choices

| Change | Why |
|--------|-----|
| **PlaceWorker hard-requires adjacent water** | Design says ford near water; hard gate beats “build anywhere then idle forever” confusion. |
| **Toll via Harmony Postfix on trader/visitor/traveler arrival** | Event-driven, no per-tick caravan scan; still player-visible message + silver drop at ford. |
| **Daily + 15-day rolling caps (not calendar quadrum)** | Caps anti-print-money without brittle longitude/quadrum math; inspect shows both. |
| **&lt;35% HP disables tolls/fish** | Flood “must repair” without inventing a custom repair Job — vanilla repair is the verb. |
| **Fishing on Hunting work type + long cooldown** | Slow/few garnish; avoids inventing a whole fishery loop or depending on Odyssey Fishing DLC. |
| **Custom `FR_RiverFish` item** | Works without Odyssey fish defs; modest nutrition + fast rot. |
| **Coarse water map sample (step 10 + edges)** | Sleep detection without `AllCells`. |
| **DEV gizmos** | Force water refresh / fish ready / toll / flood for playtest. |

## Not done (later)

- Landworks channel / river rewrite soft-link.
- Multi-cell bridge graphic / path cost shaping of the river crossing itself.
- Faction goodwill for “fair toll” vs extortion tiers.
- Odyssey Fishing work-type merge when DLC present.
