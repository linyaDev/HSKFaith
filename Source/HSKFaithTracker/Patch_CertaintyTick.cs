using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

// Replace vanilla mood-based certainty change with meme-based daily rate
[HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoTrackerTick))]
public static class Patch_CertaintyTick
{
    public const float PastorCertaintyPerDay = 0.02f;

    private const int UpdateInterval = 2500; // once per game hour

    public static bool Prefix(Pawn_IdeoTracker __instance)
    {
        // Only update every 2500 ticks (1 game hour)
        if (Find.TickManager.TicksGame % UpdateInterval != __instance.pawn.thingIDNumber % UpdateInterval)
            return false;

        var ideo = __instance?.Ideo;
        if (ideo?.memes == null) return false;

        // Pastor role: fixed +2% per day, ignores meme penalties
        var pawn = __instance.pawn;
        if (pawn != null && ideo.GetRole(pawn)?.def?.defName == "IdeoRole_Moralist")
        {
            float pastorPerHour = PastorCertaintyPerDay / 24f;
            __instance.Certainty = Mathf.Clamp01(__instance.Certainty + pastorPerHour);
            return false;
        }

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

        if (totalPerSeason == 0f) return false;

        // Per season -> per hour: divide by 15 days * 24 hours
        float perHour = totalPerSeason / (15f * 24f);
        __instance.Certainty = Mathf.Clamp01(__instance.Certainty + perHour);
        return false;
    }
}
