using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(TreeDestructionTracker), nameof(TreeDestructionTracker.Notify_TreeCut))]
public static class Patch_NaturePrimacyTreeCut
{
    public static void Postfix(Pawn by)
    {
        if (by?.Faction != Faction.OfPlayer) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        comp.natureTreesCut++;
    }
}

[HarmonyPatch(typeof(TreeDestructionTracker), nameof(TreeDestructionTracker.Notify_TreeDestroyed))]
public static class Patch_NaturePrimacyTreeDestroyed
{
    public static void Postfix(TreeDestructionTracker __instance, DamageInfo dInfo)
    {
        if (dInfo.Instigator == null || dInfo.Instigator.Faction != Faction.OfPlayer) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        comp.natureTreesCut++;
    }
}
