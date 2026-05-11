using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Alert_RaiderProtection : Alert
{
    public Alert_RaiderProtection()
    {
        defaultLabel = "FT_RaiderProtection".Translate();
        defaultPriority = AlertPriority.Medium;
    }

    public override Color BGColor => new Color(0.2f, 0.6f, 0.2f, 0.35f);

    public override TaggedString GetExplanation()
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return "";

        int ticksLeft = comp.raiderProtectionUntilTick - Find.TickManager.TicksGame;
        float daysLeft = ticksLeft / 60000f;
        return "FT_RaiderProtectionDesc".Translate(daysLeft.ToString("F1"));
    }

    public override AlertReport GetReport()
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return false;

        return Find.TickManager.TicksGame < comp.raiderProtectionUntilTick;
    }
}
