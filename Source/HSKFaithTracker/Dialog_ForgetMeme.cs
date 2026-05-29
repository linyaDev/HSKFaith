using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Dialog_ForgetMeme : Window
{
    private readonly Ideo ideo;

    private static readonly Color HeaderColor = new Color(1f, 0.85f, 0.4f);
    private static readonly Color DimText = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color RedText = new Color(0.95f, 0.4f, 0.4f);
    private static readonly Color RowBg = new Color(0.15f, 0.15f, 0.25f, 0.5f);

    public override Vector2 InitialSize => new Vector2(400f, 350f);

    public Dialog_ForgetMeme(Ideo ideo)
    {
        this.ideo = ideo;
        doCloseButton = true;
        doCloseX = true;
        draggable = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        GUI.color = HeaderColor;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "FT_ForgetMeme".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        float y = 40f;

        // Warning
        GUI.color = RedText;
        Widgets.Label(new Rect(0f, y, inRect.width, 20f), "FT_ForgetMemeWarn".Translate());
        GUI.color = Color.white;
        y += 24f;

        // List memes
        var memes = ideo.memes.Where(m => m.category != MemeCategory.Structure).ToList();

        for (int i = 0; i < memes.Count; i++)
        {
            var meme = memes[i];
            Rect rowRect = new Rect(0f, y, inRect.width, 36f);

            if (i % 2 == 0)
                Widgets.DrawBoxSolid(rowRect, RowBg);

            // Icon
            Texture2D icon = meme.Icon;
            if (icon != null)
                GUI.DrawTexture(new Rect(5f, y + 2f, 32f, 32f), icon, ScaleMode.ScaleToFit);

            // Name
            Widgets.Label(new Rect(42f, y, inRect.width - 150f, 36f), meme.LabelCap);

            // Forget button
            if (Widgets.ButtonText(new Rect(inRect.width - 100f, y + 4f, 90f, 28f), "FT_Forget".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "FT_ForgetConfirm".Translate(meme.LabelCap),
                    () => DoForget(meme)));
            }

            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
                TooltipHandler.TipRegion(rowRect, meme.description);
            }

            y += 38f;
        }
    }

    private void DoForget(MemeDef meme)
    {
        ideo.memes.Remove(meme);
        ideo.RecachePrecepts();

        // Mood penalty for all colonists
        var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("FT_MemeForgotten_Thought");
        if (thoughtDef != null)
        {
            foreach (var p in PawnsFinder.AllMaps_FreeColonists)
            {
                if (p.IsSlave || p.needs?.mood == null) continue;
                p.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
            }
        }

        Messages.Message("FT_MemeForgottenMsg".Translate(meme.LabelCap), MessageTypeDefOf.NeutralEvent);

        Close();
    }
}
