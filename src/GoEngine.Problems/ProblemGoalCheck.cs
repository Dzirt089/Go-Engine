namespace GoEngine.Problems;

using GoEngine.AI;
using GoEngine.Core;

/// <summary>Проверка достижения цели задачи по правилам движка.</summary>
/// <remarks>
/// Для <see cref="ProblemGoal.Capture"/> цель достигнута, когда все камни целевой группы исчезли
/// с доски. Для <see cref="ProblemGoal.Live"/> — когда у выжившей части целевой группы есть два
/// настоящих глаза: глаза считает <see cref="EyeDetector"/> из слоя AI, своего второго определения
/// глаза здесь нет. Для <see cref="ProblemGoal.Dead"/> приговор выносит <see cref="ForcedCapture(Board, Problem, int)"/>
/// (факт уровня правил: атакующий форсирует захват за заявленную границу), а глазная проверка
/// <see cref="DefenderCanForceEyes"/> остаётся вспомогательной: она даёт достаточное условие жизни,
/// но не необходимое, и потому приговором быть не может.
/// </remarks>

public static class ProblemGoalCheck
{
    /// <summary>Итог проверки форсированного захвата вместе с отчётом о работе перебора.</summary>
    /// <param name="Forced">Захват форсирован за указанную границу.</param>
    /// <param name="Nodes">Сколько позиций перебрано, включая вложенные переборы.</param>
    /// <param name="LimitReached">Перебор упёрся в предел узлов: приговора нет.</param>
    public readonly record struct ForcedCaptureResult(bool Forced, int Nodes, bool LimitReached);

    /// <summary>Проверяет, достигнута ли цель задачи в позиции.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если цель достигнута.</returns>
    public static bool IsAchieved(Problem problem, Board board)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(board);

        return IsAchieved(problem, board, problem.CheckDepth > 0 ? problem.CheckDepth : ProblemLife.DefaultHorizon);
    }

    /// <summary>Может ли сторона создать ко в этой позиции.</summary>
    /// <param name="board">Позиция без истории партии.</param>
    /// <param name="color">Сторона, которая ходит.</param>
    /// <returns><c>true</c>, если есть ход, снимающий один камень с запрещённым ответом.</returns>
    /// <remarks>
    /// Критерий механический, без догадок: ход снимает <b>ровно один</b> камень, ответный ход
    /// по форме возможен, но восстанавливает ровно ту позицию, что стояла до хода, — значит,
    /// в партии он запрещён правилом ко (позиция повторилась бы). История для такой проверки
    /// не нужна: сравниваются камни двух позиций по каждой точке.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Позиция или сторона не заданы.</exception>
    public static bool KoAvailable(Board board, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        foreach (var point in board.EmptyPoints())
        {
            var move = Move.Play(point, color);

            if (!board.IsLegal(move).IsSuccess)
            {
                continue;
            }

            var after = board.ApplyMove(move);

            if (after.CapturedStones.Count != 1)
            {
                continue;
            }

            var recapture = Move.Play(after.CapturedStones[0], color.Opponent());

            // Ответ возможен по форме и возвращает прежнюю позицию — это ко, а не размен.
            if (after.IsLegal(recapture).IsSuccess && SamePosition(board, after.ApplyMove(recapture)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Совпадают ли позиции по каждой точке доски.</summary>
    /// <param name="left">Первая позиция.</param>
    /// <param name="right">Вторая позиция.</param>
    /// <returns><c>true</c>, если расстановка камней одинакова.</returns>
    private static bool SamePosition(Board left, Board right)
    {
        if (left.Size != right.Size)
        {
            return false;
        }

        foreach (var point in left.AllPoints())
        {
            if (left.At(point) != right.At(point))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Считает настоящие глаза целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача.</param>
    /// <returns>Число настоящих глаз, соседних с камнями группы.</returns>
    public static int CountRealEyes(Board board, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(problem);

        return CountRealEyes(board, problem.TargetColor, LiveGroup(board, problem));
    }

    /// <summary>Проверяет, достигнута ли цель задачи в позиции с учётом заявленной границы.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="board">Позиция.</param>
    /// <param name="depth">Граница перебора в полуходах для цели <see cref="ProblemGoal.Dead"/>.</param>
    /// <returns><c>true</c>, если цель достигнута.</returns>
    /// <remarks>
    /// Для <see cref="ProblemGoal.Capture"/> и <see cref="ProblemGoal.Live"/> граница не нужна —
    /// результат совпадает с <see cref="IsAchieved(Problem, Board)"/>. Для
    /// <see cref="ProblemGoal.Dead"/> цель достигнута, если защищающийся, играя первым, не может
    /// форсировать два глаза за эту границу: перебор ограничен, и граница берётся из задачи
    /// (<see cref="Problem.CheckDepth"/>), когда параметр не задан.
    /// </remarks>
    public static bool IsAchieved(Problem problem, Board board, int depth)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(board);

        var horizon = depth > 0 ? depth : problem.CheckDepth > 0 ? problem.CheckDepth : ProblemLife.DefaultHorizon;

        if (problem.Goal == ProblemGoal.Ko)
        {
            return KoAvailable(board, problem.SolverColor);
        }

        if (problem.Goal == ProblemGoal.Capture)
        {
            return problem.TargetPoints.All(point => board.At(point) == StoneColor.Empty);
        }

        var group = LiveGroup(board, problem);

        if (group.Count == 0)
        {
            // Целевой группы на доске нет: жить и спасать нечего, «мертва» достигнута.
            return problem.Goal == ProblemGoal.Dead;
        }

        var radius = problem.WindowRadius;

        if (problem.Goal == ProblemGoal.Live)
        {
            // Прежняя модель: два настоящих глаза по детектору слоя AI. Детектор консервативен —
            // ложной жизни не объявляет, но настоящие глаза формой часто не видит, поэтому для
            // задач «жизнь формой» есть отдельная цель Eyes.
            return CountRealEyes(board, problem) >= 2;
        }

        if (problem.Goal == ProblemGoal.Eyes)
        {
            // Новая модель: защищающийся успевает построить два глаза за объявленный горизонт.
            return ProblemLife.CanForceTwoEyes(board, problem.TargetColor, group, horizon, radius).Forced;
        }

        if (problem.Goal == ProblemGoal.Survive)
        {
            // Спасение: атакующий не форсирует снятие группы за горизонт.
            return ProblemLife.CanSurvive(board, problem.TargetColor, group, horizon, radius).Forced;
        }

        // «Мертва» — факт уровня правил: атакующий форсирует захват группы за горизонт.
        // Модель «за горизонт не построены два глаза» приговором здесь быть не может: на коротком
        // горизонте «не успел построить» неотличимо от «не построит никогда», и первая версия
        // объявляла мёртвыми уже живые группы (тесты библиотеки это поймали). Глаза как критерий
        // жизни остаются достаточным условием, а не приговором о смерти.
        return ForcedCapture(board, problem, horizon);
    }

    /// <summary>Стоит ли целевая группа на доске: её опорный камень ещё занят цветом цели.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача: из неё берутся целевая точка и цвет группы.</param>
    /// <returns><c>true</c>, если группа ещё на доске.</returns>
    /// <remarks>
    /// Нужно цели <see cref="ProblemGoal.Dead"/>: «мертва, но не снята» и «уже снята» — разные цели.
    /// Снятая группа — это <see cref="ProblemGoal.Capture"/>, и лист задачи «мертва» снятым быть не может.
    /// </remarks>
    public static bool TargetPresent(Board board, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(problem);

        return board.At(problem.Target) == problem.TargetColor;
    }

    /// <summary>Форсирует ли атакующий захват целевой группы за указанную глубину.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача: из неё берутся целевая точка и цвет группы.</param>
    /// <param name="depth">Граница перебора в полуходах.</param>
    /// <returns><c>true</c>, если захват форсирован; при неполном переборе — <c>false</c>.</returns>
    /// <remarks>
    /// Считается тем же <see cref="ProblemSolver"/>, вторым перебором не дублируется. Атакующий
    /// (противник цвета цели) ходит первым; цель перебора — <see cref="ProblemGoal.Capture"/>.
    /// Это факт уровня правил, а не глазная эвристика: глазами жизнь доказывается достаточно,
    /// но не необходимо, поэтому приговором они быть не могут.
    /// </remarks>
    public static bool ForcedCapture(Board board, Problem problem, int depth) =>
        ForcedCapture(board, problem, depth, problem?.TargetColor.Opponent() ?? StoneColor.Black);

    /// <summary>Форсирует ли атакующий захват целевой группы, если первым ходит указанная сторона.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача: из неё берутся целевая точка и цвет группы.</param>
    /// <param name="depth">Граница перебора в полуходах.</param>
    /// <param name="toMove">Кто ходит первым: атакующий или защищающийся.</param>
    /// <returns><c>true</c>, если захват форсирован; при неполном переборе — <c>false</c>.</returns>
    public static bool ForcedCapture(Board board, Problem problem, int depth, StoneColor toMove) =>
        ForcedCaptureDetailed(board, problem, depth, toMove).Forced;

    /// <summary>То же, но с отчётом о работе: сколько позиций перебрано и не упёрся ли перебор в предел.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача: из неё берутся целевая точка и цвет группы.</param>
    /// <param name="depth">Граница перебора в полуходах.</param>
    /// <param name="toMove">Кто ходит первым: атакующий или защищающийся.</param>
    /// <returns>Приговор вместе с числом перебранных позиций и признаком неполного перебора.</returns>
    /// <remarks>
    /// Нужен там, где приговор попадает в отчёт (например, <see cref="ProblemSolver.Search"/>): без суммы
    /// по вложенным переборам «0 узлов» читается как приговор без вычислений, хотя работа сделана.
    /// </remarks>
    public static ForcedCaptureResult ForcedCaptureDetailed(Board board, Problem problem, int depth, StoneColor toMove)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(toMove);

        if (depth <= 0)
        {
            return new ForcedCaptureResult(false, 0, false);
        }

        if (board.At(problem.Target) != problem.TargetColor)
        {
            // Целевой группы на доске нет: захват уже состоялся.
            return new ForcedCaptureResult(true, 0, false);
        }

        var group = GroupTracker.FindGroup(board, problem.Target).Stones;
        var attacker = problem.TargetColor.Opponent();

        if (toMove == attacker)
        {
            var capture = new ProblemSolver(board, ProblemGoal.Capture, attacker, problem.Target, group);
            var direct = capture.Search(depth);

            return new ForcedCaptureResult(!direct.LimitReached && direct.SolverWins, direct.Nodes, direct.LimitReached);
        }

        var any = false;
        var nodes = 0;

        foreach (var point in LocalCandidates(board, problem))
        {
            var move = Move.Play(point, attacker.Opponent());

            if (!board.IsLegal(move).IsSuccess)
            {
                continue;
            }

            any = true;

            var after = board.ApplyMove(move);
            var capture = new ProblemSolver(after, ProblemGoal.Capture, attacker, problem.Target, group);
            var result = capture.Search(depth - 1);
            nodes += result.Nodes;

            if (result.LimitReached)
            {
                return new ForcedCaptureResult(false, nodes, true);
            }

            if (!result.SolverWins)
            {
                return new ForcedCaptureResult(false, nodes, false);
            }
        }

        return new ForcedCaptureResult(any, nodes, false);
    }

    private static IEnumerable<Point> LocalCandidates(Board board, Problem problem)
    {
        var radius = problem.WindowRadius;

        HashSet<Point> points = [];
        HashSet<Point> stones = [.. GroupTracker.FindGroup(board, problem.Target).Stones];

        foreach (var stone in stones)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var x = stone.X + dx;
                    var y = stone.Y + dy;

                    if (x >= 0 && y >= 0 && x < board.Size.Value && y < board.Size.Value)
                    {
                        points.Add(new Point((byte)x, (byte)y));
                    }
                }
            }

            foreach (var neighbour in stone.Neighbors(board.Size))
            {
                points.Add(neighbour);
            }
        }

        return points.Where(point => board.IsEmpty(point));
    }

    /// <summary>Может ли целевая группа форсировать два глаза за указанную глубину.</summary>
    /// <param name="board">Позиция; защищающийся ходит первым.</param>
    /// <param name="problem">Задача: из неё берутся целевая точка и цвет группы.</param>
    /// <param name="depth">Граница перебора в полуходах.</param>
    /// <returns><c>true</c>, если защита успевает или перебор оказался неполным.</returns>
    /// <remarks>
    /// <b>Вспомогательная проверка.</b> Ограниченная эвристика: даёт достаточное условие жизни,
    /// но не необходимое, поэтому в приговоре по цели <see cref="ProblemGoal.Dead"/> не участвует
    /// (приговор — <see cref="ForcedCapture(Board, Problem, int)"/>). Второго перебора нет: используется
    /// <see cref="ProblemSolver"/> с обратными ролями.
    /// </remarks>
    public static bool DefenderCanForceEyes(Board board, Problem problem, int depth)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(problem);

        if (depth <= 0)
        {
            return true;
        }

        if (board.At(problem.Target) != problem.TargetColor)
        {
            return false;
        }

        var group = GroupTracker.FindGroup(board, problem.Target).Stones;
        var solver = new ProblemSolver(board, ProblemGoal.Live, problem.TargetColor, problem.Target, group);
        var result = solver.Search(depth);

        return result.LimitReached || result.SolverWins;
    }

    /// <summary>Считает настоящие глаза группы в позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет группы.</param>
    /// <param name="group">Камни группы.</param>
    /// <returns>Число настоящих глаз, соседних с камнями группы.</returns>
    public static int CountRealEyes(Board board, StoneColor color, IReadOnlyList<Point> group)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(group);

        if (group.Count == 0)
        {
            return 0;
        }

        var eyes = 0;

        foreach (var point in board.EmptyPoints())
        {
            if (!IsAdjacentTo(group, point, board.Size))
            {
                continue;
            }

            if (EyeDetector.IsEye(board, point, color))
            {
                eyes++;
            }
        }

        return eyes;
    }

    /// <summary>Возвращает выжившую часть целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача.</param>
    /// <returns>Точки камней целевого цвета из начальной группы.</returns>
    public static IReadOnlyList<Point> LiveGroup(Board board, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(problem);

        List<Point> group = [];

        foreach (var point in problem.TargetPoints)
        {
            if (board.At(point) == problem.TargetColor)
            {
                group.Add(point);
            }
        }

        return group.AsReadOnly();
    }

    private static bool IsAdjacentTo(IReadOnlyList<Point> group, Point point, BoardSize size)
    {
        foreach (var stone in group)
        {
            foreach (var neighbour in stone.Neighbors(size))
            {
                if (neighbour == point)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
