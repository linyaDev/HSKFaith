using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
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
            Log.Message($"[HSKFaith] Storyteller ThreatBig FIRE: {fi.def.defName} | target={target} | points={fi.parms.points:F0}");
        }
        if (count == 0)
        {
            float daysPassed = GenDate.DaysPassedSinceSettleFloat;
            int ticksSinceSettle = Find.TickManager.TicksSinceSettle;
            int minIntervals = Mathf.RoundToInt(props.minDaysPassed * 60f);
            int elapsed = ticksSinceSettle / 1000 - minIntervals;
            int onIntervals = Mathf.RoundToInt(props.onDays * 60f);
            int offIntervals = Mathf.RoundToInt(props.offDays * 60f);
            int cycleLen = onIntervals + offIntervals;
            string phase = "BEFORE_MIN";
            if (elapsed >= 0 && cycleLen > 0)
            {
                int posInCycle = elapsed % cycleLen;
                phase = posInCycle < onIntervals ? "ON" : "OFF";
            }
            float daysSinceLastThreat = -1f;
            if (target is Map map)
                daysSinceLastThreat = (Find.TickManager.TicksGame - map.storyState.LastThreatBigTick) / 60000f;
            Log.Message($"[HSKFaith] Storyteller ThreatBig: {phase} | daysPassed={daysPassed:F1} | sinceLastThreat={daysSinceLastThreat:F1} | target={target}");
        }
        __result = list;
    }
}

// Debug: trace DefaultThreatPointsNow
[HarmonyPatch(typeof(StorytellerUtility), nameof(StorytellerUtility.DefaultThreatPointsNow))]
public static class Patch_DefaultThreatPointsDebug
{
    public static void Postfix(IIncidentTarget target, float __result)
    {
        if (__result < 400f)
        {
            float wealth = target.PlayerWealthForStoryteller;
            int pawns = target.PlayerPawnsForStoryteller.Count();
            float threatScale = Find.Storyteller.difficulty.threatScale;
            float adaptFactor = Find.StoryWatcher.watcherAdaptation.TotalThreatPointsFactor;
            float daysFactor = Find.Storyteller.def.pointsFactorFromDaysPassed.Evaluate(GenDate.DaysPassedSinceSettle);
            Log.Warning($"[HSKFaith] DefaultThreatPointsNow LOW: result={__result:F0} | wealth={wealth:F0} | pawns={pawns} | threatScale={threatScale:F2} | adaptFactor={adaptFactor:F2} | daysFactor={daysFactor:F2} | target={target}");
        }
    }
}

// Debug: trace TryGenerateRaidInfo — the method that calls TryResolveRaidFaction, strategy, arrival, pawn gen
[HarmonyPatch]
public static class Patch_TryGenerateRaidInfoDebug
{
    static System.Reflection.MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(IncidentWorker_Raid), "TryGenerateRaidInfo",
            new[] { typeof(IncidentParms), typeof(List<Pawn>).MakeByRefType(), typeof(bool) });
    }

    public static void Postfix(IncidentParms parms, List<Pawn> pawns, bool __result)
    {
        int pawnCount = pawns?.Count ?? 0;
        Log.Message($"[HSKFaith] TryGenerateRaidInfo: result={__result} | faction={parms.faction?.Name ?? "none"} | strategy={parms.raidStrategy?.defName ?? "none"} | arrival={parms.raidArrivalMode?.defName ?? "none"} | points={parms.points:F0} | pawns={pawnCount} | daysPassed={GenDate.DaysPassedSinceSettleFloat:F1}");

        if (!__result && parms.target is Map map)
        {
            string faction = parms.faction?.Name ?? "???";
            string strategy = parms.raidStrategy?.defName ?? "???";
            string label = "FT_RaidFailed".Translate();
            string text = "FT_RaidFailedDesc".Translate(faction, strategy, parms.points.ToString("F0"));
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent, new LookTargets(map.Center, map));
        }
    }
}

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
public static class Patch_RaiderThreatBlock
{
    public static void Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (__instance.def.category == IncidentCategoryDefOf.ThreatBig)
            Log.Warning($"[HSKFaith] CanFireNow: {__instance.def.defName} | category={__instance.def.category} | result={__result} | forced={parms.forced} | points={parms.points:F0} | faction={parms.faction?.Name ?? "none"} | strategy={parms.raidStrategy?.defName ?? "none"} | quest={parms.questScriptDef?.defName ?? "none"}");
        else
            Log.Message($"[HSKFaith] CanFireNow: {__instance.def.defName} | category={__instance.def.category} | result={__result} | forced={parms.forced} | points={parms.points:F0}");
        if (__instance.def.category == IncidentCategoryDefOf.ThreatBig && parms.points < 100f)
            Log.Message($"[HSKFaith] LOW POINTS STACKTRACE:\n{System.Environment.StackTrace}");
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
