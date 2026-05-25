using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

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

// Quarry mining also counts for Tunneler — patch on CellsMined record increment
[HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment))]
public static class Patch_TunnelerQuarry
{
    public static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
    {
        if (def != RecordDefOf.CellsMined) return;

        var pawn = __instance.pawn;
        if (pawn == null || pawn.Faction != Faction.OfPlayer) return;

        // Skip if already counted by Mineable.DestroyMined (regular mining)
        // Only count quarry mining — check if current job is QRY_MineQuarry
        var job = pawn.CurJob;
        if (job == null) return;
        if (job.def.defName != "QRY_MineQuarry") return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.tunnelerPoints++;
    }
}
