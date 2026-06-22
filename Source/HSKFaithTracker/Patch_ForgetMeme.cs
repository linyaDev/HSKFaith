using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_ForgetMeme
{
    public static void Postfix(Building __instance, ref IEnumerable<Gizmo> __result)
    {
        if (!IsRitualFocus(__instance))
            return;

        if (__instance.Faction != Faction.OfPlayer)
            return;

        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null)
            return;

        int normalMemes = ideo.memes.Count(m => m.category != MemeCategory.Structure);
        if (normalMemes < 2)
            return;

        __result = __result.Append(new Command_Action
        {
            defaultLabel = "FT_ForgetMeme".Translate(),
            defaultDesc = "FT_ForgetMemeDesc".Translate(),
            icon = ContentFinder<Texture2D>.Get("UI/Icons/ForgetMeme", true),
            action = () => Find.WindowStack.Add(new Dialog_ForgetMeme(ideo))
        });
    }

    private static readonly HashSet<string> ForgetBuildings = new HashSet<string>
    {
        "RitualSpot",
        "Altar_Small", "Altar_Medium", "Altar_Large", "Altar_Grand",
        "Ideogram"
    };

    private static bool IsRitualFocus(Building b)
    {
        return ForgetBuildings.Contains(b.def.defName);
    }
}
