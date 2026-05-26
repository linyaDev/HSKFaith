using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_ColonistKilled
{
    public static void Prefix(Pawn __instance, DamageInfo? dinfo)
    {
        if (__instance?.Faction != Faction.OfPlayer || !__instance.IsColonist)
            return;

        Pawn killer = dinfo?.Instigator as Pawn;
        if (killer == null || killer.Faction != Faction.OfPlayer || !killer.IsColonist)
            return;

        if (killer == __instance)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        comp?.RecordRitual("FT_ColonistKilled".Translate(), RitualRecordType.FaithDecay, customWeight: -5);
    }
}
