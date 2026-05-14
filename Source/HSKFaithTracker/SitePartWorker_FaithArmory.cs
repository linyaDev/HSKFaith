using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class SitePartWorker_FaithArmory : SitePartWorker_WorkSite
{
    private const float ArmoryThreatMultiplier = 2.5f;
    private const float StashThreatMultiplier = 1.5f;

    public override IEnumerable<PreceptDef> DisallowedPrecepts => Enumerable.Empty<PreceptDef>();

    public override PawnGroupKindDef WorkerGroupKind => PawnGroupKindDefOf.Combat;

    // GenerateDefaultParams is NOT called by QuestNode_Root_WorkSite — multiplier applied in Init() instead


    public override bool FactionCanOwn(Faction faction)
    {
        if (faction == null) return false;
        TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
        // Allow factions at most 1 tech level above player
        return (int)faction.def.techLevel <= (int)playerTech + 1;
    }

    public override void Init(Site site, SitePart sitePart)
    {
        // Replace faction if tech level too high
        if (site.Faction != null && !FactionCanOwn(site.Faction))
        {
            var oldFaction = site.Faction;
            var better = Find.FactionManager.AllFactionsListForReading
                .Where(f => FactionCanOwn(f) && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction)
                .RandomElementWithFallback();
            if (better != null)
            {
                site.SetFaction(better);
                Log.Message($"[HSKFaith] {def.defName}: replaced faction {oldFaction.Name}({oldFaction.def.techLevel}) -> {better.Name}({better.def.techLevel})");
            }
        }

        // Apply threat multiplier (GenerateDefaultParams is bypassed by QuestNode_Root_WorkSite)
        float mult = def.defName.StartsWith("FaithArmory") ? ArmoryThreatMultiplier : StashThreatMultiplier;
        float before = sitePart.parms.threatPoints;
        sitePart.parms.threatPoints *= mult;
        Log.Message($"[HSKFaith] {def.defName}: faction={site.Faction?.Name}({site.Faction?.def?.techLevel}), threat {before:F0} x{mult} = {sitePart.parms.threatPoints:F0}");

        // Skip base.Init loot generation — we do our own
        sitePart.things = new ThingOwner<Thing>(sitePart);

        TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
        TechLevel nextTech = (TechLevel)Mathf.Min((int)playerTech + 1, (int)TechLevel.Spacer);
        switch (def.defName)
        {
            case "FaithArmory_Weapons":
            {
                Thing weapon = GenerateWeapon(nextTech);
                if (weapon != null)
                {
                    sitePart.things.TryAdd(weapon);
                    Thing ammo = GenerateAmmo(weapon.def);
                    if (ammo != null)
                        sitePart.things.TryAdd(ammo);
                }
                break;
            }
            case "FaithArmory_Armor":
            {
                var slots = new List<string> { "FullHead", "Torso", "Hands", "Legs" };
                slots.Shuffle();
                foreach (string bodyPart in slots.Take(2))
                {
                    Thing armor = GenerateArmorPiece(nextTech, bodyPart);
                    if (armor != null)
                        sitePart.things.TryAdd(armor);
                }
                break;
            }
            case "FaithStash_Leather":
                GenerateLeatherLoot(sitePart);
                break;
            case "FaithStash_Steel":
                GenerateSteelLoot(sitePart);
                break;
        }
        sitePart.lootThings = new List<ThingDefCount>();

        Log.Message($"[HSKFaith] {def.defName} Init: faction={site.Faction?.Name}, threatPts={sitePart.parms.threatPoints:F0}, loot={sitePart.things?.Count ?? 0} items");
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
                && !d.destroyOnDrop
                && d.tradeability != Tradeability.None
                && !d.defName.StartsWith("TFJ_")
                && !d.defName.StartsWith("RK_"))
            .ToList();

        if (!candidates.Any()) return null;

        Log.Message($"[HSKFaith] FaithStash weapon candidates ({techLevel}, {candidates.Count}): {string.Join(", ", candidates.Select(d => d.defName))}");

        ThingDef chosen = candidates.RandomElement();
        ThingDef stuff = GenStuff.DefaultStuffFor(chosen);
        Thing weapon = ThingMaker.MakeThing(chosen, stuff);
        weapon.TryGetComp<CompQuality>()?.SetQuality((QualityCategory)Rand.RangeInclusive((int)QualityCategory.Normal, (int)QualityCategory.Excellent), ArtGenerationContext.Outsider);
        Log.Message($"[HSKFaith] FaithStash: generated weapon {chosen.defName}{(stuff != null ? $" ({stuff.defName})" : "")} ({techLevel})");
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
        { TechLevel.Medieval, new List<string> { "Medieval", "MedievalKnightly", "NorbalWarrior", "NorbalKingly", "NorbalSage", "EmpireMedium", "EmpireScout", "EmpireLight", "BanditsLight", "RenegadesLight", "PredatorsScout" } },
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
            Log.Warning($"[HSKFaith] FaithStash: no armor for {bodyPartGroup} at {techLevel}, tags: {string.Join(",", tags)}");
            return null;
        }

        Log.Message($"[HSKFaith] FaithStash armor candidates ({techLevel}, {bodyPartGroup}, {candidates.Count}): {string.Join(", ", candidates.Select(d => d.defName))}");

        ThingDef chosen = candidates.RandomElement();
        ThingDef stuff = GenStuff.DefaultStuffFor(chosen);
        Thing armor = ThingMaker.MakeThing(chosen, stuff);
        armor.TryGetComp<CompQuality>()?.SetQuality((QualityCategory)Rand.RangeInclusive((int)QualityCategory.Normal, (int)QualityCategory.Excellent), ArtGenerationContext.Outsider);
        Log.Message($"[HSKFaith] FaithStash: generated {chosen.defName} for {bodyPartGroup} ({techLevel})");
        return armor;
    }

    private static readonly Dictionary<TechLevel, List<string>> leatherByTech = new Dictionary<TechLevel, List<string>>
    {
        { TechLevel.Medieval, new List<string> { "Leather_Bear", "Leather_Wolf", "Leather_Rhinoceros", "Leather_Elephant" } },
        { TechLevel.Industrial, new List<string> { "Leather_Thrumbo", "Leather_FireDragonskin", "Leather_KirinHide", "Leather_BarghestFur" } },
        { TechLevel.Spacer, new List<string> { "Hyperweave", "DevilstrandCloth", "Synthread" } },
    };

    private static void GenerateLeatherLoot(SitePart sitePart)
    {
        TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
        TechLevel nextTech = (TechLevel)Mathf.Min((int)playerTech + 1, (int)TechLevel.Spacer);

        if (!leatherByTech.TryGetValue(nextTech, out var pool))
            pool = leatherByTech[TechLevel.Medieval];

        var candidates = pool
            .Select(name => DefDatabase<ThingDef>.GetNamedSilentFail(name))
            .Where(d => d != null)
            .ToList();

        if (!candidates.Any())
        {
            Log.Warning("[HSKFaith] FaithStash: no leather/fabric candidates found");
            return;
        }

        Log.Message($"[HSKFaith] FaithStash leather/fabric candidates ({nextTech}, {candidates.Count}): {string.Join(", ", candidates.Select(d => d.defName))}");

        candidates.Shuffle();
        foreach (var mat in candidates.Take(2))
        {
            Thing thing = ThingMaker.MakeThing(mat);
            thing.stackCount = Rand.RangeInclusive(80, 120);
            sitePart.things.TryAdd(thing);
            Log.Message($"[HSKFaith] FaithStash: generated {mat.defName} x{thing.stackCount}");
        }
    }

    private static readonly Dictionary<TechLevel, List<string>> alloysByTech = new Dictionary<TechLevel, List<string>>
    {
        { TechLevel.Medieval, new List<string> { "AlnicoAlloy" } },
        { TechLevel.Industrial, new List<string> { "Chromium", "NickelBar", "Cobalt", "AluminiumBar", "CarbonAlloy", "DepletedUranium" } },
        { TechLevel.Spacer, new List<string> { "Tungsten", "Plasteel", "Titanium", "StelliteAlloy", "NitinolAlloy", "PobediteAlloy" } },
    };

    private static void GenerateSteelLoot(SitePart sitePart)
    {
        TechLevel playerTech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
        TechLevel nextTech = (TechLevel)Mathf.Min((int)playerTech + 1, (int)TechLevel.Spacer);

        // Alloys from next tech level only
        if (!alloysByTech.TryGetValue(nextTech, out var pool))
            pool = alloysByTech[TechLevel.Medieval];

        // Resolve to ThingDefs
        var candidates = pool
            .Select(name => DefDatabase<ThingDef>.GetNamedSilentFail(name))
            .Where(d => d != null)
            .ToList();

        if (!candidates.Any())
        {
            Log.Warning("[HSKFaith] FaithStash: no alloy candidates found");
            return;
        }

        Log.Message($"[HSKFaith] FaithStash alloy candidates ({nextTech}, {candidates.Count}): {string.Join(", ", candidates.Select(d => d.defName))}");

        candidates.Shuffle();
        foreach (var alloy in candidates.Take(2))
        {
            Thing thing = ThingMaker.MakeThing(alloy);
            thing.stackCount = 250;
            sitePart.things.TryAdd(thing);
            Log.Message($"[HSKFaith] FaithStash: generated {alloy.defName} x{thing.stackCount}");
        }
    }
}
