using RimWorld;
using Verse;

namespace HSKFaithTracker;

public class ThoughtWorker_HereticSelf : ThoughtWorker
{
    public override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (!ModsConfig.IdeologyActive)
            return ThoughtState.Inactive;

        if (p.Faction != Faction.OfPlayer || p.IsQuestLodger())
            return ThoughtState.Inactive;

        var playerIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (playerIdeo == null)
            return ThoughtState.Inactive;

        if (p.Ideo == null || p.Ideo == playerIdeo)
            return ThoughtState.Inactive;

        return ThoughtState.ActiveAtStage(0);
    }
}
