using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Need_Faith : Need
{
    private static Texture2D cachedInfoIcon;
    private static Texture2D InfoIcon => cachedInfoIcon ??= ContentFinder<Texture2D>.Get("UI/Buttons/InfoButton", true);

    public Need_Faith(Pawn pawn) : base(pawn)
    {
    }

    private bool IsGuest => pawn.Faction != Faction.OfPlayer || pawn.IsQuestLodger();

    public override bool ShowOnNeedList
    {
        get
        {
            if (IsGuest)
                return false;
            if (!ModsConfig.IdeologyActive)
                return false;
            if (pawn.Ideo == null)
                return false;
            if (pawn.Ideo != Faction.OfPlayer?.ideos?.PrimaryIdeo)
                return false;
            return true;
        }
    }

    public override int GUIChangeArrow => 0;

    public override void SetInitialLevel()
    {
        CurLevel = 0.5f;
    }

    public override void NeedInterval()
    {
        if (!ShowOnNeedList || IsGuest)
            return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        float score = Mathf.Clamp(comp.Score, GameComponent_FaithTracker.ScoreMin, GameComponent_FaithTracker.ScoreMax);
        CurLevel = Mathf.InverseLerp(GameComponent_FaithTracker.ScoreMin, GameComponent_FaithTracker.ScoreMax, score);
    }

    public override string GetTipString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(def.LabelCap);
        sb.AppendLine();
        sb.AppendLine(def.description);

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null && comp.Records.Count > 0)
        {
            int fulfilled = 0, missed = 0;
            foreach (var r in comp.Records)
            {
                if (r.type == RitualRecordType.Fulfilled) fulfilled++;
                else missed++;
            }
            sb.AppendLine();
            sb.AppendLine("FT_TipStats".Translate(fulfilled, missed, comp.Score));
        }

        return sb.ToString();
    }

    public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = int.MaxValue,
        float customMargin = -1f, bool drawArrows = true, bool doTooltip = true,
        Rect? rectForTooltip = null, bool drawLabel = true)
    {
        float margin = customMargin >= 0f ? customMargin : 29f;

        Rect btnRect = new Rect(rect.x + margin, rect.y + rect.height - 10f, 16f, 16f);
        GUI.DrawTexture(btnRect, InfoIcon, ScaleMode.ScaleToFit);
        if (Widgets.ButtonInvisible(btnRect))
        {
            Find.WindowStack.Add(new Dialog_FaithInfo(pawn));
        }

        base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows, doTooltip, rectForTooltip, drawLabel);
    }

    public override void ExposeData()
    {
        base.ExposeData();
    }
}
