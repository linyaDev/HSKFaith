using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKFaithTracker;

public class JobDriver_CreateRelic : JobDriver
{
    private const int PrayerDuration = 3000; // ~50 seconds

    // job.targetA = altar, job.targetB = weapon (or null for random), job.count = 1 for weapon, 0 for random
    private Building Altar => (Building)TargetThingA;
    private bool IsWeaponRelic => job.targetB.HasThing;
    private int FaithCost => job.count;

    // Remembered weapon info after despawn
    private ThingDef weaponDef;
    private ThingDef weaponStuff;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!pawn.Reserve(TargetA, job, errorOnFailed: errorOnFailed))
            return false;
        if (IsWeaponRelic && !pawn.Reserve(TargetB, job, errorOnFailed: errorOnFailed))
            return false;
        return true;
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

        if (IsWeaponRelic)
        {
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B);

            var pickup = new Toil();
            pickup.initAction = () =>
            {
                var weapon = job.targetB.Thing;
                weaponDef = weapon.def;
                weaponStuff = weapon.Stuff;
                weapon.Destroy();
            };
            pickup.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pickup;
        }

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

        // Pray at altar
        var pray = Toils_General.WaitWith(TargetIndex.A, PrayerDuration, true, true);
        pray.WithProgressBarToilDelay(TargetIndex.A);
        yield return pray;

        // Create relic
        yield return new Toil
        {
            initAction = () =>
            {
                if (IsWeaponRelic && weaponDef != null)
                    CreateWeaponRelic();
                else
                    CreateRandomRelic();
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
    }

    private void CreateWeaponRelic()
    {
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        var relicDef = DefDatabase<PreceptDef>.GetNamedSilentFail("IdeoRelic");
        if (relicDef == null) return;

        var relic = (Precept_Relic)PreceptMaker.MakePrecept(relicDef);
        ideo.AddPrecept(relic);
        relic.ThingDef = weaponDef;
        if (weaponStuff != null)
            relic.stuff = weaponStuff;
        relic.RegenerateName();

        // Spawn new legendary weapon at altar
        Thing weapon = ThingMaker.MakeThing(weaponDef, weaponStuff);
        weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Colony);
        weapon.HitPoints = weapon.MaxHitPoints;
        weapon.StyleSourcePrecept = relic;
        GenPlace.TryPlaceThing(weapon, Altar.Position, Altar.Map, ThingPlaceMode.Near);

        comp.RecordRitual("FT_RelicCreated".Translate(relic.LabelCap), RitualRecordType.FaithDecay, customWeight: -FaithCost);

        Find.LetterStack.ReceiveLetter(
            "FT_RelicCreatedTitle".Translate(),
            "FT_RelicCreatedDesc".Translate(pawn.LabelShortCap, relic.LabelCap),
            LetterDefOf.PositiveEvent, weapon);
    }

    private void CreateRandomRelic()
    {
        var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
        if (ideo == null) return;

        var comp = Current.Game?.GetComponent<GameComponent_FaithTracker>();
        if (comp == null) return;

        var relicDef = DefDatabase<PreceptDef>.GetNamedSilentFail("IdeoRelic");
        if (relicDef == null) return;

        var relic = (Precept_Relic)PreceptMaker.MakePrecept(relicDef);

        var candidates = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.relicChance > 0f && !d.IsWeapon)
            .ToList();
        if (candidates.Count == 0) return;

        var chosenDef = candidates.RandomElementByWeight(d => d.relicChance);
        ideo.AddPrecept(relic);
        relic.ThingDef = chosenDef;
        relic.RegenerateName();

        Thing relicThing = relic.GenerateRelic();
        GenPlace.TryPlaceThing(relicThing, Altar.Position, Altar.Map, ThingPlaceMode.Near);

        comp.RecordRitual("FT_RelicCreated".Translate(relic.LabelCap), RitualRecordType.FaithDecay, customWeight: -FaithCost);

        Find.LetterStack.ReceiveLetter(
            "FT_RelicCreatedTitle".Translate(),
            "FT_RelicCreatedDesc".Translate(pawn.LabelShortCap, relic.LabelCap),
            LetterDefOf.PositiveEvent, relicThing);
    }
}
