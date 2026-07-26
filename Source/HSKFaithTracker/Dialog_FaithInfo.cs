using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Dialog_FaithInfo : Window
{
    private readonly Pawn pawn;
    private Vector2 scrollPosition;
    private int activeTab; // 0 = history, 1 = rituals

    private static string FaithWord(int count)
    {
        int abs = System.Math.Abs(count);
        if (abs == 1) return "FT_Faith1".Translate();
        int mod100 = abs % 100;
        int mod10 = abs % 10;
        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
            return "FT_Faith24".CanTranslate() ? "FT_Faith24".Translate() : "FT_Faith5".Translate();
        return "FT_Faith5".Translate();
    }
    private static Vector2? savedPosition;

    private static readonly Color GreenBg = new Color(0.2f, 0.5f, 0.2f, 0.3f);
    private static readonly Color RedBg = new Color(0.5f, 0.2f, 0.2f, 0.3f);
    private static readonly Color GreenText = new Color(0.4f, 0.95f, 0.4f);
    private static readonly Color RedText = new Color(0.95f, 0.4f, 0.4f);
    private static readonly Color DimText = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color BlueText = new Color(0.6f, 0.8f, 1f);
    private static readonly Color OrangeText = new Color(1f, 0.8f, 0.3f);
    private static readonly Color RowBg = new Color(0.15f, 0.15f, 0.25f, 0.5f);
    private static readonly Color TabActive = new Color(0.3f, 0.3f, 0.4f, 0.8f);
    private static readonly Color TabInactive = new Color(0.15f, 0.15f, 0.2f, 0.6f);

    // Important ritual defNames
    private static readonly HashSet<string> importantDefs = new HashSet<string>
    {
        "GladiatorDuel", "Execution", "LeaderSpeech", "ThroneSpeech",
        "DuelChallenge", "GrandTournament", "Contest", "SupremeContest",
        "Conversion"
    };

    public override Vector2 InitialSize => new Vector2(770f, 580f);

    public Dialog_FaithInfo(Pawn pawn)
    {
        this.pawn = pawn;
        doCloseButton = true;
        doCloseX = false;
        draggable = true;
        absorbInputAroundWindow = false;

        // Debug: log ideo stats on open
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo != null)
        {
            var rituals = ideo.PreceptsListForReading?.Where(p => p is Precept_Ritual).ToList();
            int memeCount = ideo.memes?.Count ?? 0;
            string ritualList = rituals != null ? string.Join(", ", rituals.Select(r => $"{r.LabelCap}({r.def.defName})")) : "none";
            string memeList = ideo.memes != null ? string.Join(", ", ideo.memes.Select(m => $"{m.LabelCap}({m.defName})")) : "none";
            try { File.AppendAllText(@"D:\Mods\faith_debug.txt",
                $"OPEN: rituals={rituals?.Count ?? 0}: [{ritualList}]\n  memes={memeCount}: [{memeList}]\n"); } catch { }
        }
    }

    public override void SetInitialSizeAndPosition()
    {
        base.SetInitialSizeAndPosition();
        if (savedPosition.HasValue)
        {
            windowRect.x = savedPosition.Value.x;
            windowRect.y = savedPosition.Value.y;
        }
    }

    public override void PreClose()
    {
        base.PreClose();
        savedPosition = new Vector2(windowRect.x, windowRect.y);
    }

    public override void DoWindowContents(Rect inRect)
    {
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null)
            return;

        // Close button (custom, bigger)
        float closeBtnSize = 28f;
        Rect closeRect = new Rect(inRect.width - closeBtnSize - 2f, 2f, closeBtnSize, closeBtnSize);
        GUI.color = Mouse.IsOver(closeRect) ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        GUI.DrawTexture(closeRect, TexButton.CloseXBig, ScaleMode.ScaleToFit);
        GUI.color = Color.white;
        if (Widgets.ButtonInvisible(closeRect))
            Close();

        // Debug button (dev mode only)
        if (Prefs.DevMode)
        {
            Rect debugRect = new Rect(inRect.width - closeBtnSize - 34f, 2f, closeBtnSize, closeBtnSize);
            GUI.color = Mouse.IsOver(debugRect) ? Color.yellow : new Color(1f, 1f, 0.5f, 0.6f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(debugRect, "DBG");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(debugRect))
            {
                if (!Find.WindowStack.TryRemove(typeof(Dialog_FaithDebug), true))
                    Find.WindowStack.Add(new Dialog_FaithDebug());
            }
        }

        // Ideo info button
        var ideoForBtn = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideoForBtn != null)
        {
            float ideoBtnSize = 28f;
            Rect ideoRect = new Rect(inRect.width - closeBtnSize - ideoBtnSize - 16f, 2f, ideoBtnSize, ideoBtnSize);
            Texture2D ideoIcon = ideoForBtn.Icon;
            if (ideoIcon != null)
            {
                Color ideoColor = ideoForBtn.Color;
                GUI.color = Mouse.IsOver(ideoRect) ? ideoColor : new Color(ideoColor.r, ideoColor.g, ideoColor.b, 0.7f);
                GUI.DrawTexture(ideoRect, ideoIcon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
                if (Mouse.IsOver(ideoRect))
                    TooltipHandler.TipRegion(ideoRect, ideoForBtn.name);
                if (Widgets.ButtonInvisible(ideoRect))
                    IdeoUIUtility.OpenIdeoInfo(ideoForBtn);
            }
        }

        // Title
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width - closeBtnSize * 2 - 10f, 35f), "FT_Title".Translate());
        Text.Font = GameFont.Small;

        // Reform button: exchange faith for development points
        var fluidIdeo = Faction.OfPlayer?.ideos?.FluidIdeo;
        if (fluidIdeo?.development != null)
        {
            float ideoBtnOffset = 28f + 20f; // ideo button + gap
            float rightX = inRect.width - closeBtnSize - ideoBtnOffset;

            // Development points label
            int curPts = fluidIdeo.development.Points;
            int needPts = fluidIdeo.development.NextReformationDevelopmentPoints;
            string ptsStr = curPts + "/" + needPts;
            float ptsW = Text.CalcSize(ptsStr).x + 8f;
            GUI.color = fluidIdeo.development.CanReformNow ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 1f, 1f, 0.7f);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rightX - ptsW - 4f, 4f, ptsW, 28f), ptsStr);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            // Reform button (if ready) or Exchange button
            if (fluidIdeo.development.CanReformNow)
            {
                float reformW = 100f;
                Rect reformRect = new Rect(rightX - ptsW - 4f - reformW - 4f, 4f, reformW, 28f);
                if (Widgets.ButtonText(reformRect, "FT_Reform".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_ChooseMemes(fluidIdeo, MemeCategory.Normal, done: () =>
                    {
                        fluidIdeo.development.Notify_Reformed();
                    }, reformingIdeo: true));
                }
            }
            else
            {
                float btnW = 75f;
                Rect btnRect = new Rect(rightX - ptsW - 4f - btnW - 4f, 4f, btnW, 28f);
                bool canAfford = comp.Score >= 2;
                GUI.color = canAfford ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                TooltipHandler.TipRegion(btnRect, "FT_ExchangeFaithTip".Translate(2, 1));
                if (Widgets.ButtonText(btnRect, "FT_ExchangeFaith".Translate()) && canAfford)
                {
                    comp.RecordRitual("FT_FaithExchange".Translate(), RitualRecordType.FaithDecay, customWeight: -2);
                    fluidIdeo.development.TryAddDevelopmentPoints(1);
                    Messages.Message("FT_FaithExchanged".Translate(), MessageTypeDefOf.PositiveEvent);
                }
                GUI.color = Color.white;
            }
        }

        float y = 38f;

        // Stats bar
        int gained = 0, lost = 0;
        foreach (var r in comp.Records)
        {
            int w = r.Points;
            if (w > 0) gained += w;
            else lost += w;
        }

        Rect statsRect = new Rect(0f, y, inRect.width, 50f);
        Widgets.DrawBoxSolid(statsRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));

        float thirdW = inRect.width / 3f;
        Text.Anchor = TextAnchor.MiddleCenter;

        GUI.color = gained > 0 ? GreenText : DimText;
        Widgets.Label(new Rect(0f, y + 2f, thirdW, 22f), "FT_Gained".Translate());
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, y + 22f, thirdW, 26f), gained > 0 ? "+" + gained + " " + FaithWord(gained) : "—");
        Text.Font = GameFont.Small;

        GUI.color = comp.Score > 0 ? GreenText : comp.Score < 0 ? RedText : DimText;
        Widgets.Label(new Rect(thirdW, y + 2f, thirdW, 22f), "FT_Score".Translate());
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(thirdW, y + 22f, thirdW, 26f), (comp.Score != 0 ? (comp.Score > 0 ? "+" + comp.Score : comp.Score.ToString()) : "0") + " " + FaithWord(comp.Score));
        Text.Font = GameFont.Small;

        GUI.color = lost < 0 ? RedText : DimText;
        Widgets.Label(new Rect(thirdW * 2f, y + 2f, thirdW, 22f), "FT_Lost".Translate());
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(thirdW * 2f, y + 22f, thirdW, 26f), lost < 0 ? lost + " " + FaithWord(lost) : "—");
        Text.Font = GameFont.Small;

        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 56f;

        // === Progress bar (full width) ===
        y = DrawFaithProgressBar(inRect, y, comp.Score);

        // === Two-column layout ===
        float splitX = inRect.width * 0.50f;
        float panelGap = 10f;
        float panelTop = y;
        float panelH = inRect.height - panelTop - 30f;

        // --- LEFT PANEL: Meme bars ---
        Rect leftPanel = new Rect(0f, panelTop, splitX - panelGap / 2f, panelH);
        GUI.BeginGroup(leftPanel);
        Rect leftInner = new Rect(0f, 0f, leftPanel.width, leftPanel.height);
        DrawMemeBars(leftInner, comp);
        GUI.EndGroup();

        // Vertical separator
        Widgets.DrawBoxSolid(new Rect(splitX - 1f, panelTop, 2f, panelH), new Color(1f, 1f, 1f, 0.15f));

        // --- RIGHT PANEL: Tabs + content ---
        Rect rightPanel = new Rect(splitX + panelGap / 2f, panelTop, inRect.width - splitX - panelGap / 2f, panelH);
        GUI.BeginGroup(rightPanel);
        Rect rightInner = new Rect(0f, 0f, rightPanel.width, rightPanel.height);

        // Tab bar
        float tabW = rightInner.width / 2f;
        Rect tab0 = new Rect(0f, 0f, tabW, 28f);
        Rect tab1 = new Rect(tabW, 0f, tabW, 28f);

        Widgets.DrawBoxSolid(tab0, activeTab == 0 ? TabActive : TabInactive);
        Widgets.DrawBoxSolid(tab1, activeTab == 1 ? TabActive : TabInactive);

        if (activeTab == 0)
            Widgets.DrawBoxSolid(new Rect(tab0.x, tab0.yMax - 3f, tab0.width, 3f), BlueText);
        else
            Widgets.DrawBoxSolid(new Rect(tab1.x, tab1.yMax - 3f, tab1.width, 3f), BlueText);

        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = activeTab == 0 ? Color.white : DimText;
        Widgets.Label(tab0, "FT_TabHistory".Translate());
        GUI.color = activeTab == 1 ? Color.white : DimText;
        Widgets.Label(tab1, "FT_TabRituals".Translate());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(tab0)) { activeTab = 0; scrollPosition = Vector2.zero; }
        if (Widgets.ButtonInvisible(tab1)) { activeTab = 1; scrollPosition = Vector2.zero; }

        // Content area with scroll
        Rect contentRect = new Rect(0f, 34f, rightInner.width, rightInner.height - 34f);

        if (activeTab == 0)
            DrawHistoryTab(contentRect, comp);
        else
            DrawRitualsTab(contentRect);

        GUI.EndGroup();

    }

    private void DrawRitualsTab(Rect rect)
    {
        if (!ModsConfig.IdeologyActive) return;
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return;

        var holidays = new List<Precept_Ritual>();
        var important = new List<Precept_Ritual>();
        var situational = new List<Precept_Ritual>();
        var seenNames = new HashSet<string>();

        foreach (var p in ideo.PreceptsListForReading)
        {
            if (!(p is Precept_Ritual r)) continue;
            string name = r.LabelCap;
            if (seenNames.Contains(name)) continue;
            seenNames.Add(name);

            bool isFuneral = r.obligationTriggers != null && r.obligationTriggers.Any(t =>
                t is RitualObligationTrigger_MemberDied || t.GetType().Name.Contains("MemberCorpse"));
            if (isFuneral && (r.activeObligations == null || r.activeObligations.Count == 0))
                continue;

            bool hasDate = r.obligationTriggers != null &&
                r.obligationTriggers.Any(t => t is RitualObligationTrigger_Date);

            if (hasDate)
                holidays.Add(r);
            else if (importantDefs.Contains(r.def.defName) || r.def.defName.Contains("Duel")
                     || r.def.defName.Contains("Execution") || r.def.defName.Contains("Speech")
                     || r.def.defName.Contains("Contest") || r.def.defName.Contains("Gladiator")
                     || r.def.defName.Contains("Conversion"))
                important.Add(r);
            else
                situational.Add(r);
        }

        // Sort holidays: active obligations first, then by days until next
        int currentDay = GenDate.DayOfYear(Find.TickManager.TicksAbs, 0f);
        holidays.Sort((a, b) =>
        {
            bool activeA = a.activeObligations != null && a.activeObligations.Count > 0;
            bool activeB = b.activeObligations != null && b.activeObligations.Count > 0;
            if (activeA != activeB) return activeA ? -1 : 1;

            int dayA = a.obligationTriggers?.OfType<RitualObligationTrigger_Date>().FirstOrDefault()?.triggerDaysSinceStartOfYear ?? 0;
            int dayB = b.obligationTriggers?.OfType<RitualObligationTrigger_Date>().FirstOrDefault()?.triggerDaysSinceStartOfYear ?? 0;
            int untilA = dayA - currentDay; if (untilA <= 0) untilA += 60;
            int untilB = dayB - currentDay; if (untilB <= 0) untilB += 60;
            return untilA.CompareTo(untilB);
        });

        float holidayH = holidays.Count > 0 ? 26f + holidays.Count * 28f + 6f : 0f;
        float importantH = important.Count > 0 ? 26f + important.Count * 28f + 6f : 0f;
        float situationalH = situational.Count > 0 ? 26f + situational.Count * 28f : 0f;
        float totalH = holidayH + importantH + situationalH + 10f;

        Rect viewRect = new Rect(0f, 0f, rect.width - 16f, totalH);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        float rowY = 0f;

        if (holidays.Count > 0)
        {
            GUI.color = BlueText;
            Widgets.Label(new Rect(0f, rowY, viewRect.width, 24f), "FT_Holidays".Translate());
            GUI.color = Color.white;
            rowY += 26f;
            foreach (var r in holidays) rowY = DrawRitualRow(viewRect.width, rowY, r);
            rowY += 6f;
        }

        if (important.Count > 0)
        {
            if (holidays.Count > 0) { GUI.color = new Color(1f,1f,1f,0.1f); Widgets.DrawLineHorizontal(0f, rowY - 3f, viewRect.width); GUI.color = Color.white; }
            GUI.color = OrangeText;
            Widgets.Label(new Rect(0f, rowY, viewRect.width, 24f), "FT_Important".Translate());
            GUI.color = Color.white;
            rowY += 26f;
            foreach (var r in important) rowY = DrawRitualRow(viewRect.width, rowY, r);
            rowY += 6f;
        }

        if (situational.Count > 0)
        {
            if (holidays.Count > 0 || important.Count > 0) { GUI.color = new Color(1f,1f,1f,0.1f); Widgets.DrawLineHorizontal(0f, rowY - 3f, viewRect.width); GUI.color = Color.white; }
            GUI.color = DimText;
            Widgets.Label(new Rect(0f, rowY, viewRect.width, 24f), "FT_Situational".Translate());
            GUI.color = Color.white;
            rowY += 26f;
            foreach (var r in situational) rowY = DrawRitualRow(viewRect.width, rowY, r);
        }

        Widgets.EndScrollView();
    }

    private void DrawHistoryTab(Rect rect, GameComponent_FaithTracker comp)
    {
        var records = comp.Records;

        if (records.Count == 0)
        {
            GUI.color = DimText;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "FT_NoEvents".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        float listH = records.Count * 30f;
        Rect viewRect = new Rect(0f, 0f, rect.width - 16f, listH);

        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        float rowY = 0f;

        for (int i = records.Count - 1; i >= 0; i--)
        {
            var r = records[i];
            Rect rowRect = new Rect(0f, rowY, viewRect.width, 28f);

            int points = r.Points;
            bool isPositive = points > 0;

            Widgets.DrawBoxSolid(rowRect, isPositive ? GreenBg : RedBg);
            if (i % 2 == 0) Widgets.DrawLightHighlight(rowRect);

            // Hover + click
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);

                // Tooltips
                if (r.type == RitualRecordType.CorpsePenalty)
                    TooltipHandler.TipRegion(rowRect, "FT_CorpseTooltip".Translate());
                else if (r.type == RitualRecordType.FaithDecay)
                {
                    GameComponent_FaithTracker.CountRitualsByType(out int d, out int a);
                    int total = -GameComponent_FaithTracker.CalcYearlyPenalty(d, a);
                    TooltipHandler.TipRegion(rowRect, "FT_FaithDecayTooltip".Translate() + "\n\n" + "FT_FaithDecayFormula".Translate(d, a, total));
                }
                else if (r.ritualName == "FT_SlaveryPenalty".Translate())
                    TooltipHandler.TipRegion(rowRect, "FT_SlaveryPenaltyTooltip".Translate());
                else if (r.ritualName == "FT_EarlyPenalty".Translate())
                    TooltipHandler.TipRegion(rowRect, "FT_EarlyPenaltyTooltip".Translate());

                // Click to jump to thing
                if (r.thingId > 0 && Widgets.ButtonInvisible(rowRect))
                {
                    var thing = Find.CurrentMap?.listerThings.AllThings.FirstOrDefault(t => t.thingIDNumber == r.thingId);
                    if (thing != null)
                        CameraJumper.TryJump(thing);
                }
            }

            Text.WordWrap = false;

            GUI.color = isPositive ? GreenText : RedText;
            Widgets.Label(new Rect(5f, rowY, 20f, 28f), isPositive ? "▲" : "▼");

            GUI.color = Color.white;
            Widgets.Label(new Rect(25f, rowY, viewRect.width - 170f, 28f), r.ritualName);

            GUI.color = isPositive ? GreenText : RedText;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(viewRect.width - 140f, rowY, 50f, 28f),
                isPositive ? "+" + points : points.ToString());

            GUI.color = DimText;
            Text.Anchor = TextAnchor.MiddleRight;
            int daysAgo = (Find.TickManager.TicksGame - r.tick) / 60000;
            string timeStr = daysAgo <= 0 ? "FT_Today".Translate().RawText : "FT_DaysAgo".Translate(daysAgo).RawText;
            Widgets.Label(new Rect(viewRect.width - 105f, rowY, 100f, 28f), timeStr);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.WordWrap = true;
            rowY += 30f;
        }

        Widgets.EndScrollView();
        Text.Font = GameFont.Small;
    }

    private static readonly Color RedBorder = new Color(0.9f, 0.25f, 0.25f, 0.8f);
    private const int CooldownTicks = 1200000; // 20 days

    private float DrawRitualRow(float width, float rowY, Precept_Ritual ritual)
    {
        Rect rowRect = new Rect(0f, rowY, width, 26f);
        Widgets.DrawBoxSolid(rowRect, RowBg);

        Text.WordWrap = false;

        Texture2D icon = ritual.Icon;
        if (icon != null && icon != BaseContent.BadTex)
            GUI.DrawTexture(new Rect(4f, rowY + 1f, 24f, 24f), icon, ScaleMode.ScaleToFit);

        Widgets.Label(new Rect(32f, rowY, width * 0.5f, 26f), ritual.LabelCap);

Text.Anchor = TextAnchor.MiddleRight;
        string status;
        bool urgent = false;

        if (ritual.activeObligations != null && ritual.activeObligations.Count > 0)
        {
            // Active obligation — show time until expiration
            int ticksLeft = ritual.activeObligations[0].TicksUntilExpiration;
            int daysLeft = ticksLeft / 60000;
            urgent = daysLeft <= 2;
            GUI.color = urgent ? RedText : GreenText;
            if (daysLeft > 0)
                status = "FT_RitualActive".Translate(daysLeft);
            else
            {
                int hoursLeft = System.Math.Max(ticksLeft / 2500, 0);
                status = "FT_RitualActiveHours".Translate(hoursLeft);
            }
        }
        else if (ritual.lastFinishedTick > 0)
        {
            // Completed — show time until next available
            // Anytime rituals with cooldown: prioritize cooldown over date trigger
            if (ritual.isAnytime && ritual.def.useRepeatPenalty)
            {
                // Anytime with cooldown: days until cooldown ends
                int ticksSince = Find.TickManager.TicksGame - ritual.lastFinishedTick;
                int ticksRemaining = CooldownTicks - ticksSince;
                if (ticksRemaining > 0)
                {
                    int daysLeft = ticksRemaining / 60000;
                    GUI.color = new Color(0.85f, 0.45f, 0.4f);
                    status = "FT_RitualCooldown".Translate(daysLeft);
                }
                else
                {
                    GUI.color = GreenText;
                    status = "FT_RitualAvailable".Translate();
                }
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
                    urgent = daysUntil <= 3;
                    GUI.color = urgent ? OrangeText : DimText;
                    status = "FT_RitualNext".Translate(daysUntil);
                }
                else
                {
                    GUI.color = GreenText;
                    status = "FT_RitualAvailable".Translate();
                }
            }
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
                urgent = daysUntil <= 3;
                GUI.color = urgent ? OrangeText : DimText;
                status = "FT_RitualNext".Translate(daysUntil);
            }
            else
            {
                GUI.color = GreenText;
                status = "FT_RitualAvailable".Translate();
            }
        }
        Widgets.Label(new Rect(width * 0.55f, rowY, width * 0.42f, 26f), status);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        Text.WordWrap = true;

        // Red border for urgent rituals
        if (urgent)
        {
            GUI.color = RedBorder;
            Widgets.DrawBox(rowRect, 2);
            GUI.color = Color.white;
        }

        if (Mouse.IsOver(rowRect))
        {
            Widgets.DrawHighlight(rowRect);
            string tip = ritual.LabelCap;
            if (!ritual.def.description.NullOrEmpty())
                tip += "\n\n" + ritual.def.description;
            else
                tip += "\n\n" + ritual.TipLabel;
            TooltipHandler.TipRegion(rowRect, tip);
        }

        return rowY + 28f;
    }

    private static string FormatSlaveCount(int count)
    {
        if (count == 0)
            return "FT_Slaves0".CanTranslate() ? "FT_Slaves0".Translate().RawText : "0 slaves";
        if (count == 1)
            return "FT_Slaves1".Translate(count).RawText;

        int mod100 = count % 100;
        int mod10 = count % 10;

        // Russian: 2-4 (but not 12-14) use "раба"
        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
            return "FT_Slaves24".CanTranslate()
                ? "FT_Slaves24".Translate(count).RawText
                : "FT_Slaves5".Translate(count).RawText;

        return "FT_Slaves5".Translate(count).RawText;
    }

    // === Slavery bar ===
    private static readonly Color SlaveryColor = new Color(0.8f, 0.6f, 0.2f);
    private static readonly Color SlaveryBg = new Color(0.8f, 0.6f, 0.2f, 0.7f);

    private float DrawSlaveryBar(Rect inRect, float y, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;
        int memes = comp.MemeCount;
        float max = comp.SlaveryMax;

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        // Supremacist meme icon — centered vertically across label + bar (22 + 18 = 40px)
        var supremacistMeme = Faction.OfPlayer?.ideos?.PrimaryIdeo?.memes?
            .FirstOrDefault(m => m.defName == "Supremacist");
        float blockH = 22f + barH; // text + bar
        Texture2D memeIcon = supremacistMeme?.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, (supremacistMeme?.LabelCap ?? "") + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect) && supremacistMeme != null)
                Find.WindowStack.Add(new Dialog_MemeInfo(supremacistMeme));
        }

        // Forecast right side — centered vertically across label + bar
        int slaveryForecast = 0;
        if (max > 0f && memes > 0)
        {
            float pointsPerSection = max / memes;
            int filledSec = (int)(comp.slaveryPoints / pointsPerSection);
            var slavExt = supremacistMeme?.GetModExtension<MemeEffectExtension>();
            slaveryForecast = ComputeForecast(slavExt, filledSec, memes);
        }
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = slaveryForecast >= 0 ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
        Widgets.Label(new Rect(inRect.width - forecastW - 2f, y, forecastW, blockH), slaveryForecast.ToStringWithSign() + " в.");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        // Label centered
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = SlaveryColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), supremacistMeme?.LabelCap ?? "Supremacist");
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Background
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // Sections
        if (memes > 0 && max > 0f)
        {
            float sectionW = barW / memes;
            float filledW = (comp.slaveryPoints / max) * barW;

            // Filled portion
            if (filledW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, Mathf.Min(filledW, barW), barH), SlaveryBg);

            // Section dividers
            for (int i = 1; i < memes; i++)
            {
                float divX = barX + sectionW * i;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            // Border
            Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

            // Tooltip
            Rect barRect = new Rect(barX, y, barW, barH);
            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect);
                int filled = (int)(comp.slaveryPoints / (max / memes));
                int unfilled = memes - filled;
                TooltipHandler.TipRegion(barRect,
                    "FT_SlaveryTooltip".Translate((int)comp.slaveryPoints));
            }
        }

        y += barH + 8f;
        return y;
    }

    // === Collectivist bar ===
    private static readonly Color CollectivistColor = new Color(0.3f, 0.7f, 0.9f);
    private static readonly Color CollectivistBg = new Color(0.3f, 0.7f, 0.9f, 0.7f);

    private float DrawCollectivistBar(Rect inRect, float y, MemeDef meme)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Season timer

        // Label with timer
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = CollectivistColor;
        Text.Font = GameFont.Small;

        // Forecast for Collectivist
        float cBlockH = 22f + barH;
        var compC = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        int collectForecast = 0;
        if (compC != null)
        {
            int cSections = compC.MemeCount;
            int cFilled = System.Math.Min(compC.collectivistPoints / 2, cSections);
            var ext = meme.GetModExtension<MemeEffectExtension>();
            collectForecast = ComputeForecast(ext, cFilled, cSections);
        }

        // Forecast right side
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = collectForecast >= 0 ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
        Widgets.Label(new Rect(inRect.width - forecastW - 2f, y, forecastW, cBlockH), collectForecast.ToStringWithSign() + " в.");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = CollectivistColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar with sections (sections = memes, each needs 2 legendaries)
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        int sections = comp?.MemeCount ?? 0;
        int target = comp?.CollectivistTarget ?? 0;
        int points = comp?.collectivistPoints ?? 0;

        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (sections > 0 && target > 0)
        {
            float filledW = ((float)System.Math.Min(points, target) / target) * barW;
            if (filledW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, filledW, barH), CollectivistBg);

            // Section dividers
            for (int i = 1; i < sections; i++)
            {
                float divX = barX + (barW / sections) * i;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            int unfilled = System.Math.Max(0, sections - System.Math.Min(points / 2, sections));
            var ext = meme.GetModExtension<MemeEffectExtension>();
            string tip = "FT_CollectivistTooltip".Translate(points);
            if (ext != null && ext.seasonalFaithChange != 0)
                tip += "\n" + "FT_MemeSeasonalFaith".Translate(ext.seasonalFaithChange.ToStringWithSign());
            TooltipHandler.TipRegion(barRect, tip);
        }

        y += barH + 8f;
        return y;
    }

    // === Gender supremacy bar ===
    private static readonly Color MaleColor = new Color(0.4f, 0.7f, 1f);
    private static readonly Color FemaleColor = new Color(1f, 0.5f, 0.7f);

    private float DrawGenderBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Forecast right side — centered vertically across label + bar
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int genderForecast = 0;
        {
            int gtotal = comp.malePoints + comp.femalePoints;
            if (gtotal > 0)
            {
                bool isMaleF = meme.defName == "MaleSupremacy";
                int dominantPts = isMaleF ? comp.malePoints : comp.femalePoints;
                float ratio = (float)dominantPts / gtotal;
                int sectionsF = comp.MemeCount;
                int filledSec = GameComponent_FaithTracker.FilledFromRatio(ratio, sectionsF);
                genderForecast = ComputeForecast(ext, filledSec, sectionsF);
            }
            else
            {
                genderForecast = ComputeForecast(ext, 0, comp.MemeCount);
            }
        }
        DrawForecastLabel(inRect, y, forecastW, blockH, genderForecast);


        // Label with timer
        Text.Anchor = TextAnchor.MiddleCenter;
        Color labelColor = meme.defName == "MaleSupremacy" ? MaleColor : FemaleColor;
        GUI.color = labelColor;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — male left (blue), female right (pink)
        int total = comp.malePoints + comp.femalePoints;
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (total > 0)
        {
            float maleW = ((float)comp.malePoints / total) * barW;
            float femaleW = barW - maleW;

            // Male from left
            if (maleW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, maleW, barH), MaleColor);
            // Female from right
            if (femaleW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + maleW, y, femaleW, barH), FemaleColor);

            // Center divider (gender split)
            Widgets.DrawBoxSolid(new Rect(barX + maleW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            // Section dividers (by meme count)
            int sections = comp.MemeCount;
            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            // Labels on bar
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (maleW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX, y, maleW, barH), comp.malePoints.ToString());
            }
            if (femaleW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX + maleW, y, femaleW, barH), comp.femalePoints.ToString());
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string malePct = total > 0 ? ((float)comp.malePoints / total * 100f).ToString("F0") + "%" : "0%";
            string femalePct = total > 0 ? ((float)comp.femalePoints / total * 100f).ToString("F0") + "%" : "0%";
            // Count current colonists and slaves for tomorrow's forecast
            int curMales = 0, curFemales = 0, slvMales = 0, slvFemales = 0;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists)
            {
                if (p.IsSlave) continue;
                if (p.gender == Gender.Male) curMales++;
                else if (p.gender == Gender.Female) curFemales++;
            }
            foreach (var p in Find.CurrentMap?.mapPawns?.SlavesOfColonySpawned ?? new System.Collections.Generic.List<Pawn>())
            {
                if (p.gender == Gender.Male) slvMales++;
                else if (p.gender == Gender.Female) slvFemales++;
            }
            bool isMaleT = meme.defName == "MaleSupremacy";
            int tomorrowMale = curMales + (isMaleT ? slvFemales * 3 : 0);
            int tomorrowFemale = curFemales + (isMaleT ? 0 : slvMales * 3);

            string tip = "♂ " + comp.malePoints + " (" + malePct + ")  ♀ " + comp.femalePoints + " (" + femalePct + ")"
                + "\n\n" + "FT_GenderTooltip".Translate();
            TooltipHandler.TipRegion(barRect, tip);
        }

        // (forecast drawn above, before label)
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        y += barH + 8f;
        return y;
    }

    // === Xenophilia bar ===
    private static readonly Color XenoColor = new Color(0.6f, 0.4f, 0.9f);
    private static readonly Color XenoBg = new Color(0.6f, 0.4f, 0.9f, 0.7f);

    private float DrawXenophiliaBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int total = comp.xenoPhiliaOtherPoints + comp.xenoPhiliaMainPoints;
        int sections = comp.MemeCount;

        // For xenophilia: other races should dominate
        float ratio = total > 0 ? (float)comp.xenoPhiliaOtherPoints / total : 0f;
        int filledSec = GameComponent_FaithTracker.FilledFromRatio(ratio, sections);
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filledSec, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = XenoColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — other races (purple, left) vs main race (orange, right)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (total > 0)
        {
            float otherW = ((float)comp.xenoPhiliaOtherPoints / total) * barW;
            float mainW = barW - otherW;

            if (otherW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, otherW, barH), XenoColor);
            if (mainW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + otherW, y, mainW, barH), XenoOtherColor);

            Widgets.DrawBoxSolid(new Rect(barX + otherW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            // Section dividers
            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (otherW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX, y, otherW, barH), comp.xenoPhiliaOtherPoints.ToString());
            }
            if (mainW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX + otherW, y, mainW, barH), comp.xenoPhiliaMainPoints.ToString());
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string otherPct = total > 0 ? ((float)comp.xenoPhiliaOtherPoints / total * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_XenoTooltip".Translate(comp.xenoPhiliaOtherPoints, otherPct, comp.xenoPhiliaMainPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Xenophobia bar ===
    private static readonly Color XenoMainColor = new Color(0.3f, 0.7f, 0.9f);
    private static readonly Color XenoOtherColor = new Color(0.9f, 0.5f, 0.3f);

    private float DrawXenophobiaBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int total = comp.xenoMainRacePoints + comp.xenoOtherRacePoints;
        int sections = comp.MemeCount;

        float ratio = total > 0 ? (float)comp.xenoMainRacePoints / total : 0.5f;
        int filledSec = GameComponent_FaithTracker.FilledFromRatio(ratio, sections);
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filledSec, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = XenoMainColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — main race (blue) vs others (orange)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (total > 0)
        {
            float mainW = ((float)comp.xenoMainRacePoints / total) * barW;
            float otherW = barW - mainW;

            if (mainW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, mainW, barH), XenoMainColor);
            if (otherW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + mainW, y, otherW, barH), XenoOtherColor);

            Widgets.DrawBoxSolid(new Rect(barX + mainW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            // Section dividers
            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (mainW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX, y, mainW, barH), comp.xenoMainRacePoints.ToString());
            }
            if (otherW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX + mainW, y, otherW, barH), comp.xenoOtherRacePoints.ToString());
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string mainPct = total > 0 ? ((float)comp.xenoMainRacePoints / total * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_XenophobiaTooltip".Translate(comp.xenoMainRacePoints, mainPct, comp.xenoOtherRacePoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Animal personhood bar ===
    private static readonly Color AnimalGreenColor = new Color(0.4f, 0.85f, 0.4f);
    private static readonly Color AnimalRedColor = new Color(0.85f, 0.3f, 0.3f);

    private void DrawMemeBars(Rect inRect, GameComponent_FaithTracker comp)
    {
        float y = 0f;
        totalForecastAccum = 0;
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo?.memes == null) return;


        // Custom bars for specific memes
        foreach (var meme in ideo.memes)
        {
            if (meme.category == MemeCategory.Structure) continue;
            string def = meme.defName;

            if (def == "Supremacist") y = DrawSlaveryBar(inRect, y, comp);
            else if (def == "HumanPrimacy") y = DrawCollectivistBar(inRect, y, meme);
            else if (def == "MaleSupremacy" || def == "FemaleSupremacy") y = DrawGenderBar(inRect, y, meme, comp);
            else if (def == "HAR_Xenophilia") y = DrawXenophiliaBar(inRect, y, meme, comp);
            else if (def == "HAR_Xenophobia") y = DrawXenophobiaBar(inRect, y, meme, comp);
            else if (def == "Ritualist") y = DrawRitualistBar(inRect, y, meme, comp);
            else if (def == "Inhuman") y = DrawInhumanBar(inRect, y, meme, comp);
            else if (def == "Raider") y = DrawRaiderBar(inRect, y, meme, comp);
            else if (def == "Tunneler") y = DrawTunnelerBar(inRect, y, meme, comp);
            else if (def == "Darkness") y = DrawDarknessBar(inRect, y, meme, comp);
            else if (def == "Rancher") y = DrawRancherBar(inRect, y, meme, comp);
            else if (def == "Proselytizer") y = DrawProselytzerBar(inRect, y, meme, comp);
            else if (def == "Transhumanist") y = DrawTranshumanistBar(inRect, y, meme, comp);
            else if (def == "FleshPurity") y = DrawFleshPurityBar(inRect, y, meme, comp);
            else if (def == "PainIsVirtue") y = DrawPainIsVirtueBar(inRect, y, meme, comp);
            else if (def == "Blindsight") y = DrawBlindsightBar(inRect, y, meme, comp);
            else if (def == "Cannibal") y = DrawCannibalBar(inRect, y, meme, comp);
            else if (def == "Guilty") y = DrawGuiltyBar(inRect, y, meme, comp);
            else if (def == "HighLife") y = DrawHighLifeBar(inRect, y, meme, comp);
            else if (def == "Bloodfeeding") y = DrawBloodfeedingBar(inRect, y, meme, comp);
            else if (def == "AnimalPersonhood") y = DrawAnimalPersonhoodBar(inRect, y, meme, comp);
            else if (def == "TreeConnection") y = DrawTreeConnectionBar(inRect, y, meme, comp);
            else if (def == "NaturePrimacy") y = DrawNaturePrimacyBar(inRect, y, meme, comp);
            else if (def == "Nudism")
            {
                var ext = meme.GetModExtension<MemeEffectExtension>();
                if (ext != null && ext.faithPerSection != 0)
                    y = DrawFaithPerSectionBar(inRect, y, meme, comp, ext);
            }
            else if (def == "ReviaRaceSkarniteMeme") y = DrawSkarniteBar(inRect, y, meme, comp);
            else
                y = DrawGenericMemeBar(inRect, y, meme);
        }

        // Empty slot placeholders (max 4 memes total)
        const float slotH = 52f;
        const int maxMemes = 4;
        int currentMemes = comp.MemeCount;
        int emptySlots = System.Math.Max(0, maxMemes - currentMemes);
        for (int slot = 0; slot < emptySlots && y + slotH <= inRect.height; slot++)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            Widgets.DrawLineHorizontal(0f, y, inRect.width);
            GUI.color = Color.white;
            y += 4f;

            // Dashed border
            Rect slotRect = new Rect(20f, y, inRect.width - 30f, 40f);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            Widgets.DrawBox(slotRect, 1);
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(slotRect, "+ " + "FT_EmptySlot".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            y += 48f;
        }

        // Season total summary — sum of current forecasts from all bars
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 6f;

        float forecastW = 55f;
        const float lineH = 22f;
        Text.Font = GameFont.Small;

        // Line 1: season faith
        string totalStr = (totalForecastAccum >= 0 ? "+" : "") + totalForecastAccum;
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(4f, y, inRect.width - forecastW - 8f, lineH), "FT_FaithPerSeason".Translate());
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = totalForecastAccum > 0 ? new Color(0.4f, 0.95f, 0.4f) : totalForecastAccum < 0 ? new Color(0.95f, 0.4f, 0.4f) : new Color(1f, 1f, 1f, 0.5f);
        Widgets.Label(new Rect(inRect.width - forecastW - 2f, y, forecastW, lineH), totalStr + " в.");
        y += lineH;

        // Line 2: season timer
        int stTicks = Find.TickManager.TicksGame;
        int stCurrentSeason = stTicks / 900000;
        int stNextSeasonTick = (stCurrentSeason + 1) * 900000;
        int stTicksLeft = stNextSeasonTick - stTicks;
        int stDaysLeft = stTicksLeft / 60000;
        int stHoursLeft = (stTicksLeft % 60000) / 2500;
        string seasonStr = stDaysLeft > 0
            ? "FT_SeasonDeadline".Translate(stDaysLeft)
            : "FT_SeasonDeadlineHours".Translate(stHoursLeft);
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(4f, y, inRect.width - 8f, lineH), seasonStr);
        y += lineH;

        // Line 3: yearly decay
        int penalty2 = comp.YearlyPenaltyForecast;
        if (penalty2 != 0)
        {
            string decayStr = (penalty2 >= 0 ? "+" : "") + penalty2;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(4f, y, inRect.width - forecastW - 8f, lineH), "FT_YearDecayLabel".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = new Color(0.95f, 0.4f, 0.4f);
            Widgets.Label(new Rect(inRect.width - forecastW - 2f, y, forecastW, lineH), decayStr + " в.");
            Rect decayRow = new Rect(0f, y, inRect.width, lineH);
            if (Mouse.IsOver(decayRow))
            {
                Widgets.DrawHighlight(decayRow);
                GameComponent_FaithTracker.CountRitualsByType(out int d2, out int a2);
                int total2 = -GameComponent_FaithTracker.CalcYearlyPenalty(d2, a2);
                TooltipHandler.TipRegion(decayRow, "FT_FaithDecayTooltip".Translate() + "\n\n" + "FT_FaithDecayFormula".Translate(d2, a2, total2));
            }
            y += lineH;
        }

        // Line 4: year timer
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(4f, y, inRect.width - 8f, lineH), comp.YearTimerString);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    // === Memes with custom bars — skip in generic/faithPerSection loops ===
    private static readonly HashSet<string> customBarMemes = new HashSet<string>
    {
        "Supremacist", "Collectivist", "HumanPrimacy",
        "MaleSupremacy", "FemaleSupremacy",
        "NaturePrimacy", "TreeConnection", "AnimalPersonhood",
        "HAR_Xenophilia", "HAR_Xenophobia",
        "Blindsight", "PainIsVirtue", "FleshPurity", "Transhumanist", "Proselytizer", "Rancher", "Tunneler", "Darkness", "Raider", "Cannibal", "Guilty", "HighLife", "Bloodfeeding",
        "Nudism",
        "ReviaRaceSkarniteMeme"
    };

    private static bool HasCustomBar(string defName) => customBarMemes.Contains(defName);

    /// <summary>Compute forecast from XML values: seasonal + filled*mechanicMax + unfilled*penalty</summary>
    private static int totalForecastAccum;

    private static int ComputeForecast(MemeEffectExtension ext, int filled, int sections)
    {
        if (ext == null) return 0;
        int result = ext.ComputeSeasonTotal(filled, sections - filled);
        totalForecastAccum += result;
        return result;
    }

    private static void DrawForecastLabel(Rect inRect, float y, float forecastW, float blockH, int forecast)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = forecast > 0 ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
        string str = (forecast >= 0 ? "+" : "") + forecast + " в.";
        Widgets.Label(new Rect(inRect.width - forecastW - 2f, y, forecastW, blockH), str);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    // === Inhuman bar ===
    private static readonly Color InhumanColor = new Color(0.6f, 0.25f, 0.7f);
    private static readonly Color InhumanEmptyColor = new Color(0.2f, 0.08f, 0.25f);

    private float DrawInhumanBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int total = comp.inhumanPoints + comp.humanPoints;
        int filled = (total > 0) ? (int)((float)comp.inhumanPoints / total * sections) : 0;
        int unfilled = sections - filled;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = InhumanColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));
        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), InhumanColor);
        if (unfilled > 0)
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, unfilled * sectionW, barH), InhumanEmptyColor);

        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            int ihTotal = comp.inhumanPoints + comp.humanPoints;
            string ihPct = ihTotal > 0 ? ((float)comp.inhumanPoints / ihTotal * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_InhumanTooltip".Translate(comp.inhumanPoints, ihPct, comp.humanPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Ritualist bar ===
    private static readonly Color RitualistColor = new Color(0.7f, 0.5f, 0.9f);
    private static readonly Color RitualistEmptyColor = new Color(0.3f, 0.2f, 0.4f);

    private float DrawRitualistBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.ritualistPoints, sections) : 0;
        int unfilled = sections - filled;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = RitualistColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));
        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), RitualistColor);
        if (unfilled > 0)
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, unfilled * sectionW, barH), RitualistEmptyColor);

        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_RitualistTooltip".Translate(comp.ritualistPoints, sections));
        }

        y += barH + 8f;
        return y;
    }

    // === Raider bar ===
    private static readonly Color RaiderColor = new Color(0.85f, 0.3f, 0.2f);
    private static readonly Color RaiderEmptyColor = new Color(0.35f, 0.15f, 0.1f);

    private float DrawRaiderBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.raiderPoints, sections) : 0;
        int unfilled = sections - filled;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = RaiderColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), RaiderColor);
        if (unfilled > 0)
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, unfilled * sectionW, barH), RaiderEmptyColor);

        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            int carryOver = System.Math.Max(0, comp.raiderPoints - sections);
            TooltipHandler.TipRegion(barRect,
                "FT_RaiderTooltip".Translate(comp.raiderPoints, sections, carryOver));
        }

        y += barH + 8f;
        return y;
    }

    // === Tunneler bar ===
    // === Darkness bar ===
    private static readonly Color DarkBarColor = new Color(0.15f, 0.1f, 0.35f);
    private static readonly Color DarkTextColor = new Color(0.6f, 0.5f, 0.85f);
    private static readonly Color DarkPartialColor = new Color(0.1f, 0.07f, 0.2f);

    private float DrawDarknessBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.darknessPoints / 10, sections) : 0;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = DarkTextColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));
        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), DarkBarColor);

        int ptsInSection = comp.darknessPoints % 10;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / 10f);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), DarkPartialColor);
        }

        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_DarknessTooltip".Translate(comp.darknessPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Tunneler bar ===
    private static readonly Color TunnelerColor = new Color(0.5f, 0.4f, 0.3f);
    private static readonly Color TunnelerTextColor = new Color(0.8f, 0.65f, 0.45f);
    private static readonly Color TunnelerPartialColor = new Color(0.3f, 0.25f, 0.18f);

    private float DrawTunnelerBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.tunnelerPoints / 20, sections) : 0;
        int unfilled = sections - filled;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = TunnelerTextColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), TunnelerColor);

        int ptsInSection = comp.tunnelerPoints % 20;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / 20f);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), TunnelerPartialColor);
        }

        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_TunnelerTooltip".Translate(comp.tunnelerPoints));
        }

        y += barH + 8f;
        return y;
    }


    // === Rancher bar ===
    private static readonly Color RancherColor = new Color(0.6f, 0.45f, 0.2f);
    private static readonly Color RancherPartialColor = new Color(0.35f, 0.25f, 0.1f);

    private float DrawRancherBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.rancherPoints / 10, sections) : 0;
        int unfilled = sections - filled;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = RancherColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), RancherColor);

        // Partial fill
        int ptsInSection = comp.rancherPoints % 10;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / 10f);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), RancherPartialColor);
        }

        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string birthList = "";
            if (comp.rancherBirths.Count > 0)
            {
                birthList = "\n\n" + "FT_RancherBirths".Translate();
                foreach (var kvp in comp.rancherBirths.OrderByDescending(k => k.Value))
                {
                    int ptsEach = comp.rancherPtsPerAnimal.TryGetValue(kvp.Key, out int p) ? p : 1;
                    birthList += "\n  " + kvp.Key + " x" + kvp.Value + " × " + ptsEach + " " + "FT_RancherPtsEach".Translate();
                }
            }
            TooltipHandler.TipRegion(barRect,
                "FT_RancherTooltip".Translate(comp.rancherPoints) + birthList);
        }

        y += barH + 8f;
        return y;
    }

    // === Proselytizer bar ===
    private static readonly Color ProsColor = new Color(0.9f, 0.8f, 0.3f);
    private static readonly Color ProsEmptyColor = new Color(0.4f, 0.2f, 0.2f);

    private float DrawProselytzerBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.proselytizePoints, sections) : 0;
        int unfilled = sections - filled;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = ProsColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — filled (gold) vs empty (red)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), ProsColor);
        if (unfilled > 0)
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, unfilled * sectionW, barH), ProsEmptyColor);

        // Partial fill from extra points
        int ptsInSection = comp.proselytizePoints > filled ? 1 : 0;
        // No partial — 1 point = 1 section exactly

        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_ProselytizeTooltip".Translate(comp.proselytizePoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Transhumanist bar ===
    private static readonly Color TransImplantColor = new Color(0.3f, 0.6f, 0.9f);
    private static readonly Color TransPureColor = new Color(0.8f, 0.7f, 0.5f);

    private float DrawTranshumanistBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int total = comp.transImplantedPoints + comp.transPurePoints;
        int sections = comp.MemeCount;

        float ratio = total > 0 ? (float)comp.transImplantedPoints / total : 0f;
        int filledSec = GameComponent_FaithTracker.FilledFromRatio(ratio, sections);
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filledSec, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = TransImplantColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — implanted (blue, left) vs pure (brown, right)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (total > 0)
        {
            float implW = ((float)comp.transImplantedPoints / total) * barW;
            float pureW = barW - implW;

            if (implW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, implW, barH), TransImplantColor);
            if (pureW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + implW, y, pureW, barH), TransPureColor);

            Widgets.DrawBoxSolid(new Rect(barX + implW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (implW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX, y, implW, barH), comp.transImplantedPoints.ToString());
            }
            if (pureW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX + implW, y, pureW, barH), comp.transPurePoints.ToString());
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string implPct = total > 0 ? ((float)comp.transImplantedPoints / total * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_TranshumanistTooltip".Translate(comp.transImplantedPoints, implPct, comp.transPurePoints));
        }

        y += barH + 8f;
        return y;
    }

    // === FleshPurity bar ===
    private static readonly Color PureColor = new Color(0.3f, 0.8f, 0.5f);
    private static readonly Color ImplantedColor = new Color(0.6f, 0.3f, 0.6f);

    private float DrawFleshPurityBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int total = comp.purePoints + comp.implantedPoints;
        int sections = comp.MemeCount;

        float ratio = total > 0 ? (float)comp.purePoints / total : 0f;
        int filledSec = GameComponent_FaithTracker.FilledFromRatio(ratio, sections);
        int unfilled = sections - filledSec;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filledSec, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = PureColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — pure (green, left) vs implanted (purple, right)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (total > 0)
        {
            float pureW = ((float)comp.purePoints / total) * barW;
            float impW = barW - pureW;

            if (pureW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, pureW, barH), PureColor);
            if (impW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + pureW, y, impW, barH), ImplantedColor);

            Widgets.DrawBoxSolid(new Rect(barX + pureW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (pureW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX, y, pureW, barH), comp.purePoints.ToString());
            }
            if (impW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX + pureW, y, impW, barH), comp.implantedPoints.ToString());
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string purePct = total > 0 ? ((float)comp.purePoints / total * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_FleshPurityTooltip".Translate(comp.purePoints, purePct, comp.implantedPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === PainIsVirtue bar ===
    private static readonly Color ScarredColor = new Color(0.8f, 0.4f, 0.2f);
    private static readonly Color UnscarredColor = new Color(0.5f, 0.7f, 0.5f);

    private float DrawPainIsVirtueBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int total = comp.scarredPoints + comp.unscarredPoints;
        int sections = comp.MemeCount;

        float ratio = total > 0 ? (float)comp.scarredPoints / total : 0f;
        int filledSec = total > 0 ? System.Math.Min((int)(ratio * sections), sections) : 0;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filledSec, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Timer

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = ScarredColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — scarred (orange, left) vs unscarred (green, right)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (total > 0)
        {
            float scarW = ((float)comp.scarredPoints / total) * barW;
            float unscarW = barW - scarW;

            if (scarW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, scarW, barH), ScarredColor);
            if (unscarW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + scarW, y, unscarW, barH), UnscarredColor);

            Widgets.DrawBoxSolid(new Rect(barX + scarW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (scarW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX, y, scarW, barH), comp.scarredPoints.ToString());
            }
            if (unscarW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX + scarW, y, unscarW, barH), comp.unscarredPoints.ToString());
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string scarPct = total > 0 ? ((float)comp.scarredPoints / total * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_PainIsVirtueTooltip".Translate(comp.scarredPoints, scarPct, comp.unscarredPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Blindsight bar ===
    private static readonly Color BlindColor = new Color(0.2f, 0.2f, 0.5f);
    private static readonly Color BlindTextColor = new Color(0.5f, 0.5f, 0.9f);
    private static readonly Color SightedColor = new Color(0.7f, 0.7f, 0.3f);

    private float DrawBlindsightBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int total = comp.blindPoints + comp.sightedPoints;
        int sections = comp.MemeCount;

        float ratio = total > 0 ? (float)comp.blindPoints / total : 0f;
        int filledSec = GameComponent_FaithTracker.FilledFromRatio(ratio, sections);
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filledSec, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Timer

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = BlindTextColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — blind (dark blue, left) vs sighted (yellow, right)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        if (total > 0)
        {
            float blindW = ((float)comp.blindPoints / total) * barW;
            float sightW = barW - blindW;

            if (blindW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, blindW, barH), BlindColor);
            if (sightW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + blindW, y, sightW, barH), SightedColor);

            Widgets.DrawBoxSolid(new Rect(barX + blindW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            // Section dividers
            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (blindW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX, y, blindW, barH), comp.blindPoints.ToString());
            }
            if (sightW > 30f)
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(barX + blindW, y, sightW, barH), comp.sightedPoints.ToString());
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string blindPct = total > 0 ? ((float)comp.blindPoints / total * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_BlindsightTooltip".Translate(comp.blindPoints, blindPct, comp.sightedPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Cannibal bar ===
    private static readonly Color CannibalColor = new Color(0.9f, 0.35f, 0.35f);
    private static readonly Color CannibalPartialColor = new Color(0.45f, 0.15f, 0.15f);

    private float DrawCannibalBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.cannibalPoints / 25, sections) : 0;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Timer

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = CannibalColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), CannibalColor);

        // Partial fill
        int ptsInSection = comp.cannibalPoints % 25;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / 25f);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), CannibalPartialColor);
        }

        // Section dividers
        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_CannibalTooltip".Translate(comp.cannibalPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Skarnite bar ===
    private static readonly Color SkarniteColor = new Color(0.8f, 0.2f, 0.2f);
    private static readonly Color SkarnitePartialColor = new Color(0.5f, 0.1f, 0.1f);

    private float DrawSkarniteBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int totalTails = System.Math.Max(1, Patch_Skarnite.GetTotalTails());
        int filled = totalTails > 0 ? System.Math.Min(comp.skarnitePoints / totalTails, sections) : 0;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = SkarniteColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), SkarniteColor);

        // Partial fill for current section progress
        int remainder = comp.skarnitePoints - filled * totalTails;
        float partialFill = totalTails > 0 ? (float)remainder / totalTails : 0f;
        if (partialFill > 0f && filled < sections)
        {
            float partialW = partialFill * sectionW;
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), SkarniteColor * 0.7f);
        }

        // Section dividers
        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_SkarniteTooltip".Translate(comp.skarnitePoints, totalTails).Resolve());
        }

        y += barH + 8f;
        return y;
    }

    // === Guilty bar ===
    private static readonly Color GuiltyColor = new Color(0.6f, 0.4f, 0.7f);
    private static readonly Color GuiltyPartialColor = new Color(0.35f, 0.2f, 0.4f);

    private float DrawGuiltyBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int total = comp.guiltyWounds + comp.guiltyCharityPoints;
        int filled = (sections > 0 && total > 0) ? System.Math.Min(total / 10, sections) : 0;
        int unfilled = sections - filled;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Timer

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = GuiltyColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), GuiltyColor);

        // Partial fill
        int ptsInSection = total > 0 ? total % 10 : 0;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / 10f);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), GuiltyPartialColor);
        }

        // Section dividers
        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_GuiltyTooltip".Translate(comp.guiltyWounds, comp.guiltyCharityPoints, total));
        }

        y += barH + 8f;
        return y;
    }

    // === HighLife bar ===
    private static readonly Color HighLifeColor = new Color(0.4f, 0.85f, 0.2f);
    private static readonly Color HighLifePartialColor = new Color(0.25f, 0.5f, 0.12f);

    private float DrawHighLifeBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int threshold = comp.HighLifeThreshold;
        int filled = sections > 0 ? System.Math.Min(comp.highLifePoints / threshold, sections) : 0;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Timer

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = HighLifeColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), HighLifeColor);

        // Partial fill
        int ptsInSection = comp.highLifePoints % threshold;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / threshold);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), HighLifePartialColor);
        }

        // Section dividers
        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_HighLifeTooltip".Translate(comp.highLifePoints, threshold));
        }

        y += barH + 8f;
        return y;
    }

    // === Bloodfeeding bar ===
    private static readonly Color BloodColor = new Color(0.9f, 0.25f, 0.3f);
    private static readonly Color BloodEmptyColor = new Color(0.25f, 0.08f, 0.08f);

    private float DrawBloodfeedingBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.bloodfeedCount / 6, sections) : 0;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Timer

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = BloodColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        // Filled sections
        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), BloodColor);

        // Partial fill in current section
        int ptsInSection = comp.bloodfeedCount % 6;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / 6f);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), BloodEmptyColor);
        }

        // Section dividers
        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_BloodTooltip".Translate(comp.bloodfeedCount));
        }

        y += barH + 8f;
        return y;
    }

    private float DrawAnimalPersonhoodBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int filled = sections > 0 ? System.Math.Min(comp.animalCompanionPoints / 45, sections) : 0;
        int lost = System.Math.Min(comp.animalKills / 5, filled);
        filled -= lost;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(4f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = AnimalGreenColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — green filled by companion points, partial fill
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float sectionW = sections > 0 ? barW / sections : barW;

        if (filled > 0)
            Widgets.DrawBoxSolid(new Rect(barX, y, filled * sectionW, barH), AnimalGreenColor);

        // Partial fill in current section
        int ptsInSection = comp.animalCompanionPoints % 45;
        if (ptsInSection > 0 && filled < sections)
        {
            float partialW = sectionW * ((float)ptsInSection / 45f);
            Widgets.DrawBoxSolid(new Rect(barX + filled * sectionW, y, partialW, barH), new Color(0.2f, 0.35f, 0.2f));
        }

        // Section dividers
        for (int s = 1; s < sections; s++)
        {
            float divX = barX + sectionW * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);

            // Count free animals
            int freeAnimals = 0;
            foreach (var map in Find.Maps)
                foreach (var animal in map.mapPawns.AllPawnsSpawned)
                    if (animal.RaceProps.Animal && animal.Faction == Faction.OfPlayer && !animal.RaceProps.FenceBlocked)
                        freeAnimals++;

            // Forecast: days left in season
            int ticks = Find.TickManager.TicksGame;
            int currentSeason = ticks / 900000;
            int nextSeasonTick = (currentSeason + 1) * 900000;
            int daysLeft = (nextSeasonTick - ticks) / 60000;
            int forecastPoints = comp.animalCompanionPoints + freeAnimals * daysLeft;
            float forecastFilled = System.Math.Min((float)forecastPoints / 45f, sections);

            TooltipHandler.TipRegion(barRect,
                "FT_AnimalPersonhoodTooltip".Translate(comp.animalCompanionPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Tree connection bar ===
    private static readonly Color TreePlantedColor = new Color(0.2f, 0.7f, 0.4f);
    private static readonly Color TreeDestroyedColor = new Color(0.85f, 0.3f, 0.3f);

    private float DrawTreeConnectionBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int points = comp.treeConnectionPoints;
        int pointsPerSection = 30;

        int sections = comp.MemeCount;
        int filledSec = sections > 0 ? System.Math.Min(points / pointsPerSection, sections) : 0;
        int unfilledSec = sections - filledSec;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        int forecast = ComputeForecast(ext, filledSec, sections);

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = forecast >= 0 ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
        Widgets.Label(new Rect(inRect.width - forecastW - 2f, y, forecastW, blockH), forecast.ToStringWithSign() + " в.");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = TreePlantedColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // Fill bar based on points progress
        int totalNeeded = pointsPerSection * sections;
        if (totalNeeded > 0)
        {
            float fillRatio = System.Math.Min((float)points / totalNeeded, 1f);
            float fillW = fillRatio * barW;
            if (fillW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, fillW, barH), TreePlantedColor);

            // Section dividers
            for (int s = 1; s < sections; s++)
            {
                float divX = barX + (barW / sections) * s;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(new Rect(barX, y, barW, barH), points + " / " + totalNeeded);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_TreeTooltip".Translate(points, pointsPerSection)
                + "\n" + "FT_TreeSections".Translate(filledSec, sections, forecast.ToStringWithSign()));
        }

        y += barH + 8f;
        return y;
    }

    // === Nature primacy bar ===
    private static readonly Color PlantedColor = new Color(0.3f, 0.85f, 0.3f);


    private float DrawNaturePrimacyBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int score = comp.naturePlantsSown - comp.natureTreesCut * 2;

        var ext = meme.GetModExtension<MemeEffectExtension>();
        int sections = comp.MemeCount;
        int threshold = 30 * sections;
        int filled = score > 0 ? System.Math.Min(score / 30, sections) : 0;
        int forecast = ComputeForecast(ext, filled, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = PlantedColor;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar background
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // Progress fill (only positive)
        if (score > 0)
        {
            float fillPct = threshold > 0 ? System.Math.Min((float)score / threshold, 1f) : 0f;
            Widgets.DrawBoxSolid(new Rect(barX, y, barW * fillPct, barH), PlantedColor);
        }

        // Section dividers
        for (int s = 1; s < sections; s++)
        {
            float divX = barX + (barW / sections) * s;
            Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
        }

        // Score label on bar
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.white;
        Widgets.Label(new Rect(barX, y, barW, barH), score + " / " + threshold);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            TooltipHandler.TipRegion(barRect,
                "FT_NatureTooltip".Translate(score, threshold, comp.naturePlantsSown, comp.natureTreesCut));
        }

        y += barH + 8f;
        return y;
    }

    // === Faith-per-section bar (Nudism etc.) ===
    private static readonly Color FaithSectionColor = new Color(0.6f, 0.85f, 0.5f);
    private static readonly Color FaithSectionBg = new Color(0.6f, 0.85f, 0.5f, 0.7f);

    private float DrawFaithPerSectionBar(Rect inRect, float y, MemeDef meme, GameComponent_FaithTracker comp, MemeEffectExtension ext)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;
        int sections = comp.MemeCount;
        int total = comp.nudismPoints + comp.nudismDressedPoints;
        float ratio = total > 0 ? (float)comp.nudismPoints / total : 0f;
        int filledSections = GameComponent_FaithTracker.FilledFromRatio(ratio, sections);
        int forecast = ComputeForecast(ext, filledSections, sections);

        DrawForecastLabel(inRect, y, forecastW, blockH, forecast);

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }


        // Label with nudism points if applicable
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = FaithSectionColor;
        string label = meme.LabelCap;
        Widgets.Label(new Rect(barX, y, barW, 20f), label);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar — ratio-based (nude vs dressed)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));
        if (total > 0)
        {
            float nudeW = ratio * barW;
            float dressedW = barW - nudeW;
            if (nudeW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX, y, nudeW, barH), FaithSectionBg);
            if (dressedW > 0f)
                Widgets.DrawBoxSolid(new Rect(barX + nudeW, y, dressedW, barH), new Color(0.5f, 0.3f, 0.3f, 0.7f));
            Widgets.DrawBoxSolid(new Rect(barX + nudeW - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.6f));

            // Section dividers
            for (int i = 1; i < sections; i++)
            {
                float divX = barX + (barW / sections) * i;
                Widgets.DrawBoxSolid(new Rect(divX - 1f, y, 2f, barH), new Color(1f, 1f, 1f, 0.4f));
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (nudeW > 30f)
                Widgets.Label(new Rect(barX, y, nudeW, barH), comp.nudismPoints.ToString());
            if (dressedW > 30f)
                Widgets.Label(new Rect(barX + nudeW, y, dressedW, barH), comp.nudismDressedPoints.ToString());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        // Tooltip
        Rect barRect = new Rect(barX, y, barW, barH);
        if (Mouse.IsOver(barRect))
        {
            Widgets.DrawHighlight(barRect);
            string nudePct = total > 0 ? ((float)comp.nudismPoints / total * 100f).ToString("F0") + "%" : "0%";
            TooltipHandler.TipRegion(barRect,
                "FT_NudismTooltip".Translate(comp.nudismPoints, nudePct, comp.nudismDressedPoints));
        }

        y += barH + 8f;
        return y;
    }

    // === Generic meme bar ===
    private static readonly Color GenericMemeColor = new Color(0.5f, 0.7f, 0.9f);
    private static readonly Color GenericMemeBg = new Color(0.5f, 0.7f, 0.9f, 0.7f);

    private float DrawGenericMemeBar(Rect inRect, float y, MemeDef meme)
    {
        float barH = 18f;
        float iconSize = 24f;
        float iconPad = 4f;
        float forecastW = 55f;
        float barX = 20f + iconSize + iconPad;
        float barW = inRect.width - barX - 10f - forecastW;

        // Generic bar is always full — forecast = all filled
        var ext = meme.GetModExtension<MemeEffectExtension>();
        var compG = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        int sections = compG?.MemeCount ?? 0;
        int forecast = ComputeForecast(ext, sections, sections);

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        float blockH = 22f + barH;

        // Forecast
        if (forecast != 0)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = forecast > 0 ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
            string forecastStr = (forecast >= 0 ? "+" : "") + forecast + " в.";
            Widgets.Label(new Rect(inRect.width - forecastW - 2f, y, forecastW, blockH), forecastStr);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        // Icon
        Texture2D memeIcon = meme.Icon;
        if (memeIcon != null)
        {
            Rect iconRect = new Rect(10f, y + (blockH - iconSize) / 2f, iconSize, iconSize);
            Rect clickRect = iconRect.ExpandedBy(10f);
            if (Mouse.IsOver(clickRect))
            {
                GUI.color = new Color(1f, 1f, 0.6f);
                Widgets.DrawHighlight(clickRect);
                TooltipHandler.TipRegion(clickRect, meme.LabelCap + "\n" + "FT_ClickForDetails".Translate());
            }
            GUI.DrawTexture(iconRect, memeIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new Dialog_MemeInfo(meme));
        }

        // Label
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = GenericMemeColor;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(barX, y, barW, 20f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 22f;

        // Bar background
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));
        // Filled (100%)
        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), GenericMemeBg);
        // Border
        Widgets.DrawBox(new Rect(barX, y, barW, barH), 1);

        y += barH + 8f;
        return y;
    }

    // === Faith progress bar ===
    private static readonly int[] FaithMoods = { -6, -4, -2, 0, 2, 4, 6 };
    private static readonly float[] FaithThresholds = { 0.14f, 0.28f, 0.42f, 0.57f, 0.71f, 0.85f, 1.0f };
    private static readonly Color[] FaithColors =
    {
        new Color(0.7f, 0.1f, 0.1f),
        new Color(0.85f, 0.25f, 0.2f),
        new Color(0.95f, 0.55f, 0.2f),
        new Color(0.7f, 0.7f, 0.7f),
        new Color(0.4f, 0.75f, 0.3f),
        new Color(0.3f, 0.85f, 0.3f),
        new Color(0.15f, 0.95f, 0.4f),
    };
    private static readonly string[] FaithStageKeys =
    {
        "FT_FStage0", "FT_FStage1", "FT_FStage2", "FT_FStage3",
        "FT_FStage4", "FT_FStage5", "FT_FStage6"
    };

    private float DrawFaithProgressBar(Rect inRect, float y, int score)
    {
        float barH = 18f;
        float barX = 20f;
        float barW = inRect.width - 40f;

        Widgets.DrawBoxSolid(new Rect(barX, y, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        float prevX = 0f;
        for (int i = 0; i < FaithThresholds.Length; i++)
        {
            float segEnd = FaithThresholds[i] * barW;
            Rect segRect = new Rect(barX + prevX, y, segEnd - prevX, barH);
            Widgets.DrawBoxSolid(segRect, FaithColors[i]);
            GUI.color = new Color(0f, 0f, 0f, 0.3f);
            Widgets.DrawBox(segRect, 1);
            GUI.color = Color.white;

            if (Mouse.IsOver(segRect))
            {
                string moodVal = FaithMoods[i] >= 0 ? "+" + FaithMoods[i] : FaithMoods[i].ToString();
                TooltipHandler.TipRegion(segRect, FaithStageKeys[i].Translate() + " (" + moodVal + ")");
            }
            prevX = segEnd;
        }

        float normalized = Mathf.InverseLerp(GameComponent_FaithTracker.ScoreMin, GameComponent_FaithTracker.ScoreMax, score);
        float markerX = barX + normalized * barW;
        Widgets.DrawBoxSolid(new Rect(markerX - 2f, y - 2f, 4f, barH + 4f), Color.white);

        y += barH + 4f;

        int currentStage = 0;
        for (int i = 0; i < FaithThresholds.Length; i++)
        {
            if (normalized <= FaithThresholds[i]) { currentStage = i; break; }
            if (i == FaithThresholds.Length - 1) currentStage = i;
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = FaithColors[currentStage];
        string moodStr = FaithMoods[currentStage] >= 0 ? "+" + FaithMoods[currentStage] : FaithMoods[currentStage].ToString();
        string text = FaithStageKeys[currentStage].Translate() + " (" + moodStr + ")";

        if (currentStage < FaithThresholds.Length - 1)
        {
            int nextScore = (int)(FaithThresholds[currentStage] * 60f - 30f) + 1;
            int pointsToNext = nextScore - score;
            if (pointsToNext > 0)
                text += "  →  " + "FT_FNextLevel".Translate(pointsToNext);
        }

        Widgets.Label(new Rect(0f, y, inRect.width, 20f), text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        y += 24f;

        return y;
    }
}
