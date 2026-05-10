using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

public class MemeEffectExtension : DefModExtension
{
    public List<StatModifier> statOffsets;
    public List<StatModifier> statFactors;
    public List<BiomeStatBonus> biomeStatBonuses;
    public int yearlyGoodwillChange;
    public int seasonalFaithChange;
    public int penaltyPerSection;
    public int sectionsPerMeme;
    public int faithPerSection; // positive = bonus per meme section each season (e.g. Nudism +2)
    public int MechanicMinFaith => penaltyPerSection;
    public int MechanicMaxFaith => faithPerSection;

    public int ComputeSeasonTotal(int filled, int unfilled)
    {
        int bonus = faithPerSection;
        int penalty = penaltyPerSection;
        return seasonalFaithChange + bonus * filled + penalty * unfilled;
    }
    public int moodBonus; // mood bonus from thought (for tooltip display)
    public string passiveEffect; // translation key for passive effect shown in tooltip
    public List<MemeDef> conflictingMemes;
    public List<MemeDef> synergyMemes;
    public string memeGroup;
    public float certaintyPerSeason; // certainty bonus/penalty per season (e.g. 0.05 = +5%)
    public List<ThingDefCountClass> startingItems;
}

public class BiomeStatBonus
{
    public List<string> biomes;
    public List<StatModifier> statOffsets;
}
