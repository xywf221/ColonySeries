# Rumor Mill — deviations from design brief

## Skipped: Stingy

**Design listed** “抠门 / Stingy” (refuse gifts / low social gifts) as tag #4, with “慎用”.

**Choice:** **skip entirely** in Phase 1.

**Why:** Fair detection is hard without double-taxing vanilla gift/trade UI or punishing frugal colonies. Prefer skip over unfair sticker tax (authority §2.4 / user MUST).

## Softened: Squeamish

Not a second mental-break system. Counts **line-of-sight humanlike death witnesses** on the death-thought pipeline (max 8 colonists, 12-cell LOS) — no hourly mesh. Psychopath / Bloodlust exempt. Does **not** force breaks.

## Player verb: Commend (not funeral / silence cell)

Phase 1 agency is **Commend gizmo + job** (soften one negative tier or reinforce Reliable) plus **chitchat timestamp reinforce**. Funerals / “封口” imprisonment left for a later pass if needed.

## Nerved soft-link

Uses **hediff defName** (`TE_NeuralScar`, `TE_NeuralTrauma`, `TE_ExtractionCooldown`) + AddHediff postfix + rare player-home scan. No hard reference to Trait Extractor assembly.

## Effects path

- Recruit: small `guest.resistance` nudge on recruit interaction + `NegotiationAbility` StatPart
- Trade: `Tradeable.GetPriceFor` using **negotiator only** (no colony-wide face scan) + `TradePriceImprovement` StatPart
- Social: `Thought_RumorSocial` dynamic opinion

Magnitudes default ~8% / 4% / ±10 with settings + easy mode.
