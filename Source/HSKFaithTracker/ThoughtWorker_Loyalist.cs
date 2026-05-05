using RimWorld;
using Verse;

namespace HSKFaithTracker;

public class ThoughtWorker_Loyalist : ThoughtWorker
{
    public override ThoughtState CurrentStateInternal(Pawn p)
    {
        var ideo = p.Ideo;
        if (ideo == null || !ideo.memes.Any(m => m.defName == "Loyalist"))
            return ThoughtState.Inactive;

        return ThoughtState.ActiveAtStage(0);
    }
}
