# Workshop Wear — interesting deviations

Core redesign (overwork hero button, two repair tiers, whitelist, four prose states) is intact. Optional extras:

## 1. Overwork quality-risk flavor (default ON)

While a station is overworking, completed bill iterations have a small chance (~4%) to show a **silent flavor message** that thrashing may hurt quality.

- Does **not** rewrite product quality mid-bill (avoids brittle Harmony on `GenRecipe` finish).
- Purely telegraphs risk so overwork feels like a real gamble beyond jam.
- Toggle: **Settings → Overwork quality risk**.

If a later version hooks actual quality rolls, keep the effect tiny (e.g. −1 quality step ≤5% while overworking).

## 2. Soft skill gate on proper repair (not a hard lock)

Proper overhaul prefers Crafting 6+. Under-skilled pawns still can do it but take **+35% work time** instead of a hard fail. Keeps early colonies able to unjam with sweat.

## 3. Not added (on purpose)

- No “maintenance day” designation — two workgivers already cover the decision space.
- No explosions on jam (dev fire spark only).
- No AI overwork.
- No default research/sculpting wear.
