using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// +30 treeConnectionPoints when any tree is planted (including Gauranlen via CompPlantable).
/// </summary>
[HarmonyPatch(typeof(CompPlantable), nameof(CompPlantable.DoPlant))]
public static class Patch_TreeConnectionPlanted
{
    public static void Postfix(CompPlantable __instance)
    {
        var plantDef = __instance.Props?.plantDefToSpawn;
        if (plantDef?.plant == null || !plantDef.plant.IsTree) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("TreeConnection")) return;

        comp.treeConnectionPoints += 30;
    }
}
