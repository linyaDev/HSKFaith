using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

// Taming
[HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.DoRecruit), typeof(Pawn), typeof(Pawn), typeof(bool))]
public static class Patch_RancherTame
{
    public static void Postfix(Pawn recruiter, Pawn recruitee)
    {
        if (recruitee == null || !recruitee.RaceProps.Animal)
            return;
        if (recruiter == null || recruiter.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
        {
            int pts = System.Math.Max(1, (int)(recruitee.RaceProps.baseBodySize * 2));
            comp.AddRancherEvent(recruitee.def.LabelCap, pts);

            // AnimalPersonhood: auto-enable follow when drafted + assign master
            if (comp.HasMeme("AnimalPersonhood"))
            {
                if (recruitee.playerSettings == null)
                    recruitee.playerSettings = new Pawn_PlayerSettings(recruitee);
                recruitee.playerSettings.followDrafted = true;
                if (recruitee.playerSettings.Master == null && recruiter != null)
                    recruitee.playerSettings.Master = recruiter;
                // Log.Message($"[FaithTracker] AnimalPersonhood: {recruitee.LabelShortCap} followDrafted=true, master={recruitee.playerSettings.Master?.LabelShortCap ?? "none"}");
            }
        }
    }
}

// Live birth
[HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.DoBirthSpawn))]
public static class Patch_RancherBirth
{
    public static void Postfix(Pawn mother)
    {
        if (mother == null || !mother.RaceProps.Animal)
            return;
        if (mother.Faction != Faction.OfPlayer)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
        {
            int pts = System.Math.Max(1, (int)(mother.RaceProps.baseBodySize * 2));
            comp.AddRancherEvent(mother.def.LabelCap, pts);
        }
    }
}

// Egg hatch
[HarmonyPatch(typeof(CompHatcher), nameof(CompHatcher.Hatch))]
public static class Patch_RancherHatch
{
    public static void Prefix(CompHatcher __instance)
    {
        var egg = __instance.parent;
        if (egg?.Map == null) return;

        if (!egg.Map.IsPlayerHome) return;

        if (__instance.hatcheeFaction != Faction.OfPlayer) return;

        var hatcherPawn = __instance.Props?.hatcherPawn;
        if (hatcherPawn == null) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
        {
            int ptsPerEgg = System.Math.Max(1, (int)(hatcherPawn.race.race.baseBodySize * 2));
            for (int i = 0; i < egg.stackCount; i++)
                comp.AddRancherEvent(hatcherPawn.LabelCap, ptsPerEgg);
        }
    }
}
