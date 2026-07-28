# Quarantine Line — interesting deviations

Core §2.7 (sleep when healthy, zone + isolation protocol, visit risk vs containment, no medicine replace) is intact. Notes:

## 1. Isolation is a map policy flag, not auto door locks

Strict isolation multiplies exposure math and moods. It does **not** forcibly lock doors or rewrite pathing (unfair “invisible betrayal”). Players still place doors/rooms; the protocol is the readable tradeoff layer.

## 2. Designation cells, not a full AreaManager area

Quarantine uses a `HashSet<IntVec3>` + `DesignationDef` so paint/release matches Fieldcraft-style designators without inventing a second allowed-area UI. Fine for ward-sized regions; not meant as a whole-map paint.

## 3. Patient gizmo seeds 3×3 ward

When outbreak is awake, a sick colonist can **Seed quarantine here** (3×3). Speeds the first response without forcing Architect tab mid-crisis. Release gizmo only clears **that cell**.

## 4. Tend-time doctor exposure (tiny)

Harmony on `TendUtility.DoTend` adds a small catch chance for doctors — stricter protocol reduces it. Optional flavor on top of the rare pulse; still not a medicine replacement.

## 5. Not added (on purpose)

- No replacement of immunity gain curves or drug efficacy.
- No automatic bed assignment / hospital AI overhaul.
- No full PPE apparel system.
- No hourly all-pawn mesh; pulse is rare and list-bounded.
