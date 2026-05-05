using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Pawn_TrainingTracker), nameof(Pawn_TrainingTracker.TrainingTrackerTickRare))]
public static class Patch_AnimalTrainingDecay
{
    public static bool Prefix(Pawn_TrainingTracker __instance)
    {
        var pawn = __instance.pawn;
        if (pawn == null || !pawn.RaceProps.Animal || pawn.Faction != Faction.OfPlayer)
            return true;

        // Check if any colonist has AnimalPersonhood
        if (AnimalPersonhoodUtility.AnyColonistHasAnimalPersonhood())
            return false; // Skip decay entirely

        return true;
    }
}
