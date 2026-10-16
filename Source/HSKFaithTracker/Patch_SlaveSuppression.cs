using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Need_Suppression), nameof(Need_Suppression.NeedInterval))]
public static class Patch_SlaveSuppression
{
    public static bool Prefix(Need_Suppression __instance)
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasSupremacist)
            return true;

        if (comp.SlaveCount <= comp.MemeCount)
        {
            __instance.CurLevel = 1f;
            return false;
        }

        return true;
    }
}
