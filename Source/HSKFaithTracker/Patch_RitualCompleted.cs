using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.ApplyOutcome))]
public static class Patch_RitualCompleted
{
    public static void Prefix(LordJob_Ritual __instance, out int __state)
    {
        __state = __instance.Ritual?.lastFinishedTick ?? -1;
        Patch_RitualObligation.justRecorded = false;
        Patch_RitualOutcomeQuality.Reset();
    }

    public static void Postfix(LordJob_Ritual __instance, int __state)
    {
        var ritual = __instance.Ritual;
        string ritualName = ritual?.LabelCap ?? "null";

        // Already recorded by RemoveObligation patch
        if (Patch_RitualObligation.justRecorded)
        {
            Log.Message($"[FaithTracker] RITUAL '{ritualName}': skipped (already recorded by obligation patch)");
            return;
        }

        if (ritual == null)
        {
            Log.Message("[FaithTracker] RITUAL: skipped (ritual is null)");
            return;
        }

        // Skip role change ceremonies — they don't affect faith
        if (IsRoleChange(ritual))
        {
            Log.Message($"[FaithTracker] RITUAL '{ritualName}': skipped (role change)");
            return;
        }

        Log.Message($"[FaithTracker] RITUAL '{ritualName}': lastFinishedTick={ritual.lastFinishedTick}, prevTick={__state}, isAnytime={ritual.isAnytime}, useRepeatPenalty={ritual.def.useRepeatPenalty}, requiredMemes={ritual.def.requiredMemes?.Select(m => m.defName).ToStringSafeEnumerable() ?? "none"}");

        // lastFinishedTick changed = ritual completed successfully
        if (ritual.lastFinishedTick != __state && ritual.lastFinishedTick > 0)
        {
            var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
            if (comp == null) return;

            int weight = GetRitualWeight(ritual);
            string faithStatus;
            if (Patch_RitualOutcomeQuality.outcomeKnown && Patch_RitualOutcomeQuality.lastOutcomeNegative)
                faithStatus = "faith skipped (negative outcome)";
            else
                faithStatus = comp.TryRecordRitualFaith(ritual, weight) ? $"RECORDED weight={weight}" : "faith skipped (repeat this year)";
            Log.Message($"[FaithTracker] RITUAL '{ritualName}': {faithStatus}");


            // Certainty shift: holidays with date +3%/-3%, others +1%/-1%
            bool hasDate = ritual.obligationTriggers?.Any(t => t is RitualObligationTrigger_Date) == true;
            bool hasCooldown = ritual.def.useRepeatPenalty;
            float shift = (hasDate && !hasCooldown) ? 0.03f : 0.01f;
            ApplyCertaintyShift(shift);

            // Blindsight ritual bonus: enough blindPoints to fill 2 sections
            if (ritual.def.requiredMemes != null && ritual.def.requiredMemes.Any(m => m.defName == "Blindsight"))
            {
                int colonists = System.Math.Max(1, PawnsFinder.AllMaps_FreeColonists.Count(p => !p.IsSlave));
                int sections = System.Math.Max(1, comp.MemeCount);
                int bonus = 15 * colonists * 4 / sections;
                comp.blindPoints += bonus;
                Log.Message($"[FaithTracker] BLINDSIGHT BONUS: +{bonus} blindPoints (colonists={colonists}, sections={sections})");

                // Add psylink + adaptation hediffs to all blind colonists who don't have them
                var psyHediffDef = DefDatabase<HediffDef>.GetNamed("FT_BlindsightPsylink", false);
                foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                {
                    if (p.IsSlave) continue;
                    if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Sight))
                    {
                        if (psyHediffDef != null && p.health.hediffSet.GetFirstHediffOfDef(psyHediffDef) == null)
                        {
                            var h = HediffMaker.MakeHediff(psyHediffDef, p);
                            h.Severity = 0.01f;
                            p.health.AddHediff(h);
                            Log.Message($"[FaithTracker] BLINDSIGHT PSY HEDIFF added to {p.LabelShortCap}");
                        }
                        var trueSightDef = DefDatabase<HediffDef>.GetNamed("FT_BlindsightTrueSight", false);
                        if (trueSightDef != null && p.health.hediffSet.GetFirstHediffOfDef(trueSightDef) == null)
                        {
                            var h = HediffMaker.MakeHediff(trueSightDef, p);
                            h.Severity = 0.01f;
                            p.health.AddHediff(h);
                            Log.Message($"[FaithTracker] BLINDSIGHT TRUE SIGHT added to {p.LabelShortCap}");
                        }
                    }
                }
            }
        }
        else
        {
            Log.Message($"[FaithTracker] RITUAL '{ritualName}': NOT recorded (tick unchanged or <=0)");
        }
    }

    public static bool IsRoleChange(Precept_Ritual ritual)
    {
        return ritual.outcomeEffect?.def?.defName == "RoleChange";
    }

    public static int GetRitualWeight(Precept_Ritual ritual)
    {
        if (IsRoleChange(ritual)) return 0;
        bool hasDate = ritual.obligationTriggers?.Any(t => t is RitualObligationTrigger_Date) == true;
        bool hasCooldown = ritual.def.useRepeatPenalty;
        if (hasDate && !hasCooldown) return 3; // Holiday with date, no cooldown
        return 1; // Anytime, cooldown, or any other ritual
    }

    public static void ApplyCertaintyShift(float shift)
    {
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return;

        int gained = 0, lost = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave || p.ideo == null) continue;
            if (p.Ideo == ideo)
            {
                p.ideo.Certainty = Mathf.Min(p.ideo.Certainty + shift, 1f);
                gained++;
            }
            else
            {
                p.ideo.Certainty = Mathf.Max(p.ideo.Certainty - shift, 0f);
                lost++;
            }
        }

        string pct = (shift * 100f).ToString("F0") + "%";
        if (gained > 0)
            Messages.Message("FT_CertaintyGained".Translate(pct, gained), MessageTypeDefOf.PositiveEvent, false);
        if (lost > 0)
            Messages.Message("FT_CertaintyLost".Translate(pct, lost), MessageTypeDefOf.NegativeEvent, false);
    }
}
