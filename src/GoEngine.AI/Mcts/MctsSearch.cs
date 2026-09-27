namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Поиск по дереву MCTS: спуск, оценка узла и бюджет итераций.</summary>
/// <remarks>
/// Поиск знает только про дерево и бюджет; чем оценивается позиция, решает <see cref="IMctsEvaluation"/>.
/// Итерация — это «спуск → расширение → оценка → передача результата»: без сети оценкой служит
/// случайное доигрывание, с сетью — её мнение об исходе и политика ходов (D-051).
/// Поиск детерминирован при фиксированном <see cref="Random"/> оценки и своего генератора
/// не создаёт (<c>AGENTS_GO.md</c>, п. 7). Логов нет: слой AI не логирует.
/// </remarks>
public sealed class MctsSearch
{
    private readonly MctsConfig _config;
    private readonly IMctsEvaluation _evaluation;
    private readonly TimeProvider _timeProvider;

    /// <summary>Создаёт поиск.</summary>
    /// <param name="config">Настройки поиска: бюджет, коэффициент исследования, RAVE.</param>
    /// <param name="evaluation">Оценка позиции узла.</param>
    /// <param name="timeProvider">Источник времени; в тестах — <c>FakeTimeProvider</c>.</param>
    /// <exception cref="ArgumentNullException">Оценка не задана.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Коэффициент исследования отрицательный.</exception>
    public MctsSearch(MctsConfig config, IMctsEvaluation evaluation, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentOutOfRangeException.ThrowIfNegative(config.Ucb1C);

        _config = config;
        _evaluation = evaluation;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Бюджет поиска по числу итераций или <c>null</c>, если бюджет задан временем.</summary>
    public int? PlayoutBudget => _config.PlayoutBudget;

    /// <summary>Бюджет поиска по времени на ход или <c>null</c>, если бюджет задан числом итераций.</summary>
    public TimeSpan? TimeBudget => _config.TimeBudget;

    /// <summary>Строит дерево поиска для позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="komi">Коми доигрываний.</param>
    /// <param name="history">Ходы партии до этой позиции: нужны сети для плоскостей истории.</param>
    /// <returns>Корень дерева с разобранными вариантами.</returns>
    /// <exception cref="ArgumentNullException">Позиция или цвет не заданы.</exception>
    /// <remarks>
    /// Открыт для тестов и разбора: по дереву видно, сколько итераций получил каждый ход.
    /// </remarks>
    public MctsNode Search(Board board, StoneColor color, Komi komi, IReadOnlyList<Move>? history = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        var moves = history ?? [];
        var tree = new MctsTree(board, color, _config.Ucb1C, _config.RaveK, usePriors: _evaluation.GivesPriors);

        // Бюджет по времени: сила не зависит от скорости машины. Отсчёт — только через TimeProvider
        // (AGENTS.md, п. 6): DateTime.Now и Stopwatch в AI запрещены.
        var startedAt = _timeProvider.GetUtcNow();
        var playouts = 0;

        while (HasBudget(startedAt, playouts))
        {
            _evaluation.Evaluate(tree, tree.Select(), moves, komi);
            playouts++;
        }

        return tree.Root;
    }

    /// <summary>Проверяет, остался ли бюджет поиска.</summary>
    /// <param name="startedAt">Момент начала поиска.</param>
    /// <param name="playouts">Сколько итераций уже сделано.</param>
    /// <returns><c>true</c>, если можно сделать ещё одну итерацию.</returns>
    /// <remarks>
    /// Режим задаётся настройками: либо время на ход, либо число итераций.
    /// Проверка стоит перед итерацией, поэтому поиск всегда завершает начатую оценку.
    /// </remarks>
    private bool HasBudget(DateTimeOffset startedAt, int playouts) =>
        _config.TimeBudget is { } time
            ? _timeProvider.GetUtcNow() - startedAt < time
            : playouts < _config.PlayoutBudget!.Value;
}
