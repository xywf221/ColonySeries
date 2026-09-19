using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    /// <summary>
    /// Target effect for the PK_PsychicShockPulser artifact: slams every hostile /
    /// factionless pawn on the map into PsychicShock (consciousness clamped to 10%,
    /// so they drop and stay down) for a configurable duration.
    ///
    /// Why a custom comp instead of vanilla CompTargetEffect_PsychicShock:
    /// that one is a fixed 7500-tick hediff with no duration hook, and the
    /// artifact has no per-instance settings. We reuse the same vanilla hediff
    /// and only override how long it lasts, so the "shock down" feel, the
    /// battle-log entry and the immunity rules stay vanilla.
    /// </summary>
    public class CompTargetEffect_PsychicShockDown : CompTargetEffect
    {
        public override void DoEffectOn(Pawn user, Thing target)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            // Master switch off: artifact still exists but does nothing.
            if (s == null || !s.enableShockPulser) return;

            if (target is not Pawn pawn || pawn.Dead) return;

            Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn);
            pawn.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.ConsciousnessSource)
                .TryRandomElement(out BodyPartRecord part);

            // Same battle-log bookkeeping as vanilla CompTargetEffect_PsychicShock.
            BattleLogEntry_ItemUsed logEntry =
                new BattleLogEntry_ItemUsed(user, target, parent.def, RulePackDefOf.Event_ItemUsed);
            hediff.combatLogEntry = new WeakReference<LogEntry>(logEntry);
            hediff.combatLogText = logEntry.ToGameStringFromPOV(null);

            pawn.health.AddHediff(hediff, part);
            Find.BattleLog.Add(logEntry);

            // Duration override. Vanilla is 7500 ticks (3h); the hediff reads its
            // disappearsAfterTicks at make time, so SetDuration right after adding
            // is enough — no need to patch the hediff def.
            int ticks = Mathf.RoundToInt(Mathf.Clamp(s.shockPulserDownedHours, 0.1f, 240f) * 2500f);
            hediff.TryGetComp<HediffComp_Disappears>()?.SetDuration(ticks);
        }

        public override bool CanApplyOn(Thing target)
        {
            // Mirrors vanilla CompTargetEffect_PsychicShock: never insta-kill
            // pawns flagged to die on downed, and skip shock-immune mutants.
            if (target is Pawn pawn)
            {
                if (pawn.kindDef.forceDeathOnDowned) return false;
                if (pawn.IsMutant && pawn.mutant.Def.psychicShockUntargetable) return false;
            }
            return true;
        }
    }
}
