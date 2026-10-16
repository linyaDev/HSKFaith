using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKFaithTracker;

/// <summary>
/// Утилитарный класс для проверки мема AnimalPersonhood.
/// </summary>
internal static class AnimalPersonhoodUtility
{
    /// <summary>
    /// Проверяет, есть ли у pawn мем AnimalPersonhood.
    /// Использует foreach для производительности (без LINQ-аллокаций).
    /// </summary>
    public static bool HasAnimalPersonhood(Pawn pawn)
    {
        if (pawn.Ideo?.memes == null)
            return false;

        foreach (var meme in pawn.Ideo.memes)
        {
            if (meme.defName == "AnimalPersonhood")
                return true;
        }

        return false;
    }

    public static bool AnyColonistHasAnimalPersonhood()
    {
        foreach (var p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (!p.IsSlave && HasAnimalPersonhood(p))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Предотвращает охоту диких животных на колонистов с мемом AnimalPersonhood.
/// Патчим FoodUtility.IsAcceptablePreyFor — возвращает false если цель имеет AnimalPersonhood.
/// </summary>
[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.IsAcceptablePreyFor))]
public static class Patch_AnimalPersonhoodPrey
{
    public static void Postfix(Pawn predator, Pawn prey, ref bool __result)
    {
        // Если уже не приемлемая добыча, нечего проверять
        if (!__result)
            return;

        // Проверяем, что predator — это животное
        if (!predator.RaceProps.Animal)
            return;

        // Проверяем, что это ДИКОЕ животное (не прирученное игроком)
        if (predator.Faction == Faction.OfPlayer)
            return;

        // Проверяем, есть ли у цели (prey) мем AnimalPersonhood
        if (AnimalPersonhoodUtility.HasAnimalPersonhood(prey))
        {
            __result = false;
            return;
        }

        // Защита животных колонии: если prey — животное игрока и есть колонист с AnimalPersonhood
        if (prey.RaceProps.Animal && prey.Faction == Faction.OfPlayer && AnimalPersonhoodUtility.AnyColonistHasAnimalPersonhood())
        {
            __result = false;
        }
    }
}

/// <summary>
/// Предотвращает переход животного в состояние manhunter при неудачной попытке приручения
/// колонистом с мемом AnimalPersonhood.
/// Используем Prefix для предотвращения выполнения оригинального метода.
/// </summary>
[HarmonyPatch(typeof(Pawn_MindState), nameof(Pawn_MindState.CheckStartMentalStateBecauseRecruitAttempted))]
public static class Patch_AnimalPersonhoodTameFail
{
    public static bool Prefix(Pawn_MindState __instance, Pawn tamer)
    {
        // Проверяем, что это животное
        if (!__instance.pawn.RaceProps.Animal)
            return true;

        // Проверяем, что tamer не null и у него есть AnimalPersonhood
        if (tamer != null && AnimalPersonhoodUtility.HasAnimalPersonhood(tamer))
        {
            // Отменяем выполнение оригинального метода — животное не станет manhunter
            return false;
        }

        // Разрешаем выполнение оригинального метода
        return true;
    }
}
