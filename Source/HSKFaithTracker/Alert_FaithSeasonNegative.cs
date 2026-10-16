using RimWorld;
using Verse;

namespace HSKFaithTracker;

public class Alert_FaithSeasonNegative : Alert
{
    public Alert_FaithSeasonNegative()
    {
        defaultLabel = "FT_AlertSeasonNegative".Translate();
        defaultPriority = AlertPriority.Medium;
    }

    public override TaggedString GetExplanation()
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return "";
        return "FT_AlertSeasonNegativeDesc".Translate(comp.SeasonForecast);
    }

    public override AlertReport GetReport()
    {
        if (!ModsConfig.IdeologyActive) return false;
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        return comp != null && comp.SeasonForecast < 0;
    }
}
