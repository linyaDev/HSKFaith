using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace HSKFaithTracker;

public class SitePartWorker_FaithArmory : SitePartWorker
{
    private static readonly SimpleCurve ThreatPointsLootMarketValue = new SimpleCurve
    {
        new CurvePoint(100f, 200f),
        new CurvePoint(250f, 450f),
        new CurvePoint(800f, 1000f),
        new CurvePoint(10000f, 2000f)
    };

    public override SitePartParams GenerateDefaultParams(float myThreatPoints,
#if V15
        int tile,
#else
        PlanetTile tile,
#endif
        Faction faction)
    {
        SitePartParams parms = base.GenerateDefaultParams(myThreatPoints, tile, faction);
        parms.threatPoints = Mathf.Max(parms.threatPoints, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Settlement));
        parms.lootMarketValue = ThreatPointsLootMarketValue.Evaluate(parms.threatPoints);
        return parms;
    }

    public override string GetArrivedLetterPart(Map map, out LetterDef preferredLetterDef, out LookTargets lookTargets)
    {
        string text = base.GetArrivedLetterPart(map, out preferredLetterDef, out lookTargets);
        lookTargets = new LookTargets(map.Parent);
        return text;
    }

    public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
    {
        base.Notify_GeneratedByQuestGen(part, slate, outExtraDescriptionRules, outExtraDescriptionConstants);
        int enemiesCount = GetEnemiesCount(part.site, part.parms);
        outExtraDescriptionRules.Add(new Rule_String("enemiesCount", enemiesCount.ToString()));
        outExtraDescriptionRules.Add(new Rule_String("enemiesLabel", GetEnemiesLabel(part.site, enemiesCount)));

        // Generate the armory loot
        Thing loot = GenerateArmoryLoot();
        if (loot != null)
        {
            outExtraDescriptionRules.Add(new Rule_String("armoryLoot", loot.LabelCap));
            slate.Set("armoryLoot", loot);
            slate.Set("armoryLootLabel", loot.LabelCap);
        }
    }

    public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
    {
        return base.GetPostProcessedThreatLabel(site, sitePart) + ": " +
            "KnownSiteThreatEnemyCountAppend".Translate(GetEnemiesCount(site, sitePart.parms), "Enemies".Translate());
    }

    private int GetEnemiesCount(Site site, SitePartParams parms)
    {
        return PawnGroupMakerUtility.GeneratePawnKindsExample(new PawnGroupMakerParms
        {
            tile = site.Tile,
            faction = site.Faction,
            groupKind = PawnGroupKindDefOf.Settlement,
            points = parms.threatPoints,
            inhabitants = true,
            seed = OutpostSitePartUtility.GetPawnGroupMakerSeed(parms)
        }).Count();
    }

    private string GetEnemiesLabel(Site site, int enemiesCount)
    {
        if (site.Faction == null)
            return (enemiesCount == 1) ? "Enemy".Translate() : "Enemies".Translate();
        return enemiesCount != 1 ? site.Faction.def.pawnsPlural : site.Faction.def.pawnSingular;
    }

    public static Thing GenerateWeapon(TechLevel techLevel)
    {
        var candidates = DefDatabase<ThingDef>.AllDefs
            .Where(d => d.IsWeapon
                && d.techLevel == techLevel
                && d.HasComp(typeof(CompQuality))
                && d.MadeFromStuff == false
                && !d.destroyOnDrop
                && d.tradeability != Tradeability.None)
            .ToList();

        if (!candidates.Any()) return null;

        ThingDef chosen = candidates.RandomElement();
        Thing weapon = ThingMaker.MakeThing(chosen);
        weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
        return weapon;
    }

    public static Thing GenerateArmor(TechLevel techLevel)
    {
        var candidates = DefDatabase<ThingDef>.AllDefs
            .Where(d => d.IsApparel
                && d.techLevel == techLevel
                && d.HasComp(typeof(CompQuality))
                && d.apparel?.bodyPartGroups?.Any(bp => bp.defName == "Torso") == true
                && d.tradeability != Tradeability.None)
            .ToList();

        if (!candidates.Any()) return null;

        ThingDef chosen = candidates.RandomElement();
        ThingDef stuff = GenStuff.DefaultStuffFor(chosen);
        Thing armor = ThingMaker.MakeThing(chosen, stuff);
        armor.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
        return armor;
    }
}
