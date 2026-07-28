using System.Text;
using RimWorld;
using Verse;

namespace ArtisanMark
{
    public class CompProperties_ArtisanMark : CompProperties
    {
        public CompProperties_ArtisanMark()
        {
            compClass = typeof(CompArtisanMark);
        }
    }

    /// <summary>
    /// Maker identity on a crafted thing. Stores pawn load ID + name snapshot.
    /// No map scans — all reads are local to this ThingComp.
    /// </summary>
    public class CompArtisanMark : ThingComp
    {
        public string makerLoadId;
        public string makerNameSnapshot;
        public int craftedTick = -1;

        public bool HasMaker => !string.IsNullOrEmpty(makerLoadId) || !string.IsNullOrEmpty(makerNameSnapshot);

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(makerNameSnapshot))
                {
                    return makerNameSnapshot;
                }
                return "AM_UnknownMaker".Translate();
            }
        }

        public void Stamp(Pawn maker)
        {
            if (maker == null)
            {
                return;
            }

            makerLoadId = maker.GetUniqueLoadID();
            // Plain name for save/inspect stability (CompArt uses NameFullColored for art only).
            makerNameSnapshot = maker.Name?.ToStringFull ?? maker.LabelShortCap;
            craftedTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        }

        public Pawn TryResolveMaker()
        {
            if (string.IsNullOrEmpty(makerLoadId))
            {
                return null;
            }

            // Fast path: map pawns
            if (parent?.Map != null)
            {
                var list = parent.Map.mapPawns?.AllPawns;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Pawn p = list[i];
                        if (p != null && p.GetUniqueLoadID() == makerLoadId)
                        {
                            return p;
                        }
                    }
                }
            }

            // World / other maps / corpses in world pawns
            if (Find.WorldPawns != null)
            {
                foreach (Pawn p in Find.WorldPawns.AllPawnsAliveOrDead)
                {
                    if (p != null && p.GetUniqueLoadID() == makerLoadId)
                    {
                        return p;
                    }
                }
            }

            // Colonist/pawn lists across maps
            if (Current.Game?.Maps != null)
            {
                foreach (Map map in Current.Game.Maps)
                {
                    if (map == null || map == parent?.Map)
                    {
                        continue;
                    }
                    var list = map.mapPawns?.AllPawns;
                    if (list == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        Pawn p = list[i];
                        if (p != null && p.GetUniqueLoadID() == makerLoadId)
                        {
                            return p;
                        }
                    }
                }
            }

            return null;
        }

        public bool MakerIsAlive()
        {
            Pawn maker = TryResolveMaker();
            return maker != null && !maker.Dead && !maker.Destroyed;
        }

        public bool MakerIsDead()
        {
            Pawn maker = TryResolveMaker();
            if (maker != null)
            {
                return maker.Dead;
            }
            // Unresolved load id with a name snapshot: treat as dead for legacy thoughts
            // only if we once had an id (pawn fully gone from world).
            return !string.IsNullOrEmpty(makerLoadId) && !string.IsNullOrEmpty(makerNameSnapshot);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref makerLoadId, "makerLoadId");
            Scribe_Values.Look(ref makerNameSnapshot, "makerNameSnapshot");
            Scribe_Values.Look(ref craftedTick, "craftedTick", -1);
        }

        public override string CompInspectStringExtra()
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !s.showInspectString || !HasMaker)
            {
                return null;
            }

            return "AM_Inspect_MadeBy".Translate(DisplayName);
        }

        public override string GetDescriptionPart()
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !HasMaker)
            {
                return null;
            }

            var sb = new StringBuilder();
            sb.AppendLine("AM_Inspect_MadeBy".Translate(DisplayName));
            if (craftedTick >= 0 && Find.TickManager != null)
            {
                // Optional flavor only — no pressure.
            }
            return sb.ToString().TrimEnd();
        }
    }
}
