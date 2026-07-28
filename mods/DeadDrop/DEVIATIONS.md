# Dead Drop — Phase 1 deviations

Core loop from `mod-design-review.md` §2.1 is preserved: research → edge cairn → max 1 order → accept → colonist Job → success/fail letters → rep → optional search party.

## Intentional extras (still grey-edge trade)

| Change | Why |
|--------|-----|
| **Order codenames** ("Ash Relay", …) | Makes letters/gizmos memorable; pure flavor. |
| **Order heat** scales discovery | Hotter goods (luci, serum, cores) feel riskier without new systems. |
| **Night glow modifier** (−25% discovery in dark) | Rewards waiting for night runs; player verb without UI spam. |
| **Kind trait** refuse + higher discovery + slower service | Design doc already suggested honesty friction; made concrete. |
| **Haul-to-cairn Job toils** | Goods/silver are carried to the edge before exchange — harder AFK than remote map-consume-only. Final settle still uses map lister consume so multi-stack orders complete. |
| **DEV gizmos** on cairn | Force order / +rep / complete for playtest. |
| **PlaceWorker_DeadDropEdge** | Currently permissive (no hard edge ban) to avoid soft-locking odd maps; inspect/README still steer edge placement. |

## Not done (later)

- Multi-stack haul chains when one stack < order count (Phase 1 takes closest stack then map-consumes remainder at settle — acceptable; full multi-haul is Phase 2).
- Empire-specific "watch" incident separate from generic search party.
- Witness pawns mid-path (currently roll at exchange only).
