using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// +1 raiderPoints when History.Notify_PlayerRaidedSomeone fires.
/// This is the most reliable hook — fires for settlements, WorkSites, BanditCamps.
/// </summary>
[HarmonyPatch(typeof(History), nameof(History.Notify_PlayerRaidedSomeone))]
public static class Patch_Raider
{
    public static void Postfix()
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Raider")) return;

        comp.raiderPoints++;
        Log.Message($"[HSKFaith] Raider: +1 point (total {comp.raiderPoints})");
    }
}
