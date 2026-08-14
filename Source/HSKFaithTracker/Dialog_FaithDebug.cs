using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

public class Dialog_FaithDebug : Window
{
    private Vector2 scrollPos;

    public override Vector2 InitialSize => new Vector2(500f, 520f);

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

        Rect scrollArea = new Rect(0f, 40f, inRect.width, inRect.height - 80f);
        Rect viewRect = new Rect(0f, 0f, scrollArea.width - 16f, 1200f);
        Widgets.BeginScrollView(scrollArea, ref scrollPos, viewRect);

        float y = 0f;
        float w = viewRect.width;

        // === WorkSite Section ===
        DrawSection(ref y, w, "WorkSite Info");

        // Active WorkSites
        var activeSites = Find.WorldObjects.Sites
            .Where(s => s.MainSitePartDef?.tags != null && s.MainSitePartDef.tags.Contains("WorkSite"))
            .ToList();

        int totalWorkSites = activeSites.Count;
        var faithArmories = activeSites.Where(s => s.MainSitePartDef.defName.StartsWith("FaithArmory")).ToList();

        DrawLine(ref y, w, "Active WorkSites", totalWorkSites.ToString());
        DrawLine(ref y, w, "  Faith Armories", faithArmories.Count.ToString());

        // Multiplier from existing camps
        float existingMult = EvalExistingCampsCurve(totalWorkSites);
        DrawLine(ref y, w, "Existing camps multiplier", $"{existingMult:F2}x  (0-2: 1.0, 3: 0.3, 4+: 0.1)");

        // Per-map info
        Map playerMap = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
        if (playerMap != null && playerMap.IsPlayerHome)
        {
            y += 6f;
            DrawLine(ref y, w, $"Map: {playerMap.info?.parent?.Label ?? "???"}", "");

            int colonists = playerMap.mapPawns.FreeColonists.Count();
            string colonistMult = colonists <= 1 ? "0 (blocked)" : colonists == 2 ? "0.5x" : "1.0x";
            DrawLine(ref y, w, "  Free colonists", $"{colonists}  (mult: {colonistMult})");

            // Potential tiles and biome weights
            var potentialTiles = PotentialSiteTiles(playerMap.Tile);
            DrawLine(ref y, w, "  Potential tiles (3-9 range)", potentialTiles.Count.ToString());

            if (potentialTiles.Count > 0)
            {
                float avgWeight = 0f;
                var biomeCounts = new Dictionary<string, int>();
                var biomeWeights = new Dictionary<string, float>();
                foreach (int tile in potentialTiles)
                {
                    var biome = Find.WorldGrid[tile].biome;
                    float bw = biome.campSelectionWeight;
                    avgWeight += bw;
                    string key = biome.LabelCap.ToString();
                    if (!biomeCounts.ContainsKey(key))
                    {
                        biomeCounts[key] = 0;
                        biomeWeights[key] = bw;
                    }
                    biomeCounts[key]++;
                }
                avgWeight /= potentialTiles.Count;
                DrawLine(ref y, w, "  Avg campSelectionWeight", $"{avgWeight:F3}");

                // MTB calculation
                float freq = avgWeight * existingMult;
                if (colonists <= 1) freq = 0f;
                else if (colonists == 2) freq /= 2f;

                float baseMtb = 5f;
                float effectiveMtb = freq > 0f ? baseMtb / freq : float.PositiveInfinity;
                DrawLine(ref y, w, "  Effective MTB", $"{effectiveMtb:F1} days  (base: {baseMtb})");
                DrawLine(ref y, w, "  Appearance frequency", $"{freq:F3}");

                // Biome breakdown
                y += 6f;
                DrawSection(ref y, w, "Nearby Biomes");
                foreach (var kvp in biomeCounts.OrderByDescending(x => x.Value))
                {
                    DrawLine(ref y, w, $"  {kvp.Key}", $"{kvp.Value} tiles, weight: {biomeWeights[kvp.Key]:F2}");
                }
            }
        }
        else
        {
            DrawLine(ref y, w, "No player home map", "");
        }

        // Active sites list
        if (activeSites.Any())
        {
            y += 6f;
            DrawSection(ref y, w, "Active WorkSite List");
            foreach (var site in activeSites)
            {
                string faction = site.Faction?.Name ?? "none";
                string biome = Find.WorldGrid[site.Tile].biome.LabelCap.ToString();
                int dist = (int)Find.WorldGrid.ApproxDistanceInTiles(site.Tile,
                    (playerMap ?? Find.AnyPlayerHomeMap)?.Tile ?? -1);
                string label = site.MainSitePartDef.defName;
                string loot = "";
                if (site.parts?.FirstOrDefault()?.things is { Count: > 0 } things)
                {
                    loot = string.Join(", ", ((IEnumerable<Thing>)things).Select(t => t.LabelShortCap));
                }
                DrawLine(ref y, w, $"  {label}", $"{biome}, ~{dist} tiles");
                DrawLine(ref y, w, $"    Faction", faction);
                if (!string.IsNullOrEmpty(loot))
                    DrawLine(ref y, w, $"    Loot", loot);
            }
        }

        // Game tick info
        y += 6f;
        DrawSection(ref y, w, "Timing");
        int ticks = Find.TickManager.TicksGame;
        bool canSpawn = ticks >= 600000;
        DrawLine(ref y, w, "Game ticks", $"{ticks}  ({ticks / 60000f:F1} days)");
        DrawLine(ref y, w, "WorkSite enabled", canSpawn ? "Yes (>600k ticks)" : $"No (need {600000 - ticks} more)");

        // Faith adjustment
        y += 10f;
        DrawSection(ref y, w, "Faith Score (Dev)");
        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp != null)
        {
            DrawLine(ref y, w, "Current Score", comp.Score.ToString());
            float btnW = (w - 15f) / 4f;
            if (Widgets.ButtonText(new Rect(0f, y, btnW, 28f), "-5"))
                comp.RecordRitual("Debug", RitualRecordType.FaithDecay, customWeight: -5, showMote: false);
            if (Widgets.ButtonText(new Rect(btnW + 5f, y, btnW, 28f), "-1"))
                comp.RecordRitual("Debug", RitualRecordType.FaithDecay, customWeight: -1, showMote: false);
            if (Widgets.ButtonText(new Rect((btnW + 5f) * 2, y, btnW, 28f), "+1"))
                comp.RecordRitual("Debug", RitualRecordType.Fulfilled, customWeight: 1, showMote: false);
            if (Widgets.ButtonText(new Rect((btnW + 5f) * 3, y, btnW, 28f), "+5"))
                comp.RecordRitual("Debug", RitualRecordType.Fulfilled, customWeight: 5, showMote: false);
            y += 32f;
        }

        // Spawn button
        y += 10f;
        DrawSection(ref y, w, "Spawn (Dev)");
        Widgets.Label(new Rect(0f, y, 100f, 24f), "Timeout days:");
        string buf = customTimeoutDays.ToString();
        buf = Widgets.TextField(new Rect(105f, y, 60f, 24f), buf);
        if (int.TryParse(buf, out int val) && val > 0)
            customTimeoutDays = val;
        y += 28f;
        if (Widgets.ButtonText(new Rect(0f, y, w, 30f), "Spawn WorkSite Quest"))
            SpawnWorkSiteQuest();
        y += 35f;

        Widgets.EndScrollView();
    }

    private static float EvalExistingCampsCurve(int count)
    {
        // Mirrors ExistingCampsAppearanceFrequencyMultiplier from QuestNode_Root_WorkSite
        if (count <= 2) return 1f;
        if (count == 3) return 0.3f;
        return 0.1f;
    }

    private static List<int> PotentialSiteTiles(int root)
    {
        var tiles = new List<int>();
#if V16
        // 1.6: flood filler moved to the planet layer; PlanetTile <-> int are implicit
        RimWorld.Planet.PlanetTile rootTile = root;
        rootTile.Layer.Filler.FloodFill(rootTile,
            p => !Find.World.Impassable(p) && Find.WorldGrid.ApproxDistanceInTiles(p, rootTile) <= 9f,
            delegate(RimWorld.Planet.PlanetTile p)
            {
                if (Find.WorldGrid.ApproxDistanceInTiles(p, rootTile) >= 3f)
                    tiles.Add(p);
            });
#else
        Find.WorldFloodFiller.FloodFill(root,
            p => !Find.World.Impassable(p) && Find.WorldGrid.ApproxDistanceInTiles(p, root) <= 9f,
            delegate(int p)
            {
                if (Find.WorldGrid.ApproxDistanceInTiles(p, root) >= 3f)
                    tiles.Add(p);
            });
#endif
        return tiles;
    }

    private void DrawSection(ref float y, float w, string title)
    {
        Text.Font = GameFont.Small;
        GUI.color = new Color(1f, 0.85f, 0.4f);
        Widgets.Label(new Rect(0f, y, w, 22f), $"── {title} ──");
        GUI.color = Color.white;
        y += 22f;
    }

    private void DrawLine(ref float y, float w, string label, string value)
    {
        Widgets.Label(new Rect(0f, y, w * 0.55f, 22f), label);
        Widgets.Label(new Rect(w * 0.55f, y, w * 0.45f, 22f), value);
        y += 20f;
    }

    private static int customTimeoutDays = 15;

    private static void SpawnWorkSiteQuest()
    {
        Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
        if (map == null)
        {
            Messages.Message("No player map found", MessageTypeDefOf.RejectInput);
            return;
        }

        QuestScriptDef scriptDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail("OpportunitySite_WorkSite");
        if (scriptDef == null)
        {
            Messages.Message("QuestScriptDef 'OpportunitySite_WorkSite' not found", MessageTypeDefOf.RejectInput);
            return;
        }

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
        Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(scriptDef, parms.points);

        // Override timeout
        int originalTicks = 1800000;
        int customTicks = customTimeoutDays * 60000;
        foreach (var part in quest.PartsListForReading)
        {
            if (part is QuestPart_WorldObjectTimeout timeout)
                timeout.delayTicks = customTicks;
            else if (part is QuestPart_Delay delay && !(part is QuestPart_WorldObjectTimeout))
                delay.delayTicks = customTicks;
        }

        // Fix quest description: timeout and loot
        string oldDuration = originalTicks.ToStringTicksToPeriod();
        string newDuration = customTicks.ToStringTicksToPeriod();
        quest.description = quest.description.Replace(oldDuration, newDuration);
        GameComponent_FaithTracker.FixQuestLootDescription(quest);

        Messages.Message($"WorkSite quest spawned ({quest.name}), timeout {customTimeoutDays}d", MessageTypeDefOf.PositiveEvent);
        // Log.Message($"[HSKFaith] Debug: WorkSite quest generated, points {parms.points:F0}, timeout {customTimeoutDays}d");
    }
}
