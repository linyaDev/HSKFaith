using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(IdeoFoundation), nameof(IdeoFoundation.CanAdd))]
public static class Patch_RoleLimit
{
    public static void Postfix(ref AcceptanceReport __result, IdeoFoundation __instance, PreceptDef precept)
    {
        // Remove the 2 multi-role limit
        if (!__result.Accepted
            && precept.preceptClass == typeof(Precept_RoleMulti)
            && __instance.ideo.PreceptsListForReading.Count(p => p is Precept_RoleMulti && p.def.visible) >= 2)
        {
            __result = true;
        }
    }
}
