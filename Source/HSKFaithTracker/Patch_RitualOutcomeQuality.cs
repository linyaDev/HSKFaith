using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace HSKFaithTracker;

// Captures the positivity of the ritual outcome that was just rolled, so the
// faith-recording patches can withhold faith when the outcome is "bad"
// (RitualOutcomePossibility.positivityIndex < 0, e.g. Terrible/Boring/Lackluster).
// GetOutcome runs inside LordJob_Ritual.ApplyOutcome BEFORE faith is recorded by
// either the obligation patch or the completion patch, so a static is enough.
// Patches FromQuality plus the two overriding workers (ChildBirth, Trial).
[HarmonyPatch]
public static class Patch_RitualOutcomeQuality
{
    public static bool outcomeKnown;
    public static bool lastOutcomeNegative;

    public static void Reset()
    {
        outcomeKnown = false;
        lastOutcomeNegative = false;
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(RitualOutcomeEffectWorker_FromQuality), nameof(RitualOutcomeEffectWorker_FromQuality.GetOutcome));
        yield return AccessTools.Method(typeof(RitualOutcomeEffectWorker_ChildBirth), "GetOutcome");
        yield return AccessTools.Method(typeof(RitualOutcomeEffectWorker_Trial), "GetOutcome");
    }

    public static void Postfix(RitualOutcomePossibility __result)
    {
        outcomeKnown = true;
        lastOutcomeNegative = __result != null && !__result.Positive;
    }
}
