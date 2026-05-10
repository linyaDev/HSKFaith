using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

// Allow all memes (any impact) for initial fluid ideo
[HarmonyPatch(typeof(IdeoUtility), nameof(IdeoUtility.IsMemeAllowedForInitialFluidIdeo))]
public static class Patch_FluidIdeoAllMemes
{
    public static bool Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}

[HarmonyPatch]
public static class Patch_ChooseMemes
{
    internal static bool useCustomView = true;
    private static bool showFaithBadge = true;
    private static bool showCertaintyBadge = false;

    private static readonly HashSet<string> disabledMemes = new HashSet<string>
    {
        "Ritualist", "Inhuman"
    };

    static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Dialog_ChooseMemes), "DoNormalMemeSelector");
    }

    public static bool Prefix(object __instance, Rect viewRect, ref float curY, List<MemeDef> memes)
    {
        // Toggle buttons row
        float btnY = curY - 20f;
        float btnX = viewRect.xMax;

        // Vanilla/Custom view
        btnX -= 120f;
        string toggleLabel = useCustomView ? "FT_ViewVanilla".Translate() : "FT_ViewCustom".Translate();
        if (Widgets.ButtonText(new Rect(btnX, btnY, 120f, 20f), toggleLabel, drawBackground: true, doMouseoverSound: true, active: true))
            useCustomView = !useCustomView;

        if (!useCustomView)
            return true; // vanilla

        // Show certainty badge
        float certLabelW = Text.CalcSize("FT_ShowCertainty".Translate()).x;
        btnX -= certLabelW + 4f;
        Widgets.Label(new Rect(btnX, btnY, certLabelW, 20f), "FT_ShowCertainty".Translate());
        btnX -= 22f;
        Widgets.Checkbox(new Vector2(btnX, btnY + 1f), ref showCertaintyBadge, 18f);

        // Show faith badge
        float faithLabelW = Text.CalcSize("FT_ShowFaith".Translate()).x;
        btnX -= faithLabelW + 8f;
        Widgets.Label(new Rect(btnX, btnY, faithLabelW, 20f), "FT_ShowFaith".Translate());
        btnX -= 22f;
        Widgets.Checkbox(new Vector2(btnX, btnY + 1f), ref showFaithBadge, 18f);

        // Get newMemes list via reflection
        var newMemesField = AccessTools.Field(typeof(Dialog_ChooseMemes), "newMemes");
        var newMemes = (List<MemeDef>)newMemesField.GetValue(__instance);

        // Check if selection is complete
        var rangeProp = AccessTools.Property(typeof(Dialog_ChooseMemes), "MemeCountRangeAbsolute");
        IntRange range = (IntRange)rangeProp.GetValue(__instance);
        int selectedCount = newMemes.Count(m => m.category != MemeCategory.Structure);
        bool selectionComplete = selectedCount >= range.max;

        float gapBetweenBoxes = 8f;
        int maxPerRow = Mathf.FloorToInt(viewRect.width / (IdeoUIUtility.MemeBoxSize.x + gapBetweenBoxes));

        // Group memes by our categories
        var early = new List<MemeDef>();
        var mid = new List<MemeDef>();
        var late = new List<MemeDef>();
        var situational = new List<MemeDef>();

        foreach (var meme in memes)
        {
            var ext = meme.GetModExtension<MemeEffectExtension>();
            string group = ext?.memeGroup;
            if (group == "early") early.Add(meme);
            else if (group == "mid") mid.Add(meme);
            else if (group == "late") late.Add(meme);
            else situational.Add(meme);
        }

        var drawMemeMethod = AccessTools.Method(typeof(Dialog_ChooseMemes), "DrawMeme");

        if (early.Count > 0)
            DrawCategory(viewRect, ref curY, "FT_MemeCatEarly".Translate(), early, maxPerRow, gapBetweenBoxes, __instance, newMemes, drawMemeMethod, selectionComplete);
        if (mid.Count > 0)
            DrawCategory(viewRect, ref curY, "FT_MemeCatMid".Translate(), mid, maxPerRow, gapBetweenBoxes, __instance, newMemes, drawMemeMethod, selectionComplete);
        if (late.Count > 0)
            DrawCategory(viewRect, ref curY, "FT_MemeCatLate".Translate(), late, maxPerRow, gapBetweenBoxes, __instance, newMemes, drawMemeMethod, selectionComplete);
        if (situational.Count > 0)
            DrawCategory(viewRect, ref curY, "FT_MemeCatSituational".Translate(), situational, maxPerRow, gapBetweenBoxes, __instance, newMemes, drawMemeMethod, selectionComplete);

        return false;
    }

    private static int CountNonStructureMemes(List<MemeDef> memes)
    {
        int count = 0;
        foreach (var m in memes)
            if (m.category != MemeCategory.Structure)
                count++;
        return count;
    }

    private static void DrawCategory(Rect viewRect, ref float curY, string label, List<MemeDef> memes,
        int maxPerRow, float gap, object instance, List<MemeDef> newMemes, MethodInfo drawMemeMethod, bool selectionComplete)
    {
        // Category header
        Rect headerRect = new Rect(viewRect.x, curY, viewRect.width, 30f);
        Widgets.Label(headerRect, label);
        curY = headerRect.yMax;

        GUI.color = Color.gray;
        Widgets.DrawLineHorizontal(headerRect.x, curY - 7f, headerRect.width);
        GUI.color = Color.white;

        // Draw meme grid
        int col = 0;
        int rowCount = Mathf.Min(memes.Count, maxPerRow);
        float rowOffset = (viewRect.width - rowCount * (IdeoUIUtility.MemeBoxSize.x + gap)) / 2f;
        float startX = rowOffset;

        for (int i = 0; i < memes.Count; i++)
        {
            if (col >= maxPerRow)
            {
                col = 0;
                curY += IdeoUIUtility.MemeBoxSize.y + gap;
                int remaining = memes.Count - i;
                rowCount = Mathf.Min(remaining, maxPerRow);
                rowOffset = (viewRect.width - rowCount * (IdeoUIUtility.MemeBoxSize.x + gap)) / 2f;
                startX = rowOffset;
            }

            Rect memeBox = new Rect(viewRect.x + startX + col * (IdeoUIUtility.MemeBoxSize.x + gap),
                curY, IdeoUIUtility.MemeBoxSize.x, IdeoUIUtility.MemeBoxSize.y).Rounded();

            bool isDisabled = disabledMemes.Contains(memes[i].defName);

            // Block click BEFORE DrawMeme processes it
            if (isDisabled && Event.current.type == EventType.MouseDown && Mouse.IsOver(memeBox))
                Event.current.Use();

            // Call private DrawMeme
            drawMemeMethod.Invoke(instance, new object[] { memes[i], memeBox, false });

            // Disabled overlay — dark tint
            if (isDisabled)
            {
                Widgets.DrawBoxSolid(memeBox, new Color(0f, 0f, 0f, 0.6f));
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(1f, 0.4f, 0.4f);
                Text.Anchor = TextAnchor.LowerCenter;
                Widgets.Label(memeBox, "FT_MemeDisabled".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // Badges
            bool isSelected = newMemes.Contains(memes[i]);
            float badgeY = memeBox.y + 2f;

            if (showFaithBadge)
            {
                if (selectionComplete && isSelected)
                    badgeY = DrawFaithBadgeFinal(memeBox, memes[i], newMemes);
                else if (!selectionComplete && !isSelected)
                    badgeY = DrawFaithBadge(memeBox, memes[i], newMemes);
            }

            if (showCertaintyBadge)
            {
                if (selectionComplete && isSelected)
                    DrawCertaintyBadgeFinal(memeBox, badgeY, newMemes);
                else if (!selectionComplete && !isSelected)
                    DrawCertaintyBadge(memeBox, badgeY, memes[i], newMemes);
            }

            col++;
        }

        curY += IdeoUIUtility.MemeBoxSize.y + gap;
    }

    private static (int min, int max) ComputeMemeFaith(MemeEffectExtension ext, int memeCount)
    {
        if (ext == null) return (0, 0);

        int min = ext.seasonalFaithChange + memeCount * ext.penaltyPerSection;
        int max = ext.seasonalFaithChange + memeCount * ext.MechanicMaxFaith;
        return (min, max);
    }

    private static float DrawFaithBadge(Rect memeBox, MemeDef meme, List<MemeDef> selectedMemes)
    {
        var ext = meme.GetModExtension<MemeEffectExtension>();

        // Count non-structure memes if this meme is added
        int newCount = 1; // this meme
        foreach (var m in selectedMemes)
            if (m.category != MemeCategory.Structure)
                newCount++;

        // Sum min/max for all selected + this meme
        int totalMin = 0, totalMax = 0;

        // This meme's contribution
        var (thisMin, thisMax) = ComputeMemeFaith(ext, newCount);
        totalMin += thisMin;
        totalMax += thisMax;

        // Selected memes' contributions at new count
        foreach (var m in selectedMemes)
        {
            if (m.category == MemeCategory.Structure) continue;
            var mExt = m.GetModExtension<MemeEffectExtension>();
            var (mMin, mMax) = ComputeMemeFaith(mExt, newCount);
            totalMin += mMin;
            totalMax += mMax;
        }

        int min = totalMin;
        int max = totalMax;

        if (min == 0 && max == 0) return memeBox.y + 2f;

        string minStr = (min > 0 ? "+" : "") + min;
        string maxStr = (max > 0 ? "+" : "") + max;
        string text = (min == max ? minStr : minStr + ".." + maxStr) + "FT_FaithShort".Translate();

        // Color based on overall tone
        bool positive = max > 0 && min >= 0;
        bool negative = max <= 0 && min < 0;
        Color bgColor, textColor;
        if (positive)
        {
            bgColor = new Color(0.15f, 0.4f, 0.15f, 0.85f);
            textColor = new Color(0.5f, 1f, 0.5f);
        }
        else if (negative)
        {
            bgColor = new Color(0.5f, 0.1f, 0.1f, 0.85f);
            textColor = new Color(1f, 0.5f, 0.5f);
        }
        else
        {
            bgColor = new Color(0.35f, 0.35f, 0.1f, 0.85f);
            textColor = new Color(1f, 1f, 0.6f);
        }

        Text.Font = GameFont.Tiny;
        float badgeW = Text.CalcSize(text).x + 6f;
        float badgeH = 18f;
        Rect badgeRect = new Rect(memeBox.x + 2f, memeBox.y + 2f, badgeW, badgeH);

        Widgets.DrawBoxSolid(badgeRect, bgColor);
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = textColor;
        Widgets.Label(badgeRect, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        return badgeRect.yMax + 2f;
    }

    private static float DrawFaithBadgeFinal(Rect memeBox, MemeDef meme, List<MemeDef> selectedMemes)
    {
        // Total faith from all selected memes
        int memeCount = 0;
        foreach (var m in selectedMemes)
            if (m.category != MemeCategory.Structure)
                memeCount++;

        int totalMin = 0, totalMax = 0;
        foreach (var m in selectedMemes)
        {
            if (m.category == MemeCategory.Structure) continue;
            var mExt = m.GetModExtension<MemeEffectExtension>();
            var (mMin, mMax) = ComputeMemeFaith(mExt, memeCount);
            totalMin += mMin;
            totalMax += mMax;
        }

        int min = totalMin;
        int max = totalMax;

        if (min == 0 && max == 0) return memeBox.y + 2f;

        string minStr = (min > 0 ? "+" : "") + min;
        string maxStr = (max > 0 ? "+" : "") + max;
        string text = (min == max ? minStr : minStr + ".." + maxStr) + "FT_FaithShort".Translate();

        bool positive = max > 0 && min >= 0;
        bool negative = max <= 0 && min < 0;
        Color bgColor, textColor;
        if (positive)
        {
            bgColor = new Color(0.15f, 0.4f, 0.15f, 0.85f);
            textColor = new Color(0.5f, 1f, 0.5f);
        }
        else if (negative)
        {
            bgColor = new Color(0.5f, 0.1f, 0.1f, 0.85f);
            textColor = new Color(1f, 0.5f, 0.5f);
        }
        else
        {
            bgColor = new Color(0.35f, 0.35f, 0.1f, 0.85f);
            textColor = new Color(1f, 1f, 0.6f);
        }

        Text.Font = GameFont.Tiny;
        float badgeW = Text.CalcSize(text).x + 6f;
        float badgeH = 18f;
        Rect badgeRect = new Rect(memeBox.x + 2f, memeBox.y + 2f, badgeW, badgeH);

        Widgets.DrawBoxSolid(badgeRect, bgColor);
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = textColor;
        Widgets.Label(badgeRect, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        return badgeRect.yMax + 2f;
    }

    // Certainty badge: preview (if this meme added)
    private static void DrawCertaintyBadge(Rect memeBox, float badgeY, MemeDef meme, List<MemeDef> selectedMemes)
    {
        float totalCertainty = 0f;
        var ext = meme.GetModExtension<MemeEffectExtension>();
        if (ext != null) totalCertainty += ext.certaintyPerSeason;

        foreach (var m in selectedMemes)
        {
            if (m.category == MemeCategory.Structure) continue;
            var mExt = m.GetModExtension<MemeEffectExtension>();
            if (mExt != null) totalCertainty += mExt.certaintyPerSeason;
        }

        string text = (totalCertainty * 100f).ToString("F0") + "%";
        if (totalCertainty >= 0f) text = "+" + text;

        DrawCertaintyBadgeRect(memeBox, badgeY, text, totalCertainty);
    }

    // Certainty badge: final (total from all selected)
    private static void DrawCertaintyBadgeFinal(Rect memeBox, float badgeY, List<MemeDef> selectedMemes)
    {
        float totalCertainty = 0f;
        foreach (var m in selectedMemes)
        {
            if (m.category == MemeCategory.Structure) continue;
            var mExt = m.GetModExtension<MemeEffectExtension>();
            if (mExt != null) totalCertainty += mExt.certaintyPerSeason;
        }

        string text = (totalCertainty * 100f).ToString("F0") + "%";
        if (totalCertainty >= 0f) text = "+" + text;

        DrawCertaintyBadgeRect(memeBox, badgeY, text, totalCertainty);
    }

    private static void DrawCertaintyBadgeRect(Rect memeBox, float badgeY, string text, float value)
    {
        Color bgColor = new Color(0.4f, 0.15f, 0.15f, 0.85f);
        Color textColor = new Color(1f, 0.6f, 0.6f);
        if (value >= 0f)
        {
            bgColor = new Color(0.15f, 0.15f, 0.4f, 0.85f);
            textColor = new Color(0.6f, 0.6f, 1f);
        }

        Text.Font = GameFont.Tiny;
        float badgeW = Text.CalcSize(text).x + 6f;
        float badgeH = 18f;
        Rect badgeRect = new Rect(memeBox.x + 2f, badgeY, badgeW, badgeH);

        Widgets.DrawBoxSolid(badgeRect, bgColor);
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = textColor;
        Widgets.Label(badgeRect, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }
}
