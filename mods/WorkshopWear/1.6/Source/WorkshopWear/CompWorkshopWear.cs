using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkshopWear
{
    public class CompProperties_WorkshopWear : CompProperties
    {
        public float wearMultiplier = 1f;

        public CompProperties_WorkshopWear()
        {
            compClass = typeof(CompWorkshopWear);
        }
    }

    public class CompWorkshopWear : ThingComp
    {
        public float wear;
        public bool overworkActive;
        public int overworkTicksLeft;
        public bool jammed;

        /// <summary>Bills we auto-suspended on jam; restored on proper repair.</summary>
        private List<Bill> suspendedByJam;

        public CompProperties_WorkshopWear Props => (CompProperties_WorkshopWear)props;

        public WorkshopWearSettings S => WorkshopWearMod.Settings;

        public bool IsEnabled => S == null || S.modEnabled;

        public float MaxWear => S != null ? Mathf.Max(20f, S.wornThreshold) : 100f;

        public WearState State
        {
            get
            {
                if (jammed)
                {
                    return WearState.Jammed;
                }
                float max = MaxWear;
                float t = wear / max;
                if (t < 0.30f)
                {
                    return WearState.Good;
                }
                if (t < 0.70f)
                {
                    return WearState.Used;
                }
                return WearState.Worn;
            }
        }

        /// <summary>
        /// Good ~100%, Used modest dip, Worn ~78% default, Jammed crawls.
        /// Overwork multiplies on top while active.
        /// </summary>
        public float SpeedFactor
        {
            get
            {
                if (!IsEnabled)
                {
                    return 1f;
                }

                float factor;
                switch (State)
                {
                    case WearState.Jammed:
                        factor = 0.05f;
                        break;
                    case WearState.Good:
                        factor = 1f;
                        break;
                    case WearState.Used:
                    {
                        // Smooth 1.0 → ~0.92 across Used band
                        float max = MaxWear;
                        float t = Mathf.InverseLerp(max * 0.30f, max * 0.70f, wear);
                        factor = Mathf.Lerp(1f, 0.92f, t);
                        break;
                    }
                    default: // Worn
                    {
                        float max = MaxWear;
                        float floor = S != null ? S.wornSpeedFactor : 0.78f;
                        float t = Mathf.InverseLerp(max * 0.70f, max, wear);
                        factor = Mathf.Lerp(0.92f, floor, t);
                        break;
                    }
                }

                if (overworkActive && overworkTicksLeft > 0 && !jammed)
                {
                    float bonus = S != null ? S.overworkSpeedBonus : 0.25f;
                    factor *= 1f + bonus;
                }

                return factor;
            }
        }

        public bool NeedsAnyRepair => IsEnabled && (jammed || wear >= MaxWear * 0.25f);

        public bool NeedsSlapdash => IsEnabled && !jammed && wear >= MaxWear * 0.25f;

        public bool NeedsProper => IsEnabled && (jammed || wear >= MaxWear * 0.55f);

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref wear, "wear", 0f);
            Scribe_Values.Look(ref overworkActive, "overworkActive", false);
            Scribe_Values.Look(ref overworkTicksLeft, "overworkTicksLeft", 0);
            Scribe_Values.Look(ref jammed, "jammed", false);
            // suspendedByJam is rebuilt only while jammed; not scribed (bills re-suspend on load if needed)
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (respawningAfterLoad && jammed)
            {
                SuspendBillsForJam();
            }
        }

        public override void CompTickRare()
        {
            // Most worktables never rare-tick; MapComponent_WorkshopWear drives overwork.
            TickOverworkPulse(250);
        }

        /// <summary>Called by MapComponent every ~250 ticks while overwork is active.</summary>
        public void TickOverworkPulse(int ticks)
        {
            if (!IsEnabled || !overworkActive)
            {
                return;
            }

            overworkTicksLeft -= ticks;
            if (overworkTicksLeft <= 0)
            {
                EndOverwork();
            }
        }

        public void OnBillIterationCompleted()
        {
            if (!IsEnabled || jammed)
            {
                return;
            }

            float add = (S?.wearPerBill ?? 1f)
                        * (Props?.wearMultiplier ?? 1f)
                        * (S?.WearMult ?? 1f);

            if (overworkActive)
            {
                add *= 2f;
            }

            wear = Mathf.Min(MaxWear, wear + add);
        }

        public void BeginOverwork()
        {
            if (!IsEnabled || jammed)
            {
                return;
            }
            if (S != null && !S.enableOverwork)
            {
                return;
            }
            if (overworkActive && overworkTicksLeft > 0)
            {
                return;
            }

            overworkActive = true;
            overworkTicksLeft = GenDate.TicksPerDay;
            Messages.Message(
                "WW_Message_OverworkStarted".Translate(parent.LabelShort),
                parent,
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }

        private void EndOverwork()
        {
            overworkActive = false;
            overworkTicksLeft = 0;

            if (S != null && !S.JamsAllowed)
            {
                Messages.Message(
                    "WW_Message_OverworkEnded".Translate(parent.LabelShort),
                    parent,
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
                return;
            }

            float baseChance = S?.overworkJamChance ?? 0.12f;
            // Wear raises jam risk: pristine ~ base*0.5, fully worn ~ base*1.75
            float wearT = Mathf.Clamp01(wear / MaxWear);
            float chance = baseChance * Mathf.Lerp(0.5f, 1.75f, wearT);

            if (Rand.Chance(chance))
            {
                ApplyJam();
            }
            else
            {
                Messages.Message(
                    "WW_Message_OverworkEnded".Translate(parent.LabelShort),
                    parent,
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
            }
        }

        public void ApplyJam()
        {
            if (jammed)
            {
                return;
            }

            jammed = true;
            overworkActive = false;
            overworkTicksLeft = 0;
            SuspendBillsForJam();

            Messages.Message(
                "WW_Message_Jammed".Translate(parent.LabelShort),
                parent,
                MessageTypeDefOf.NegativeEvent,
                historical: true);

            // No explosions by default. Dev flag only: tiny fire spark, not a boom.
            if (S != null && S.devJamExplosions && parent.Map != null)
            {
                FireUtility.TryStartFireIn(parent.Position, parent.Map, 0.2f, null, null);
            }
        }

        private void SuspendBillsForJam()
        {
            if (!(parent is IBillGiver billGiver) || billGiver.BillStack == null)
            {
                return;
            }

            suspendedByJam = suspendedByJam ?? new List<Bill>();
            suspendedByJam.Clear();
            foreach (Bill bill in billGiver.BillStack)
            {
                if (bill != null && !bill.suspended)
                {
                    bill.suspended = true;
                    suspendedByJam.Add(bill);
                }
            }
        }

        private void ResumeBillsAfterJam()
        {
            if (suspendedByJam == null)
            {
                return;
            }

            for (int i = 0; i < suspendedByJam.Count; i++)
            {
                Bill bill = suspendedByJam[i];
                if (bill != null)
                {
                    bill.suspended = false;
                }
            }
            suspendedByJam.Clear();
        }

        /// <summary>
        /// Slapdash: restore 40–60% of current wear toward good; small fail chance to worsen.
        /// Does not clear jam.
        /// </summary>
        public void ApplySlapdashRepair(Pawn worker)
        {
            if (!IsEnabled)
            {
                return;
            }

            int skill = worker?.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 5;
            // Fail chance: low skill ~18%, high skill ~4%
            float failChance = Mathf.Lerp(0.18f, 0.04f, skill / 20f);
            if (Rand.Chance(failChance))
            {
                float worsen = Rand.Range(4f, 12f);
                wear = Mathf.Min(MaxWear, wear + worsen);
                Messages.Message(
                    "WW_Message_SlapdashFailed".Translate(parent.LabelShort, worker?.LabelShort ?? "someone"),
                    parent,
                    MessageTypeDefOf.NegativeEvent,
                    historical: false);
                return;
            }

            float restoreFrac = Rand.Range(0.40f, 0.60f);
            // Skill nudges restore toward high end
            restoreFrac = Mathf.Clamp(restoreFrac + (skill - 8) * 0.01f, 0.35f, 0.65f);
            wear = Mathf.Max(0f, wear * (1f - restoreFrac));

            Messages.Message(
                "WW_Message_SlapdashDone".Translate(parent.LabelShort),
                parent,
                MessageTypeDefOf.TaskCompletion,
                historical: false);
        }

        /// <summary>
        /// Proper: full restore + clear jam. Slow, components.
        /// </summary>
        public void ApplyProperRepair(Pawn worker)
        {
            if (!IsEnabled)
            {
                return;
            }

            wear = 0f;
            if (jammed)
            {
                jammed = false;
                ResumeBillsAfterJam();
            }
            overworkActive = false;
            overworkTicksLeft = 0;

            Messages.Message(
                "WW_Message_ProperDone".Translate(parent.LabelShort),
                parent,
                MessageTypeDefOf.TaskCompletion,
                historical: false);

            if (worker?.skills != null)
            {
                worker.skills.Learn(SkillDefOf.Crafting, 120f);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!IsEnabled)
            {
                return null;
            }

            var sb = new StringBuilder();
            string stateKey;
            switch (State)
            {
                case WearState.Good:
                    stateKey = "WW_State_Good";
                    break;
                case WearState.Used:
                    stateKey = "WW_State_Used";
                    break;
                case WearState.Worn:
                    stateKey = "WW_State_Worn";
                    break;
                default:
                    stateKey = "WW_State_Jammed";
                    break;
            }

            sb.Append(stateKey.Translate());
            // Secondary wear number — not the headline
            sb.Append(" (");
            sb.Append(wear.ToString("F0"));
            sb.Append("/");
            sb.Append(MaxWear.ToString("F0"));
            sb.Append(")");

            if (!Mathf.Approximately(SpeedFactor, 1f))
            {
                sb.AppendLine();
                sb.Append("WW_Inspect_Speed".Translate(SpeedFactor.ToStringPercent()));
            }

            if (overworkActive && overworkTicksLeft > 0)
            {
                sb.AppendLine();
                int hours = Mathf.Max(1, overworkTicksLeft / 2500);
                sb.Append("WW_Inspect_Overwork".Translate(hours));
            }

            if (jammed)
            {
                sb.AppendLine();
                sb.Append("WW_Inspect_JammedHint".Translate());
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!IsEnabled)
            {
                yield break;
            }

            if (S == null || S.enableOverwork)
            {
                bool canOverwork = !jammed && !(overworkActive && overworkTicksLeft > 0);
                yield return new Command_Action
                {
                    defaultLabel = "WW_Gizmo_Overwork".Translate(),
                    defaultDesc = "WW_Gizmo_OverworkDesc".Translate(
                        ((S?.overworkSpeedBonus ?? 0.25f)).ToStringPercent(),
                        ((S?.overworkJamChance ?? 0.12f)).ToStringPercent()),
                    action = BeginOverwork,
                    icon = TexCommand.DesirePower,
                    Disabled = !canOverwork,
                    disabledReason = jammed
                        ? "WW_Gizmo_DisabledJammed".Translate()
                        : "WW_Gizmo_DisabledActive".Translate()
                };
            }

            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Dev: +20 wear",
                    action = () =>
                    {
                        wear = Mathf.Min(MaxWear, wear + 20f);
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "Dev: jam",
                    action = ApplyJam
                };
                yield return new Command_Action
                {
                    defaultLabel = "Dev: full repair",
                    action = () => ApplyProperRepair(null)
                };
            }
        }
    }
}
