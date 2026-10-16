using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch]
public static class Patch_MemeTip
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(IdeoUIUtility), "GetMemeTip", new[] { typeof(MemeDef), typeof(Ideo) });
    }

    public static void Postfix(MemeDef meme, ref string __result)
    {
        if (!Patch_ChooseMemes.useCustomView) return;

        var ext = meme.GetModExtension<MemeEffectExtension>();
        if (ext == null) return;

        // Replace vanilla text with our description
        string gameplayKey = "FT_MemeGameplay_" + meme.defName;
        if (gameplayKey.CanTranslate())
        {
            __result = meme.LabelCap.Colorize(ColoredText.TipSectionTitleColor)
                + "\n\n" + gameplayKey.Translate().Resolve();
        }

        string block = "";

        // Effects
        block += "\n\n" + "FT_MemeEffects".Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor);

        if (ext.statOffsets != null)
            foreach (var mod in ext.statOffsets)
                block += "\n  " + mod.stat.LabelCap.Resolve() + ": " + mod.stat.ValueToString(mod.value, ToStringNumberSense.Offset);

        if (ext.statFactors != null)
            foreach (var mod in ext.statFactors)
            {
                int pct = Mathf.RoundToInt((mod.value - 1f) * 100f);
                block += "\n  " + mod.stat.LabelCap.Resolve() + ": " + (pct >= 0 ? "+" : "") + pct + "%";
            }

        if (ext.moodBonus != 0)
            block += "\n  " + "FT_MemeMood".Translate(ext.moodBonus.ToStringWithSign()).Resolve();

        if (!ext.passiveEffect.NullOrEmpty() && ext.passiveEffect.CanTranslate())
            block += "\n  " + ext.passiveEffect.Translate().Resolve();

        if (ext.startingItems != null && ext.startingItems.Count > 0)
        {
            block += "\n  " + "FT_StartingItems".Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor);
            foreach (var item in ext.startingItems)
                if (item.thingDef != null)
                    block += "\n    " + item.thingDef.LabelCap.Resolve() + " x" + item.count;
        }

        if (ext.startingResearchProjects != null && ext.startingResearchProjects.Count > 0)
        {
            block += "\n  " + "FT_StartingResearch".Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor);
            foreach (var proj in ext.startingResearchProjects)
                if (proj != null)
                    block += "\n    " + proj.LabelCap.Resolve();
        }

        if (ext.yearlyGoodwillChange != 0)
            block += "\n  " + "FT_MemeGoodwill".Translate(ext.yearlyGoodwillChange.ToStringWithSign()).Resolve();

        // Biome stat bonuses
        if (ext.biomeStatBonuses != null && ext.biomeStatBonuses.Count > 0)
        {
            foreach (var bsb in ext.biomeStatBonuses)
            {
                if (bsb.statOffsets == null || bsb.biomes == null) continue;
                foreach (var mod in bsb.statOffsets)
                    block += "\n  " + mod.stat.LabelCap.Resolve() + ": " + mod.stat.ValueToString(mod.value, ToStringNumberSense.Offset) + " (" + string.Join(", ", bsb.biomes) + ")";
            }
        }

        // Hidden/bonus mechanics
        string hiddenKey = "FT_MemeHidden_" + meme.defName;
        if (hiddenKey.CanTranslate())
        {
            string hiddenText = hiddenKey.Translate().Resolve().Replace("\n", "\n  ");
            block += "\n  " + hiddenText;
        }

        // Faith generation range (calculated for 4 memes)
        if (ext.seasonalFaithChange != 0 || ext.MechanicMaxFaith != 0 || ext.penaltyPerSection != 0)
        {
            int minFaith = ext.seasonalFaithChange + 4 * ext.penaltyPerSection; // all empty
            int maxFaith = ext.seasonalFaithChange + 4 * ext.MechanicMaxFaith; // all filled
            string minStr = (minFaith >= 0 ? "+" : "") + minFaith;
            string maxStr = (maxFaith >= 0 ? "+" : "") + maxFaith;
            string faithStr = minFaith == maxFaith ? minStr : minStr + " .. " + maxStr;
            block += "\n  " + "FT_FaithGeneration".Translate(faithStr).Resolve();
        }

        // Certainty per season
        if (ext.certaintyPerSeason != 0f)
        {
            string certStr = (ext.certaintyPerSeason > 0 ? "+" : "") + (ext.certaintyPerSeason * 100f).ToString("F0") + "%";
            block += "\n  " + "FT_CertaintyPerSeason".Translate(certStr).Resolve();
        }


        // Unlocked roles (skip for xenophobia — specialists are not relevant)
        if (meme.defName != "HAR_Xenophobia")
        {
            var roles = meme.UnlockedRoles(null);
            if (roles != null && roles.Count > 0)
            {
                block += "\n\n" + "FT_MemeRoles".Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor);
                foreach (var role in roles)
                    block += "\n  " + role;
            }
        }

        // Recommendations
        string recKey = "FT_MemeRecommend_" + meme.defName;
        if (recKey.CanTranslate())
        {
            string recText = recKey.Translate().Resolve();
            if (!recText.NullOrEmpty())
            {
                block += "\n\n" + "FT_MemeRecommend".Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor);
                block += "\n" + recText;
            }
        }

        // Gender colonist count for supremacy memes
        if (meme.defName == "MaleSupremacy" || meme.defName == "FemaleSupremacy")
        {
            int males = 0, females = 0;
            foreach (var p in VersionCompat.FreeColonistsEverywhere)
            {
                if (p.gender == Gender.Male) males++;
                else if (p.gender == Gender.Female) females++;
            }
            bool isMale = meme.defName == "MaleSupremacy";
            int dominant = isMale ? males : females;
            int other = isMale ? females : males;
            string dominantSymbol = isMale ? "♂" : "♀";
            string otherSymbol = isMale ? "♀" : "♂";

            block += "\n\n" + "FT_XenoColonists".Translate();
            block += "\n  " + (dominantSymbol + " " + dominant).Colorize(new Color(0.4f, 0.9f, 0.4f));
            block += "\n  " + otherSymbol + " " + other;

            // Pastor gender check
            var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (ideo != null)
            {
                foreach (var precept in ideo.PreceptsListForReading)
                {
                    if (precept is Precept_RoleSingle role && role.def.defName == "IdeoRole_Moralist")
                    {
                        var pastor = role.ChosenPawnSingle();
                        if (pastor != null)
                        {
                            bool rightGender = isMale ? pastor.gender == Gender.Male : pastor.gender == Gender.Female;
                            string genderStr = pastor.gender == Gender.Male ? "FT_GenderMale".Translate() : "FT_GenderFemale".Translate();
                            string pastorStr = "FT_PastorGender".Translate(pastor.LabelShortCap, genderStr);
                            block += "\n\n" + (rightGender ? pastorStr : pastorStr.Colorize(new Color(0.95f, 0.4f, 0.4f)));
                        }
                        break;
                    }
                }
            }
        }

        // Race count for xenophilia/xenophobia memes
        if (meme.defName == "HAR_Xenophilia" || meme.defName == "HAR_Xenophobia")
        {
            var raceCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var p in VersionCompat.FreeColonistsEverywhere)
            {
                string race = p.def.label ?? p.def.defName;
                if (raceCounts.ContainsKey(race)) raceCounts[race]++;
                else raceCounts[race] = 1;
            }
            if (raceCounts.Count > 0)
            {
                bool isPhilia = meme.defName == "HAR_Xenophilia";
                block += "\n\n" + "FT_XenoColonists".Translate();
                foreach (var kvp in raceCounts)
                {
                    string countStr = kvp.Value.ToString();
                    // For xenophilia highlight minorities, for xenophobia highlight majority
                    bool highlight = isPhilia ? !IsMajority(kvp.Value, raceCounts) : IsMajority(kvp.Value, raceCounts);
                    string line = highlight
                        ? (kvp.Key.CapitalizeFirst() + ": " + countStr).Colorize(new Color(0.4f, 0.9f, 0.4f))
                        : kvp.Key.CapitalizeFirst() + ": " + countStr;
                    block += "\n  " + line;
                }
            }
        }

        if (block.Length > 0)
            __result += block;
    }

    private static bool IsMajority(int count, System.Collections.Generic.Dictionary<string, int> raceCounts)
    {
        foreach (var kvp in raceCounts)
            if (kvp.Value > count) return false;
        return true;
    }
}
