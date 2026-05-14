using System.Linq;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// Spawns the armory loot (pre-generated in SitePartWorker_FaithArmory.Init) inside the outpost.
/// </summary>
public class GenStep_FaithArmoryLoot : GenStep
{
    public override int SeedPart => 784523169;

    public override void Generate(Map map, GenStepParams parms)
    {
        if (parms.sitePart?.things == null || parms.sitePart.things.Count == 0)
        {
            Log.Warning("[HSKFaith] FaithStash: no loot in sitePart.things");
            return;
        }

        // Try to find a cell inside a roofed building (the outpost)
        IntVec3 cell = map.Center;
        bool found = CellFinder.TryFindRandomCellNear(map.Center, map, 15,
            (IntVec3 c) => c.Standable(map) && c.Roofed(map) && c.GetRoom(map)?.IsDoorway == false,
            out cell);

        if (!found)
            CellFinder.TryFindRandomCellNear(map.Center, map, 10,
                (IntVec3 c) => c.Standable(map), out cell);

        parms.sitePart.things.TryDropAll(cell, map, ThingPlaceMode.Near);
        Log.Message($"[HSKFaith] FaithStash: loot dropped at {cell}, {parms.sitePart.things.Count} items");
    }
}
