using System.Collections.Generic;
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

// Debug: trace storyteller ThreatBig generation
[HarmonyPatch(typeof(StorytellerComp_OnOffCycle), nameof(StorytellerComp_OnOffCycle.MakeIntervalIncidents))]
public static class Patch_StorytellerDebug
{
    public static void Postfix(StorytellerComp_OnOffCycle __instance, IIncidentTarget target, ref IEnumerable<FiringIncident> __result)
    {
        var props = __instance.props as StorytellerCompProperties_OnOffCycle;
        if (props?.category == null) return;
        string cat = props.category.defName;
        if (cat != "ThreatBig") return;

        int count = 0;
        var list = new List<FiringIncident>();
        foreach (var fi in __result)
        {
            count++;
            list.Add(fi);
            // Log.Message($"[HSKFaith] Storyteller ThreatBig FIRE: {fi.def.defName} | target={target} | points={fi.parms.points:F0}");
        }
        if (count == 0)
        {
            float daysPassed = GenDate.DaysPassedSinceSettleFloat;
            int compIdx = Find.Storyteller.storytellerComps.IndexOf(__instance);
            int incCount = IncidentCycleUtility.IncidentCountThisInterval(target, compIdx,
                props.minDaysPassed, props.onDays, props.offDays, props.minSpacingDays,
                props.numIncidentsRange.min, props.numIncidentsRange.max, 1f);
            // Log.Message($"[HSKFaith] Storyteller ThreatBig: no incidents | daysPassed={daysPassed:F1} | incCount={incCount} | minDays={props.minDaysPassed} | onDays={props.onDays} | offDays={props.offDays} | target={target}");
        }
        __result = list;
    }
}

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
public static class Patch_RaiderThreatBlock
{
    public static void Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        // Log.Message($"[HSKFaith] CanFireNow: {__instance.def.defName} | category={__instance.def.category} | result={__result} | forced={parms.forced} | points={parms.points:F0} | faction={parms.faction?.Name ?? "none"} | strategy={parms.raidStrategy?.defName ?? "none"} | quest={parms.questScriptDef?.defName ?? "none"}");
        if (!__result) return;
        if (parms.forced) return; // don't block quest/forced incidents
        if (__instance.def.category != IncidentCategoryDefOf.ThreatBig) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        if (Find.TickManager.TicksGame < comp.raiderProtectionUntilTick)
        {
            __result = false;
            Log.Message($"[HSKFaith] Threat blocked by raider protection: {__instance.def.defName}");
        }
    }
}
