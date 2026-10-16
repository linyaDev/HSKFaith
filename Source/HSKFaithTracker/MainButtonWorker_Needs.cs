using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class MainButtonWorker_Needs : MainButtonWorker
{
    private static bool? dietTrackerLoaded;
    private bool labelSet;
    private static Rect lastButtonRect;

    private static bool IsDietTrackerLoaded
    {
        get
        {
            dietTrackerLoaded ??= ModsConfig.IsActive("linya.hskdiettracker");
            return dietTrackerLoaded.Value;
        }
    }

    public override void DoButton(Rect rect)
    {
        if (!labelSet)
        {
            labelSet = true;
            def.label = IsDietTrackerLoaded ? "FT_ButtonNeeds".Translate() : "FT_ButtonFaith".Translate();
        }
        lastButtonRect = rect;
        base.DoButton(rect);
    }

    public override void Activate()
    {
        bool hasDiet = IsDietTrackerLoaded;

        if (!hasDiet)
        {
            // Only faith tracker — open directly
            var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
            if (comp == null) return;

            var pawn = Find.Selector.SingleSelectedThing as Pawn ?? PawnsFinder.AllMaps_FreeColonists.FirstOrFallback();
            if (pawn != null)
                Find.WindowStack.Add(new Dialog_FaithInfo(pawn));
            return;
        }

        // Multiple mods — show popup
        var options = new List<(string, System.Action)>();

        options.Add(("FT_OpenFaith".Translate(), () =>
        {
            var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
            if (comp == null) return;
            var pawn = Find.Selector.SingleSelectedThing as Pawn ?? PawnsFinder.AllMaps_FreeColonists.FirstOrFallback();
            if (pawn != null)
                Find.WindowStack.Add(new Dialog_FaithInfo(pawn));
        }));

        if (hasDiet)
        {
            options.Add(("FT_OpenDiet".Translate(), () =>
            {
                var type = GenTypes.GetTypeInAnyAssembly("HSKDietTracker.Dialog_DietInfo");
                if (type != null)
                {
                    var pawn = Find.Selector.SingleSelectedThing as Pawn ?? PawnsFinder.AllMaps_FreeColonists.FirstOrFallback();
                    if (pawn != null)
                    {
                        var dialog = System.Activator.CreateInstance(type, pawn) as Window;
                        if (dialog != null)
                            Find.WindowStack.Add(dialog);
                    }
                }
            }));
        }

        Find.WindowStack.Add(new FloatMenuAbove(options, lastButtonRect));
    }
}
