using Verse;

namespace HSKFaithTracker;

public class Hediff_BlindsightTrueSight : HediffWithComps
{
    public override string TipStringExtra
    {
        get
        {
            if (Severity >= 1.0f) return null;

            var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
            int daysLeft = comp != null ? 15 - comp.TrueSightDayCounter : 15;
            if (daysLeft <= 0) daysLeft = 15;
            return "FT_TrueSightNext".Translate(daysLeft);
        }
    }
}
