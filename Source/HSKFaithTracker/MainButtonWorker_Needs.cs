using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class MainButtonWorker_Needs : MainButtonWorker
{
    private static bool? dietTrackerLoaded;
    private static bool? onlineLoaded;
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

    private static bool IsOnlineLoaded
    {
        get
        {
            onlineLoaded ??= ModsConfig.IsActive("linya.hskmultiplayer");
            return onlineLoaded.Value;
        }
    }

    private static bool OnlineInSession()
    {
        var bridge = GenTypes.GetTypeInAnyAssembly("HSKMultiplayer.Online.MultiplayerBridge");
        PropertyInfo prop = bridge?.GetProperty("InSession", BindingFlags.Public | BindingFlags.Static);
        return prop != null && (bool)prop.GetValue(null, null);
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
        bool hasOnline = IsOnlineLoaded && OnlineInSession();

        if (!hasDiet && !hasOnline)
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

        if (hasOnline)
        {
            options.Add(("FT_OpenOnline".Translate(), () =>
            {
                var type = GenTypes.GetTypeInAnyAssembly("HSKMultiplayer.Online.Dialog_OnlineInfo");
                if (type == null || Find.WindowStack.IsOpen(type))
                    return;

                var dialog = System.Activator.CreateInstance(type) as Window;
                if (dialog != null)
                    Find.WindowStack.Add(dialog);
            }));
        }

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
