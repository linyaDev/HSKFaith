using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.StartedNewGame))]
public static class Patch_StartingItems
{
    public static void Postfix()
    {
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return;

        var map = Find.CurrentMap;
        if (map == null) return;

        var items = new List<Thing>();

        foreach (var meme in ideo.memes)
        {
            var ext = meme.GetModExtension<MemeEffectExtension>();
            if (ext?.startingItems == null) continue;

            foreach (var entry in ext.startingItems)
            {
                if (entry.thingDef == null || entry.count <= 0) continue;

                var thing = ThingMaker.MakeThing(entry.thingDef, GenStuff.DefaultStuffFor(entry.thingDef));
                thing.stackCount = entry.count;
                items.Add(thing);
            }
        }

        // Collect starting animals
        var animals = new List<(PawnKindDef kind, int count, Gender gender)>();
        foreach (var meme in ideo.memes)
        {
            var ext2 = meme.GetModExtension<MemeEffectExtension>();
            if (ext2?.startingAnimals == null) continue;
            foreach (var entry in ext2.startingAnimals)
            {
                if (entry.kindDef != null && entry.count > 0)
                    animals.Add((entry.kindDef, entry.count, entry.gender));
            }
        }

        // Unlock starting research
        foreach (var meme in ideo.memes)
        {
            var ext3 = meme.GetModExtension<MemeEffectExtension>();
            if (ext3?.startingResearchProjects == null) continue;
            foreach (var proj in ext3.startingResearchProjects)
            {
                if (proj != null && !proj.IsFinished)
                {
                    Find.ResearchManager.FinishProject(proj);
                    Log.Message($"[HSKFaith] Starting research unlocked: {proj.LabelCap}");
                }
            }
        }

        if (items.Count == 0 && animals.Count == 0) return;

        // Find a spot near colonists
        var colonist = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
        IntVec3 dropCell = colonist != null ? colonist.Position : map.Center;

        foreach (var thing in items)
            GenPlace.TryPlaceThing(thing, dropCell, map, ThingPlaceMode.Near);

        // Spawn animals
        foreach (var (kind, count, gender) in animals)
        {
            for (int i = 0; i < count; i++)
            {
                var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    kind, Faction.OfPlayer, forceGenerateNewPawn: true,
                    fixedGender: gender != Gender.None ? gender : (Gender?)null));
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(dropCell, map, 5), map);
            }
        }
    }

}
