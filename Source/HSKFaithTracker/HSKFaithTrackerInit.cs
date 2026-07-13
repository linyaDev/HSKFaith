using HarmonyLib;
using Verse;

namespace HSKFaithTracker;

[StaticConstructorOnStartup]
public static class HSKFaithTrackerInit
{
    static HSKFaithTrackerInit()
    {
        var harmony = new Harmony("linya.hskfaithtracker");
        harmony.PatchAll();
        Patch_Skarnite.TryPatch(harmony);
        Log.Message("[HSKFaithTracker] Patches applied.");
    }
}
