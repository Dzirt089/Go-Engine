using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Собирает модель представления партии для тестов: одно место на все наборы.</summary>
/// <remarks>
/// Настройки и зерно задаются здесь, чтобы наборы не описывали построение модели заново
/// и не расходились между собой. Зерно фиксировано: партии в тестах детерминированы
/// (<c>AGENTS.md</c>, п. 6 и 9).
/// </remarks>
internal static class TestViewModel
{
    /// <summary>Зерно проверок: партия должна повторяться от запуска к запуску.</summary>
    public const int Seed = 20260926;

    /// <summary>Создаёт модель представления партии.</summary>
    /// <param name="color">Цвет игрока; по умолчанию чёрные.</param>
    /// <param name="level">Уровень AI; по умолчанию 20 кю — поиск без сети.</param>
    /// <param name="modelSizes">Стороны доски, для которых есть модель; <c>null</c> — модели нет.</param>
    /// <param name="size">Размер доски; по умолчанию 9×9.</param>
    /// <returns>Модель представления партии.</returns>
    public static MainViewModel Create(
        StoneColor? color = null,
        DifficultyLevel? level = null,
        IReadOnlySet<int>? modelSizes = null,
        BoardSize? size = null)
    {
        var boardSize = size ?? BoardSize.Size9;
        var settings = AppSettings.From(
            boardSize,
            level ?? DifficultyLevel.Kyu20,
            color ?? StoneColor.Black,
            Komi.For(boardSize));

        return modelSizes is null
            ? new MainViewModel(settings, new Random(Seed))
            : new MainViewModel(settings, new Random(Seed), new FakeEvaluator(), modelSizes);
    }
}
