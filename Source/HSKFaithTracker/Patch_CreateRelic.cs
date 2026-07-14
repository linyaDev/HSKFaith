using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_CreateRelic
{
    private const float MinFaithScore = 1f;
    private const int FaithCost = 1;

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
        string desc = canCreate
            ? "FT_CreateRelicDesc".Translate(FaithCost)
            : "FT_RelicNeedMoreFaith".Translate(MinFaithScore.ToString("F0"));

        var list = new List<Gizmo>(__result);
        list.Add(new Command_Action
        {
            defaultLabel = "FT_CreateRelic".Translate(),
            defaultDesc = desc,
            icon = ContentFinder<Texture2D>.Get("UI/Icons/ForgetMeme", true),
            disabled = !canCreate,
            disabledReason = canCreate ? null : desc,
            action = () => ShowRelicChoices(__instance, ideo, comp)
        });
        __result = list;
    }

    private static void ShowRelicChoices(Building building, Ideo ideo, GameComponent_FaithTracker comp)
    {
        var choices = new List<FloatMenuOption>();

        // Find moralist pawn nearby
        var moralist = FindMoralistNear(building);

        // Option 1: Weapon from moralist's equipment
        if (moralist?.equipment?.Primary != null)
        {
            var weapon = moralist.equipment.Primary;
            choices.Add(new FloatMenuOption(
                "FT_RelicFromWeapon".Translate(weapon.LabelCap),
                () => CreateRelicFromWeapon(moralist, weapon, ideo, comp)));
        }

        // Option 2: Random relic item
        choices.Add(new FloatMenuOption(
            "FT_RelicRandom".Translate(),
            () => CreateRandomRelic(building, ideo, comp)));

        if (choices.Count > 0)
            Find.WindowStack.Add(new FloatMenu(choices));
    }

    private static Pawn FindMoralistNear(Building building)
    {
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return null;

        foreach (var p in ideo.PreceptsListForReading)
        {
            if (p is Precept_RoleSingle role && role.def.defName == "IdeoRole_Moralist")
            {
                var pawn = role.ChosenPawnSingle();
                if (pawn != null && pawn.Spawned && pawn.Map == building.Map)
                    return pawn;
            }
        }
        return null;
    }

    private static void CreateRelicFromWeapon(Pawn moralist, Thing weapon, Ideo ideo, GameComponent_FaithTracker comp)
    {
        moralist.equipment.Remove((ThingWithComps)weapon);

        var relicDef = DefDatabase<PreceptDef>.GetNamedSilentFail("IdeoRelic");
        if (relicDef == null) return;

        var relic = (Precept_Relic)PreceptMaker.MakePrecept(relicDef);
        ideo.AddPrecept(relic);
        relic.ThingDef = weapon.def;
        if (weapon.Stuff != null)
            relic.stuff = weapon.Stuff;
        relic.RegenerateName();

        var compQuality = weapon.TryGetComp<CompQuality>();
        if (compQuality != null)
            compQuality.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Colony);

        weapon.StyleSourcePrecept = relic;
        GenPlace.TryPlaceThing(weapon, moralist.Position, moralist.Map, ThingPlaceMode.Near);

        comp.RecordRitual("FT_RelicCreated".Translate(relic.LabelCap), RitualRecordType.FaithDecay, customWeight: -FaithCost);

        Find.LetterStack.ReceiveLetter(
            "FT_RelicCreatedTitle".Translate(),
            "FT_RelicCreatedDesc".Translate(moralist.LabelShortCap, relic.LabelCap),
            LetterDefOf.PositiveEvent, weapon);
    }

    private static void CreateRandomRelic(Building building, Ideo ideo, GameComponent_FaithTracker comp)
    {
        var relicDef = DefDatabase<PreceptDef>.GetNamedSilentFail("IdeoRelic");
        if (relicDef == null) return;

        var relic = (Precept_Relic)PreceptMaker.MakePrecept(relicDef);

        var candidates = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.relicChance > 0f && !d.IsWeapon)
            .ToList();

        if (candidates.Count == 0) return;

        var chosenDef = candidates.RandomElementByWeight(d => d.relicChance);
        ideo.AddPrecept(relic);
        relic.ThingDef = chosenDef;
        relic.RegenerateName();

        Thing relicThing = relic.GenerateRelic();
        GenPlace.TryPlaceThing(relicThing, building.Position, building.Map, ThingPlaceMode.Near);

        comp.RecordRitual("FT_RelicCreated".Translate(relic.LabelCap), RitualRecordType.FaithDecay, customWeight: -FaithCost);

        Find.LetterStack.ReceiveLetter(
            "FT_RelicCreatedTitle".Translate(),
            "FT_RelicCreatedDesc".Translate("FT_Colony".Translate(), relic.LabelCap),
            LetterDefOf.PositiveEvent, relicThing);
    }
}
