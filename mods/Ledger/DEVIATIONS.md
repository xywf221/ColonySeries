# Ledger v2 deviations

## Deleted from v1

- Weekly abstract power cost
- Global permanent trade price penalty
- Default low-reserve thoughts
- Nutrition full-def scans for “credit weather”

## How debt actually opens (Phase 1 fix)

Design §2.5 wants **credit leverage the player chooses**.  
Vanilla `TradeDeal` cannot finish with unpaid silver, so a Postfix
`spent > silverAtStart` check **never fired** — the old “formalize shortfall”
path was dead.

**Current player verb:** colonist gizmo **Draw Credit**

1. Float-menu amount (50…room, min 50)
2. Spawn silver near the pawn
3. Open a dated debt to a visible non-hostile faction
4. Tiny −1 credit on draw (not interest; anti-free-money feel)
5. ~1h cooldown between draws

Repay / extend / default letters unchanged in spirit; multi-debt menus added
(oldest quick-repay + choose list).

TradeDeal patch now only grants a **small honest-trade credit bump** on
successful deals — it does **not** invent mid-deal debt.

## Collection

On default: goodwill hit; optional small `RaidEnemy` only if the creditor is
already hostile and the setting allows; easy mode letter-biased.

## Not in Phase 1

- True in-trade “buy on credit” UI row inside `Dialog_Trade`
- Per-faction credit ledgers / interest schedules
- Goods-in-kind repayment
- Dedicated MainTab (gizmos + letters are the surface)
