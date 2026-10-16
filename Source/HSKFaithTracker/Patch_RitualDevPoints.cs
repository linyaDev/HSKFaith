using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

// Limit ritual dev points to once per year per ritual type
[HarmonyPatch(typeof(IdeoDevelopmentTracker), nameof(IdeoDevelopmentTracker.TryGainDevelopmentPointsForRitualOutcome))]
public static class Patch_RitualDevPointsOncePerYear
{
    public static bool Prefix(Precept_Ritual ritual, ref int developmentPoints, ref bool __result)
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return true;

        string ritualId = ritual.sourcePattern?.defName ?? ritual.def.defName ?? ritual.Label;
        if (comp.ritualDevPointsThisYear.Contains(ritualId))
        {
            developmentPoints = 0;
            __result = false;
            Messages.Message("FT_DevPointsAlreadyEarned".Translate(ritual.LabelCap), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        // Allow, but record after
        return true;
    }

    public static void Postfix(Precept_Ritual ritual, int developmentPoints, bool __result)
    {
        if (!__result || developmentPoints <= 0) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        string ritualId = ritual.sourcePattern?.defName ?? ritual.def.defName ?? ritual.Label;
        if (!comp.ritualDevPointsThisYear.Contains(ritualId))
            comp.ritualDevPointsThisYear.Add(ritualId);
    }
}
