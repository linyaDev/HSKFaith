using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public enum RitualRecordType : byte
{
    Fulfilled,
    Missed,
    CorpsePenalty,
    FaithDecay
}

public struct RitualRecord : IExposable
{
    public int tick;
    public string ritualName;
    public RitualRecordType type;
    public int thingId;
    public int customWeight;

    public int Points
    {
        get
        {
            switch (type)
            {
                case RitualRecordType.Fulfilled: return customWeight != 0 ? customWeight : 1;
                case RitualRecordType.FaithDecay: return customWeight;
                case RitualRecordType.CorpsePenalty: return GameComponent_FaithTracker.CorpsePenaltyWeight;
                default: return GameComponent_FaithTracker.MissedWeight;
            }
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref tick, "tick");
        Scribe_Values.Look(ref ritualName, "ritualName");
        Scribe_Values.Look(ref type, "type");
        Scribe_Values.Look(ref thingId, "thingId");
        Scribe_Values.Look(ref customWeight, "customWeight");
    }
}

public class GameComponent_FaithTracker : GameComponent
{
    // === Constants ===
    public const int MissedWeight = -2;
    public const int CorpsePenaltyWeight = -1;
    public const float ScoreMin = -30f;
    public const float ScoreMax = 30f;
    private const int YearTicks = 3600000;
    private const int SeasonTicks = 900000;
    private const int MemeCheckInterval = 2500; // 1 game hour

    // === Records ===
    private List<RitualRecord> records = new List<RitualRecord>();

    // === Tick management ===
    private int cleanupCounter;
    private int nextYearlyPenaltyTick;
    private int lastDailyCheck;
    private int lastBerrySpawnDay = -1;
    private int lastSeasonCheck;
    private bool firstSeasonStarted;
    private int lastMemeHash;
    private int lastMemeCheckTick;
    private bool startupLogged;

    // === Dev points tracking: once per year per ritual type ===
    public List<string> ritualDevPointsThisYear = new List<string>();

    // === Meme trackers ===
    public float slaveryPoints;
    public int collectivistPoints;
    public int malePoints;
    public int femalePoints;
    public int nudismPoints;
    public int nudismDressedPoints;
    public int naturePlantsSown;
    public int natureTreesCut;
    public int treesSown;
    public int treeConnectionPoints;
    public int cachedHereticCount;
    public int animalKills;
    public int animalCompanionPoints;
    public int xenoPhiliaMainPoints;
    public int xenoPhiliaOtherPoints;
    public int xenoMainRacePoints;
    public int xenoOtherRacePoints;

    // Bloodfeeding mechanic
    public int bloodfeedCount;

    // HighLife mechanic
    public int highLifePoints;

    // Guilty mechanic
    public int guiltyWounds;
    public int guiltyCharityPoints;

    // Raider mechanic
    public int raiderPoints;
    public int raiderProtectionUntilTick;
    public int raiderProtectionCooldownUntilTick;

    // Tunneler mechanic
    public int tunnelerPoints;

    // Darkness mechanic
    public int darknessPoints;

    // Rancher mechanic
    public int rancherPoints;
    public Dictionary<string, int> rancherBirths = new Dictionary<string, int>();
    public Dictionary<string, int> rancherPtsPerAnimal = new Dictionary<string, int>();

    public void AddRancherEvent(string animalLabel, int pts)
    {
        rancherPoints += pts;
        if (rancherBirths.ContainsKey(animalLabel))
            rancherBirths[animalLabel] += 1;
        else
            rancherBirths[animalLabel] = 1;
        rancherPtsPerAnimal[animalLabel] = pts;
    }

    // Proselytizer mechanic
    public int proselytizePoints;

    // FleshPurity / Transhumanist mechanic
    public int purePoints;
    public int implantedPoints;
    public int transImplantedPoints;
    public int transPurePoints;

    // PainIsVirtue mechanic
    public int scarredPoints;
    public int unscarredPoints;

    // Cannibal mechanic
    public int cannibalPoints;

    // Blindsight mechanic
    public int blindPoints;
    public int sightedPoints;
    public int TrueSightDayCounter;

    // === Constructor ===
    public GameComponent_FaithTracker(Game game) : base() { }

    // =========================================================================
    // Properties
    // =========================================================================

    public List<RitualRecord> Records => records;

    public int Score
    {
        get
        {
            int score = 0;
            foreach (var r in records)
                score += r.Points;
            return score;
        }
    }

    public int TicksUntilYearEnd
    {
        get
        {
            if (nextYearlyPenaltyTick <= 0) return 60 * 60000;
            int ticksLeft = nextYearlyPenaltyTick - Find.TickManager.TicksGame;
            return ticksLeft > 0 ? ticksLeft : 0;
        }
    }

    public string YearTimerString
    {
        get
        {
            int ticks = TicksUntilYearEnd;
            int days = ticks / 60000;
            if (days >= 1)
                return "FT_YearTimer".Translate(days);
            int hours = ticks / 2500;
            return "FT_YearTimerHours".Translate(hours);
        }
    }

    public int YearlyPenaltyForecast
    {
        get
        {
            int colonists = PawnsFinder.AllMaps_FreeColonists.Count;
            if (colonists <= 3) return 0;

            var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (ideo == null) return 0;

            int holidays = 0;
            foreach (var p in ideo.PreceptsListForReading)
            {
                if (p is Precept_Ritual r && r.obligationTriggers?.Any(t => t is RitualObligationTrigger_Date) == true)
                    holidays++;
            }

            return -(holidays * 2);
        }
    }

    public int MemeCount
    {
        get
        {
            var memes = GetPlayerMemes();
            if (memes == null) return 0;
            int count = 0;
            foreach (var m in memes)
                if (m.category != MemeCategory.Structure)
                    count++;
            return count;
        }
    }

    public float SlaveryMax => MemeCount * 15f;
    public int CollectivistTarget => MemeCount * 2;

    public int SlaveCount
    {
        get
        {
            int count = 0;
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.SlavesOfColonySpawned)
                    count++;
            return count;
        }
    }

    // === Meme presence checks ===

    public bool HasMeme(string defName)
    {
        var memes = GetPlayerMemes();
        return memes?.Any(m => m.defName == defName) == true;
    }

    public bool HasSupremacist => ModsConfig.IdeologyActive && HasMeme("Supremacist");
    public bool HasGenderSupremacy => HasMeme("MaleSupremacy") || HasMeme("FemaleSupremacy");
    public bool HasNudism => HasMeme("Nudism");
    public bool HasHumanPrimacy => HasMeme("HumanPrimacy");

    // =========================================================================
    // Helpers
    // =========================================================================

    private static List<MemeDef> GetPlayerMemes()
    {
        return Faction.OfPlayer?.ideos?.PrimaryIdeo?.memes;
    }

    public static int FilledFromRatio(float ratio, int sections)
    {
        if (sections <= 0) return 0;
        return System.Math.Min((int)(ratio * sections + 0.5f), sections);
    }

    private void RecordSections(string memeName, int filled, int unfilled)
    {
        var memes = GetPlayerMemes();
        if (memes == null) return;
        var meme = memes.FirstOrDefault(m => m.defName == memeName);
        if (meme == null) return;
        var ext = meme.GetModExtension<MemeEffectExtension>();

        int total = ext != null ? ext.ComputeSeasonTotal(filled, unfilled) : 0;

        if (total == 0) return;

        var type = total > 0 ? RitualRecordType.Fulfilled : RitualRecordType.FaithDecay;
        RecordRitual(meme.LabelCap, type, customWeight: total);
    }

    private void UpdateHereticCount()
    {
        var playerIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (playerIdeo == null) { cachedHereticCount = 0; return; }

        int count = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsQuestLodger()) continue;
            if (p.Ideo != null && p.Ideo != playerIdeo)
                count++;
        }
        cachedHereticCount = count;
    }

    private static void DebugLog(string message)
    {
        try { File.AppendAllText(@"D:\Mods\faith_debug.txt", message + "\n"); } catch { }
    }

    /// <summary>Counts colonists by race (excluding slaves). Returns (mainRace, mainCount, otherCount).</summary>
    private static (string mainRace, int mainCount, int otherCount) CountRaces()
    {
        var raceCounts = new Dictionary<string, int>();
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            string race = p.def.defName;
            if (raceCounts.ContainsKey(race)) raceCounts[race]++;
            else raceCounts[race] = 1;
        }

        string mainRace = null;
        int mainCount = 0, otherCount = 0;
        foreach (var kvp in raceCounts)
        {
            if (kvp.Value > mainCount)
            {
                if (mainRace != null) otherCount += mainCount;
                mainRace = kvp.Key;
                mainCount = kvp.Value;
            }
            else otherCount += kvp.Value;
        }

        return (mainRace, mainCount, otherCount);
    }

    private Pawn FindLeaderOrMoral()
    {
        if (!ModsConfig.IdeologyActive) return null;
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return null;

        foreach (var p in ideo.PreceptsListForReading)
        {
            if (p is Precept_RoleSingle role && role.ChosenPawnSingle() != null && role.ChosenPawnSingle().Spawned)
                return role.ChosenPawnSingle();
        }
        return null;
    }

    // =========================================================================
    // Recording
    // =========================================================================

    public void RecordRitual(string ritualName, RitualRecordType type, int thingId = 0, int customWeight = 0, bool showMote = true)
    {
        records.Add(new RitualRecord
        {
            tick = Find.TickManager.TicksGame,
            ritualName = ritualName,
            type = type,
            thingId = thingId,
            customWeight = customWeight
        });

        if (showMote)
        {
            var last = records[records.Count - 1];
            ShowMoteOverLeader(last.Points, "FT_MoteFaith".Translate());
        }
    }

    private void ShowMoteOverLeader(int points, string label)
    {
        var leader = FindLeaderOrMoral();
        if (leader == null || !leader.Spawned) return;

        string text = (points > 0 ? "+" : "") + points + " " + label;
        Color color = points > 0 ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
        MoteMaker.ThrowText(leader.DrawPos, leader.Map, text, color);
    }

    // =========================================================================
    // Main tick
    // =========================================================================

    public override void GameComponentTick()
    {
        if (!startupLogged)
            LogStartup();

        if (!ModsConfig.IdeologyActive)
            return;

        TickDaily();
        TickSeasonCheck();
        TickDarknessHediff();

        // Slow tasks — once per game day (60000 ticks)
        cleanupCounter++;
        if (cleanupCounter < 60000)
            return;
        cleanupCounter = 0;

        CleanupRecords();
        TickYearlyPenalty();
    }

    // =========================================================================
    // Daily tick — runs once per game day, or immediately when memes change
    // =========================================================================

    private void TickDaily()
    {
        int today = GenDate.DaysPassed;
        int ticks = Find.TickManager.TicksGame;

        // Check meme changes every game hour
        if (ticks - lastMemeCheckTick >= MemeCheckInterval)
        {
            lastMemeCheckTick = ticks;
            HourlyNudism();
            UpdateHereticCount();
            int memeHash = ComputeMemeHash();
            bool changed = memeHash != lastMemeHash;
            DebugLog($"HASH tick={ticks}: new={memeHash}, old={lastMemeHash}, changed={changed}, today={today}, lastDaily={lastDailyCheck}");
            if (changed && memeHash != 0)
            {
                lastMemeHash = memeHash;
                if (lastMemeHash != 0)
                    lastDailyCheck = -1;
            }
        }

        if (today == lastDailyCheck)
            return;

        lastDailyCheck = today;
        DebugLog($"DAILY day={today}: entering daily block");

        DailySlavery();
        DailyGender();
        DailyXenophilia();
        DailyXenophobia();
        DailyBlindsight();
        DailyAnimalCompanion();
        DailyPainIsVirtue();
        DailyFleshPurity();
        DailyTranshumanist();
        UpdateRancherHediffs();
        UpdateDarknessHediffs();
        UpdateNudismHediffs();
        UpdateSupremacistHediffs();
        UpdateGenderHediffs();
        DailyTreeConnection();
        DailyNaturePrimacySpawn();
    }

    private int ComputeMemeHash()
    {
        int hash = 0;
        var memes = GetPlayerMemes();
        if (memes != null)
            foreach (var m in memes)
                hash ^= m.defName.GetHashCode();
        return hash;
    }

    private void DailySlavery()
    {
        if (!HasSupremacist) return;
        int slaves = SlaveCount;
        slaveryPoints += slaves;
        if (slaveryPoints > SlaveryMax)
            slaveryPoints = SlaveryMax;
    }

    private void DailyGender()
    {
        if (!HasGenderSupremacy) return;

        int males = 0, females = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            if (p.gender == Gender.Male) males++;
            else if (p.gender == Gender.Female) females++;
        }

        int maleSlaves = 0, femaleSlaves = 0;
        foreach (var p in Find.CurrentMap?.mapPawns?.SlavesOfColonySpawned ?? new List<Pawn>())
        {
            if (p.gender == Gender.Male) maleSlaves++;
            else if (p.gender == Gender.Female) femaleSlaves++;
        }

        malePoints += males;
        femalePoints += females;

        bool isMaleSupremacy = HasMeme("MaleSupremacy");
        if (isMaleSupremacy)
            malePoints += femaleSlaves * 3;
        else
            femalePoints += maleSlaves * 3;
    }

    private void HourlyNudism()
    {
        if (!HasNudism) return;
        int nude = 0, dressed = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            bool isNude = true;
            if (p.apparel != null)
            {
                foreach (var ap in p.apparel.WornApparel)
                {
                    if (ap.def.apparel.defaultOutfitTags != null && ap.def.apparel.defaultOutfitTags.Contains("Nudist"))
                        continue;
                    foreach (var group in ap.def.apparel.bodyPartGroups)
                    {
                        if (group == BodyPartGroupDefOf.Torso || group == BodyPartGroupDefOf.Legs)
                        {
                            isNude = false;
                            break;
                        }
                    }
                    if (!isNude) break;
                }
            }
            if (isNude)
            {
                nude++;
                if (p.story?.traits != null && p.story.traits.HasTrait(TraitDefOf.Nudist))
                    nude += 2;
            }
            else dressed++;
        }
        nudismPoints += nude;
        nudismDressedPoints += dressed;
    }

    private void DailyXenophilia()
    {
        if (!HasMeme("HAR_Xenophilia")) return;

        var (mainRace, mainCount, otherCount) = CountRaces();
        xenoPhiliaOtherPoints += otherCount;
        xenoPhiliaMainPoints += mainCount;
        DebugLog($"XENO: mainRace={mainRace}, main={mainCount}, other={otherCount}, totalMain={xenoPhiliaMainPoints}, totalOther={xenoPhiliaOtherPoints}");
    }

    private void DailyXenophobia()
    {
        if (!HasMeme("HAR_Xenophobia")) return;

        var (mainRace, mainCount, otherCount) = CountRaces();
        xenoMainRacePoints += mainCount;
        xenoOtherRacePoints += otherCount;
        DebugLog($"XENOPHOBIA: mainRace={mainRace}, main={mainCount}, other={otherCount}, totalMain={xenoMainRacePoints}, totalOther={xenoOtherRacePoints}");
    }


    private void DailyTranshumanist()
    {
        if (!HasMeme("Transhumanist")) return;

        int impl = 0, pure = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            bool hasImplant = false;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_AddedPart || h is Hediff_Implant)
                {
                    hasImplant = true;
                    break;
                }
            }
            if (hasImplant) impl++;
            else pure++;
        }
        transImplantedPoints += impl;
        transPurePoints += pure;
    }

    private void DailyAnimalCompanion()
    {
        if (!HasMeme("AnimalPersonhood")) return;

        int count = 0;
        foreach (var map in Find.Maps)
        {
            foreach (var animal in map.mapPawns.AllPawnsSpawned)
            {
                if (animal.RaceProps.Animal && animal.Faction == Faction.OfPlayer && !animal.RaceProps.FenceBlocked)
                    count++;
            }
        }
        animalCompanionPoints += count;
    }

    private void DailyFleshPurity()
    {
        if (!HasMeme("FleshPurity")) return;

        int pure = 0, implanted = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            bool hasImplant = false;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_AddedPart || h is Hediff_Implant)
                {
                    hasImplant = true;
                    break;
                }
            }
            if (hasImplant) implanted++;
            else pure++;
        }
        purePoints += pure;
        implantedPoints += implanted;
    }

    private void DailyPainIsVirtue()
    {
        if (!HasMeme("PainIsVirtue")) return;

        int scarred = 0, unscarred = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            bool hasScar = false;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury injury && injury.IsPermanent())
                {
                    hasScar = true;
                    break;
                }
            }
            if (hasScar) scarred++;
            else unscarred++;
        }
        scarredPoints += scarred;
        unscarredPoints += unscarred;
    }

    private const float BlindsightPsyThreshold = 1.0f; // severity 1.0 = ready for psylink
    private const float BlindsightDailySeverity = 1f / 60f; // ~60 days (1 year) to fill

    private void DailyBlindsight()
    {
        if (!HasMeme("Blindsight")) return;

        var hediffDef = DefDatabase<HediffDef>.GetNamed("FT_BlindsightPsylink", false);
        var trueSightDef = DefDatabase<HediffDef>.GetNamed("FT_BlindsightTrueSight", false);

        int blind = 0, sighted = 0;
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.IsSlave) continue;
            bool isBlind = !p.health.capacities.CapableOf(PawnCapacityDefOf.Sight);
            if (isBlind)
            {
                blind++;

                // Psylink hediff: accumulate severity, grant psylink at threshold
                if (hediffDef != null)
                {
                    var hediff = p.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                    if (hediff != null)
                    {
                        hediff.Severity += BlindsightDailySeverity;
                        if (hediff.Severity >= BlindsightPsyThreshold)
                        {
                            var psylink = p.GetMainPsylinkSource();
                            if (psylink != null)
                            {
                                psylink.ChangeLevel(1, true);
                            }
                            else
                            {
                                var psylinkDef = DefDatabase<HediffDef>.GetNamed("PsychicAmplifier", false);
                                if (psylinkDef != null)
                                    p.health.AddHediff(psylinkDef, p.health.hediffSet.GetBrain());
                            }
                            hediff.Severity = 0.01f;
                            DebugLog($"BLINDSIGHT PSY: {p.LabelShortCap} gained psylink level");
                        }
                    }
                }

                // True sight: +5% every 15 days via daily counter
                if (trueSightDef != null)
                {
                    var trueSight = p.health.hediffSet.GetFirstHediffOfDef(trueSightDef);
                    if (trueSight != null && trueSight.Severity < 1.0f)
                    {
                        TrueSightDayCounter++;
                        if (TrueSightDayCounter >= 15)
                        {
                            TrueSightDayCounter = 0;
                            trueSight.Severity = System.Math.Min(trueSight.Severity + 0.05f, 1.0f);
                        }
                    }
                }
            }
            else
            {
                sighted++;
                // Remove hediff from sighted colonists
                if (hediffDef != null)
                {
                    var hediff = p.health?.hediffSet?.GetFirstHediffOfDef(hediffDef);
                    if (hediff != null)
                        p.health.RemoveHediff(hediff);
                }
            }
        }
        blindPoints += blind * 3;
        sightedPoints += sighted;
    }

    private void UpdateDarknessHediffs()
    {
        var darkDef = DefDatabase<HediffDef>.GetNamed("FT_DarknessBonus", false);
        var deepDef = DefDatabase<HediffDef>.GetNamed("FT_DeepDarknessBonus", false);
        var lightDef = DefDatabase<HediffDef>.GetNamed("FT_LightWeakness", false);
        if (darkDef == null || deepDef == null || lightDef == null) return;

        if (!HasMeme("Darkness"))
        {
            // Remove all darkness hediffs if meme is gone
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    var h1 = p.health.hediffSet.GetFirstHediffOfDef(darkDef);
                    var h2 = p.health.hediffSet.GetFirstHediffOfDef(deepDef);
                    var h3 = p.health.hediffSet.GetFirstHediffOfDef(lightDef);
                    if (h1 != null) p.health.RemoveHediff(h1);
                    if (h2 != null) p.health.RemoveHediff(h2);
                    if (h3 != null) p.health.RemoveHediff(h3);
                }
            return;
        }

        foreach (var map in Find.Maps)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.IsSlave) continue;
                float glow = map.glowGrid.GroundGlowAt(p.Position);

                var hasDark = p.health.hediffSet.GetFirstHediffOfDef(darkDef);
                var hasDeep = p.health.hediffSet.GetFirstHediffOfDef(deepDef);
                var hasLight = p.health.hediffSet.GetFirstHediffOfDef(lightDef);

                // Deep darkness: glow < 0.1
                if (glow < 0.1f)
                {
                    if (hasDeep == null) p.health.AddHediff(deepDef);
                    if (hasDark != null) p.health.RemoveHediff(hasDark);
                    if (hasLight != null) p.health.RemoveHediff(hasLight);
                }
                // Darkness: glow < 0.3
                else if (glow < 0.3f)
                {
                    if (hasDark == null) p.health.AddHediff(darkDef);
                    if (hasDeep != null) p.health.RemoveHediff(hasDeep);
                    if (hasLight != null) p.health.RemoveHediff(hasLight);
                }
                // Light sensitivity: glow >= 0.5
                else if (glow >= 0.5f)
                {
                    if (hasLight == null) p.health.AddHediff(lightDef);
                    if (hasDark != null) p.health.RemoveHediff(hasDark);
                    if (hasDeep != null) p.health.RemoveHediff(hasDeep);
                }
                // Neutral zone: 0.3-0.5 — remove all
                else
                {
                    if (hasDark != null) p.health.RemoveHediff(hasDark);
                    if (hasDeep != null) p.health.RemoveHediff(hasDeep);
                    if (hasLight != null) p.health.RemoveHediff(hasLight);
                }
            }
        }
    }

    private void DailyTreeConnection()
    {
        if (!HasMeme("TreeConnection")) return;

        int dryadCount = 0;
        foreach (var map in Find.Maps)
        {
            if (!map.IsPlayerHome) continue;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction == Faction.OfPlayer && pawn.RaceProps.Dryad)
                    dryadCount++;
            }
        }
        treeConnectionPoints += dryadCount;
    }

    private void DailyNaturePrimacySpawn()
    {
        if (!HasMeme("NaturePrimacy")) return;

        int today = GenDate.DaysPassed;
        if (today == lastBerrySpawnDay) return;
        lastBerrySpawnDay = today;

        var berryDef = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Berry");
        if (berryDef == null) return;

        var map = Find.CurrentMap;
        if (map == null) return;

        var homeZone = map.areaManager?.Home;
        if (homeZone == null) return;

        // Spawn 1 bush per day
        {
            IntVec3 cell = IntVec3.Invalid;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var candidate = CellFinder.RandomCell(map);

                // 15-75 tiles from home zone
                if (homeZone[candidate]) continue;
                float dist = MinDistToHomeZone(candidate, map, homeZone);
                if (dist < 15f || dist > 75f) continue;

                // Valid growing conditions
                if (map.roofGrid.Roofed(candidate)) continue;
                if (candidate.GetEdifice(map) != null) continue;
                if (candidate.GetPlant(map) != null) continue;
                if (map.terrainGrid.TerrainAt(candidate).fertility < 0.5f) continue;
                if (map.zoneManager.ZoneAt(candidate) is Zone_Growing) continue;

                cell = candidate;
                break;
            }

            if (cell.IsValid)
            {
                var plant = (Plant)GenSpawn.Spawn(berryDef, cell, map);
                plant.Growth = 0.05f;
            }
        }
    }

    private float MinDistToHomeZone(IntVec3 cell, Map map, Area_Home home)
    {
        float minDist = float.MaxValue;
        // Sample nearby cells instead of checking all home zone cells
        int radius = 80;
        int minX = System.Math.Max(0, cell.x - radius);
        int maxX = System.Math.Min(map.Size.x - 1, cell.x + radius);
        int minZ = System.Math.Max(0, cell.z - radius);
        int maxZ = System.Math.Min(map.Size.z - 1, cell.z + radius);
        for (int x = minX; x <= maxX; x += 3)
        {
            for (int z = minZ; z <= maxZ; z += 3)
            {
                var c = new IntVec3(x, 0, z);
                if (home[c])
                {
                    float d = c.DistanceTo(cell);
                    if (d < minDist) minDist = d;
                }
            }
        }
        return minDist;
    }

    private void UpdateGenderHediffs()
    {
        var domDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_GenderDominant");
        var subDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_GenderSubmissive");
        if (domDef == null || subDef == null) return;

        bool isMale = HasMeme("MaleSupremacy");
        bool isFemale = HasMeme("FemaleSupremacy");
        bool hasGender = isMale || isFemale;

        foreach (var map in Find.Maps)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.IsSlave) continue;
                var existDom = p.health.hediffSet.GetFirstHediffOfDef(domDef);
                var existSub = p.health.hediffSet.GetFirstHediffOfDef(subDef);

                if (!hasGender)
                {
                    if (existDom != null) p.health.RemoveHediff(existDom);
                    if (existSub != null) p.health.RemoveHediff(existSub);
                    continue;
                }

                bool isDominant = (isMale && p.gender == Gender.Male) || (isFemale && p.gender == Gender.Female);

                if (isDominant)
                {
                    if (existDom == null) p.health.AddHediff(domDef);
                    if (existSub != null) p.health.RemoveHediff(existSub);
                }
                else
                {
                    if (existSub == null) p.health.AddHediff(subDef);
                    if (existDom != null) p.health.RemoveHediff(existDom);
                }
            }
        }
    }

    private void UpdateSupremacistHediffs()
    {
        var hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_SupremacistVigor");
        if (hediffDef == null) return;

        bool hasSupremacist = HasSupremacist;
        foreach (var map in Find.Maps)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.IsSlave) continue;
                var existing = p.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (hasSupremacist && existing == null)
                    p.health.AddHediff(hediffDef);
                else if (!hasSupremacist && existing != null)
                    p.health.RemoveHediff(existing);
            }
        }
    }

    private void UpdateNudismHediffs()
    {
        var hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_NudismBonus");
        if (hediffDef == null) return;

        if (!HasMeme("Nudism"))
        {
            // Remove all nudism hediffs if meme is gone
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    var h = p.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                    if (h != null) p.health.RemoveHediff(h);
                }
            return;
        }

        foreach (var map in Find.Maps)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.IsSlave) continue;
                var existing = p.health.hediffSet.GetFirstHediffOfDef(hediffDef);

                // Check: all worn apparel is nudist-compatible (tagged "Nudist" or doesn't cover Torso/Legs)
                bool naked = true;
                if (p.apparel != null)
                {
                    foreach (var ap in p.apparel.WornApparel)
                    {
                        if (ap.def.apparel.defaultOutfitTags != null && ap.def.apparel.defaultOutfitTags.Contains("Nudist"))
                            continue;
                        foreach (var group in ap.def.apparel.bodyPartGroups)
                        {
                            if (group == BodyPartGroupDefOf.Torso || group == BodyPartGroupDefOf.Legs)
                            {
                                naked = false;
                                break;
                            }
                        }
                        if (!naked) break;
                    }
                }

                if (naked && existing == null)
                    p.health.AddHediff(hediffDef);
                else if (!naked && existing != null)
                    p.health.RemoveHediff(existing);
            }
        }
    }

    private void UpdateRancherHediffs()
    {
        bool hasRancher = HasMeme("Rancher");
        var hediffDef = DefDatabase<HediffDef>.GetNamed("FT_RancherCare", false);
        if (hediffDef == null) return;

        foreach (var map in Find.Maps)
        {
            foreach (var animal in map.mapPawns.SpawnedColonyAnimals)
            {
                var existing = animal.health?.hediffSet?.GetFirstHediffOfDef(hediffDef);
                if (hasRancher && existing == null)
                    animal.health.AddHediff(hediffDef);
                else if (!hasRancher && existing != null)
                    animal.health.RemoveHediff(existing);
            }
        }
    }

    // =========================================================================
    // Season check — every 2500 ticks (1 game hour)
    // =========================================================================

    private int darknessHediffCounter;

    private void TickDarknessHediff()
    {
        darknessHediffCounter++;
        if (darknessHediffCounter < 180) return;
        darknessHediffCounter = 0;

        if (!HasMeme("Darkness")) return;

        var bonusDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_DarknessBonus");
        var deepDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_DeepDarknessBonus");
        var debuffDef = DefDatabase<HediffDef>.GetNamedSilentFail("FT_LightWeakness");

        foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists)
        {
            if (pawn.Dead || pawn.Map == null) continue;
            float glow = pawn.Map.glowGrid.GroundGlowAt(pawn.Position);
            bool inDeepDark = glow < 0.35f;
            bool inDark = glow < 0.51f;
            bool inBrightLight = glow > 0.75f;

            // Deep darkness: replaces normal darkness bonus
            if (deepDef != null)
            {
                var deep = pawn.health.hediffSet.GetFirstHediffOfDef(deepDef);
                if (inDeepDark && deep == null)
                    pawn.health.AddHediff(deepDef);
                else if (!inDeepDark && deep != null)
                    pawn.health.RemoveHediff(deep);
            }

            // Normal darkness: only if not in deep darkness
            if (bonusDef != null)
            {
                var bonus = pawn.health.hediffSet.GetFirstHediffOfDef(bonusDef);
                if (inDark && !inDeepDark && bonus == null)
                    pawn.health.AddHediff(bonusDef);
                else if ((!inDark || inDeepDark) && bonus != null)
                    pawn.health.RemoveHediff(bonus);
            }

            // Light debuff
            if (debuffDef != null)
            {
                var debuff = pawn.health.hediffSet.GetFirstHediffOfDef(debuffDef);
                if (inBrightLight && debuff == null)
                    pawn.health.AddHediff(debuffDef);
                else if (!inBrightLight && debuff != null)
                    pawn.health.RemoveHediff(debuff);
            }
        }
    }

    private void TickSeasonCheck()
    {
        if (cleanupCounter % 2500 != 0) return;

        int ticks = Find.TickManager.TicksGame;
        int currentSeason = ticks / SeasonTicks;

        if (!firstSeasonStarted)
        {
            firstSeasonStarted = true;
            lastSeasonCheck = currentSeason;
            return;
        }

        if (currentSeason <= lastSeasonCheck)
            return;

        DebugLog($"SEASON END: season={currentSeason}, lastCheck={lastSeasonCheck}, slaveryPts={slaveryPoints}, slaves={SlaveCount}, memes={MemeCount}");
        lastSeasonCheck = currentSeason;

        SeasonSlavery();
        SeasonGender();
        SeasonTreeConnection();
        SeasonAnimalPersonhood();
        SeasonXenophilia();
        SeasonXenophobia();
        SeasonCollectivist();
        SeasonBloodfeeding();
        SeasonHighLife();
        SeasonGuilty();
        SeasonCannibal();
        SeasonBlindsight();
        SeasonPainIsVirtue();
        SeasonFleshPurity();
        SeasonTranshumanist();
        SeasonProselytizer();
        SeasonRancher();
        SeasonTunneler();
        SeasonDarkness();
        SeasonRaider();
        SeasonNudism();
        SeasonNaturePrimacy();
        SeasonPassiveMemes();
        // Certainty now applied per-tick in Patch_CertaintyTick
        GrantSeasonalDevPoint();
        ResetSeasonTrackers();
    }

    // Certainty now applied per-tick in Patch_CertaintyTick using certaintyPerSeason from XML

    private void GrantSeasonalDevPoint()
    {
        var playerIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (playerIdeo == null || !playerIdeo.Fluid)
            return;

        playerIdeo.development.TryAddDevelopmentPoints(1);
        DebugLog("Seasonal dev point granted");
    }

    private void ResetSeasonTrackers()
    {
        slaveryPoints = 0f;
        malePoints = 0;
        femalePoints = 0;
        nudismPoints = 0;
        nudismDressedPoints = 0;
        naturePlantsSown = 0;
        natureTreesCut = 0;
        treesSown = 0;
        treeConnectionPoints = 0;
        xenoPhiliaMainPoints = 0;
        xenoPhiliaOtherPoints = 0;
        xenoMainRacePoints = 0;
        xenoOtherRacePoints = 0;
        bloodfeedCount = 0;
        highLifePoints = 0;
        guiltyWounds = 0;
        cannibalPoints = 0;
        blindPoints = 0;
        sightedPoints = 0;
        purePoints = 0;
        implantedPoints = 0;
        transImplantedPoints = 0;
        transPurePoints = 0;
        proselytizePoints = 0;
        rancherPoints = 0;
        rancherBirths.Clear();
        rancherPtsPerAnimal.Clear();
        raiderPoints = 0;
        tunnelerPoints = 0;
        darknessPoints = 0;
        scarredPoints = 0;
        unscarredPoints = 0;
    }

    private void SeasonSlavery()
    {
        if (!HasSupremacist) return;
        float max = SlaveryMax;
        int memes = MemeCount;
        if (max <= 0f) return;

        float pointsPerSection = max / memes;
        int filled = (int)(slaveryPoints / pointsPerSection);
        int unfilled = memes - filled;

        RecordSections("Supremacist", filled, unfilled);
    }

    private void SeasonGender()
    {
        if (!HasGenderSupremacy) return;
        int total = malePoints + femalePoints;
        if (total <= 0) return;

        bool isMale = HasMeme("MaleSupremacy");
        int dominant = isMale ? malePoints : femalePoints;
        int sections = MemeCount;
        if (sections <= 0) return;

        float ratio = (float)dominant / total;
        int filled = FilledFromRatio(ratio, sections);

        int unfilled = sections - filled;
        string memeName = isMale ? "MaleSupremacy" : "FemaleSupremacy";
        RecordSections(memeName, filled, unfilled);
    }

    private void SeasonTreeConnection()
    {
        if (!HasMeme("TreeConnection")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        // 30 points per section (1 dryad/day = 15 over season, division = 30)
        int filled = System.Math.Min(treeConnectionPoints / 30, sections);
        int unfilled = sections - filled;
        RecordSections("TreeConnection", filled, unfilled);
    }

    private void SeasonAnimalPersonhood()
    {
        if (!HasMeme("AnimalPersonhood"))
        {
            animalCompanionPoints = 0;
            animalKills = 0;
            return;
        }

        int sections = MemeCount;
        if (sections <= 0) { animalCompanionPoints = 0; animalKills = 0; return; }

        // Companion points fill sections (10 pts per section)
        int filled = System.Math.Min(animalCompanionPoints / 45, sections);
        int unfilled = sections - filled;

        RecordSections("AnimalPersonhood", filled, unfilled);

        animalCompanionPoints = 0;
        animalKills = 0;
    }

    private void SeasonXenophilia()
    {
        if (!HasMeme("HAR_Xenophilia")) return;
        int total = xenoPhiliaOtherPoints + xenoPhiliaMainPoints;
        int sections = MemeCount;
        if (sections <= 0 || total <= 0) return;

        float ratio = (float)xenoPhiliaOtherPoints / total;
        int filled = FilledFromRatio(ratio, sections);
        int unfilled = sections - filled;
        RecordSections("HAR_Xenophilia", filled, unfilled);
    }

    private void SeasonXenophobia()
    {
        if (!HasMeme("HAR_Xenophobia")) return;
        int total = xenoMainRacePoints + xenoOtherRacePoints;
        int sections = MemeCount;
        if (sections <= 0 || total <= 0) return;

        float ratio = (float)xenoMainRacePoints / total;
        int filled = FilledFromRatio(ratio, sections);
        int unfilled = sections - filled;
        RecordSections("HAR_Xenophobia", filled, unfilled);
    }

    private void SeasonCollectivist()
    {
        if (!HasHumanPrimacy) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        int filled = System.Math.Min(collectivistPoints / 2, sections);
        int unfilled = sections - filled;

        RecordSections("Collectivist", filled, unfilled);

        collectivistPoints = 0;
    }

    private void SeasonBloodfeeding()
    {
        if (!HasMeme("Bloodfeeding")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        // 6 points per section (bite=3, pack=1)
        int filled = System.Math.Min(bloodfeedCount / 6, sections);
        int unfilled = sections - filled;
        RecordSections("Bloodfeeding", filled, unfilled);
    }

    private void SeasonRaider()
    {
        if (!HasMeme("Raider")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        // 1 raid per section
        int filled = System.Math.Min(raiderPoints, sections);
        int unfilled = sections - filled;

        RecordSections("Raider", filled, unfilled);
    }

    private void SeasonTunneler()
    {
        if (!HasMeme("Tunneler")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        int filled = System.Math.Min(tunnelerPoints / 20, sections);
        int unfilled = sections - filled;

        RecordSections("Tunneler", filled, unfilled);
    }



    private void SeasonDarkness()
    {
        if (!HasMeme("Darkness")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        int filled = System.Math.Min(darknessPoints / 10, sections);
        int unfilled = sections - filled;

        RecordSections("Darkness", filled, unfilled);
    }

    private void SeasonRancher()
    {
        if (!HasMeme("Rancher")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        int filled = System.Math.Min(rancherPoints / 10, sections);
        int unfilled = sections - filled;

        RecordSections("Rancher", filled, unfilled);
    }

    private void SeasonProselytizer()
    {
        if (!HasMeme("Proselytizer")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        // 1 point per section
        int filled = System.Math.Min(proselytizePoints, sections);
        int unfilled = sections - filled;

        RecordSections("Proselytizer", filled, unfilled);
    }

    private void SeasonTranshumanist()
    {
        if (!HasMeme("Transhumanist")) return;
        int total = transImplantedPoints + transPurePoints;
        int sections = MemeCount;
        if (sections <= 0 || total <= 0) return;

        // Implanted should dominate
        float ratio = (float)transImplantedPoints / total;
        int filled = FilledFromRatio(ratio, sections);
        int unfilled = sections - filled;
        RecordSections("Transhumanist", filled, unfilled);
    }

    private void SeasonFleshPurity()
    {
        if (!HasMeme("FleshPurity")) return;
        int total = purePoints + implantedPoints;
        int sections = MemeCount;
        if (sections <= 0 || total <= 0) return;

        float ratio = (float)purePoints / total;
        int filled = FilledFromRatio(ratio, sections);
        int unfilled = sections - filled;

        RecordSections("FleshPurity", filled, unfilled);
    }

    private void SeasonPainIsVirtue()
    {
        if (!HasMeme("PainIsVirtue")) return;
        int total = scarredPoints + unscarredPoints;
        int sections = MemeCount;
        if (sections <= 0 || total <= 0) return;

        float ratio = (float)scarredPoints / total;
        int filled = FilledFromRatio(ratio, sections);
        int unfilled = sections - filled;
        RecordSections("PainIsVirtue", filled, unfilled);
    }

    private void SeasonBlindsight()
    {
        if (!HasMeme("Blindsight")) return;
        int total = blindPoints + sightedPoints;
        int sections = MemeCount;
        if (sections <= 0 || total <= 0) return;

        float ratio = (float)blindPoints / total;
        int filled = FilledFromRatio(ratio, sections);
        int unfilled = sections - filled;
        RecordSections("Blindsight", filled, unfilled);
    }

    private void SeasonCannibal()
    {
        if (!HasMeme("Cannibal")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        // 25 points per section (butcher=5, human meat meal=1)
        int filled = System.Math.Min(cannibalPoints / 25, sections);
        int unfilled = sections - filled;
        RecordSections("Cannibal", filled, unfilled);
    }

    private void SeasonGuilty()
    {
        if (!HasMeme("Guilty")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        // Combine wounds (seasonal) + charity points (persistent)
        int total = guiltyWounds + guiltyCharityPoints;
        int filled = total > 0 ? System.Math.Min(total / 10, sections) : 0;
        int unfilled = sections - filled;

        RecordSections("Guilty", filled, unfilled);

        // Carry over remainder in charityPoints, reset wounds
        guiltyCharityPoints = total - filled * 10;
    }

    public int HighLifeThreshold => System.Math.Max(PawnsFinder.AllMaps_FreeColonistsSpawned.Count * 2, 2);

    private void SeasonHighLife()
    {
        if (!HasMeme("HighLife")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        int filled = System.Math.Min(highLifePoints / HighLifeThreshold, sections);
        int unfilled = sections - filled;
        RecordSections("HighLife", filled, unfilled);
    }

    private void SeasonNudism()
    {
        if (!HasMeme("Nudism")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        int total = nudismPoints + nudismDressedPoints;
        int filled = 0;
        if (total > 0)
        {
            float ratio = (float)nudismPoints / total;
            filled = FilledFromRatio(ratio, sections);
        }
        int unfilled = sections - filled;
        RecordSections("Nudism", filled, unfilled);
    }

    private void SeasonNaturePrimacy()
    {
        if (!HasMeme("NaturePrimacy")) return;
        int sections = MemeCount;
        if (sections <= 0) return;

        int score = naturePlantsSown - natureTreesCut * 2;
        int filled = score > 0 ? System.Math.Min(score / 30, sections) : 0;
        int unfilled = sections - filled;
        RecordSections("NaturePrimacy", filled, unfilled);
    }

    // Memes with only seasonalFaithChange and no mechanic
    private static readonly string[] passiveMemeNames = { "Individualist", "Loyalist", "HumanPrimacy", "Inhuman" };

    private void SeasonPassiveMemes()
    {
        foreach (var name in passiveMemeNames)
        {
            if (HasMeme(name))
                RecordSections(name, 0, 0);
        }
    }

    // =========================================================================
    // Yearly penalty & cleanup
    // =========================================================================

    private void TickYearlyPenalty()
    {
        int colonists = PawnsFinder.AllMaps_FreeColonists.Count;
        if (colonists <= 3) return;

        int ticksNow = GenTicks.TicksGame;

        if (nextYearlyPenaltyTick <= 0)
        {
            nextYearlyPenaltyTick = ticksNow + YearTicks;
            return;
        }

        if (ticksNow < nextYearlyPenaltyTick) return;

        nextYearlyPenaltyTick = ticksNow + YearTicks;
        ritualDevPointsThisYear.Clear();
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return;

        int holidays = 0;
        foreach (var p in ideo.PreceptsListForReading)
        {
            if (!(p is Precept_Ritual r)) continue;
            bool hasDate = r.obligationTriggers?.Any(t => t is RitualObligationTrigger_Date) == true;
            if (hasDate) holidays++;
        }

        int penaltyWeight = -(holidays * 2);

        DebugLog($"YEARLY PENALTY: ticks={ticksNow}, holidays={holidays}, weight={penaltyWeight}");
        RecordRitual("FT_FaithDecay".Translate(), RitualRecordType.FaithDecay, customWeight: penaltyWeight);

        ApplyYearlyGoodwillChanges();
        CompressYearlyRecords();
    }

    private void CleanupRecords()
    {
        int cutoff = GenTicks.TicksGame - YearTicks;
        records.RemoveAll(r => r.tick < cutoff);

        // Remove corpse penalties if corpse no longer exists or is far from home area
        records.RemoveAll(r =>
        {
            if (r.type != RitualRecordType.CorpsePenalty || r.thingId <= 0)
                return false;
            foreach (var map in Find.Maps)
            {
                var thing = map.listerThings.AllThings.FirstOrDefault(t => t.thingIDNumber == r.thingId);
                if (thing != null)
                    return !Patch_CorpseObserved.IsNearHomeArea(thing);
            }
            return true;
        });
    }

    private void CompressYearlyRecords()
    {
        int totalScore = 0;
        int count = 0;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            var r = records[i];
            if (r.type == RitualRecordType.FaithDecay || r.type == RitualRecordType.CorpsePenalty)
                continue;
            totalScore += r.Points;
            count++;
        }

        if (count <= 1) return;

        records.RemoveAll(r => r.type != RitualRecordType.FaithDecay && r.type != RitualRecordType.CorpsePenalty);
        records.Insert(0, new RitualRecord
        {
            tick = Find.TickManager.TicksGame,
            ritualName = "FT_YearlySummary".Translate(GenDate.Year(Find.TickManager.TicksAbs, 0)),
            type = totalScore >= 0 ? RitualRecordType.Fulfilled : RitualRecordType.FaithDecay,
            customWeight = totalScore
        });
    }

    private void ApplyYearlyGoodwillChanges()
    {
        var memes = GetPlayerMemes();
        if (memes == null) return;

        int totalChange = 0;
        foreach (var meme in memes)
        {
            var ext = meme.GetModExtension<MemeEffectExtension>();
            if (ext != null && ext.yearlyGoodwillChange != 0)
                totalChange += ext.yearlyGoodwillChange;
        }

        if (totalChange == 0) return;

        int affected = 0;
        foreach (var faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
        {
            if (faction.IsPlayer || faction.defeated || !faction.HasGoodwill) continue;
            if (Faction.OfPlayer.TryAffectGoodwillWith(faction, totalChange, canSendMessage: false, canSendHostilityLetter: true, HistoryEventDefOf.DebugGoodwill))
                affected++;
        }

        if (affected > 0)
        {
            string key = totalChange < 0 ? "FT_GoodwillPenalty" : "FT_GoodwillBonus";
            Messages.Message(key.Translate(totalChange.ToStringWithSign()), MessageTypeDefOf.NegativeEvent);
        }
    }

    // =========================================================================
    // Startup log
    // =========================================================================

    private void LogStartup()
    {
        startupLogged = true;
        try { File.WriteAllText(@"D:\Mods\faith_debug.txt", ""); } catch { }
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return;

        var allRituals = ideo.PreceptsListForReading?.Where(p => p is Precept_Ritual).Cast<Precept_Ritual>().ToList();
        int ritCount = allRituals?.Count ?? 0;
        int memCount = ideo.memes?.Count ?? 0;

        var important = new List<string>();
        var situational = new List<string>();
        if (allRituals != null)
        {
            foreach (var r in allRituals)
            {
                bool hasDate = r.obligationTriggers?.Any(t => t is RitualObligationTrigger_Date) == true;
                bool hasCooldown = r.isAnytime && r.def.useRepeatPenalty;
                if (hasDate || hasCooldown)
                    important.Add($"{r.LabelCap}(def={r.def.defName}, pattern={r.sourcePattern?.defName})");
                else
                    situational.Add($"{r.LabelCap}(def={r.def.defName}, pattern={r.sourcePattern?.defName})");
            }
        }

        string memeList = ideo.memes != null ? string.Join(", ", ideo.memes.Select(m => $"{m.LabelCap}({m.defName})")) : "none";
        string devPtsUsed = ritualDevPointsThisYear.Count > 0 ? string.Join(", ", ritualDevPointsThisYear) : "none";
        DebugLog($"STARTUP: rituals={ritCount}, memes={memCount}\n" +
                 $"  important ({important.Count}): [{string.Join(", ", important)}]\n" +
                 $"  situational ({situational.Count}): [{string.Join(", ", situational)}]\n" +
                 $"  memes: [{memeList}]\n" +
                 $"  ritualDevPointsThisYear: [{devPtsUsed}]\n" +
                 $"  nextYearlyPenaltyTick={nextYearlyPenaltyTick}");
    }

    // =========================================================================
    // Save/Load
    // =========================================================================

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref records, "records", LookMode.Deep);
        Scribe_Values.Look(ref slaveryPoints, "slaveryPoints");
        Scribe_Values.Look(ref collectivistPoints, "collectivistPoints");
        Scribe_Values.Look(ref nudismPoints, "nudismPoints");
        Scribe_Values.Look(ref nudismDressedPoints, "nudismDressedPoints");
        Scribe_Values.Look(ref naturePlantsSown, "naturePlantsSown");
        Scribe_Values.Look(ref natureTreesCut, "natureTreesCut");
        Scribe_Values.Look(ref animalKills, "animalKills");
        Scribe_Values.Look(ref animalCompanionPoints, "animalCompanionPoints");
        Scribe_Values.Look(ref xenoPhiliaMainPoints, "xenoPhiliaMainPoints");
        Scribe_Values.Look(ref xenoPhiliaOtherPoints, "xenoPhiliaOtherPoints");
        Scribe_Values.Look(ref xenoMainRacePoints, "xenoMainRacePoints");
        Scribe_Values.Look(ref xenoOtherRacePoints, "xenoOtherRacePoints");
        Scribe_Values.Look(ref bloodfeedCount, "bloodfeedCount");
        Scribe_Values.Look(ref highLifePoints, "highLifePoints");
        Scribe_Values.Look(ref guiltyWounds, "guiltyWounds");
        Scribe_Values.Look(ref guiltyCharityPoints, "guiltyCharityPoints");
        Scribe_Values.Look(ref raiderPoints, "raiderPoints");
        Scribe_Values.Look(ref raiderProtectionUntilTick, "raiderProtectionUntilTick");
        Scribe_Values.Look(ref raiderProtectionCooldownUntilTick, "raiderProtectionCooldownUntilTick");
        Scribe_Values.Look(ref tunnelerPoints, "tunnelerPoints");
        Scribe_Values.Look(ref darknessPoints, "darknessPoints");
        Scribe_Values.Look(ref rancherPoints, "rancherPoints");
        Scribe_Collections.Look(ref rancherBirths, "rancherBirths", LookMode.Value, LookMode.Value);
        if (rancherBirths == null) rancherBirths = new Dictionary<string, int>();
        Scribe_Collections.Look(ref rancherPtsPerAnimal, "rancherPtsPerAnimal", LookMode.Value, LookMode.Value);
        if (rancherPtsPerAnimal == null) rancherPtsPerAnimal = new Dictionary<string, int>();
        Scribe_Values.Look(ref proselytizePoints, "proselytizePoints");
        Scribe_Values.Look(ref purePoints, "purePoints");
        Scribe_Values.Look(ref implantedPoints, "implantedPoints");
        Scribe_Values.Look(ref transImplantedPoints, "transImplantedPoints");
        Scribe_Values.Look(ref transPurePoints, "transPurePoints");
        Scribe_Values.Look(ref scarredPoints, "scarredPoints");
        Scribe_Values.Look(ref unscarredPoints, "unscarredPoints");
        Scribe_Values.Look(ref cannibalPoints, "cannibalPoints");
        Scribe_Values.Look(ref blindPoints, "blindPoints");
        Scribe_Values.Look(ref TrueSightDayCounter, "trueSightDayCounter");
        Scribe_Values.Look(ref sightedPoints, "sightedPoints");
        Scribe_Values.Look(ref treesSown, "treesSown");
        Scribe_Values.Look(ref treeConnectionPoints, "treeConnectionPoints");
        Scribe_Values.Look(ref malePoints, "malePoints");
        Scribe_Values.Look(ref femalePoints, "femalePoints");
        Scribe_Values.Look(ref lastDailyCheck, "lastDailyCheck");
        Scribe_Values.Look(ref lastBerrySpawnDay, "lastBerrySpawnDay", -1);
        Scribe_Values.Look(ref lastSeasonCheck, "lastSeasonCheck");
        Scribe_Values.Look(ref firstSeasonStarted, "firstSeasonStarted");
        Scribe_Values.Look(ref nextYearlyPenaltyTick, "nextYearlyPenaltyTick");
        Scribe_Collections.Look(ref ritualDevPointsThisYear, "ritualDevPointsThisYear", LookMode.Value);
        if (ritualDevPointsThisYear == null)
            ritualDevPointsThisYear = new List<string>();
        if (records == null)
            records = new List<RitualRecord>();
    }
}
