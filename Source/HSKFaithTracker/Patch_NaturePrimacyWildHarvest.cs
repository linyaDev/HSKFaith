using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Plant), nameof(Plant.YieldNow))]
public static class Patch_NaturePrimacyWildHarvest
{
    public static void Postfix(Plant __instance, ref int __result)
    {
    }
}
