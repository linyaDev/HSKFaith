using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Dialog_FaithDebug : Window
{
    public override Vector2 InitialSize => new Vector2(400f, 300f);

    public Dialog_FaithDebug()
    {
        doCloseButton = true;
        doCloseX = true;
        draggable = true;
        absorbInputAroundWindow = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "FaithTracker Debug");
        Text.Font = GameFont.Small;
    }
}
