using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class SitePartWorker_FaithArmory : SitePartWorker_WorkSite
{
    private const float ThreatMultiplier = 2.0f;

    public override IEnumerable<PreceptDef> DisallowedPrecepts => Enumerable.Empty<PreceptDef>();

    public override PawnGroupKindDef WorkerGroupKind => PawnGroupKindDefOf.Settlement;

    public override SitePartParams GenerateDefaultParams(float myThreatPoints,
#if V15
        int tile,
#else
        PlanetTile tile,
#endif
        Faction faction)
    {
        SitePartParams parms = base.GenerateDefaultParams(myThreatPoints, tile, faction);
        parms.threatPoints *= ThreatMultiplier;
        return parms;
    }

    public override bool FactionCanOwn(Faction faction)
    {
        if (faction == null) return false;
        TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
        // Allow factions at most 1 tech level above player
        return (int)faction.def.techLevel <= (int)playerTech + 1;
    }

    public override void Init(Site site, SitePart sitePart)
    {
        // Skip base.Init loot generation — we do our own
        sitePart.things = new ThingOwner<Thing>(sitePart);

        TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
        TechLevel nextTech = (TechLevel)Mathf.Min((int)playerTech + 1, (int)TechLevel.Spacer);
        bool isWeapons = def.defName == "FaithArmory_Weapons";

        if (isWeapons)
        {
            Thing weapon = GenerateWeapon(nextTech);
            if (weapon != null)
            {
                sitePart.things.TryAdd(weapon);
                Thing ammo = GenerateAmmo(weapon.def);
                if (ammo != null)
                    sitePart.things.TryAdd(ammo);
            }
        }
        else
        {
            // 2 random armor pieces from different slots
            var slots = new List<string> { "FullHead", "Torso", "Hands", "Legs" };
            slots.Shuffle();
            foreach (string bodyPart in slots.Take(2))
            {
                Thing armor = GenerateArmorPiece(nextTech, bodyPart);
                if (armor != null)
                    sitePart.things.TryAdd(armor);
            }
        }
        sitePart.lootThings = new List<ThingDefCount>();

        sitePart.expectedEnemyCount = GenStep_WorkSitePawns.GetEnemiesCount(site, sitePart.parms, WorkerGroupKind);
    }

    public override IEnumerable<CampLootThingStruct> LootThings(
#if V15
        int tile
#else
        PlanetTile tile
#endif
    )
    {
        yield break;
    }

    public override bool CanSpawnOn(
#if V15
        int tile
#else
        PlanetTile tile
#endif
    )
    {
        return true;
    }

    public static Thing GenerateWeapon(TechLevel techLevel)
    {
        var candidates = DefDatabase<ThingDef>.AllDefs
            .Where(d => d.IsWeapon
                && d.techLevel == techLevel
                && d.HasComp(typeof(CompQuality))
                && !d.MadeFromStuff
                && !d.destroyOnDrop
                && d.tradeability != Tradeability.None)
            .ToList();

        if (!candidates.Any()) return null;

        ThingDef chosen = candidates.RandomElement();
        Thing weapon = ThingMaker.MakeThing(chosen);
        weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
        return weapon;
    }

    public static Thing GenerateAmmo(ThingDef weaponDef)
    {
        // Find CompProperties_AmmoUser via reflection (CE dependency)
        var ammoComp = weaponDef.comps?.FirstOrDefault(c => c.GetType().Name == "CompProperties_AmmoUser");
        if (ammoComp == null) return null;

        // Get ammoSet field
        var ammoSetField = ammoComp.GetType().GetField("ammoSet");
        if (ammoSetField == null) return null;

        var ammoSet = ammoSetField.GetValue(ammoComp);
        if (ammoSet == null) return null;

        // Get ammoTypes list
        var ammoTypesField = ammoSet.GetType().GetField("ammoTypes");
        if (ammoTypesField == null) return null;

        var ammoTypes = ammoTypesField.GetValue(ammoSet) as System.Collections.IList;
        if (ammoTypes == null || ammoTypes.Count == 0) return null;

        // Pick first ammo type (FMJ usually)
        var firstLink = ammoTypes[0];
        var ammoField = firstLink.GetType().GetField("ammo");
        if (ammoField == null) return null;

        var ammoDef = ammoField.GetValue(firstLink) as ThingDef;
        if (ammoDef == null) return null;

        // Get magazine size for count
        var magSizeField = ammoComp.GetType().GetField("magazineSize");
        int magSize = magSizeField != null ? (int)magSizeField.GetValue(ammoComp) : 6;
        int ammoCount = magSize * 6; // 6 magazines worth

        Thing ammo = ThingMaker.MakeThing(ammoDef);
        ammo.stackCount = ammoCount;
        return ammo;
    }

    // Map tech level to HSK apparel tags
    private static readonly Dictionary<TechLevel, List<string>> armorTagsByTech = new Dictionary<TechLevel, List<string>>
    {
        { TechLevel.Medieval, new List<string> { "Medieval", "MedievalKnightly", "NorbalWarrior" } },
        { TechLevel.Industrial, new List<string> { "IndustrialMilitaryAdvanced", "IndustrialMilitaryBasic", "SectarianMedium", "BrotherhoodMedium" } },
        { TechLevel.Spacer, new List<string> { "SpacerMilitary", "Spacer", "OrionMedium", "SyndicateMedium" } },
    };

    public static Thing GenerateArmorPiece(TechLevel techLevel, string bodyPartGroup)
    {
        if (!armorTagsByTech.TryGetValue(techLevel, out var tags))
            return null;

        var candidates = DefDatabase<ThingDef>.AllDefs
            .Where(d => d.IsApparel
                && d.apparel?.bodyPartGroups?.Any(bp => bp.defName == bodyPartGroup) == true
                && d.apparel?.tags?.Any(t => tags.Contains(t)) == true
                && d.statBases?.Any(s => s.stat == StatDefOf.ArmorRating_Sharp && s.value >= 1) == true
                && d.tradeability != Tradeability.None)
            .ToList();

        if (!candidates.Any())
        {
            Log.Warning($"[HSKFaith] FaithArmory: no armor for {bodyPartGroup} at {techLevel}, tags: {string.Join(",", tags)}");
            return null;
        }

        ThingDef chosen = candidates.RandomElement();
        ThingDef stuff = GenStuff.DefaultStuffFor(chosen);
        Thing armor = ThingMaker.MakeThing(chosen, stuff);
        armor.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
        Log.Message($"[HSKFaith] FaithArmory: generated {chosen.defName} for {bodyPartGroup} ({techLevel})");
        return armor;
    }
}
