using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(IdeoUtility), nameof(IdeoUtility.Notify_PlayerRaidedSomeone))]
public static class Patch_Raider
{
    public static void Postfix()
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.raiderPoints++;
    }
}
