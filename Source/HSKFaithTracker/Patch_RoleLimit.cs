using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(IdeoFoundation), nameof(IdeoFoundation.CanAdd))]
public static class Patch_RoleLimit
{
    public static void Postfix(ref AcceptanceReport __result, IdeoFoundation __instance, PreceptDef precept)
    {
        // Remove the 2 multi-role limit, but only if requiredMemes are satisfied
        if (!__result.Accepted
            && precept.preceptClass == typeof(Precept_RoleMulti)
            && __instance.ideo.PreceptsListForReading.Count(p => p is Precept_RoleMulti && p.def.visible) >= 2)
        {
            // Check requiredMemes — don't unlock roles that require memes we don't have
            if (precept.requiredMemes != null && precept.requiredMemes.Count > 0)
            {
                bool hasRequiredMeme = precept.requiredMemes.Any(m => __instance.ideo.memes.Contains(m));
                if (!hasRequiredMeme)
                    return; // keep rejected — missing required meme
            }
            __result = true;
        }
    }
}
