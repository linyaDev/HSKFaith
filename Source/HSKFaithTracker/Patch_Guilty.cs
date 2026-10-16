using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

// Wound tracking: +1 guiltyWounds per colonist injury
[HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
public static class Patch_Guilty
{
    public static void Postfix(Pawn __instance, DamageInfo dinfo)
    {
        if (__instance.Dead || __instance.Faction != Faction.OfPlayer)
            return;

        if (!__instance.RaceProps.Humanlike || __instance.IsSlave)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
            comp.guiltyWounds++;
    }
}

// Charity event tracking: fulfilled +20, refused -10, betrayed -30
[HarmonyPatch(typeof(HistoryEventsManager), nameof(HistoryEventsManager.RecordEvent))]
public static class Patch_GuiltyCharity
{
    public static void Postfix(HistoryEvent historyEvent)
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Guilty")) return;

        string defName = historyEvent.def.defName;

        if (defName.StartsWith("CharityRefused_") && defName.Contains("_Betrayed"))
            comp.guiltyCharityPoints -= 30;
        else if (defName.StartsWith("CharityRefused_"))
            comp.guiltyCharityPoints -= 10;
        else if (defName.StartsWith("CharityFulfilled_"))
            comp.guiltyCharityPoints += 20;
    }
}
