using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Dialog_MemeInfo : Window
{
    private readonly MemeDef meme;
    private Vector2 scrollPosition;

    private static readonly Color HeaderColor = new Color(1f, 0.85f, 0.4f);
    private static readonly Color DimText = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color GreenText = new Color(0.4f, 0.95f, 0.4f);
    private static readonly Color RedText = new Color(0.95f, 0.4f, 0.4f);
    private static readonly Color RowBg = new Color(0.15f, 0.15f, 0.25f, 0.5f);

    // Stats where lower value = better for the player
    private static readonly HashSet<string> InvertedStats = new HashSet<string>
    {
        "SlaveSuppressionFallRate",
        "IncomingDamageFactor",
        "MentalBreakThreshold",
        "RestFallRateFactor",
        "JoyFallRateFactor",
        "MeleeCooldownFactor",
        "RangedCooldownFactor",
        "AimingDelayFactor",
        "StaggerDurationFactor",
        "PawnTrapSpringChance",
        "FoodPoisonChance",
        "FilthRate",
    };

    public override Vector2 InitialSize => new Vector2(450f, 500f);

    public Dialog_MemeInfo(MemeDef meme)
    {
        this.meme = meme;
        doCloseButton = true;
        doCloseX = true;
        draggable = true;
        absorbInputAroundWindow = false;
    }

    public override void SetInitialSizeAndPosition()
    {
        base.SetInitialSizeAndPosition();
        // Position to the right of Dialog_FaithInfo if open
        var faithWindow = Find.WindowStack.Windows.FirstOrDefault(w => w is Dialog_FaithInfo);
        if (faithWindow != null)
        {
            windowRect.x = faithWindow.windowRect.xMax + 10f;
            windowRect.y = faithWindow.windowRect.y;
            // Clamp to screen
            if (windowRect.xMax > UI.screenWidth)
                windowRect.x = faithWindow.windowRect.x - windowRect.width - 10f;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Title: icon + name
        Texture2D icon = meme.Icon;
        if (icon != null)
            GUI.DrawTexture(new Rect(0f, 0f, 32f, 32f), icon, ScaleMode.ScaleToFit);

        Text.Font = GameFont.Medium;
        GUI.color = HeaderColor;
        Widgets.Label(new Rect(38f, 0f, inRect.width - 38f, 35f), meme.LabelCap);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        float y = 38f;

        // Description
        GUI.color = DimText;
        float descH = Text.CalcHeight(meme.description, inRect.width);
        Widgets.Label(new Rect(0f, y, inRect.width, descH), meme.description);
        GUI.color = Color.white;
        y += descH + 4f;

        // Separator
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        Widgets.DrawLineHorizontal(0f, y, inRect.width);
        GUI.color = Color.white;
        y += 4f;

        // Collect stat effects from MemeEffectExtension
        var ext = meme.GetModExtension<MemeEffectExtension>();
        var statItems = new List<StatItem>();
        if (ext?.statOffsets != null)
        {
            foreach (var mod in ext.statOffsets)
            {
                bool positive = InvertedStats.Contains(mod.stat.defName) ? mod.value < 0 : mod.value > 0;
                string valueStr = mod.stat.ValueToString(mod.value, ToStringNumberSense.Offset);
                statItems.Add(new StatItem
                {
                    label = mod.stat.LabelCap + ": " + valueStr,
                    tooltip = mod.stat.description,
                    positive = positive
                });
            }
        }
        if (ext?.statFactors != null)
        {
            foreach (var mod in ext.statFactors)
            {
                bool positive = mod.value > 1f;
                string valueStr = mod.stat.ValueToString(mod.value, ToStringNumberSense.Factor);
                statItems.Add(new StatItem
                {
                    label = mod.stat.LabelCap + ": " + valueStr,
                    tooltip = mod.stat.description,
                    positive = positive
                });
            }
        }
        if (ext?.biomeStatBonuses != null)
        {
            foreach (var bsb in ext.biomeStatBonuses)
            {
                if (bsb.statOffsets == null || bsb.biomes == null) continue;
                string biomeList = string.Join(", ", bsb.biomes);
                foreach (var mod in bsb.statOffsets)
                {
                    string valueStr = mod.stat.ValueToString(mod.value, ToStringNumberSense.Offset);
                    statItems.Add(new StatItem
                    {
                        label = mod.stat.LabelCap + ": " + valueStr + " (" + "FT_BiomeBonus".Translate() + ")",
                        tooltip = "FT_BiomeBonusList".Translate(biomeList),
                        positive = mod.value > 0
                    });
                }
            }
        }
        if (ext != null && ext.seasonalFaithChange != 0)
        {
            statItems.Add(new StatItem
            {
                label = "FT_MemeSeasonalFaith".Translate(ext.seasonalFaithChange.ToStringWithSign()),
                positive = ext.seasonalFaithChange > 0
            });
        }
        if (ext != null && ext.yearlyGoodwillChange != 0)
        {
            statItems.Add(new StatItem
            {
                label = "FT_MemeGoodwill".Translate(ext.yearlyGoodwillChange.ToStringWithSign()),
                positive = ext.yearlyGoodwillChange > 0
            });
        }

        // Scrollable content
        float contentH = (statItems.Count > 0 ? 26f + statItems.Count * 28f : 26f) + 10f;
        Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
        Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, contentH);

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        float rowY = 0f;

        if (statItems.Count > 0)
        {
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(0f, rowY, viewRect.width, 24f), "FT_MemeEffects".Translate());
            GUI.color = Color.white;
            rowY += 26f;

            for (int i = 0; i < statItems.Count; i++)
            {
                var item = statItems[i];
                Rect rowRect = new Rect(0f, rowY, viewRect.width, 26f);

                if (i % 2 == 0)
                    Widgets.DrawBoxSolid(rowRect, RowBg);

                GUI.color = item.positive ? GreenText : RedText;
                Widgets.Label(new Rect(5f, rowY, 20f, 26f), item.positive ? "+" : "−");
                GUI.color = Color.white;

                Widgets.Label(new Rect(22f, rowY, viewRect.width - 27f, 26f), item.label);

                if (Mouse.IsOver(rowRect) && !item.tooltip.NullOrEmpty())
                {
                    Widgets.DrawHighlight(rowRect);
                    TooltipHandler.TipRegion(rowRect, item.tooltip);
                }

                rowY += 28f;
            }
        }
        else
        {
            GUI.color = DimText;
            Widgets.Label(new Rect(0f, rowY, viewRect.width, 24f), "FT_MemeNoEffects".Translate());
            GUI.color = Color.white;
        }

        Widgets.EndScrollView();
    }

    private struct StatItem
    {
        public string label;
        public string tooltip;
        public bool positive;
    }
}
