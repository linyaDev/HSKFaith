using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// +1 raiderPoints when HistoryEvent "Raided" fires (once per raid, not per pawn).
/// </summary>
[HarmonyPatch(typeof(HistoryEventsManager), nameof(HistoryEventsManager.RecordEvent))]
public static class Patch_Raider
{
    private static int lastRaidTick = -1;

    public static void Postfix(HistoryEvent ev)
    {
        if (ev.def?.defName != "Raided") return;

        int tick = Find.TickManager.TicksGame;
        if (tick == lastRaidTick) return;
        lastRaidTick = tick;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Raider")) return;

        comp.raiderPoints++;
        Log.Message($"[HSKFaith] Raider: +1 point (total {comp.raiderPoints})");
    }
}
