using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoConversionAttempt))]
public static class Patch_Proselytizer
{
    public static void Postfix(bool __result, Ideo initiatorIdeo)
    {
        if (!__result)
            return;

        // Only count conversions to player's ideology
        var playerIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (playerIdeo == null || initiatorIdeo != playerIdeo)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.proselytizePoints += 2;
    }
}
