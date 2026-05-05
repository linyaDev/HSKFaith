using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

// Butchering a human: +5 points
[HarmonyPatch(typeof(Corpse), nameof(Corpse.ButcherProducts))]
public static class Patch_Cannibal
{
    public static void Postfix(Corpse __instance, Pawn butcher)
    {
        if (!__instance.InnerPawn.RaceProps.Humanlike)
            return;

        if (butcher == null || butcher.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.cannibalPoints += 5;
    }
}

// Eating a meal with human meat: +1 point
[HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
public static class Patch_CannibalEat
{
    public static void Postfix(Thing __instance, Pawn ingester)
    {
        if (ingester == null || ingester.Faction != Faction.OfPlayer || ingester.IsSlave)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Cannibal")) return;

        // Check if meal contains human meat
        var ingredients = __instance.TryGetComp<CompIngredients>();
        if (ingredients?.ingredients == null) return;

        if (ingredients.ingredients.Any(def => def.ingestible?.sourceDef?.race?.Humanlike == true))
            comp.cannibalPoints++;
    }
}
