using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Alert_FaithHigh : Alert
{
    private const int Threshold = 50;

    public Alert_FaithHigh()
    {
        defaultLabel = "FT_AlertFaithHigh".Translate();
        defaultPriority = AlertPriority.Medium;
    }

    public override Color BGColor => new Color(0.2f, 0.5f, 0.2f, 0.35f);

    public override TaggedString GetExplanation()
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return "";
        return "FT_AlertFaithHighDesc".Translate(comp.Score, GameComponent_FaithTracker.ScoreMax);
    }

    public override AlertReport GetReport()
    {
        if (!ModsConfig.IdeologyActive) return false;
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        return comp != null && comp.Score >= Threshold;
    }
}
