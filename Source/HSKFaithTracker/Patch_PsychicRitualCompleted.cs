using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HSKFaithTracker;

/// <summary>
/// +2 ritualistPoints when a psychic ritual succeeds.
/// Hooks into PsychicRitual.CancelPsychicRitual — if NOT canceled and succeeded, award points.
/// We also check on Lord removal since successful rituals remove their lord.
/// </summary>
[HarmonyPatch(typeof(PsychicRitualToil), nameof(PsychicRitualToil.End))]
public static class Patch_PsychicRitualCompleted
{
    private static readonly HashSet<int> recordedRituals = new HashSet<int>();

    public static void Postfix(PsychicRitual psychicRitual, bool success)
    {
        if (!success) return;
        if (psychicRitual == null) return;
        if (!psychicRitual.succeeded) return;

        // One reward per ritual instance
        int id = psychicRitual.GetHashCode();
        if (recordedRituals.Contains(id)) return;
        recordedRituals.Add(id);

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Ritualist")) return;

        comp.ritualistPoints += 2;
        string name = psychicRitual.def?.LabelCap ?? "unknown";
        Log.Message($"[HSKFaith] Ritualist: +2 points from psychic ritual '{name}' (total {comp.ritualistPoints})");
    }
}
