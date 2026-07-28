# Apprentice Bond — Deviations from design review §2.6

Documented interesting / practical deviations from `mod-design-review.md` §2.6.

## Implemented as designed

- **Manual** 1 mentor + 1 apprentice + **one skill**.
- XP share only when **same map, same room** (outdoors: within 8 cells) on the bonded skill.
- Apprentice share **15–25%** of mentor’s XP gain (default 20%, settings clamped).
- Mentor **5%** teaching tip on the same learn event.
- **Daily hard cap** on apprentice bond XP (default 2000).
- Moods: bond **+2/+2** (situational); break **−3** (memory); mentor death **−8～−12** (default −10); graduation small positive.
- Graduation when apprentice level ≥ mentor − N **or** min days; player gizmo + optional suggest message.
- Late-game: colony-size **hint** + settings **dissolve all**.
- **No** global aura, **no** cross-map XP, **no** multi mentor/apprentice web (one bond per pawn).

## Deviations (fun / practical)

1. **Outdoor “same room” = proximity 8 cells.**  
   Design said 同房间. Fields/mining often have null or outdoors rooms; pure Room equality would kill the mid-game loop. Still same-map only.

2. **Share triggers on any positive `SkillRecord.Learn` for the mentor on the bonded skill.**  
   Design text said 同学科技工作. Vanilla does not expose a clean “working that skill job” flag on every XP path; Learn postfix + same-room is the reliable bounded hook. Re-entry guard prevents cascade from teach/share XP.

3. **Mentor teach XP is also `direct` and guarded.**  
   Avoids infinite teach loops and passion double-dips on the tip.

4. **Graduation is a confirmation gizmo, not a full ritual job/building.**  
   Design allowed 仪式（可选）. Phase 1 uses message + mood memories; no new JobDef/building.

5. **Form-bond flow: mentor gizmo → target apprentice → float menu skill.**  
   Clear player agency without a custom main-tab UI.

6. **Easy mode** slightly raises share (+3% capped at 25%) and daily cap (×1.25). Still one-to-one, same room, hard cap.

## Explicitly not in Phase 1

- Multi-apprentice trees / skill webs.
- Cross-map or caravan XP drip.
- Forced work-together jobs (pawns still free to schedule; share only when co-located).
- Full graduation ceremony furniture / party.
