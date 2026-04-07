using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RemoveObligation))]
public static class Patch_RitualObligation
{
    public static void Prefix(Precept_Ritual __instance, RitualObligation obligation, bool completed)
    {
        if (!ModsConfig.IdeologyActive)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        string name = __instance.LabelCap ?? "Unknown ritual";

        if (completed)
            comp.RecordRitual(name, RitualRecordType.Fulfilled);
        else
            comp.RecordRitual(name, RitualRecordType.Missed);
    }
}
