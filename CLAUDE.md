# HSKFaithTracker

Colony-wide faith system for Ideology DLC. Each meme has unique gameplay mechanics tracked seasonally.

## Architecture

### Three-Layer System
1. **Global Faith Score** (-30 to +30) in `GameComponent_FaithTracker` -> mapped to `Need_Faith` (0.0-1.0) -> 7 mood stages (-6 to +6)
2. **Ritual Records** — event log: Fulfilled (+1/+3), Missed (-2), CorpsePenalty (-1), FaithDecay
3. **Per-Meme Counters** — daily accumulation, reset each season (~15 days / 900k ticks), converted to faith at season end

### Timing
- **Every 2500 ticks** (1 hour): meme change detection, daily counter updates, nudism hourly count, certainty updates
- **Every 180 ticks**: darkness hediff updates (glow-dependent)
- **Every season** (900k ticks): counters -> filled sections -> faith records, counters reset, +1 dev point
- **Every year** (3.6M ticks): faith decay penalty, goodwill changes, record compression, ritual dev points reset

### Key Classes

| File | Role |
|------|------|
| `GameComponent_FaithTracker.cs` | Central state: ~30 counters, season/year logic, save/load, RecordFilled/RecordUnfilled helpers |
| `Need_Faith.cs` | Need display, maps global score to 0-1 bar, hidden for non-primary ideo pawns |
| `MemeEffectExtension.cs` | DefModExtension on MemeDefs: stats, faith rates, categories, startingItems |
| `HSKFaithTrackerInit.cs` | Harmony patch registration |

### Meme Extension Fields (`MemeEffectExtension`)
- `seasonalFaithChange` — base faith per season (+3 early, -2 mid, -3 late)
- `faithPerSection` — bonus per filled section (all memes with Season methods, default 1)
- `penaltyPerSection` — penalty per unfilled section (-1 early, -2 mid/late)
- `MechanicMinFaith` / `MechanicMaxFaith` — computed properties (not XML), derived from penaltyPerSection/faithPerSection
- `statOffsets` / `statFactors` — pawn stat modifiers
- `biomeStatBonuses` — biome-specific stat bonuses (TreeConnection)
- `memeGroup` — "early" / "mid" / "late"
- `certaintyPerSeason` — ideology certainty drift rate
- `moodBonus`, `passiveEffect`, `conflictingMemes`, `synergyMemes`
- `startingItems` — List<ThingDefCountClass>, items spawned at game start
- `sectionsPerMeme`, `yearlyGoodwillChange`

### Meme Categories
- **Early** (+3/season, +1/filled, -1/unfilled): MaleSupremacy(+9%), FemaleSupremacy(+9%), Nudism, PainIsVirtue, HighLife, AnimalPersonhood, NaturePrimacy(-6%), Darkness, Bloodfeeding, HAR_Xenophilia(+9%), HAR_Xenophobia(+9%)
- **Mid** (-2/season, +1/filled, -2/unfilled): FleshPurity, Proselytizer(+30%), Rancher, Tunneler, TreeConnection, Blindsight, Collectivist, Individualist(+12%), Cannibal(+3/season, -2/filled, -3/unfilled)
- **Late** (-3/season, +1/filled, -2/unfilled): Supremacist, HumanPrimacy, Raider, Transhumanist, Guilty, Loyalist(+18%)
- **Disabled**: Ritualist, Inhuman, Raider, TreeConnection

### Meme Hediffs (auto-managed)
- `FT_NudismBonus` — +10% speed, -5% mental break, -15% damage, -10% food poison (when unclothed, respects "Nudist" tag)
- `FT_GenderDominant` / `FT_GenderSubmissive` — +15% / -10% SocialImpact (by pawn gender)
- `FT_DarknessBonus/DeepDarknessBonus/LightWeakness` — glow-dependent combat stats
- `FT_RancherCare` — -25% hunger on colony animals
- All hediffs removed when meme deactivated

### Heretic System
- No mood debuffs for heretics/other-faith pawns (removed: FT_HereticsInColony colony penalty and FT_HereticAmongFaithful self penalty)
- `Need_Faith` hidden for pawns with non-primary ideology

### Certainty System
- Updates per hour (2500 ticks), not per tick — performance
- Pastor role (IdeoRole_Moralist): fixed +2%/day, ignores meme penalties
- `Patch_CertaintyTick.PastorCertaintyPerDay` constant

### Dev Points Control
- Proselytizer blocks conversion dev points (both paths) — `Patch_ProselytizerDevPoints.cs`
- Ritual dev points: keyed by `sourcePattern.defName` (not def.defName), once per year per ritual pattern — `Patch_RitualDevPoints.cs`
- +1 seasonal dev point to fluid ideology
- First season tracking via `firstSeasonStarted` flag

### Ritual Faith (Fulfilled records)
- Recorded by two patches: `Patch_RitualCompleted` (ApplyOutcome) and `Patch_RitualObligation` (RemoveObligation); `justRecorded` flag prevents double-recording
- Faith granted **once per year per ritual pattern** via `TryRecordRitualFaith` (keyed by `sourcePattern.defName`); tracker `ritualFaithThisYear`, cleared yearly alongside `ritualDevPointsThisYear`, scribed
- **Bad outcome → no faith**: `Patch_RitualOutcomeQuality` captures the rolled `RitualOutcomePossibility.Positive` during `GetOutcome` (patches FromQuality + ChildBirth + Trial overrides); a negative outcome (`positivityIndex < 0`) skips the faith record without consuming the yearly slot
- Both gates affect **faith only** — certainty shift (±1%/±3%) and Blindsight bonus still fire every ritual
- Weight: 3 for dated holiday w/o cooldown, else 1; RoleChange ceremonies skipped (weight 0)

### NaturePrimacy Special
- Point-based bar: +1 per plant sown (`Patch_PlantSown`), -2 per tree cut (`Patch_NaturePrimacyTreeDestroyed`)
- 30 points per section, `faithPerSection: 2`, `penaltyPerSection: -1`
- +15% PlantHarvestYield via XML statOffsets
- Wild berry spawn: 1 bush/day, 15-75 tiles from home zone, `lastBerrySpawnDay` tracker
- Wild berries (Plant_Berry outside grow zone): x6 harvest yield via `Patch_NaturePrimacyWildHarvest`

### HighLife Special
- Dynamic threshold: `HighLifeThreshold` = colonists × 2 (min 2)
- Starting items: 300 psychoid leaves, 20 psychoid seeds, 300 smokeleaf leaves, 20 smokeleaf seeds, 10 berserk herb
- Starting research: Drugs_A1, Drug_plants_A1
- Unblocked from disabled memes list

### Corpse Penalty
- `Patch_CorpseObserved` — 30 tile distance check from home area
- Cleanup removes penalties for corpses far from home area
- Cannibals and bloodfeeders exempt

### Colonist Killed
- `Patch_ColonistKilled` — -10 faith when colonist killed by own colonist

## File Layout

```
Source/HSKFaithTracker/
  GameComponent_FaithTracker.cs   # Core state + season/year logic + RecordFilled/RecordUnfilled
  Need_Faith.cs                   # Need bar, hidden for non-primary ideo
  MemeEffectExtension.cs          # XML extension class + computed MechanicMin/MaxFaith
  HSKFaithTrackerInit.cs          # Harmony init
  Dialog_FaithInfo.cs             # Main info window
  Dialog_FaithHistory.cs          # Event log window
  Dialog_MemeInfo.cs              # Per-meme details
  Dialog_ForgetMeme.cs            # Meme removal confirm
  Dialog_FaithDebug.cs            # Debug window (dev mode)
  FloatMenuAbove.cs               # Utility: floating menu
  MainButtonWorker_Needs.cs       # Bottom bar button
  Hediff_BlindsightPsylink.cs    # Blindsight psylink accumulation
  Hediff_BlindsightTrueSight.cs  # Blindsight true sight
  ThoughtWorker_Faith.cs          # 7-stage mood from faith level
  ThoughtWorker_Loyalist.cs       # +15 mood (Loyalist meme)
  ThoughtWorker_FleshPurity.cs    # Mood if no implants
  ThoughtWorker_AnimalCompanion.cs # Mood from pet count (+2 to +10)
  Patch_StartingItems.cs          # Spawn startingItems at game start
  Patch_NaturePrimacyTreeDestroyed.cs # -2 score per tree cut/destroyed
  Patch_NaturePrimacyWildHarvest.cs   # x6 wild berry yield
  Patch_PlantSown.cs              # +1 naturePrimacyScore per plant
  Patch_ColonistKilled.cs         # -10 faith for colonist killed by own
  Patch_CorpseObserved.cs         # Corpse penalty with 30-tile distance check
  Patch_CorpseDisposed.cs         # Remove corpse penalty on destroy
  Patch_RitualCompleted.cs        # Ritual faith on ApplyOutcome (gated: once/year + bad-outcome)
  Patch_RitualObligation.cs       # Ritual faith on RemoveObligation (same gates); Missed records
  Patch_RitualOutcomeQuality.cs   # Capture rolled outcome positivity (bad outcome -> no faith)
  Patch_RitualDevPoints.cs        # Ritual dev points once/year per pattern
  Patch_ProselytizerDevPoints.cs  # Block conversion dev points (Proselytizer)
  Patch_*.cs                      # Other Harmony patches
Defs/
  NeedDefs/Need_Faith.xml
  ThoughtDefs/Thoughts_Faith.xml, _Loyalist, _FleshPurity, _AnimalCompanion
  HediffDefs/Hediff_Darkness, _BlindsightAdapt, _BlindsightPsy, _Rancher, _Nudism, _Supremacist, _GenderSupremacy
  MainButtonDefs/MainButton_Needs.xml
Patches/
  Patch_MemeEffects.xml           # MemeEffectExtension on 20+ memes (THE key config file)
```

## Build

```bash
# 1.5 only (default per project rules)
dotnet build Source/HSKFaithTracker/HSKFaithTracker.v15.csproj -c Release
```

## Design Notes

- Season = 15 game days (900,000 ticks), NOT vanilla season quarters
- All Season methods use `RecordFilled`/`RecordUnfilled` helpers that read faithPerSection/penaltyPerSection from XML
- `MechanicMinFaith`/`MechanicMaxFaith` are computed properties, not XML fields
- `firstSeasonStarted` flag prevents skipping first season on new game
- Berry spawn uses separate `lastBerrySpawnDay` to prevent double spawn on load
- Ritual dev points keyed by `sourcePattern.defName`, not `def.defName`
- Year summary includes year number
- Debug log in `faith_debug.txt`, cleared on startup
