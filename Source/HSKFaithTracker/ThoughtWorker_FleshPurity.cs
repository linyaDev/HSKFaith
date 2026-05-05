using RimWorld;
using Verse;

namespace HSKFaithTracker;

public class ThoughtWorker_FleshPurity : ThoughtWorker
{
    public override ThoughtState CurrentStateInternal(Pawn p)
    {
        var ideo = p.Ideo;
        if (ideo == null || !ideo.memes.Any(m => m.defName == "FleshPurity"))
            return ThoughtState.Inactive;

        // Only give mood if pawn has no implants
        foreach (var h in p.health.hediffSet.hediffs)
        {
            if (h is Hediff_AddedPart || h is Hediff_Implant)
                return ThoughtState.Inactive;
        }

        return ThoughtState.ActiveAtStage(0);
    }
}
