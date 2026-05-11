using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// Spawns a single weapon or armor of the next tech era at excellent quality.
/// </summary>
public class GenStep_FaithArmoryLoot : GenStep
{
    public override int SeedPart => 784523169;

    public override void Generate(Map map, GenStepParams parms)
    {
        TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
        TechLevel nextTech = (TechLevel)Mathf.Min((int)playerTech + 1, (int)TechLevel.Spacer);

        bool isWeapons = parms.sitePart?.def?.defName == "FaithArmory_Weapons";

        Thing loot = isWeapons
            ? SitePartWorker_FaithArmory.GenerateWeapon(nextTech)
            : SitePartWorker_FaithArmory.GenerateArmor(nextTech);

        if (loot == null)
        {
            Log.Warning($"[HSKFaith] FaithArmory: could not generate {(isWeapons ? "weapon" : "armor")} for tech {nextTech}");
            return;
        }

        IntVec3 cell = map.Center;
        CellFinder.TryFindRandomCellNear(map.Center, map, 10, (IntVec3 c) => c.Standable(map) && !c.Fogged(map), out cell);
        GenPlace.TryPlaceThing(loot, cell, map, ThingPlaceMode.Near);

        Log.Message($"[HSKFaith] FaithArmory: spawned {loot.LabelCap} ({nextTech}) at {cell}");
    }
}
