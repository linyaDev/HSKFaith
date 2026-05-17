using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Plant), nameof(Plant.YieldNow))]
public static class Patch_NaturePrimacyWildHarvest
{
    public static void Postfix(Plant __instance, ref int __result)
    {
        if (__result <= 0) return;

        // Only for berry bushes
        if (__instance.def.defName != "Plant_Berry") return;

        // Only if wild (not in a growing zone)
        var zone = __instance.Map?.zoneManager?.ZoneAt(__instance.Position);
        if (zone is Zone_Growing) return;

        // Only if NaturePrimacy meme is active
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("NaturePrimacy")) return;

        __result *= 2;
    }
}
