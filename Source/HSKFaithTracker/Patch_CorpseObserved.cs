using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Corpse), nameof(Corpse.GiveObservedHistoryEvent))]
public static class Patch_CorpseObserved
{
    private static readonly HashSet<int> observedCorpses = new HashSet<int>();
    private const int MaxCorpseRecords = 5;

    public const float MaxDistFromHome = 30f;

    public static bool IsNearHomeArea(Thing thing)
    {
        if (thing?.Map == null) return false;
        var homeArea = thing.Map.areaManager.Home;
        if (homeArea == null) return false;

        var pos = thing.Position;
        int radius = (int)MaxDistFromHome;

        // Check cells in radius around the corpse for home area
        for (int dx = -radius; dx <= radius; dx += 3)
        {
            for (int dz = -radius; dz <= radius; dz += 3)
            {
                var cell = new IntVec3(pos.x + dx, 0, pos.z + dz);
                if (cell.InBounds(thing.Map) && homeArea[cell])
                    return true;
            }
        }
        return false;
    }

    public static void Postfix(Corpse __instance, Pawn observer, HistoryEventDef __result)
    {
        return; // disabled: corpse penalty temporarily removed

        if (__result == null)
            return;

        if (!ModsConfig.IdeologyActive)
            return;

        if (observer == null || !observer.IsColonist)
            return;

        // One record per corpse
        if (observedCorpses.Contains(__instance.thingIDNumber))
            return;

        // Too far from home area — ignore
        if (!IsNearHomeArea(__instance))
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        // No corpse penalty for cannibals, bloodfeeders, raiders and ritualists
        if (comp.HasMeme("Cannibal") || comp.HasMeme("Bloodfeeding") || comp.HasMeme("Raider") || comp.HasMeme("Ritualist"))
            return;

        // Max 5 corpse records total
        int currentCorpseRecords = comp.Records.Count(r => r.type == RitualRecordType.CorpsePenalty);
        if (currentCorpseRecords >= MaxCorpseRecords)
            return;

        observedCorpses.Add(__instance.thingIDNumber);
        comp.RecordRitual("FT_UnburiedCorpse".Translate(), RitualRecordType.CorpsePenalty, __instance.thingIDNumber, showMote: false);
    }
}
