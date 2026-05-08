using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch]
public static class Patch_LegendaryCraft
{
    static MethodBase TargetMethod()
    {
        // Find the overload that takes Pawn as first parameter
        return typeof(QualityUtility).GetMethods()
            .First(m => m.Name == "GenerateQualityCreatedByPawn"
                        && m.GetParameters().Length >= 2
                        && m.GetParameters()[0].ParameterType == typeof(Pawn));
    }

    public static void Postfix(QualityCategory __result, Pawn pawn)
    {
        if (__result < QualityCategory.Legendary)
            return;

        if (pawn?.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        comp.collectivistPoints++;
    }
}
