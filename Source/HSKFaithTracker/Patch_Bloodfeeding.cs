using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.DoBite))]
public static class Patch_BloodfeedingBite
{
    public static void Postfix(Pawn biter)
    {
        if (biter == null || biter.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.bloodfeedCount += 3;
    }
}

[HarmonyPatch(typeof(IngestionOutcomeDoer_OffsetHemogen), "DoIngestionOutcomeSpecial")]
public static class Patch_BloodfeedingPack
{
    public static void Postfix(Pawn pawn, IngestionOutcomeDoer_OffsetHemogen __instance)
    {
        if (__instance.offset <= 0f)
            return;

        if (pawn == null || pawn.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.bloodfeedCount += 1;
    }
}
