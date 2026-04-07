using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

public enum RitualRecordType : byte
{
    Fulfilled,
    Missed
}

public struct RitualRecord : IExposable
{
    public int tick;
    public string ritualName;
    public RitualRecordType type;

    public void ExposeData()
    {
        Scribe_Values.Look(ref tick, "tick");
        Scribe_Values.Look(ref ritualName, "ritualName");
        Scribe_Values.Look(ref type, "type");
    }
}

public class GameComponent_FaithTracker : GameComponent
{
    private List<RitualRecord> records = new List<RitualRecord>();
    private int cleanupCounter;
    private bool penalty15Applied;
    private int nextYearlyPenaltyTick;
    private const int YearTicks = 3600000;
    private const int Day15Ticks = 900000;
    private const int Day30Ticks = 1800000;

    public const int FulfilledWeight = 3;
    public const int MissedWeight = -2;
    public const float ScoreMin = -30f;
    public const float ScoreMax = 30f;

    public GameComponent_FaithTracker(Game game) : base()
    {
    }

    public int Score
    {
        get
        {
            int score = 0;
            foreach (var r in records)
                score += r.type == RitualRecordType.Fulfilled ? FulfilledWeight : MissedWeight;
            return score;
        }
    }

    public List<RitualRecord> Records => records;

    public void RecordRitual(string ritualName, RitualRecordType type)
    {
        records.Add(new RitualRecord
        {
            tick = Find.TickManager.TicksGame,
            ritualName = ritualName,
            type = type
        });
    }

    public override void GameComponentTick()
    {
        cleanupCounter++;
        if (cleanupCounter < 60000)
            return;
        cleanupCounter = 0;

        // Cleanup old records
        int cutoff = Find.TickManager.TicksGame - YearTicks;
        records.RemoveAll(r => r.tick < cutoff);

        // Early game penalties (only if > 3 colonists)
        if (!ModsConfig.IdeologyActive)
            return;

        int colonists = PawnsFinder.AllMaps_FreeColonists.Count;
        if (colonists <= 3)
            return;

        int ticks = GenTicks.TicksGame;

        // Day 15 penalty: -3
        if (!penalty15Applied && ticks >= Day15Ticks)
        {
            penalty15Applied = true;
            RecordRitual("FT_EarlyPenalty".Translate(), RitualRecordType.Missed);
        }

        // Day 30 penalty and yearly repeat: -((rituals / 2) + memes * 2)
        if (nextYearlyPenaltyTick <= 0)
            nextYearlyPenaltyTick = Day30Ticks;

        if (ticks >= nextYearlyPenaltyTick)
        {
            nextYearlyPenaltyTick = ticks + YearTicks;
            var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (ideo != null)
            {
                int ritualCount = ideo.PreceptsListForReading.Count(p => p is Precept_Ritual);
                int memeCount = ideo.memes?.Count ?? 0;
                int penaltyCount = (ritualCount / 2) + (memeCount * 2);
                for (int i = 0; i < penaltyCount; i++)
                {
                    RecordRitual("FT_FaithDecay".Translate(), RitualRecordType.Missed);
                }
            }
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref records, "records", LookMode.Deep);
        Scribe_Values.Look(ref penalty15Applied, "penalty15Applied");
        Scribe_Values.Look(ref nextYearlyPenaltyTick, "nextYearlyPenaltyTick");
        if (records == null)
            records = new List<RitualRecord>();
    }
}
