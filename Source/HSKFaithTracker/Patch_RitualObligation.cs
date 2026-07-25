using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RemoveObligation))]
public static class Patch_RitualObligation
{
    // Flag to prevent double-recording from both patches
    public static bool justRecorded;

    public static void Prefix(Precept_Ritual __instance, RitualObligation obligation, bool completed)
    {
        if (!ModsConfig.IdeologyActive)
            return;

        if (__instance == null || obligation == null)
            return;

        // Skip if ritual is no longer part of player's ideology (meme was forgotten)
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null || !ideo.PreceptsListForReading.Contains(__instance))
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        string name = __instance.LabelCap ?? "Unknown ritual";

        if (completed)
        {
            // Skip role change ceremonies
            if (Patch_RitualCompleted.IsRoleChange(__instance))
            {
                Log.Message($"[FaithTracker] OBLIGATION '{name}': skipped (role change)");
                justRecorded = true;
                return;
            }
            int weight = Patch_RitualCompleted.GetRitualWeight(__instance);
            string faithStatus;
            if (Patch_RitualOutcomeQuality.outcomeKnown && Patch_RitualOutcomeQuality.lastOutcomeNegative)
                faithStatus = "faith skipped (negative outcome)";
            else
                faithStatus = comp.TryRecordRitualFaith(__instance, weight) ? $"RECORDED weight={weight}" : "faith skipped (repeat this year)";
            Log.Message($"[FaithTracker] OBLIGATION '{name}': {faithStatus}");

            // Certainty shift
            bool hasDate = __instance.obligationTriggers?.Any(t => t is RitualObligationTrigger_Date) == true;
            bool hasCooldown = __instance.def?.useRepeatPenalty ?? false;
            float shift = (hasDate && !hasCooldown) ? 0.03f : 0.01f;
            Patch_RitualCompleted.ApplyCertaintyShift(shift);

            // Blindsight ritual bonus
            if (__instance.def?.requiredMemes != null && __instance.def.requiredMemes.Any(m => m.defName == "Blindsight"))
            {
                int colonists = System.Math.Max(1, PawnsFinder.AllMaps_FreeColonists.Count(p => !p.IsSlave));
                int sections = System.Math.Max(1, comp.MemeCount);
                int bonus = 15 * colonists * 4 / sections;
                comp.blindPoints += bonus;
                Log.Message($"[FaithTracker] BLINDSIGHT BONUS (obligation): +{bonus} blindPoints (colonists={colonists}, sections={sections})");

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

            justRecorded = true;
        }
        else
        {
            // Don't record MISSED if the ritual is currently being performed
            bool ritualActive = RitualUtility.GetActiveRitualsForPrecept(__instance).Any();
            if (ritualActive)
            {
                Log.Message($"[FaithTracker] OBLIGATION '{name}': skipped MISSED (ritual is active)");
                return;
            }
            Log.Message($"[FaithTracker] OBLIGATION '{name}': MISSED");
            comp.RecordRitual(name, RitualRecordType.Missed);
        }
    }
}
