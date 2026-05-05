using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Mineable), nameof(Mineable.DestroyMined))]
public static class Patch_Tunneler
{
    public static void Prefix(Pawn pawn)
    {
        if (pawn == null || pawn.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.tunnelerPoints++;
    }
}
