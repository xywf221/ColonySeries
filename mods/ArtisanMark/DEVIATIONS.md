# Artisan Mark — Deviations from design review §2.2

Documented interesting / practical deviations from `mod-design-review.md` §2.2.

## Implemented as designed

- ThingComp stores maker load ID + name snapshot (no full-map nearest-sword scans).
- Eligible: weapons, apparel, sculptures/art, artificial body parts; **not** food/drugs/consumables.
- Inspect: "Made by X" / 「制作者：X」.
- Self-made apparel mood +1~2 (default **+2**, settings slider).
- Dead friend/lover/bonded heirloom mood +3~4 (default **+4**), **one stack**.
- Combat bonus **OFF by default**; if on: +2% hit/damage cap, same-room living maker, no multi-stack.
- Phase 1: **no** enemy "hate the maker" AI.
- No forced legendary quality / numeric inflation.

## Deviations (fun / practical)

1. **Self-made weapons also get combat bonus** (when combat setting ON).  
   Design text emphasized apparel mood + optional combat; we apply the same-room maker check to the **primary weapon** mark (self or ally maker). Self-wielding your own stamped weapon counts without needing a second pawn in the room — still capped at one +2% and off by default.

2. **Close-bond definition expanded slightly for legacy mood.**  
   Beyond lover/bonded: spouse/fiancé/ex, parent/child/sibling, **or** opinion ≥ 40 ("friend" territory). Keeps the relic fantasy when two colony friends (no formal lover flag) lose one crafter.

3. **Prostheses stamped.**  
   Design list included 人造躯体; enabled by default, toggle in settings.

4. **Dual stamp hooks.**  
   Both `GenRecipe.MakeRecipeProducts` postfix and `Thing.Notify_RecipeProduced` postfix, so minified art and odd recipe paths still get a name when possible.

5. **Combat uses StatPart + TakeDamage.**  
   Hit via `MeleeHitChance` / `ShootingAccuracyPawn`; damage via `TakeDamage` amount scale **and** weapon damage multiplier stats. Still a single conceptual +N% cap from one weapon mark — not per-apparel stacking.

## Explicitly not in Phase 1

- Enemy faction AI hatred of a maker's gear.
- Quality forcing / auto-legendary.
- Full-map maker search for gameplay loops (resolve is only for thought/combat checks on already-stamped items).
