using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

// Renamed / reshaped vanilla APIs between RimWorld 1.5 and 1.6
internal static class VersionCompat
{
    public static List<Pawn> FreeColonistsEverywhere =>
#if V16
        PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
#else
        PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists;
#endif
}
