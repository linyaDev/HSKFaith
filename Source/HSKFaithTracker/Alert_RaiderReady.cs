using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Alert_RaiderReady : Alert
{
    public Alert_RaiderReady()
    {
        defaultLabel = "FT_RaiderReady".Translate();
        defaultExplanation = "FT_RaiderReadyDesc".Translate();
        defaultPriority = AlertPriority.Critical;
    }

    public override Color BGColor => new Color(0.2f, 0.5f, 0.7f, 0.35f);

    public override AlertReport GetReport()
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || !comp.HasMeme("Raider")) return false;

        int now = Find.TickManager.TicksGame;

        // Not on cooldown and not already protected
        return now >= comp.raiderProtectionCooldownUntilTick
            && now >= comp.raiderProtectionUntilTick;
    }
}
