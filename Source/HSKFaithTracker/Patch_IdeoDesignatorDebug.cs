using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(DesignationCategoryDef), nameof(DesignationCategoryDef.AllResolvedAndIdeoDesignators), MethodType.Getter)]
public static class Patch_IdeoDesignatorDebug
{
    private static bool logged;

    public static void Prefix(DesignationCategoryDef __instance)
    {
        if (logged || !ModsConfig.IdeologyActive) return;
        if (Faction.OfPlayer?.ideos == null) return;

        foreach (var ideo in Faction.OfPlayer.ideos.AllIdeos)
        {
            for (int i = 0; i < ideo.PreceptsListForReading.Count; i++)
            {
                var precept = ideo.PreceptsListForReading[i];
                if (precept is Precept_ThingDef ptd && (precept is Precept_Building || precept is Precept_RitualSeat))
                {
                    var td = ptd.ThingDef;
                    string comps = "none";
                    if (td?.comps != null)
                    {
                        var names = new List<string>();
                        foreach (var c in td.comps)
                            names.Add(c?.compClass?.Name ?? "NULL_COMP");
                        comps = string.Join(", ", names);
                    }
                    // Log.Message($"[HSKFaith] IdeoBuilding: ideo={ideo.name} precept={precept.def.defName} thingDef={td?.defName ?? "NULL"} category={td?.designationCategory?.defName ?? "NULL"} comps=[{comps}]");

                    if (td == null || td.comps == null || comps.Contains("NULL_COMP"))
                    {
                        string problem = td == null ? "ThingDef is NULL" : "has NULL comp";
                        Log.Error($"[HSKFaith] PROBLEM: precept={precept.def.defName} {problem}");
                        Find.LetterStack.ReceiveLetter(
                            "Ideology building error",
                            $"Precept '{precept.def.defName}' has invalid building data ({problem}). This may cause crashes with PerformanceFish.\n\nThingDef: {td?.defName ?? "NULL"}\nComps: [{comps}]",
                            LetterDefOf.ThreatSmall);
                    }
                }
            }
        }
        logged = true;
    }
}
