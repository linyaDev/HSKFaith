using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// When Raider meme is active and 2+ colonists form a caravan,
/// block all ThreatBig incidents for 4 days.
/// </summary>
[HarmonyPatch(typeof(CaravanMaker), nameof(CaravanMaker.MakeCaravan))]
public static class Patch_RaiderCaravanFormed
{
    private const int ProtectionDays = 4;

    public static void Postfix(IEnumerable<Pawn> pawns, Faction faction)
    {
        if (faction != Faction.OfPlayer) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Raider")) return;

        int colonists = pawns.Count(p => p.IsColonist && !p.IsSlave);
        if (colonists < 2) return;

        comp.raiderProtectionUntilTick = Find.TickManager.TicksGame + (ProtectionDays * 60000);
        Log.Message($"[HSKFaith] Raider protection: {colonists} colonists left, {ProtectionDays} days protection");
    }
}

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
public static class Patch_RaiderThreatBlock
{
    public static void Postfix(IncidentWorker __instance, ref bool __result)
    {
        if (!__result) return;
        if (__instance.def.category != IncidentCategoryDefOf.ThreatBig) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        if (Find.TickManager.TicksGame < comp.raiderProtectionUntilTick)
            __result = false;
    }
}
