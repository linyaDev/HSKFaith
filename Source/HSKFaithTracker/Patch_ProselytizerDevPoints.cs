using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

// Block Ideo.Notify_MemberGainedByConversion dev points
[HarmonyPatch(typeof(Ideo), nameof(Ideo.Notify_MemberGainedByConversion))]
public static class Patch_ProselytizerDevPoints
{
    public static bool Prefix(Ideo __instance)
    {
        if (__instance.memes != null && __instance.memes.Any(m => m.defName == "Proselytizer"))
        {
            Messages.Message("FT_ConversionNoDevPoints".Translate(), MessageTypeDefOf.NeutralEvent, false);
            return false;
        }
        return true;
    }
}

// Block PreceptComp_DevelopmentPoints dev points from ConvertedNewMember event
[HarmonyPatch(typeof(PreceptComp_DevelopmentPoints), nameof(PreceptComp_DevelopmentPoints.Notify_HistoryEvent))]
public static class Patch_ProselytizerPreceptDevPoints
{
    public static bool Prefix(HistoryEvent ev, Precept precept)
    {
        if (ev.def != HistoryEventDefOf.ConvertedNewMember)
            return true;

        if (precept?.ideo?.memes != null && precept.ideo.memes.Any(m => m.defName == "Proselytizer"))
            return false;
        return true;
    }
}

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
        {
            comp.ritualDevPointsThisYear.Add(ritualId);
            Log.Message($"[FaithTracker] DEV POINTS: added '{ritualId}' to ritualDevPointsThisYear (devPoints={developmentPoints})");
        }
    }
}
