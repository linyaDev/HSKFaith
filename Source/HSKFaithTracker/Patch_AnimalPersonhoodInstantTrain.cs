using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKFaithTracker;

/// <summary>
/// Добавляет полную тренировку всех навыков при приручении животного
/// колонистом с мемом AnimalPersonhood.
/// </summary>
[HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.Interacted))]
public static class Patch_AnimalPersonhoodInstantTrain
{
    public static void Postfix(Pawn initiator, Pawn recipient)
    {
        // Проверяем что initiator не null
        if (initiator == null)
            return;

        // Проверяем что это животное (не WildMan!)
        if (!recipient.RaceProps.Animal)
            return;

        // Проверяем что животное приручено (фракция игрока)
        if (recipient.Faction != Faction.OfPlayer)
            return;

        // Проверяем что у приручающего есть AnimalPersonhood
        if (!AnimalPersonhoodUtility.HasAnimalPersonhood(initiator))
            return;

        // Проверяем что есть training tracker
        if (recipient.training == null)
            return;

        // Тренируем все доступные навыки
        int trainedCount = 0;
        foreach (var trainable in DefDatabase<TrainableDef>.AllDefs)
        {
            // Пропускаем уже обученные
            if (recipient.training.HasLearned(trainable))
                continue;

            // Проверяем можно ли тренировать этот навык
            if (recipient.training.CanAssignToTrain(trainable).Accepted)
            {
                // Тренируем полностью (instant)
                recipient.training.Train(trainable, initiator, complete: true);
                trainedCount++;
            }
        }

        // Назначить хозяина и охранять
        if (recipient.playerSettings != null)
        {
            recipient.playerSettings.Master = initiator;
            recipient.playerSettings.followDrafted = true;
        }

        // Показываем уведомление только если что-то обучили
        if (trainedCount > 0)
        {
            Messages.Message(
                "FT_AnimalTrained".Translate(recipient.LabelShort, initiator.LabelShort),
                recipient,
                MessageTypeDefOf.PositiveEvent
            );
        }
    }
}
