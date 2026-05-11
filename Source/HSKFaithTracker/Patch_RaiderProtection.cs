using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// When Raider meme is active and 2+ colonists are in a caravan,
/// activate 4 days of ThreatBig protection. Cooldown: 15 days.
/// Checked every hour (2500 ticks) in GameComponent.
/// </summary>
public static class RaiderProtectionCheck
{
    private const int ProtectionDays = 2;
    private const int CooldownDays = 15;

    public static void CheckCaravans(GameComponent_FaithTracker comp)
    {
        if (!comp.HasMeme("Raider")) return;

        int now = Find.TickManager.TicksGame;

        // Already protected or on cooldown
        if (now < comp.raiderProtectionUntilTick) return;
        if (now < comp.raiderProtectionCooldownUntilTick) return;

        // Check if any player caravan has 2+ colonists
        foreach (var caravan in Find.WorldObjects.Caravans)
        {
            if (!caravan.IsPlayerControlled) continue;
            int colonists = caravan.PawnsListForReading.Count(p => p.IsColonist && !p.IsSlave);
            if (colonists >= 2)
            {
                comp.raiderProtectionUntilTick = now + (ProtectionDays * 60000);
                comp.raiderProtectionCooldownUntilTick = now + (CooldownDays * 60000);
                Log.Message($"[HSKFaith] Raider protection: {colonists} colonists in caravan, {ProtectionDays} days protection");
                return;
            }
        }
    }
}

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
public static class Patch_RaiderThreatBlock
{
    public static void Postfix(IncidentWorker __instance, ref bool __result)
    {
        if (!__result) return;
        if (__instance.def.category != IncidentCategoryDefOf.ThreatBig) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        if (Find.TickManager.TicksGame < comp.raiderProtectionUntilTick)
            __result = false;
    }
}
