using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

// Allow ability-based conversion to bypass Proselytizer check
[HarmonyPatch(typeof(CompAbilityEffect_Convert), nameof(CompAbilityEffect_Convert.Apply))]
public static class Patch_AbilityConvertFlag
{
    public static bool abilityConversion;

    public static void Prefix() => abilityConversion = true;
    public static void Postfix() => abilityConversion = false;
}

[HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoConversionAttempt))]
public static class Patch_IdeoConversion
{
    public static bool Prefix(Pawn_IdeoTracker __instance, float certaintyReduction, Ideo initiatorIdeo, ref bool __result)
    {
        var pawn = __instance.pawn;
        string method = Patch_AbilityConvertFlag.abilityConversion ? "Ability"
            : pawn != null && pawn.IsPrisonerOfColony ? "Warden"
            : pawn != null && pawn.InMentalState ? "MentalBreak"
            : "Social";
        string pawnName = pawn?.LabelShort ?? "?";
        string ideoName = initiatorIdeo?.name ?? "?";
        Log.Message($"[HSKFaith] CONVERSION: {method} | pawn={pawnName} | ideo={ideoName} | certainty={certaintyReduction:F2}");

        // Ability conversion (e.g. Moralist role) always allowed
        if (Patch_AbilityConvertFlag.abilityConversion)
            return true;

        // If initiator's ideology doesn't have Proselytizer meme — block conversion
        if (initiatorIdeo?.memes == null || !initiatorIdeo.memes.Any(m => m.defName == "Proselytizer"))
        {
            // Allow conversion of prisoners if fewer than 2 believers
            if (pawn != null && pawn.IsPrisonerOfColony)
            {
                int believers = PawnsFinder.AllMaps_FreeColonists.Count(p => !p.IsSlave && p.Ideo == initiatorIdeo);
                if (believers < 2)
                    return true; // allow vanilla conversion
            }

            // Show mote on the pawn being converted
            if (pawn != null && pawn.Spawned)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "FT_ConversionBlocked".Translate(), Color.white);

            __result = false;
            return false; // skip vanilla
        }

        return true; // allow vanilla conversion
    }
}
