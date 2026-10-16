using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(CompDrug), nameof(CompDrug.PostIngested))]
public static class Patch_HighLife
{
    public static void Postfix(Pawn ingester)
    {
        if (ingester == null || ingester.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.highLifePoints++;
    }
}
