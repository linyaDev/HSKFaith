using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_CreateRelic
{
    public const float MinFaithScore = 15f;
    public const int WeaponFaithCost = 25;
    public const int RandomFaithCost = 15;

    private static readonly HashSet<string> RelicBuildings = new HashSet<string>
    {
        "RitualSpot",
        "Altar_Small", "Altar_Medium", "Altar_Large", "Altar_Grand",
        "Ideogram"
    };

    public static void Postfix(Building __instance, ref IEnumerable<Gizmo> __result)
    {
        if (!RelicBuildings.Contains(__instance.def.defName))
            return;

        if (__instance.Faction != Faction.OfPlayer)
            return;

        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        bool canCreate = comp.Score >= MinFaithScore;
        var moralist = FindMoralist(__instance.Map);
        bool hasMoralist = moralist != null;

        string disabledReason = null;
        if (!canCreate)
            disabledReason = "FT_RelicNeedMoreFaith".Translate(MinFaithScore.ToString("F0"));
        else if (!hasMoralist)
            disabledReason = "FT_RelicNeedMoralist".Translate();

        var gizmo = new Command_Action
        {
            defaultLabel = "FT_CreateRelic".Translate(),
            defaultDesc = "FT_CreateRelicDesc".Translate(WeaponFaithCost, RandomFaithCost),
            icon = ContentFinder<Texture2D>.Get("UI/Icons/CreateRelic", true),
            disabled = disabledReason != null,
            disabledReason = disabledReason,
            action = () => ShowRelicChoices(__instance, moralist, comp)
        };

        __result = AppendGizmo(__result, gizmo);
    }

    private static IEnumerable<Gizmo> AppendGizmo(IEnumerable<Gizmo> original, Gizmo extra)
    {
        int count = 0;
        foreach (var g in original)
        {
            count++;
            var cmd = g as Command;
            Log.Message($"[HSKFaith] Gizmo #{count}: {cmd?.defaultLabel ?? g.GetType().Name}");
            yield return g;
        }
        Log.Message($"[HSKFaith] Total gizmos: {count}, adding CreateRelic");
        yield return extra;
    }

    private static void ShowRelicChoices(Building building, Pawn moralist, GameComponent_FaithTracker comp)
    {
        var choices = new List<FloatMenuOption>();
        bool canWeapon = comp.Score >= WeaponFaithCost;
        bool canRandom = comp.Score >= RandomFaithCost;

        // Weapons from stockpile (grouped by def+stuff, pick best of each)
        var map = building.Map;
        var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
            .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.def.IsWeapon
                && t.IsInAnyStorage())
            .GroupBy(t => (t.def, t.Stuff))
            .Select(g => g.OrderByDescending(t => t.MarketValue).First())
            .OrderByDescending(t => t.MarketValue)
            .Take(10)
            .ToList();

        foreach (var weapon in weapons)
        {
            var w = weapon;
            string label = "FT_RelicFromWeapon".Translate(GenLabel.ThingLabel(w.def, w.Stuff).CapitalizeFirst()) + $" ({WeaponFaithCost})";
            if (canWeapon)
                choices.Add(new FloatMenuOption(label, () => StartRelicJob(moralist, building, w)));
            else
                choices.Add(new FloatMenuOption(label, null));
        }

        // Random relic
        string randomLabel = "FT_RelicRandom".Translate() + $" ({RandomFaithCost})";
        if (canRandom)
            choices.Add(new FloatMenuOption(randomLabel, () => StartRelicJob(moralist, building, null)));
        else
            choices.Add(new FloatMenuOption(randomLabel, null));

        if (choices.Count > 0)
            Find.WindowStack.Add(new FloatMenu(choices));
    }

    private static void StartRelicJob(Pawn moralist, Building altar, Thing weapon)
    {
        var jobDef = DefDatabase<JobDef>.GetNamedSilentFail("FT_CreateRelic");
        if (jobDef == null)
        {
            Log.Error("[HSKFaith] JobDef FT_CreateRelic not found");
            return;
        }

        var job = JobMaker.MakeJob(jobDef, altar, weapon);
        job.count = weapon != null ? WeaponFaithCost : RandomFaithCost;
        moralist.jobs.TryTakeOrderedJob(job);
    }

    private static Pawn FindMoralist(Map map)
    {
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return null;

        foreach (var p in ideo.PreceptsListForReading)
        {
            if (p is Precept_RoleSingle role && role.def.defName == "IdeoRole_Moralist")
            {
                var pawn = role.ChosenPawnSingle();
                if (pawn != null && pawn.Spawned && pawn.Map == map)
                    return pawn;
            }
        }
        return null;
    }
}
