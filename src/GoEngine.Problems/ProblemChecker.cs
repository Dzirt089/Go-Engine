namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Проверка задачи движком: расстановка, дерево решения и достижение цели в листах.</summary>
/// <remarks>
/// <para>
/// Что гарантируется: расстановка собирается легальными ходами и совпадает со схемой <b>по каждой
/// точке</b>; у доски нет истории партии, поэтому суперко к задачам не применяется; каждый
/// принимаемый ход дерева легален в своей позиции и принадлежит стороне, которая в ней ходит;
/// в каждом листе цель достигнута по правилам движка (<see cref="ProblemGoalCheck"/>).
/// </para>
/// <para>
/// Чего не гарантируется: что дерево содержит <b>все</b> выигрывающие линии и что у задачи нет
/// второго решения. Это отдельная проверка перебором — <see cref="ProblemSolver"/>, и её результат
/// приводится в отчёте по библиотеке, а не здесь.
/// </para>
/// </remarks>
public static class ProblemChecker
{
    /// <summary>Проверяет задачу.</summary>
    /// <param name="problem">Задача.</param>
    /// <returns>Отчёт с замечаниями.</returns>
    public static ProblemReport Check(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        List<string> issues = [];
        var built = ProblemSetup.Build(problem.Size, problem.Stones);

        if (!built.IsSuccess)
        {
            issues.Add($"Позиция не собирается: {built.Error}");

            return new ProblemReport(problem.Id, issues.AsReadOnly());
        }

        var board = built.Value!;

        if (board.History is not null)
        {
            issues.Add("У доски задачи есть история партии: суперко в задачах не применяется.");
        }

        var schema = ProblemSetup.Verify(board, problem.Stones);

        if (!schema.IsSuccess)
        {
            issues.Add($"Собранная позиция не совпадает со схемой: {schema.Error}");
        }

        foreach (var point in problem.TargetPoints)
        {
            if (board.At(point) != problem.TargetColor)
            {
                issues.Add($"В целевой точке {point} стоит {board.At(point).Name}, а ожидался {problem.TargetColor.Name}.");
            }
        }

        if (problem.TargetPoints.Count == 0)
        {
            issues.Add("Целевая группа пуста.");
        }

        if (Liberties(board, problem.TargetPoints).Count == 0)
        {
            issues.Add("У целевой группы нет дамэ в начальной позиции: задача начинается со снятой группы.");
        }

        if (ProblemGoalCheck.IsAchieved(problem, board))
        {
            issues.Add("Цель достигнута уже в начальной позиции: задача решается без ходов.");
        }

        Walk(problem, board, problem.Solution, problem.SolverColor, 1, issues);

        return new ProblemReport(problem.Id, issues.AsReadOnly());
    }

    private static void Walk(Problem problem, Board board, ProblemNode node, StoneColor side, int depth, List<string> issues)
    {
        if (node.IsLeaf)
        {
            if (!ProblemGoalCheck.IsAchieved(problem, board))
            {
                issues.Add($"Лист на глубине {depth}: цель не достигнута.");
            }

            return;
        }

        foreach (var accepted in node.Moves)
        {
            var move = accepted.Move;

            if (move.Type == MoveType.Play && move.Color != side)
            {
                issues.Add($"Глубина {depth}: ход {Describe(move)} сделан за {move.Color.Name}, а ходит {side.Name}.");
            }

            if (move.Type != MoveType.Play)
            {
                Walk(problem, board, accepted.Next, side.Opponent(), depth + 1, issues);

                continue;
            }

            var legal = board.IsLegal(move);

            if (!legal.IsSuccess)
            {
                issues.Add($"Глубина {depth}: ход {Describe(move)} нелегален — {legal.Error}");

                continue;
            }

            Walk(problem, board.ApplyMove(move), accepted.Next, side.Opponent(), depth + 1, issues);
        }
    }

    private static IReadOnlyList<Point> Liberties(Board board, IReadOnlyList<Point> group)
    {
        List<Point> liberties = [];

        foreach (var stone in group)
        {
            foreach (var neighbour in stone.Neighbors(board.Size))
            {
                if (board.At(neighbour) == StoneColor.Empty && !liberties.Contains(neighbour))
                {
                    liberties.Add(neighbour);
                }
            }
        }

        return liberties.AsReadOnly();
    }

    private static string Describe(Move move) =>
        move.Type == MoveType.Play ? $"{move.Color.Name} {move.Point}" : $"{move.Color.Name} пас";
}
