using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_AnimalKilled
{
    public static void Prefix(Pawn __instance, DamageInfo? dinfo)
    {
        if (!__instance.RaceProps.Animal)
            return;

        // Only count if killed by player faction
        Pawn killer = dinfo?.Instigator as Pawn;
        if (killer == null || killer.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.animalKills++;
    }
}
