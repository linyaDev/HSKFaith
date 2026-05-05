using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.CertaintyChangePerDay), MethodType.Getter)]
public static class Patch_CertaintyChangePerDay
{
    public static bool Prefix(ref float __result, Pawn_IdeoTracker __instance)
    {
        var ideo = __instance?.Ideo;
        if (ideo?.memes == null)
        {
            __result = 0f;
            return false;
        }

        Patch_CertaintyTipRegion.lastDrawnIdeo = ideo;
        Patch_CertaintyTipRegion.lastCertainty = __instance.Certainty;
        Patch_CertaintyTipRegion.lastPawn = __instance.pawn;

        const float DefaultCertaintyPerSeason = -0.03f;
        float totalPerSeason = 0f;
        foreach (var meme in ideo.memes)
        {
            if (meme.category == MemeCategory.Structure) continue;
            var ext = meme.GetModExtension<MemeEffectExtension>();
            if (ext != null && ext.certaintyPerSeason != 0f)
                totalPerSeason += ext.certaintyPerSeason;
            else
                totalPerSeason += DefaultCertaintyPerSeason;
        }

        __result = totalPerSeason / 15f;
        return false;
    }
}

// Replace vanilla certainty tooltip with our breakdown
[HarmonyPatch(typeof(TooltipHandler), nameof(TooltipHandler.TipRegion), typeof(Rect), typeof(TipSignal))]
public static class Patch_CertaintyTipRegion
{
    public static Ideo lastDrawnIdeo;
    public static float lastCertainty;
    public static Pawn lastPawn;

    public static void Prefix(ref TipSignal tip)
    {
        if (tip.uniqueId != 10218219) return;

        // Use the pawn's own ideology, not the player's
        var ideo = lastDrawnIdeo ?? Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo?.memes == null) return;

        const float DefaultCertaintyPerSeason = -0.03f;
        float totalPerSeason = 0f;
        var lines = new List<string>();

        foreach (var meme in ideo.memes)
        {
            if (meme.category == MemeCategory.Structure) continue;
            var ext = meme.GetModExtension<MemeEffectExtension>();
            float memeCertainty = (ext != null && ext.certaintyPerSeason != 0f) ? ext.certaintyPerSeason : DefaultCertaintyPerSeason;
            totalPerSeason += memeCertainty;
            float memePerDay = memeCertainty / 15f;
            string val = (memePerDay * 100f).ToString("F1") + "%/" + "FT_CertaintyDay".Translate();
            lines.Add("  " + meme.LabelCap + ": " + val);
        }

        float totalPerDay = totalPerSeason / 15f;
        string totalStr = (totalPerDay * 100f).ToString("F1") + "%/" + "FT_CertaintyDay".Translate();
        if (totalPerDay >= 0f) totalStr = "+" + totalStr;

        string currentStr = (lastCertainty * 100f).ToString("F1") + "%";
        string newTip = "FT_CertaintyCurrent".Translate() + ": " + currentStr + "\n";

        // Pastor override
        bool isPastor = lastPawn != null && ideo.GetRole(lastPawn)?.def?.defName == "IdeoRole_Moralist";
        if (isPastor)
        {
            string pastorStr = "+" + (Patch_CertaintyTick.PastorCertaintyPerDay * 100f).ToString("F0") + "%/" + "FT_CertaintyDay".Translate();
            newTip += "FT_CertaintyTotal".Translate() + ": " + pastorStr + " (" + "FT_PastorRole".Translate() + ")";
        }
        else
        {
            newTip += "FT_CertaintyTotal".Translate() + ": " + totalStr + "\n";
            foreach (var line in lines)
                newTip += "\n" + line;
        }

        tip = new TipSignal(newTip, 10218219);
    }
}

