using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValueUnfinalized))]
public static class Patch_MemeStatOffset
{
    public static void Postfix(StatWorker __instance, StatRequest req, ref float __result)
    {
        if (!req.HasThing || req.Thing is not Pawn pawn || pawn.Ideo == null)
            return;

        var memes = pawn.Ideo.memes;
        if (memes == null) return;

        var stat = __instance.stat;
        string biomeName = Find.CurrentMap?.Biome?.defName;

        for (int i = 0; i < memes.Count; i++)
        {
            var ext = memes[i].GetModExtension<MemeEffectExtension>();
            if (ext == null) continue;

            if (ext.statOffsets != null)
                __result += ext.statOffsets.GetStatOffsetFromList(stat);

            if (ext.statFactors != null)
            {
                float factor = ext.statFactors.GetStatFactorFromList(stat);
                if (factor != 1f)
                    __result *= factor;
            }

            // Biome-conditional bonuses
            if (ext.biomeStatBonuses != null && biomeName != null)
            {
                for (int j = 0; j < ext.biomeStatBonuses.Count; j++)
                {
                    var bsb = ext.biomeStatBonuses[j];
                    if (bsb.biomes != null && bsb.biomes.Contains(biomeName) && bsb.statOffsets != null)
                        __result += bsb.statOffsets.GetStatOffsetFromList(stat);
                }
            }
        }
    }
}
