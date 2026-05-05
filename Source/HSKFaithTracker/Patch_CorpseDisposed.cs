using System.Linq;
using HarmonyLib;
using Verse;

namespace HSKFaithTracker;

[HarmonyPatch(typeof(Corpse), nameof(Corpse.Destroy))]
public static class Patch_CorpseDisposed
{
    public static void Prefix(Corpse __instance)
    {
        if (!__instance.InnerPawn.RaceProps.Humanlike)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        // Remove corpse penalty record if exists
        int corpseId = __instance.thingIDNumber;
        var record = comp.Records.FirstOrDefault(r =>
            r.type == RitualRecordType.CorpsePenalty && r.thingId == corpseId);

        if (record.thingId == corpseId && record.type == RitualRecordType.CorpsePenalty)
        {
            comp.Records.Remove(record);
        }
    }
}
