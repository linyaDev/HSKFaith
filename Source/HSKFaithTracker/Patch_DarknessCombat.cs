using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
public static class Patch_DarknessCombat
{
    public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
    {
        if (totalDamageDealt <= 0f) return;

        // Attacker must be a player colonist
        var attacker = dinfo.Instigator as Pawn;
        if (attacker == null || attacker.Faction != Faction.OfPlayer || attacker.IsSlave)
            return;

        // Target must be hostile
        if (__instance.Faction == Faction.OfPlayer)
            return;

        // Check if attacker is in darkness
        if (attacker.Map == null) return;
        float glow = attacker.Map.glowGrid.GroundGlowAt(attacker.Position);
        if (glow >= 0.3f) return;

        // Add darkness combat points
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Darkness")) return;

        comp.darknessPoints++;
    }
}
