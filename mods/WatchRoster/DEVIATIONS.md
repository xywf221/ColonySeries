# Watch Posts — Deviations from design review §2.9

Core intent from `mod-design-review.md` §2.9 is preserved: **post building, not roster HR**; manned night watch → better raid/fire warning; empty → weaker fire notice only; **never** auto-doors; **never** raise raid points.

## Implemented as designed

| Item | Notes |
|------|--------|
| Watch post building | `WR_WatchPost`, Security tab, cap 1–3 (settings up to 5) |
| Night stand-watch job | `WR_StandWatch` + Hunting workgiver; float menu force |
| Manned raid warning | Letter now + raid deferred 0.5–1.5h; **points copied unchanged** |
| Fire notice | Harmony on `FireWatcher.FireWatcherTick` interval mult |
| Empty penalty | Slower fire observe only |
| No door unlock | No door/code paths at all |
| No raid point raise | Prefix never mutates `parms.points` |
| No full roster UI | Intentionally absent |
| Settings + EN/CN | Keyed + DefInject |
| Portable csproj | Fieldcraft-style `RimWorldDir` / Harmony resolve |

## Deviations (fun / practical)

1. **Work type = Hunting** (not a new WorkTypeDef).  
   Avoids another column on the work tab for a C-tier feature; hunters are the natural “armed lookout” bucket. Forced float-menu still works for anyone.

2. **Early warning = letter + deferred `TryExecute`**, not letter-timing-only.  
   Vanilla enemy raids send the letter and spawn in the same worker call. Delaying the whole worker (after a scout letter) is the smallest way to give real prep time without inventing a second spawn pipeline. Storyteller still treats the Prefix as handled (`__result = true`).

3. **FireWatcher cadence mult** instead of patching `Alert_FireInHomeArea`.  
   Alert already scans home fires; speeding/slowing `UpdateObservations` changes when `LargeFireDangerPresent` and related systems react — small hook, design-aligned “notice”.

4. **Light rest drain while watching** (−tiny per tick).  
   Design did not specify fatigue math; a mild rest cost makes all-night watch a real tradeoff without mood-tax farming.

5. **Optional day watch** setting (default off).  
   Useful for polar/debug/playtest without changing the night-first design.

6. **PlaceWorker counts blueprints/frames** toward the post cap so players cannot blueprint past the limit.

## Explicitly not in Phase 1

- Full colony night roster / shift UI  
- Auto lock/unlock doors or flick compounds  
- Raid points / threat-scale modifiers  
- Separate “watch” work type column  
- Stage-then-attack lord duration tuning (immediate deferred raid is enough)
