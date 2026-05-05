using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// ThoughtWorker для бонуса настроения за прирученных диких животных.
/// +2 за каждое животное, максимум +10 (за 5 животных).
/// Работает только для колонистов с AnimalPersonhood.
/// </summary>
public class ThoughtWorker_AnimalCompanion : ThoughtWorker
{
    private const int MaxAnimals = 5;  // Максимум учитываем 5 животных

    public override ThoughtState CurrentStateInternal(Pawn p)
    {
        // Проверяем что у колониста есть AnimalPersonhood
        if (!AnimalPersonhoodUtility.HasAnimalPersonhood(p))
            return ThoughtState.Inactive;

        // Проверяем что есть карта
        if (p.Map == null)
            return ThoughtState.Inactive;

        // Считаем приручённых диких животных
        int animalCount = CountWildTamedAnimals(p);
        if (animalCount == 0)
            return ThoughtState.Inactive;

        // Возвращаем стадию (0-4)
        return ThoughtState.ActiveAtStage(GetStageIndex(animalCount));
    }

    private int CountWildTamedAnimals(Pawn p)
    {
        if (p.Map == null)
            return 0;

        // Count tamed animals that are not pen animals (not FenceBlocked)
        return p.Map.mapPawns.AllPawnsSpawned
            .Where(x => x.RaceProps.Animal && x.Faction == Faction.OfPlayer && !x.RaceProps.FenceBlocked)
            .Count();
    }

    private int GetStageIndex(int animalCount)
    {
        // 1-2: stage 0 (+2), 3-4: stage 1 (+4), 5-6: stage 2 (+6), 7-8: stage 3 (+8), 9+: stage 4 (+10)
        return Mathf.Min(Mathf.FloorToInt((animalCount - 1) / 2f), 4);
    }
}
