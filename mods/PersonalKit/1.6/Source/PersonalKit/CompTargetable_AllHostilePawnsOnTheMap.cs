using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalKit
{
    /// <summary>
    /// CompTargetable variant for the shock pulser: hits spawned pawns that are
    /// hostile to the player OR have no faction at all (wild manhunters,
    /// unclaimed creatures). Deliberately spares:
    ///   - player faction (colonists, tamed animals, prisoners, slaves)
    ///   - neutral / allied pawns (trade caravans, visitors, guests)
    /// so a single use never wipes out your diplomacy by accident.
    ///
    /// Vanilla only offers ignorePlayerFactionPawns, which would still floor a
    /// visiting trade caravan — hence this subclass.
    /// </summary>
    public class CompTargetable_AllHostilePawnsOnTheMap : CompTargetable_AllPawnsOnTheMap
    {
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (target.Thing is not Pawn pawn) return false;
            if (pawn.Dead || pawn.Downed) return false;

            // Hostile to the player, or nobody at all.
            bool hostileOrWild = pawn.Faction == null || pawn.Faction.HostileTo(Faction.OfPlayer);
            if (!hostileOrWild) return false;

            return base.ValidateTarget(target, showMessages);
        }
    }
}
