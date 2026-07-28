using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RumorMill
{
    public static class RumorUtility
    {
        public const int MaxActiveTags = 2;
        public const int MaxStrength = 3;

        // Thresholds before a tag is written (deeds, not RNG stickers)
        public const int ReliableTendThreshold = 4;
        public const int BloodyDeedThreshold = 2;
        public const int SqueamishWitnessThreshold = 5;
        public const int WarHeroKillThreshold = 3;
        public const int DeserterFleeThreshold = 2;

        public static bool Enabled =>
            RumorMillMod.Settings == null || RumorMillMod.Settings.modEnabled;

        public static GameComponent_RumorMill Comp => GameComponent_RumorMill.Get();

        public static bool IsHumanlikeTrackable(Pawn pawn)
        {
            return pawn != null
                   && !pawn.Dead
                   && pawn.RaceProps != null
                   && pawn.RaceProps.Humanlike
                   && pawn.story != null;
        }

        public static void NotifyTendSuccess(Pawn doctor, Pawn patient)
        {
            if (!Enabled || RumorMillMod.Settings?.hookReliable == false)
            {
                return;
            }
            if (!IsHumanlikeTrackable(doctor) || patient == null)
            {
                return;
            }
            // Only meaningful care: patient was downed, or colonist/prisoner/guest of colony
            bool meaningful = patient.Downed
                              || patient.IsColonist
                              || patient.IsPrisonerOfColony
                              || patient.IsSlaveOfColony
                              || (patient.Faction != null && patient.Faction.IsPlayer);
            if (!meaningful)
            {
                return;
            }

            PawnRumorState state = Comp?.GetOrCreate(doctor);
            if (state == null)
            {
                return;
            }
            state.tendSuccessCount++;
            // War hero and deserter are exclusive-ish; reliable can coexist
            if (state.tendSuccessCount >= ReliableTendThreshold)
            {
                Reinforce(doctor, RumorTagKind.Reliable, "RM_Msg_Reliable".Translate(doctor.LabelShort));
            }
        }

        public static void NotifyBloodyDeed(Pawn actor, string reasonKey = null)
        {
            if (!Enabled || RumorMillMod.Settings?.hookBloody == false)
            {
                return;
            }
            if (!IsHumanlikeTrackable(actor))
            {
                return;
            }
            PawnRumorState state = Comp?.GetOrCreate(actor);
            if (state == null)
            {
                return;
            }
            state.bloodyDeedCount++;
            if (state.bloodyDeedCount >= BloodyDeedThreshold)
            {
                // Bloody hands crowds out squeamish on same person
                RemoveTag(actor, RumorTagKind.Squeamish, silent: true);
                Reinforce(actor, RumorTagKind.BloodyHands,
                    reasonKey != null
                        ? reasonKey.Translate(actor.LabelShort)
                        : "RM_Msg_BloodyHands".Translate(actor.LabelShort));
            }
        }

        public static void NotifyDeathWitnessed(Pawn witness)
        {
            if (!Enabled || RumorMillMod.Settings?.hookSqueamish == false)
            {
                return;
            }
            if (!IsHumanlikeTrackable(witness))
            {
                return;
            }
            // Bloodlust / psychopath / cannibal types already handle gore in vanilla — light touch
            if (witness.story?.traits != null)
            {
                if (witness.story.traits.HasTrait(TraitDefOf.Psychopath)
                    || witness.story.traits.HasTrait(TraitDefOf.Bloodlust))
                {
                    return;
                }
            }
            PawnRumorState state = Comp?.GetOrCreate(witness);
            if (state == null || state.Has(RumorTagKind.BloodyHands))
            {
                return;
            }
            state.deathWitnessCount++;
            if (state.deathWitnessCount >= SqueamishWitnessThreshold)
            {
                Reinforce(witness, RumorTagKind.Squeamish, "RM_Msg_Squeamish".Translate(witness.LabelShort));
            }
        }

        public static void NotifyNerved(Pawn pawn)
        {
            if (!Enabled || RumorMillMod.Settings?.hookNerved == false)
            {
                return;
            }
            if (!IsHumanlikeTrackable(pawn))
            {
                return;
            }
            PawnRumorState state = Comp?.GetOrCreate(pawn);
            if (state == null)
            {
                return;
            }
            state.nervedSeen = true;
            Reinforce(pawn, RumorTagKind.Nerved, "RM_Msg_Nerved".Translate(pawn.LabelShort));
        }

        public static void NotifyRaidKill(Pawn killer, Pawn victim)
        {
            if (!Enabled || RumorMillMod.Settings?.hookWar == false)
            {
                return;
            }
            if (!IsHumanlikeTrackable(killer) || victim == null || !killer.IsColonist)
            {
                return;
            }
            if (!IsHostileKillContext(killer, victim))
            {
                return;
            }
            PawnRumorState state = Comp?.GetOrCreate(killer);
            if (state == null)
            {
                return;
            }
            state.raidKillCount++;
            // Clear deserter if they fought
            if (state.Has(RumorTagKind.Deserter) && state.raidKillCount >= 2)
            {
                RemoveTag(killer, RumorTagKind.Deserter, silent: false);
            }
            if (state.raidKillCount >= WarHeroKillThreshold)
            {
                Reinforce(killer, RumorTagKind.WarHero, "RM_Msg_WarHero".Translate(killer.LabelShort));
            }
        }

        public static void NotifyFlee(Pawn pawn)
        {
            if (!Enabled || RumorMillMod.Settings?.hookWar == false)
            {
                return;
            }
            if (!IsHumanlikeTrackable(pawn) || !pawn.IsColonist)
            {
                return;
            }
            if (!IsUnderHostileThreat(pawn.Map))
            {
                return;
            }
            PawnRumorState state = Comp?.GetOrCreate(pawn);
            if (state == null)
            {
                return;
            }
            state.fleeCount++;
            if (state.fleeCount >= DeserterFleeThreshold)
            {
                // Deserter replaces WarHero
                RemoveTag(pawn, RumorTagKind.WarHero, silent: true);
                Reinforce(pawn, RumorTagKind.Deserter, "RM_Msg_Deserter".Translate(pawn.LabelShort));
            }
        }

        public static bool IsHostileKillContext(Pawn killer, Pawn victim)
        {
            if (victim.Faction != null && victim.Faction.HostileTo(Faction.OfPlayer))
            {
                return true;
            }
            if (victim.HostileTo(killer))
            {
                return true;
            }
            return IsUnderHostileThreat(killer.Map);
        }

        public static bool IsUnderHostileThreat(Map map)
        {
            if (map?.mapPawns == null)
            {
                return false;
            }
            var hostiles = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < hostiles.Count; i++)
            {
                Pawn p = hostiles[i];
                if (p != null && !p.Dead && p.HostileTo(Faction.OfPlayer) && p.RaceProps.Humanlike)
                {
                    return true;
                }
            }
            return false;
        }

        public static void Reinforce(Pawn pawn, RumorTagKind kind, string message = null, int amount = 1)
        {
            GameComponent_RumorMill comp = Comp;
            if (comp == null || !IsHumanlikeTrackable(pawn))
            {
                return;
            }
            PawnRumorState state = comp.GetOrCreate(pawn);
            RumorTagEntry entry = state.Get(kind);
            int now = Find.TickManager.TicksGame;
            bool newlyGained = false;
            if (entry == null)
            {
                EnsureSlot(state, kind);
                entry = state.Get(kind);
                if (entry == null)
                {
                    entry = new RumorTagEntry
                    {
                        kind = kind,
                        strength = 1,
                        lastReinforcedTick = now,
                        deedCount = amount
                    };
                    state.tags.Add(entry);
                    newlyGained = true;
                    TrimToMax(state, preferKeep: kind);
                }
            }
            else
            {
                entry.strength = Mathf.Min(MaxStrength, entry.strength + (amount > 0 && entry.strength < MaxStrength ? 1 : 0));
                entry.deedCount += amount;
                entry.lastReinforcedTick = now;
            }

            if (newlyGained && !message.NullOrEmpty())
            {
                Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent, historical: true);
            }
        }

        public static void RemoveTag(Pawn pawn, RumorTagKind kind, bool silent)
        {
            PawnRumorState state = Comp?.TryGet(pawn);
            if (state == null)
            {
                return;
            }
            for (int i = state.tags.Count - 1; i >= 0; i--)
            {
                if (state.tags[i].kind == kind)
                {
                    state.tags.RemoveAt(i);
                    if (!silent)
                    {
                        Messages.Message("RM_Msg_TagFaded".Translate(pawn.LabelShort, ("RM_Tag_" + kind).Translate()),
                            pawn, MessageTypeDefOf.SilentInput, historical: false);
                    }
                    return;
                }
            }
        }

        public static void DowngradeOrRemove(PawnRumorState state, RumorTagEntry entry)
        {
            if (entry.strength > 1)
            {
                entry.strength--;
                entry.lastReinforcedTick = Find.TickManager.TicksGame; // grace after downgrade
            }
            else
            {
                state.tags.Remove(entry);
            }
        }

        private static void EnsureSlot(PawnRumorState state, RumorTagKind incoming)
        {
            if (state.tags.Count < MaxActiveTags)
            {
                return;
            }
            if (state.Has(incoming))
            {
                return;
            }
            // Drop oldest least-strength tag; prefer dropping opposite polarity of incoming
            int dropIndex = 0;
            int bestScore = int.MinValue;
            for (int i = 0; i < state.tags.Count; i++)
            {
                RumorTagEntry t = state.tags[i];
                int score = (MaxStrength - t.strength) * 1000 - t.lastReinforcedTick;
                // Prefer dropping Deserter when gaining WarHero and vice versa
                if ((incoming == RumorTagKind.WarHero && t.kind == RumorTagKind.Deserter)
                    || (incoming == RumorTagKind.Deserter && t.kind == RumorTagKind.WarHero)
                    || (incoming == RumorTagKind.BloodyHands && t.kind == RumorTagKind.Squeamish)
                    || (incoming == RumorTagKind.Reliable && !t.IsPositive))
                {
                    score += 5000;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    dropIndex = i;
                }
            }
            state.tags.RemoveAt(dropIndex);
        }

        private static void TrimToMax(PawnRumorState state, RumorTagKind preferKeep)
        {
            while (state.tags.Count > MaxActiveTags)
            {
                int drop = -1;
                int best = int.MinValue;
                for (int i = 0; i < state.tags.Count; i++)
                {
                    if (state.tags[i].kind == preferKeep)
                    {
                        continue;
                    }
                    int score = (MaxStrength - state.tags[i].strength) * 1000 - state.tags[i].lastReinforcedTick;
                    if (score > best)
                    {
                        best = score;
                        drop = i;
                    }
                }
                if (drop < 0)
                {
                    state.tags.RemoveAt(0);
                }
                else
                {
                    state.tags.RemoveAt(drop);
                }
            }
        }

        /// <summary>Player commend: reinforce positive / soften one negative step.</summary>
        public static void ApplyCommend(Pawn speaker, Pawn target)
        {
            if (!Enabled || !IsHumanlikeTrackable(target))
            {
                return;
            }
            PawnRumorState state = Comp?.GetOrCreate(target);
            if (state == null)
            {
                return;
            }

            // Soften worst negative first
            RumorTagKind[] negatives = { RumorTagKind.Deserter, RumorTagKind.BloodyHands, RumorTagKind.Squeamish, RumorTagKind.Nerved };
            for (int i = 0; i < negatives.Length; i++)
            {
                RumorTagEntry neg = state.Get(negatives[i]);
                if (neg != null)
                {
                    string label = neg.LabelCap;
                    if (neg.strength > 1)
                    {
                        neg.strength--;
                        neg.lastReinforcedTick = Find.TickManager.TicksGame;
                        Messages.Message("RM_Msg_CommendSoftened".Translate(speaker.LabelShort, target.LabelShort, label),
                            target, MessageTypeDefOf.PositiveEvent, historical: true);
                    }
                    else
                    {
                        RemoveTag(target, neg.kind, silent: true);
                        Messages.Message("RM_Msg_CommendCleared".Translate(speaker.LabelShort, target.LabelShort, label),
                            target, MessageTypeDefOf.PositiveEvent, historical: true);
                    }
                    // Small social bump
                    GainCommendThoughts(speaker, target);
                    return;
                }
            }

            // No negatives: reinforce or grant Reliable
            Reinforce(target, RumorTagKind.Reliable, null, 1);
            Messages.Message("RM_Msg_CommendReliable".Translate(speaker.LabelShort, target.LabelShort),
                target, MessageTypeDefOf.PositiveEvent, historical: true);
            GainCommendThoughts(speaker, target);
        }

        private static void GainCommendThoughts(Pawn speaker, Pawn target)
        {
            if (speaker == null || target == null || speaker == target)
            {
                return;
            }
            speaker.needs?.mood?.thoughts?.memories?.TryGainMemory(RumorMillDefOf.RM_CommendedOther, target);
            target.needs?.mood?.thoughts?.memories?.TryGainMemory(RumorMillDefOf.RM_WasCommended, speaker);
            if (RumorMillDefOf.RM_WasCommendedSocial != null)
            {
                target.needs?.mood?.thoughts?.memories?.TryGainMemory(RumorMillDefOf.RM_WasCommendedSocial, speaker);
            }
        }

        /// <summary>Natural social chat: slight reinforce of known tags (no full mesh scan).</summary>
        public static void ApplyChatSpread(Pawn a, Pawn b)
        {
            if (!Enabled || RumorMillMod.Settings?.enableChatSpread == false)
            {
                return;
            }
            if (!IsHumanlikeTrackable(a) || !IsHumanlikeTrackable(b))
            {
                return;
            }
            // Only colonists gossip meaningfully for colony reputation
            if (!a.IsColonist || !b.IsColonist)
            {
                return;
            }
            // Low chance — event is the interaction itself, not a sweep
            if (!Rand.Chance(0.12f))
            {
                return;
            }
            // Pick one tagged colonist they both know (themselves or each other only — bounded)
            TryGossipAbout(a, b, a);
            if (Rand.Chance(0.5f))
            {
                TryGossipAbout(a, b, b);
            }
        }

        private static void TryGossipAbout(Pawn speaker, Pawn listener, Pawn subject)
        {
            PawnRumorState state = Comp?.TryGet(subject);
            if (state == null || state.tags.Count == 0)
            {
                return;
            }
            RumorTagEntry tag = state.tags.RandomElement();
            // Gossip reinforces the rumor (delays decay) — player can commend to reshape
            tag.lastReinforcedTick = Find.TickManager.TicksGame;
        }

        public static string InspectStringFor(Pawn pawn)
        {
            PawnRumorState state = Comp?.TryGet(pawn);
            if (state == null || state.tags.Count == 0)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("RM_InspectHeader".Translate());
            for (int i = 0; i < state.tags.Count; i++)
            {
                RumorTagEntry t = state.tags[i];
                sb.AppendLine();
                sb.Append("  • ");
                sb.Append(t.LabelCap);
                if (t.strength > 1)
                {
                    sb.Append(" ");
                    sb.Append("RM_Strength".Translate(t.strength));
                }
            }
            return sb.ToString().TrimEnd();
        }

        public static float NegotiationOffset(Pawn pawn)
        {
            if (!Enabled || pawn == null)
            {
                return 0f;
            }
            PawnRumorState state = Comp?.TryGet(pawn);
            if (state == null || state.tags.Count == 0)
            {
                return 0f;
            }
            float recruit = RumorMillMod.Settings?.RecruitFactor ?? 0.08f;
            float sum = 0f;
            for (int i = 0; i < state.tags.Count; i++)
            {
                RumorTagEntry t = state.tags[i];
                float s = t.strength / (float)MaxStrength;
                switch (t.kind)
                {
                    case RumorTagKind.Reliable:
                        sum += recruit * s;
                        break;
                    case RumorTagKind.WarHero:
                        sum += recruit * 0.75f * s;
                        break;
                    case RumorTagKind.BloodyHands:
                        sum -= recruit * s;
                        break;
                    case RumorTagKind.Deserter:
                        sum -= recruit * 0.85f * s;
                        break;
                    case RumorTagKind.Nerved:
                        sum -= recruit * 0.5f * s;
                        break;
                    case RumorTagKind.Squeamish:
                        sum -= recruit * 0.25f * s;
                        break;
                }
            }
            return Mathf.Clamp(sum, -0.15f, 0.15f);
        }

        public static float TradePriceOffset(Pawn negotiator)
        {
            if (!Enabled)
            {
                return 0f;
            }
            // Use negotiator if any; else mild colony face from free colonists is NOT scanned each trade —
            // only the trade negotiator pawn matters (bounded).
            Pawn pawn = negotiator;
            if (pawn == null)
            {
                return 0f;
            }
            PawnRumorState state = Comp?.TryGet(pawn);
            if (state == null || state.tags.Count == 0)
            {
                return 0f;
            }
            float trade = RumorMillMod.Settings?.TradeFactor ?? 0.04f;
            float sum = 0f;
            for (int i = 0; i < state.tags.Count; i++)
            {
                RumorTagEntry t = state.tags[i];
                float s = t.strength / (float)MaxStrength;
                switch (t.kind)
                {
                    case RumorTagKind.Reliable:
                        sum += trade * s;
                        break;
                    case RumorTagKind.WarHero:
                        sum += trade * 0.5f * s;
                        break;
                    case RumorTagKind.BloodyHands:
                        sum -= trade * s;
                        break;
                    case RumorTagKind.Deserter:
                        sum -= trade * 0.6f * s;
                        break;
                    case RumorTagKind.Nerved:
                        sum -= trade * 0.4f * s;
                        break;
                }
            }
            return Mathf.Clamp(sum, -0.08f, 0.08f);
        }

        public static int SocialOpinionOffset(Pawn other, Pawn pawn)
        {
            if (!Enabled || other == null || pawn == null || other == pawn)
            {
                return 0;
            }
            PawnRumorState state = Comp?.TryGet(pawn);
            if (state == null || state.tags.Count == 0)
            {
                return 0;
            }
            int bas = RumorMillMod.Settings?.SocialOpinion ?? 10;
            int sum = 0;
            for (int i = 0; i < state.tags.Count; i++)
            {
                RumorTagEntry t = state.tags[i];
                float s = t.strength / (float)MaxStrength;
                int v = Mathf.RoundToInt(bas * s);
                switch (t.kind)
                {
                    case RumorTagKind.Reliable:
                        sum += v;
                        break;
                    case RumorTagKind.WarHero:
                        sum += Mathf.RoundToInt(v * 0.8f);
                        break;
                    case RumorTagKind.BloodyHands:
                        // Bloodlust likes it
                        if (other.story?.traits != null && other.story.traits.HasTrait(TraitDefOf.Bloodlust))
                        {
                            sum += Mathf.RoundToInt(v * 0.5f);
                        }
                        else
                        {
                            sum -= v;
                        }
                        break;
                    case RumorTagKind.Deserter:
                        sum -= Mathf.RoundToInt(v * 0.8f);
                        break;
                    case RumorTagKind.Squeamish:
                        sum -= Mathf.RoundToInt(v * 0.3f);
                        break;
                    case RumorTagKind.Nerved:
                        sum -= Mathf.RoundToInt(v * 0.4f);
                        break;
                }
            }
            return Mathf.Clamp(sum, -15, 15);
        }

        public static bool IsOrganRemovalRecipe(RecipeDef recipe)
        {
            if (recipe?.workerClass == null)
            {
                return false;
            }
            // Core "remove part" / harvest path — not execute-by-cut (handled separately)
            if (typeof(Recipe_ExecuteByCut).IsAssignableFrom(recipe.workerClass))
            {
                return false;
            }
            if (typeof(Recipe_RemoveBodyPart).IsAssignableFrom(recipe.workerClass))
            {
                return true;
            }
            string n = recipe.defName ?? string.Empty;
            return n.IndexOf("Harvest", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("ExtractOrgan", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
