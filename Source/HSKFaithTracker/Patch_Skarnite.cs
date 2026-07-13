using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// Patch ReviaRace Utils.PostSacrifide to grant skarnite faith points.
/// Manage SoulreapNerf hediff for Revias without Skarnite meme.
/// Only applies if ReviaRace mod is loaded.
/// </summary>
public static class Patch_Skarnite
{
    private static bool patched;
    public static bool reviaLoaded;

    private static readonly string[] soulreapDefNames = new[]
    {
        "ReviaRaceSoulreapTier1", "ReviaRaceSoulreapTier2", "ReviaRaceSoulreapTier3",
        "ReviaRaceSoulreapTier4", "ReviaRaceSoulreapTier5", "ReviaRaceSoulreapTier6",
        "ReviaRaceSoulreapTier7", "ReviaRaceSoulreapTier8", "ReviaRaceSoulreapTier9"
    };

    public static void TryPatch(Harmony harmony)
    {
        if (patched) return;
        patched = true;

        var utilsType = AccessTools.TypeByName("ReviaRace.Utils");
        if (utilsType == null) return;

        reviaLoaded = true;

        var method = AccessTools.Method(utilsType, "PostSacrifide");
        if (method == null) return;

        var postfix = new HarmonyMethod(typeof(Patch_Skarnite), nameof(PostSacrifide_Postfix));
        harmony.Patch(method, postfix: postfix);
        Log.Message("[HSKFaith] Patched ReviaRace.Utils.PostSacrifide for Skarnite meme");
    }

    public static void PostSacrifide_Postfix(bool corpse)
    {
        if (!corpse) return; // only corpse sacrifice counts

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;
        if (!comp.HasMeme("ReviaRaceSkarniteMeme")) return;

        comp.skarnitePoints++;
    }

    /// <summary>
    /// Called daily from GameComponent. Updates SoulreapNerf hediff on all colonists.
    /// If pawn has SoulReap but colony doesn't have Skarnite meme → apply nerf.
    /// If colony has Skarnite meme → remove nerf.
    /// </summary>
    public static void UpdateSoulreapNerfs()
    {
        if (!reviaLoaded) return;

        var nerfDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_SoulreapNerf");
        if (nerfDef == null) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        bool hasSkarnite = comp?.HasMeme("ReviaRaceSkarniteMeme") == true;

        foreach (var map in Find.Maps)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.IsSlave) continue;

                int tier = GetSoulreapTier(p);
                var existing = p.health.hediffSet.GetFirstHediffOfDef(nerfDef);

                if (hasSkarnite || tier == 0)
                {
                    // Remove nerf if has meme or no soulreap
                    if (existing != null)
                        p.health.RemoveHediff(existing);
                }
                else
                {
                    // Apply/update nerf
                    float severity = tier * 0.1f;
                    if (existing == null)
                    {
                        var h = HediffMaker.MakeHediff(nerfDef, p);
                        h.Severity = severity;
                        p.health.AddHediff(h);
                    }
                    else
                    {
                        existing.Severity = severity;
                    }
                }
            }
        }
    }

    public static int GetSoulreapTier(Pawn p)
    {
        for (int i = soulreapDefNames.Length - 1; i >= 0; i--)
        {
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(soulreapDefNames[i]);
            if (def != null && p.health.hediffSet.HasHediff(def))
                return i + 1;
        }
        return 0;
    }

    /// <summary>Sum of all Soul Reap tiers (tails) across all free colonist Revias.</summary>
    public static int GetTotalTails()
    {
        if (!reviaLoaded) return 0;
        int total = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            total += GetSoulreapTier(p);
        }
        return total;
    }
}
