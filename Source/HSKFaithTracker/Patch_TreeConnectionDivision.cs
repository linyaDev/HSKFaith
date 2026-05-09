using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// +30 treeConnectionPoints when Gauranlen tree completes division (GaumakerPod).
/// </summary>
[HarmonyPatch(typeof(CompGaumakerPod), "Complete")]
public static class Patch_TreeConnectionDivision
{
    public static void Postfix(CompGaumakerPod __instance)
    {
        if (__instance.parent?.Map == null) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("TreeConnection")) return;

        comp.treeConnectionPoints += 30;
    }
}
