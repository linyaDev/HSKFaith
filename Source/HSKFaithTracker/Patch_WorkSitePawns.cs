using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace HSKFaithTracker;

/// <summary>
/// For FaithArmory/FaithStash sites: generate one combat group with full points
/// instead of vanilla 50/50 split between workers and fighters.
/// </summary>
[HarmonyPatch(typeof(GenStep_WorkSitePawns), nameof(GenStep_WorkSitePawns.Generate))]
public static class Patch_WorkSitePawns
{
    public static bool Prefix(Map map, GenStepParams parms)
    {
        if (parms.sitePart?.def?.workerClass != typeof(SitePartWorker_FaithArmory))
            return true; // not our site, run vanilla

        IntVec3 baseCenter;
        if (!MapGenerator.TryGetVar<CellRect>("RectOfInterest", out var rect))
        {
            baseCenter = rect.CenterCell;
        }
        else
        {
            baseCenter = map.Center;
        }

        Faction faction = parms.sitePart.site.Faction;
#if V16
        // 1.6: delayBeforeAssault is a ctor param; 251999 (~4 days) matches the value
        // hardcoded in 1.5 — defenders effectively never assault on their own.
        // (1.6 vanilla work sites shortened it to 25000, we keep 1.5 behavior.)
        Lord lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, baseCenter, 251999), map);
#else
        Lord lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, baseCenter), map);
#endif
        TraverseParms traverseParms = TraverseParms.For(TraverseMode.PassDoors);

        // Single combat group with ALL points (no 50/50 split)
        float points = parms.sitePart.parms.threatPoints;
        int seed = OutpostSitePartUtility.GetPawnGroupMakerSeed(parms.sitePart.parms);

        PawnGroupKindDef groupKind = PawnGroupKindDefOf.Combat;
        if (!faction.def.pawnGroupMakers.Any(maker => maker.kindDef == groupKind))
            groupKind = PawnGroupKindDefOf.Settlement;

        PawnGroupMakerParms groupParms = new PawnGroupMakerParms
        {
            groupKind = groupKind,
            tile = map.Tile,
            faction = faction,
            inhabitants = true,
            generateFightersOnly = true,
            seed = seed,
            points = Mathf.Max(points, faction.def.MinPointsToGeneratePawnGroup(groupKind))
        };

        ResolveParams resolveParams = default;
        resolveParams.rect = rect;
        resolveParams.faction = faction;
        resolveParams.singlePawnLord = lord;
        resolveParams.singlePawnSpawnCellExtraPredicate = (IntVec3 x) => map.reachability.CanReachMapEdge(x, traverseParms);
        resolveParams.pawnGroupMakerParams = groupParms;
        resolveParams.pawnGroupKindDef = groupKind;

        BaseGen.symbolStack.Push("pawnGroup", resolveParams);
        BaseGen.globalSettings.map = map;
        BaseGen.Generate();

        int spawned = map.mapPawns.AllPawnsSpawned.Count(p => p.Faction == faction);
        // Log.Message($"[HSKFaith] Patch_WorkSitePawns: site={parms.sitePart.def.defName}, faction={faction.Name}({faction.def.techLevel}), points={groupParms.points:F0}, spawned={spawned}");
        return false; // skip vanilla
    }
}
