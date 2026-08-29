using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Dialog_FaithHistory : Window
{
    private Vector2 scrollPosition;

    private static readonly Color GreenBg = new Color(0.2f, 0.5f, 0.2f, 0.3f);
    private static readonly Color RedBg = new Color(0.5f, 0.2f, 0.2f, 0.3f);
    private static readonly Color GreenText = new Color(0.4f, 0.95f, 0.4f);
    private static readonly Color RedText = new Color(0.95f, 0.4f, 0.4f);
    private static readonly Color DimText = new Color(1f, 1f, 1f, 0.5f);

    public override Vector2 InitialSize => new Vector2(420f, 450f);

    public Dialog_FaithHistory()
    {
        doCloseButton = true;
        doCloseX = true;
        draggable = true;
        absorbInputAroundWindow = false;
        focusWhenOpened = false;
        preventCameraMotion = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "FT_HistoryTitle".Translate());
        Text.Font = GameFont.Small;

        float y = 40f;

        var records = comp.Records;
        float listHeight = records.Count * 30f;
        Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
        Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, listHeight);

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

        float rowY = 0f;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            var r = records[i];
            Rect rowRect = new Rect(0f, rowY, viewRect.width, 28f);

            bool isFulfilled = r.type == RitualRecordType.Fulfilled;
            int points = r.Points;
            string pointsStr = points > 0 ? "+" + points : points.ToString();

            Widgets.DrawBoxSolid(rowRect, isFulfilled ? GreenBg : RedBg);
            if (i % 2 == 0)
                Widgets.DrawLightHighlight(rowRect);

            GUI.color = isFulfilled ? GreenText : RedText;
            Widgets.Label(new Rect(5f, rowY, 20f, 28f), isFulfilled ? "▲" : "▼");

            GUI.color = Color.white;
            Widgets.Label(new Rect(25f, rowY, viewRect.width - 170f, 28f), r.ritualName);

            GUI.color = isFulfilled ? GreenText : RedText;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(viewRect.width - 140f, rowY, 50f, 28f), pointsStr);

            GUI.color = DimText;
            Text.Anchor = TextAnchor.MiddleRight;
            int daysAgo = (Find.TickManager.TicksGame - r.tick) / 60000;
            string timeStr = daysAgo <= 0 ? "FT_Today".Translate().RawText : "FT_DaysAgo".Translate(daysAgo).RawText;
            Text.WordWrap = false;
            Widgets.Label(new Rect(viewRect.width - 85f, rowY, 80f, 28f), timeStr);
            Text.WordWrap = true;

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            rowY += 30f;
        }

        Widgets.EndScrollView();
    }
}
