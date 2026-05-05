using Verse;

namespace HSKFaithTracker;

public class Hediff_BlindsightPsylink : HediffWithComps
{
    private const float DailyGain = 1f / 60f;

    public override string TipStringExtra
    {
        get
        {
            if (Severity >= 1.0f) return null;

            float remaining = 1.0f - Severity;
            int daysLeft = (int)(remaining / DailyGain) + 1;
            return "FT_PsylinkNext".Translate(daysLeft);
        }
    }
}
