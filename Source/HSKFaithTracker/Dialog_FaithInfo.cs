using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Dialog_FaithInfo : Window
{
    private readonly Pawn pawn;
    private Vector2 scrollPosition;

    private static readonly Color GreenText = new Color(0.4f, 0.95f, 0.4f);
    private static readonly Color RedText = new Color(0.95f, 0.4f, 0.4f);
    private static readonly Color DimText = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color BlueText = new Color(0.6f, 0.8f, 1f);
    private static readonly Color RowBg = new Color(0.15f, 0.15f, 0.25f, 0.5f);

    public override Vector2 InitialSize => new Vector2(460f, 520f);

    public Dialog_FaithInfo(Pawn pawn)
    {
        this.pawn = pawn;
        doCloseButton = true;
        doCloseX = true;
        draggable = true;
        absorbInputAroundWindow = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        // Title
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "FT_Title".Translate());
        Text.Font = GameFont.Small;

        float y = 40f;

        // Stats
        int fulfilled = 0, missed = 0;
        foreach (var r in comp.Records)
        {
            if (r.type == RitualRecordType.Fulfilled) fulfilled++;
            else missed++;
        }

        int gained = fulfilled * GameComponent_FaithTracker.FulfilledWeight;
        int lost = missed * GameComponent_FaithTracker.MissedWeight;

        Rect statsRect = new Rect(0f, y, inRect.width, 50f);
        Widgets.DrawBoxSolid(statsRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));

        float thirdW = inRect.width / 3f;
        Text.Anchor = TextAnchor.MiddleCenter;

        GUI.color = GreenText;
        Widgets.Label(new Rect(0f, y + 2f, thirdW, 22f), "FT_Gained".Translate());
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, y + 22f, thirdW, 26f), "+" + gained);
        Text.Font = GameFont.Small;

        GUI.color = comp.Score >= 0 ? GreenText : RedText;
        Widgets.Label(new Rect(thirdW, y + 2f, thirdW, 22f), "FT_Score".Translate());
        Text.Font = GameFont.Medium;
        string scoreStr = comp.Score >= 0 ? "+" + comp.Score : comp.Score.ToString();
        Widgets.Label(new Rect(thirdW, y + 22f, thirdW, 26f), scoreStr);
        Text.Font = GameFont.Small;

        GUI.color = RedText;
        Widgets.Label(new Rect(thirdW * 2f, y + 2f, thirdW, 22f), "FT_Lost".Translate());
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(thirdW * 2f, y + 22f, thirdW, 26f), lost.ToString());
        Text.Font = GameFont.Small;

        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 56f;

        // History button
        Rect historyBtn = new Rect(inRect.width - 90f, y, 85f, 24f);
        if (Widgets.ButtonText(historyBtn, "FT_HistoryBtn".Translate()))
        {
            Find.WindowStack.Add(new Dialog_FaithHistory());
        }
        y += 28f;

        // === Rituals in scrollable area ===
        if (!ModsConfig.IdeologyActive)
            return;

        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null)
            return;

        // Split rituals
        var holidays = new List<Precept_Ritual>();
        var situational = new List<Precept_Ritual>();
        var seenNames = new HashSet<string>();

        foreach (var p in ideo.PreceptsListForReading)
        {
            if (!(p is Precept_Ritual r))
                continue;

            string name = r.LabelCap;
            if (seenNames.Contains(name))
                continue;
            seenNames.Add(name);

            bool hasDate = r.obligationTriggers != null &&
                r.obligationTriggers.Any(t => t is RitualObligationTrigger_Date);

            string triggers = r.obligationTriggers != null
                ? string.Join(", ", r.obligationTriggers.Select(t => t.GetType().Name))
                : "none";
            Log.Message($"[FaithTracker] {name} | hasDate: {hasDate} | isAnytime: {r.isAnytime} | triggers: {triggers} | type: {(hasDate ? "HOLIDAY" : "SITUATIONAL")}");

            if (hasDate)
                holidays.Add(r);
            else
                situational.Add(r);
        }

        // Scrollable content
        float holidayHeight = holidays.Count > 0 ? 26f + holidays.Count * 28f + 6f : 0f;
        float situationalHeight = situational.Count > 0 ? 26f + situational.Count * 28f : 0f;
        float totalHeight = holidayHeight + situationalHeight + 10f;

        Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
        Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, totalHeight);

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        float rowY = 0f;

        // Holidays
        if (holidays.Count > 0)
        {
            GUI.color = BlueText;
            Widgets.Label(new Rect(0f, rowY, viewRect.width, 24f), "FT_Holidays".Translate());
            GUI.color = Color.white;
            rowY += 26f;

            foreach (var ritual in holidays)
                rowY = DrawRitualRow(viewRect.width, rowY, ritual);
            rowY += 6f;
        }

        // Situational
        if (situational.Count > 0)
        {
            GUI.color = DimText;
            Widgets.Label(new Rect(0f, rowY, viewRect.width, 24f), "FT_Situational".Translate());
            GUI.color = Color.white;
            rowY += 26f;

            foreach (var ritual in situational)
                rowY = DrawRitualRow(viewRect.width, rowY, ritual);
        }

        Widgets.EndScrollView();
    }

    private float DrawRitualRow(float width, float rowY, Precept_Ritual ritual)
    {
        Rect rowRect = new Rect(0f, rowY, width, 26f);
        Widgets.DrawBoxSolid(rowRect, RowBg);

        // Icon
        Texture2D icon = ritual.Icon;
        if (icon != null && icon != BaseContent.BadTex)
            GUI.DrawTexture(new Rect(4f, rowY + 1f, 24f, 24f), icon, ScaleMode.ScaleToFit);

        // Name
        Widgets.Label(new Rect(32f, rowY, width * 0.4f, 26f), ritual.LabelCap);

        // Status
        Text.Anchor = TextAnchor.MiddleRight;
        string status;
        if (ritual.activeObligations != null && ritual.activeObligations.Count > 0)
        {
            int ticksLeft = ritual.activeObligations[0].TicksUntilExpiration;
            int daysLeft = ticksLeft / 60000;
            GUI.color = daysLeft <= 2 ? RedText : GreenText;
            status = "FT_RitualActive".Translate(daysLeft);
        }
        else if (ritual.lastFinishedTick > 0)
        {
            int daysAgo = (Find.TickManager.TicksGame - ritual.lastFinishedTick) / 60000;
            GUI.color = DimText;
            status = "FT_RitualDone".Translate(daysAgo);
        }
        else if (ritual.isAnytime)
        {
            GUI.color = GreenText;
            status = "FT_RitualAvailable".Translate();
        }
        else
        {
            var dateTrigger = ritual.obligationTriggers?
                .OfType<RitualObligationTrigger_Date>().FirstOrDefault();
            if (dateTrigger != null)
            {
                int currentDay = GenDate.DayOfYear(Find.TickManager.TicksAbs, 0f);
                int triggerDay = dateTrigger.triggerDaysSinceStartOfYear;
                int daysUntil = triggerDay - currentDay;
                if (daysUntil <= 0) daysUntil += 60;
                GUI.color = daysUntil <= 5 ? GreenText : DimText;
                status = "FT_RitualIn".Translate(daysUntil);
            }
            else
            {
                GUI.color = GreenText;
                status = "FT_RitualAvailable".Translate();
            }
        }
        Widgets.Label(new Rect(width * 0.45f, rowY, width * 0.5f, 26f), status);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        // Tooltip
        if (Mouse.IsOver(rowRect))
        {
            Widgets.DrawHighlight(rowRect);
            TooltipHandler.TipRegion(rowRect, ritual.TipLabel);
        }

        return rowY + 28f;
    }
}
