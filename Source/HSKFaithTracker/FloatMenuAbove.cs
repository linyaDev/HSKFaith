using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HSKFaithTracker;

public class FloatMenuAbove : Window
{
    private readonly List<(string label, Action action)> items;
    private readonly Rect buttonRect;
    private static readonly Color BgColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
    private static readonly Color HoverColor = new Color(0.25f, 0.35f, 0.25f, 0.8f);
    private static readonly Color BorderColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    private const float ItemHeight = 32f;
    private const float ItemWidth = 200f;
    private const float Padding = 8f;

    public override float Margin => 0f;
    public override Vector2 InitialSize => new Vector2(ItemWidth + Padding * 2, items.Count * ItemHeight + Padding * 2);

    public FloatMenuAbove(List<(string label, Action action)> items, Rect buttonRect = default)
    {
        this.items = items;
        this.buttonRect = buttonRect;
        doCloseButton = false;
        doCloseX = false;
        draggable = false;
        drawShadow = false;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
    }

    public override void SetInitialSizeAndPosition()
    {
        base.SetInitialSizeAndPosition();
        float x = buttonRect.center.x - windowRect.width / 2f;
        x = Mathf.Clamp(x, 0f, UI.screenWidth - windowRect.width);
        float y = buttonRect.y - windowRect.height - 4f;
        windowRect = new Rect(x, y, windowRect.width, windowRect.height);
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Background
        Widgets.DrawBoxSolid(inRect, BgColor);
        GUI.color = BorderColor;
        Widgets.DrawBox(inRect, 1);
        GUI.color = Color.white;

        float y = Padding;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;

        for (int i = 0; i < items.Count; i++)
        {
            var (label, action) = items[i];
            Rect itemRect = new Rect(Padding, y, ItemWidth, ItemHeight);

            if (Mouse.IsOver(itemRect))
            {
                Widgets.DrawBoxSolid(itemRect, HoverColor);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    action();
                    Close();
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Event.current.Use();
                }
            }

            // Separator between items
            if (i > 0)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.1f);
                Widgets.DrawLineHorizontal(Padding + 4f, y, ItemWidth - 8f);
                GUI.color = Color.white;
            }

            Widgets.Label(itemRect, label);
            y += ItemHeight;
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }
}
