using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch]
public static class Patch_GenderMemeTooltip
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(IdeoUIUtility), "GetMemeTip");
    }

    public static void Postfix(MemeDef meme, ref string __result)
    {
        if (meme.defName != "MaleSupremacy" && meme.defName != "FemaleSupremacy")
            return;

        int males = 0, females = 0;
        foreach (var p in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists)
        {
            if (p.gender == Gender.Male) males++;
            else if (p.gender == Gender.Female) females++;
        }

        bool isMale = meme.defName == "MaleSupremacy";
        int dominant = isMale ? males : females;
        int other = isMale ? females : males;
        string dominantSymbol = isMale ? "♂" : "♀";
        string otherSymbol = isMale ? "♀" : "♂";

        string dominantStr = (dominantSymbol + " " + dominant).Colorize(new Color(0.4f, 0.9f, 0.4f));

        __result += "\n\n" + "FT_GenderColonistsDetailed".Translate(dominantStr, otherSymbol + " " + other);
    }
}
