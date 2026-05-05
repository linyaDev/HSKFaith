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

        if (items.Count == 0) return;

        // Find a spot near colonists
        var colonist = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
        IntVec3 dropCell = colonist != null ? colonist.Position : map.Center;

        foreach (var thing in items)
            GenPlace.TryPlaceThing(thing, dropCell, map, ThingPlaceMode.Near);
    }
}
