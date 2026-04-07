using RimWorld;
using Verse;

namespace HSKFaithTracker;

public class ThoughtWorker_Faith : ThoughtWorker
{
    public override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (!ModsConfig.IdeologyActive)
            return ThoughtState.Inactive;

        var need = p.needs?.TryGetNeed<Need_Faith>();
        if (need == null || !need.ShowOnNeedList)
            return ThoughtState.Inactive;

        float level = need.CurLevel;

        if (level <= 0.14f) return ThoughtState.ActiveAtStage(0);  // -6
        if (level <= 0.28f) return ThoughtState.ActiveAtStage(1);  // -4
        if (level <= 0.42f) return ThoughtState.ActiveAtStage(2);  // -2
        if (level <= 0.57f) return ThoughtState.ActiveAtStage(3);  // 0
        if (level <= 0.71f) return ThoughtState.ActiveAtStage(4);  // +2
        if (level <= 0.85f) return ThoughtState.ActiveAtStage(5);  // +4
        return ThoughtState.ActiveAtStage(6);                      // +6
    }
}
