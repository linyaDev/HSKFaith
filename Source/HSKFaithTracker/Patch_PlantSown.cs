using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment))]
public static class Patch_PlantSown
{
    public static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
    {
        if (def != RecordDefOf.PlantsSown)
            return;

        if (__instance.pawn?.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        comp.naturePlantsSown++;

        // Check if it's a tree — get the plant from the current job
        var pawn = __instance.pawn;
        var plant = pawn?.CurJob?.GetTarget(TargetIndex.A).Thing as Plant;
        if (plant?.def?.plant != null && plant.def.plant.IsTree)
            comp.treesSown++;
    }
}
