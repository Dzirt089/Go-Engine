namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Жизнь и смерть по модели «два глаза за горизонт».</summary>
/// <remarks>
/// <para>
/// Прежняя модель требовала либо снятия камней, либо двух глаз по <c>EyeDetector</c> из слоя AI.
/// Он даёт достаточное условие жизни, но не необходимое: дамэ с пустыми диагоналями он глазом
/// не считает, поэтому настоящие цумэго, где жизнь строится формой, не подтверждались вовсе.
/// </para>
/// <para>
/// Здесь глаз считается строго по границе: пустая область, у которой <b>все</b> соседние камни —
/// цвета группы (край доски границу завершает и стеной не считается). Это шире <c>EyeDetector</c>,
/// и у критерия есть честная граница: в редких формах ложный глаз может сойти за настоящий
/// (записано в <c>PROBLEMS.md</c>). Жизнь и смерть проверяются перебором с объявленными
/// горизонтом (<see cref="Problem.CheckDepth"/>) и окном ходов (<see cref="Problem.WindowRadius"/>):
/// приговор без пары «горизонт + окно» невоспроизводим и потому не выносится.
/// </para>
/// <para>
/// Перебор видит только точки, примыкающие к камням в окне: ход в пустоте ничего не решает,
/// а ветвление растёт втрое. Если бюджет узлов исчерпан, приговора нет: <c>Forced = false</c>
/// и <c>LimitReached = true</c>. Недоказанное «жива» так же недопустимо, как недоказанное «мертва».
/// </para>
/// </remarks>
public static class ProblemLife
{
    /// <summary>Предел узлов перебора по умолчанию: при упоре в него приговора нет.</summary>
    public const int DefaultMaxNodes = 200_000;

    /// <summary>Горизонт по умолчанию для задач без объявленного.</summary>
    /// <remarks>
    /// Библиотека обязана объявлять горизонт явно — это проверяет <see cref="ProblemChecker"/>.
    /// Значение нужно только синтетическим проверкам, где задача собирается «на месте».
    /// </remarks>
    public const int DefaultHorizon = 5;

    /// <summary>Сколько ходов-кандидатов рассматривается в позиции: ближайшие к группе.</summary>
    private const int MaxCandidates = 32;

    /// <summary>Итог перебора жизни вместе с отчётом о работе.</summary>
    /// <param name="Forced">Сторона форсирует результат за указанный горизонт.</param>
    /// <param name="Eyes">Сколько глаз у группы в исходной позиции (для отчёта).</param>
    /// <param name="Nodes">Сколько позиций перебрано.</param>
    /// <param name="LimitReached">Перебор упёрся в предел узлов: приговора нет.</param>
    public readonly record struct LifeSearchResult(bool Forced, int Eyes, int Nodes, bool LimitReached);

    /// <summary>Настоящий ли глаз по строгому критерию границы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Пустая точка, которую проверяют.</param>
    /// <param name="color">Цвет группы, которой глаз принадлежал бы.</param>
    /// <returns><c>true</c>, если вся граница связной пустой области — камни этого цвета.</returns>
    /// <remarks>
    /// Камни противника на границе делают точку не глазом. Диагонали не проверяются: именно этим
    /// критерий шире <c>EyeDetector</c>, и потому ложный глаз редких форм может быть принят
    /// за настоящий — граница записана в <c>PROBLEMS.md</c>.
    /// </remarks>
    public static bool IsEye(Board board, Point point, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        if (board.At(point) != StoneColor.Empty)
        {
            return false;
        }

        var region = Regions(board, point);

        foreach (var point2 in region)
        {
            foreach (var neighbour in Neighbours(board, point2))
            {
                var stone = board.At(neighbour);

                if (stone != StoneColor.Empty && stone != color)
                {
                    return false;
                }
            }
        }

        // Второе условие — в область нельзя войти: иначе это не глаз, а открытое пространство
        // (или ложный глаз: камень на границе в атари соперник снимает, и вход становится законным).
        // Без этой проверки открытая часть доски у края считалась бы глазом и «доказывала» жизнь.
        foreach (var point2 in region)
        {
            if (board.IsLegal(Move.Play(point2, color.Opponent())).IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Считает глаза, примыкающие к камням группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет группы.</param>
    /// <param name="group">Камни группы: глаза ищутся только рядом с ними.</param>
    /// <returns>Число различных глазных областей рядом с группой.</returns>
    /// <remarks>Область считается один раз, даже если к группе она примыкает несколькими точками.</remarks>
    public static int CountEyes(Board board, StoneColor color, IReadOnlyList<Point> group)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(group);

        HashSet<Point> seen = [];
        var eyes = 0;

        foreach (var stone in group)
        {
            foreach (var neighbour in Neighbours(board, stone))
            {
                if (board.At(neighbour) != StoneColor.Empty || seen.Contains(neighbour))
                {
                    continue;
                }

                var region = Regions(board, neighbour);
                seen.UnionWith(region);

                if (IsEye(board, neighbour, color))
                {
                    eyes++;
                }
            }
        }

        return eyes;
    }

    /// <summary>Форсирует ли сторона два глаза за горизонт.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет защищающейся группы.</param>
    /// <param name="group">Камни группы.</param>
    /// <param name="depth">Горизонт в полуходах.</param>
    /// <param name="radius">Радиус окна ходов вокруг камней группы.</param>
    /// <param name="maxNodes">Предел узлов перебора.</param>
    /// <param name="toMove">Кто ходит первым; <c>null</c> — защищающийся (лист задачи).</param>
    /// <returns>Приговор с числом глаз в исходной позиции и отчётом о переборе.</returns>
    /// <remarks>
    /// Два глаза — победа защиты; снятие группы — поражение; исчерпание горизонта — «не успел»;
    /// упор в предел узлов — приговора нет. Порядок хода важен: в листе задачи ходит защищающийся,
    /// а после хода решающего первым ходит уже соперник.
    /// </remarks>
    public static LifeSearchResult CanForceTwoEyes(
        Board board,
        StoneColor color,
        IReadOnlyList<Point> group,
        int depth,
        int radius,
        int maxNodes = DefaultMaxNodes,
        StoneColor? toMove = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        var context = new SearchContext(board, color, [.. group], radius, maxNodes);
        var forced = context.SearchEyes(depth, toMove ?? color);

        return new LifeSearchResult(forced && !context.LimitReached, context.EyesAtStart, context.Nodes, context.LimitReached);
    }

    /// <summary>Удерживает ли защищающийся группу за горизонт.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет защищающейся группы.</param>
    /// <param name="group">Камни группы.</param>
    /// <param name="depth">Горизонт в полуходах.</param>
    /// <param name="radius">Радиус окна ходов.</param>
    /// <param name="maxNodes">Предел узлов перебора.</param>
    /// <param name="toMove">Кто ходит первым; <c>null</c> — атакующий (снятие форсирует он).</param>
    /// <returns>Приговор: атакующий не снимает группу за горизонт.</returns>
    /// <remarks>
    /// Так проверяются задачи, где группа живёт не глазами, а бегством или борьбой: «спасти камни»
    /// означает «противник не форсирует снятие». При упоре в предел узлов приговора нет.
    /// </remarks>
    public static LifeSearchResult CanSurvive(
        Board board,
        StoneColor color,
        IReadOnlyList<Point> group,
        int depth,
        int radius,
        int maxNodes = DefaultMaxNodes,
        StoneColor? toMove = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        var context = new SearchContext(board, color, [.. group], radius, maxNodes);
        var attackerForced = context.SearchCapture(depth, toMove ?? color.Opponent());

        return new LifeSearchResult(!attackerForced && !context.LimitReached, context.EyesAtStart, context.Nodes, context.LimitReached);
    }

    private static IEnumerable<Point> Neighbours(Board board, Point point)
    {
        var size = board.Size.Value;
        var x = (int)point.X;
        var y = (int)point.Y;

        if (y > 0)
        {
            yield return new Point(point.X, (byte)(y - 1));
        }

        if (y + 1 < size)
        {
            yield return new Point(point.X, (byte)(y + 1));
        }

        if (x > 0)
        {
            yield return new Point((byte)(x - 1), point.Y);
        }

        if (x + 1 < size)
        {
            yield return new Point((byte)(x + 1), point.Y);
        }
    }

    /// <summary>Собирает связную пустую область вокруг точки.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="start">Пустая точка.</param>
    /// <returns>Точки области.</returns>
    private static HashSet<Point> Regions(Board board, Point start)
    {
        HashSet<Point> region = [start];
        Stack<Point> stack = new();
        stack.Push(start);

        while (stack.Count > 0)
        {
            foreach (var neighbour in Neighbours(board, stack.Pop()))
            {
                if (board.At(neighbour) == StoneColor.Empty && region.Add(neighbour))
                {
                    stack.Push(neighbour);
                }
            }
        }

        return region;
    }

    /// <summary>Общий контекст перебора: правила хода, окно, бюджет и таблица позиций.</summary>
    private sealed class SearchContext
    {
        private readonly Board _board;
        private readonly StoneColor _defender;
        private readonly StoneColor _attacker;
        private readonly IReadOnlyList<Point> _group;
        private readonly int _radius;
        private readonly int _maxNodes;
        private readonly Dictionary<(ulong Hash, int Side, int Depth, bool Capture), bool> _memo = [];

        /// <summary>Создаёт контекст перебора.</summary>
        /// <param name="board">Позиция.</param>
        /// <param name="defender">Цвет защищающейся группы.</param>
        /// <param name="group">Камни группы.</param>
        /// <param name="radius">Радиус окна ходов.</param>
        /// <param name="maxNodes">Предел узлов.</param>
        public SearchContext(Board board, StoneColor defender, IReadOnlyList<Point> group, int radius, int maxNodes)
        {
            _board = board;
            _defender = defender;
            _attacker = defender.Opponent();
            _group = group;
            _radius = radius;
            _maxNodes = maxNodes;
            EyesAtStart = CountEyes(board, defender, group);
        }

        /// <summary>Сколько глаз у группы в исходной позиции.</summary>
        public int EyesAtStart { get; }

        /// <summary>Сколько позиций перебрано.</summary>
        public int Nodes { get; private set; }

        /// <summary>Перебор упёрся в предел: приговора нет.</summary>
        public bool LimitReached { get; private set; }

        /// <summary>Форсирует ли защищающийся два глаза.</summary>
        /// <param name="depth">Остаток горизонта.</param>
        /// <param name="toMove">Кто ходит.</param>
        /// <returns><c>true</c>, если два глаза достигаются против любой защиты.</returns>
        public bool SearchEyes(int depth, StoneColor toMove) => Search(_board, depth, toMove, capture: false);

        /// <summary>Форсирует ли атакующий снятие группы.</summary>
        /// <param name="depth">Остаток горизонта.</param>
        /// <param name="toMove">Кто ходит.</param>
        /// <returns><c>true</c>, если группа снимается при любой защите.</returns>
        public bool SearchCapture(int depth, StoneColor toMove) => Search(_board, depth, toMove, capture: true);

        // Позиция — параметр, а не поле: рекурсия обязана идти по НОВОЙ доске после хода. В первой
        // версии перебор читал исходную позицию на каждом узле и потому «не видел» ни захвата, ни глаз.
        private bool Search(Board board, int depth, StoneColor toMove, bool capture)
        {
            if (TargetGone(board))
            {
                // Группы нет: для глаз это поражение защиты, для захвата — победа атакующего.
                return capture;
            }

            if (!capture && CountEyes(board, _defender, _group) >= 2)
            {
                return true;
            }

            if (depth <= 0)
            {
                return false;
            }

            if (Nodes > _maxNodes)
            {
                LimitReached = true;

                return false;
            }

            var key = (Hash(board), toMove.Id, depth, capture);

            if (_memo.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var moves = Candidates(board, toMove);

            if (moves.Count == 0)
            {
                return false;
            }

            // Кто ищет «хотя бы один» ход, а кто «чтобы удачны были все» — зависит от задачи перебора,
            // а не от того, кто ходит: в поиске глаз перебирает защита (ей нужен один удачный ответ,
            // соперник должен провалить все свои), а в поиске захвата — атакующий (ему нужен один
            // удачный ход, защита должна провалить все свои). Перепутать эти кванторы — значит
            // получить «захват не форсирован» на любой позиции, что и было в первой версии.
            var orNode = capture ? toMove == _attacker : toMove == _defender;
            var result = !orNode;

            foreach (var move in moves)
            {
                Nodes++;

                if (Nodes > _maxNodes)
                {
                    LimitReached = true;

                    return false;
                }

                var after = board.ApplyMove(move);
                var outcome = Search(after, depth - 1, toMove.Opponent(), capture);

                if (orNode)
                {
                    if (outcome)
                    {
                        _memo[key] = true;

                        return true;
                    }
                }
                else if (!outcome)
                {
                    _memo[key] = false;

                    return false;
                }

                result = outcome;
            }

            _memo[key] = result;

            return result;
        }

        /// <summary>Ходы стороны: пустые точки в окне, примыкающие к камням, ближайшие первыми.</summary>
        /// <param name="board">Позиция, в которой ищутся ходы.</param>
        /// <param name="toMove">Кто ходит.</param>
        /// <returns>Легальные ходы по порядку.</returns>
        /// <remarks>
        /// Ход в пустоте не решает задачу, зато утраивает ветвление, поэтому рассматриваются только
        /// точки, касающиеся камней в окне. Если в окне ходов нет вовсе, сторона пропускает ход.
        /// </remarks>
        private List<Move> Candidates(Board board, StoneColor toMove)
        {
            List<(int Distance, Move Move)> candidates = [];

            foreach (var point in board.AllPoints())
            {
                if (board.At(point) != StoneColor.Empty || !InWindow(point) || !TouchesStone(board, point))
                {
                    continue;
                }

                var move = Move.Play(point, toMove);

                if (board.IsLegal(move).IsSuccess)
                {
                    candidates.Add((Distance(point), move));
                }
            }

            if (candidates.Count == 0)
            {
                var pass = Move.Pass(toMove);

                if (board.IsLegal(pass).IsSuccess)
                {
                    candidates.Add((int.MaxValue, pass));
                }
            }

            return [.. candidates.OrderBy(candidate => candidate.Distance).Take(MaxCandidates).Select(candidate => candidate.Move)];
        }

        /// <summary>Касается ли точка камня в окне.</summary>
        /// <param name="board">Позиция.</param>
        /// <param name="point">Точка.</param>
        /// <returns><c>true</c>, если рядом есть камень.</returns>
        private bool TouchesStone(Board board, Point point)
        {
            foreach (var neighbour in Neighbours(board, point))
            {
                if (board.At(neighbour) != StoneColor.Empty && InWindow(neighbour))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Расстояние до ближайшего камня группы.</summary>
        /// <param name="point">Точка.</param>
        /// <returns>Шахматное расстояние.</returns>
        private int Distance(Point point)
        {
            var best = int.MaxValue;

            foreach (var stone in _group)
            {
                var distance = Math.Max(Math.Abs(point.X - stone.X), Math.Abs(point.Y - stone.Y));

                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        /// <summary>Лежит ли точка в окне вокруг камней группы.</summary>
        /// <param name="point">Точка.</param>
        /// <returns><c>true</c>, если расстояние до камня группы не больше радиуса.</returns>
        private bool InWindow(Point point) => Distance(point) <= _radius;

        private bool TargetGone(Board board)
        {
            foreach (var stone in _group)
            {
                if (board.At(stone) == _defender)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Хеш позиции: камни по всем точкам.</summary>
        /// <param name="board">Позиция.</param>
        /// <returns>64-битный хеш.</returns>
        /// <remarks>Свой хеш, а не <c>PositionHash</c> из ядра: там он считается по истории партии.</remarks>
        private static ulong Hash(Board board)
        {
            var hash = 1469598103934665603UL;

            foreach (var point in board.AllPoints())
            {
                var stone = board.At(point);
                var value = stone == StoneColor.Black ? 1UL : stone == StoneColor.White ? 2UL : 0UL;

                hash = (hash ^ (value + 1)) * 1099511628211UL;
            }

            return hash;
        }
    }
}
