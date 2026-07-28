using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace DeadDrop
{
    /// <summary>
    /// Small "search party" — 2–5 hostiles, not a colony wipe.
    /// Fired after failed discovery rolls (or storyteller, rarely).
    /// </summary>
    public class IncidentWorker_SearchParty : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            DeadDropSettings settings = DeadDropMod.Settings;
            if (settings != null && (!settings.masterEnabled || !settings.SearchPartyAllowed))
            {
                return false;
            }

            Map map = (Map)parms.target;
            MapComponent_DeadDrop comp = MapComponent_DeadDrop.For(map);
            // Prefer maps that actually use dead drops.
            return comp != null && (comp.totalCompleted > 0 || comp.FindCairn() != null);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            DeadDropSettings settings = DeadDropMod.Settings;
            if (settings != null && (!settings.masterEnabled || !settings.SearchPartyAllowed))
            {
                return false;
            }

            Faction faction = parms.faction;
            if (faction == null || faction.IsPlayer)
            {
                faction = Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false)
                          ?? Find.FactionManager.AllFactionsVisible
                              .Where(f => !f.IsPlayer && !f.defeated && f.def.humanlikeFaction)
                              .InRandomOrder()
                              .FirstOrDefault();
            }
            if (faction == null)
            {
                return false;
            }

            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 spawnCell, map, CellFinder.EdgeRoadChance_Hostile))
            {
                return false;
            }

            float points = parms.points > 0f
                ? parms.points
                : Mathf.Clamp(StorytellerUtility.DefaultThreatPointsNow(map) * 0.32f, 200f, 800f);

            PawnGroupMakerParms groupParms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = faction,
                points = points
            };

            List<Pawn> pawns;
            try
            {
                pawns = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
            }
            catch
            {
                return false;
            }

            if (pawns.Count == 0)
            {
                return false;
            }

            while (pawns.Count > 5)
            {
                Pawn extra = pawns[pawns.Count - 1];
                pawns.RemoveAt(pawns.Count - 1);
                extra.Destroy(DestroyMode.Vanish);
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(spawnCell, map, 5);
                GenSpawn.Spawn(pawns[i], cell, map);
            }

            LordMaker.MakeNewLord(
                faction,
                new LordJob_AssaultColony(
                    faction,
                    canKidnap: false,
                    canTimeoutOrFlee: true,
                    sappers: false,
                    useAvoidGridSmart: true,
                    canSteal: false,
                    breachers: false),
                map,
                pawns);

            SendStandardLetter(
                "DD_Letter_SearchPartyTitle".Translate(),
                "DD_Letter_SearchPartyBody".Translate(faction.Name, pawns.Count),
                LetterDefOf.ThreatSmall,
                parms,
                new TargetInfo(spawnCell, map));

            return true;
        }
    }
}
