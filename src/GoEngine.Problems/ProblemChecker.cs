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

        if (problem.Goal == ProblemGoal.Reference)
        {
            // У задачи по решению источника нет машинного критерия исхода, поэтому движок проверяет
            // только форму: источник, формулировку и само дерево (это делает Walk ниже). Ни горизонт,
            // ни «цель достигнута в начале» здесь не спрашиваются — спрашивать нечего.
            if (string.IsNullOrWhiteSpace(problem.Source))
            {
                issues.Add("У задачи по решению источника не указан источник (SO).");
            }

            if (string.IsNullOrWhiteSpace(problem.Description))
            {
                issues.Add("У задачи по решению источника нет формулировки для игрока (GC).");
            }
        }
        else if (NeedsHorizon(problem.Goal) && problem.CheckDepth <= 0)
        {
            issues.Add(
                $"Цель {problem.Goal.Name} требует заявленного горизонта проверки (GX): "
                + "без него приговор о жизни невоспроизводим, а окно задаёт область ходов (GW).");
        }
        else if (problem.Goal == ProblemGoal.Dead)
        {
            // Для цели dead «решено без ходов» означает факт: группа мертва даже когда защита
            // ходит первой. Тогда убивающий ход не нужен — задача бессмысленна.
            // Спрашивается именно «мертва ли группа, когда защита ходит первой»: если да, убивающий
            // ход не нужен, и задача бессмысленна. Модель глаз здесь не участвует — см. ProblemGoalCheck.
            if (problem.CheckDepth > 0
                && ProblemGoalCheck.ForcedCapture(board, problem, problem.CheckDepth, problem.TargetColor))
            {
                issues.Add("Группа мертва уже при ходе защиты: задача решается без хода убивающего.");
            }
        }
        else if (ProblemGoalCheck.IsAchieved(problem, board, problem.CheckDepth))
        {
            issues.Add("Цель достигнута уже в начальной позиции: задача решается без ходов.");
        }

        Walk(problem, board, problem.Solution, problem.SolverColor, 1, issues);

        return new ProblemReport(problem.Id, issues.AsReadOnly());
    }

    /// <summary>Нужен ли цели объявленный горизонт проверки.</summary>
    /// <param name="goal">Цель задачи.</param>
    /// <returns><c>true</c>, если приговор зависит от горизонта.</returns>
    /// <remarks>
    /// Горизонт нужен всем целям о жизни: <see cref="ProblemGoal.Live"/>, <see cref="ProblemGoal.Dead"/>
    /// и <see cref="ProblemGoal.Survive"/>. <see cref="ProblemGoal.Capture"/> — факт снятия камней,
    /// а <see cref="ProblemGoal.Ko"/> — свойство позиции, им горизонт не нужен.
    /// </remarks>
    private static bool NeedsHorizon(ProblemGoal goal) =>
        goal == ProblemGoal.Eyes || goal == ProblemGoal.Dead || goal == ProblemGoal.Survive;

    private static void Walk(Problem problem, Board board, ProblemNode node, StoneColor side, int depth, List<string> issues)
    {
        if (node.IsLeaf)
        {
            if (problem.Goal == ProblemGoal.Reference)
            {
                // У задачи по решению источника машинного критерия исхода нет: в листе проверять
                // нечего. Легальность ходов, чередование сторон и структуру дерева проверил обход
                // выше — это и есть та форма, которую движок в этом виде подтверждает.
                return;
            }

            if (problem.Goal == ProblemGoal.Dead)
            {
                // Лист «мертва» — это позиция, где группа ещё на доске, но за горизонт она либо
                // снимается форсированно, либо не успевает построить два глаза. Снятая группа
                // подтверждает другую цель (capture), и лист задачу «мертва, но не снята» не доказывает.
                if (!ProblemGoalCheck.TargetPresent(board, problem))
                {
                    issues.Add($"Лист на глубине {depth}: целевая группа уже снята — это цель capture, а не dead.");
                }
                else if (!ProblemGoalCheck.IsAchieved(problem, board, problem.CheckDepth))
                {
                    issues.Add(
                        $"Лист на глубине {depth}: группа не мертва за {problem.CheckDepth} полуходов — "
                        + "ни захват не форсирован, ни двух глаз у защиты нет.");
                }
            }
            else if (!ProblemGoalCheck.IsAchieved(problem, board, problem.CheckDepth))
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

            var after = board.ApplyMove(move);

            // Дерево обрезается на достигнутой цели: продолжать после выполнения цели бессмысленно.
            // У задачи по решению источника цели в машинном смысле нет — линия идёт до конца решения
            // источника, и обрывать её на «цель достигнута» нечем.
            if (problem.Goal != ProblemGoal.Reference
                && ProblemGoalCheck.IsAchieved(problem, after, problem.CheckDepth)
                && !accepted.Next.IsLeaf)
            {
                issues.Add($"Глубина {depth}: ход {Describe(move)} выполняет цель, но узел не лист.");
            }

            Walk(problem, after, accepted.Next, side.Opponent(), depth + 1, issues);
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
