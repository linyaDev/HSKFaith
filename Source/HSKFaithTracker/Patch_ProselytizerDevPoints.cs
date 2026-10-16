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
