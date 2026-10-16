using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

// Replace vanilla mood-based certainty change with meme-based daily rate
#if V16
[HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoTrackerTickInterval))]
#else
[HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoTrackerTick))]
#endif
public static class Patch_CertaintyTick
{
    public const float PastorCertaintyPerDay = 0.02f;

    private const int UpdateInterval = 2500; // once per game hour

    // Per-hour certainty change for this pawn, or 0 if nothing to apply
    private static float CertaintyPerHour(Pawn_IdeoTracker tracker, Ideo ideo)
    {
        // Pastor role: fixed +2% per day, ignores meme penalties
        var pawn = tracker.pawn;
        if (pawn != null && ideo.GetRole(pawn)?.def?.defName == "IdeoRole_Moralist")
            return PastorCertaintyPerDay / 24f;

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

        // Per season -> per hour: divide by 15 days * 24 hours
        return totalPerSeason / (15f * 24f);
    }

#if V16
    public static bool Prefix(Pawn_IdeoTracker __instance, int delta)
    {
        // 1.6 also recaches ideo components in this method — keep that behavior
        var ideo = __instance?.Ideo;
        if (ideo != null && ideo.currentCacheId != __instance.lastCacheId)
        {
            __instance.lastCacheId = ideo.currentCacheId;
            __instance.RecacheIdeoComponents();
        }

        if (ideo?.memes == null) return false;

        float perHour = CertaintyPerHour(__instance, ideo);
        if (perHour == 0f) return false;

        // Interval ticking: scale the hourly rate by elapsed ticks
        __instance.Certainty = Mathf.Clamp01(__instance.Certainty + perHour * delta / UpdateInterval);
        return false;
    }
#else
    public static bool Prefix(Pawn_IdeoTracker __instance)
    {
        // Only update every 2500 ticks (1 game hour)
        if (Find.TickManager.TicksGame % UpdateInterval != __instance.pawn.thingIDNumber % UpdateInterval)
            return false;

        var ideo = __instance?.Ideo;
        if (ideo?.memes == null) return false;

        float perHour = CertaintyPerHour(__instance, ideo);
        if (perHour == 0f) return false;

        __instance.Certainty = Mathf.Clamp01(__instance.Certainty + perHour);
        return false;
    }
#endif
}
