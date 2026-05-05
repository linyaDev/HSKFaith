using RimWorld;
using Verse;

namespace HSKFaithTracker;

public class ThoughtWorker_Heretics : ThoughtWorker
{
    public override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (!ModsConfig.IdeologyActive)
            return ThoughtState.Inactive;

        if (p.Faction != Faction.OfPlayer || p.IsQuestLodger())
            return ThoughtState.Inactive;

        var playerIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (playerIdeo == null || p.Ideo != playerIdeo)
            return ThoughtState.Inactive;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null || comp.cachedHereticCount <= 0)
            return ThoughtState.Inactive;

        int stage = System.Math.Min(comp.cachedHereticCount - 1, def.stages.Count - 1);
        return ThoughtState.ActiveAtStage(stage);
    }
}
