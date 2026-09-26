namespace GoEngine.AI.Patterns;

using GoEngine.Core;

/// <summary>Политика хода для playout'ов: эвристики фигур плюс веса окрестностей 3×3.</summary>
/// <remarks>
/// Порядок приоритетов: захват группы в атари, спасение своей группы в атари, блокировка
/// глазного пространства противника, разрезание, продление своих камней, затем ход,
/// взвешенный по таблице <see cref="PatternWeights"/>. Внутри класса ход выбирается
/// взвешенно-случайно, а не «лучший»: иначе playout теряет разнообразие и оценка позиции
/// становится смещённой.
/// Дорогие проверки (легальность, безопасность группы) выполняются только для кандидатов
/// рядом с камнями, а запасной случайный ход ищется пробами точек: полный перебор всех ходов
/// на каждом ходу playout'а стоил бы сотни проверок правил.
/// Время в политике не нужно: выбор хода — это один проход по кандидатам, поэтому
/// <c>TimeProvider</c> здесь не принимается (в отличие от <see cref="Mcts.MctsMoveSelector"/>).
/// </remarks>
public sealed class PatternPolicy
{
    /// <summary>Сколько точек класса рассматривается, прежде чем выбрать ход.</summary>
    private const int ClassCandidateLimit = 16;

    /// <summary>Сколько ходов класса проверяется на легальность, прежде чем сдаться.</summary>
    private const int ClassLegalityAttempts = 4;

    /// <summary>Сколько проб делается, чтобы собрать запасные ходы.</summary>
    private const int SampleAttempts = 12;

    /// <summary>Сколько запасных ходов достаточно для взвешенного выбора.</summary>
    private const int SampleLimit = 4;

    /// <summary>Наибольший размер глазного пространства, которое стоит блокировать.</summary>
    private const int MaxEyeSpace = 4;

    private readonly PlayoutConfig _config;

    /// <summary>Создаёт политику с настройками по умолчанию.</summary>
    public PatternPolicy() : this(PlayoutConfig.Default)
    {
    }

    /// <summary>Создаёт политику с заданными настройками.</summary>
    /// <param name="config">Вероятности эвристик и защита глаз.</param>
    /// <exception cref="ArgumentOutOfRangeException">Вероятность вне диапазона 0…1.</exception>
    public PatternPolicy(PlayoutConfig config)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(config.AtariProbability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(config.AtariProbability, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(config.NeighborProbability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(config.NeighborProbability, 1);

        _config = config;
    }

    /// <summary>Выбирает ход playout'а.</summary>
    /// <param name="state">Партия, в которой идёт playout.</param>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <returns>Легальный ход или пас, если ходить некуда.</returns>
    public Move SelectMove(GameState state, Random random)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove, random);
    }

    /// <summary>Выбирает ход playout'а по позиции и цвету.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Легальный ход или пас, если ходить некуда.</returns>
    public Move SelectMove(Board board, StoneColor color, Random random)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(random);

        if (random.NextDouble() < _config.AtariProbability)
        {
            var atari = Accept(board, color, AtariHeuristics.FindCapturingMove(board, color))
                ?? Accept(board, color, AtariHeuristics.FindRescueMove(board, color));

            if (atari is not null)
            {
                return atari.Value;
            }
        }

        // Агрессивные фигуры включаются настройкой: по умолчанию они выключены, потому что
        // замеры T-019c показали падение силы (5/10 → 3/10 против Kyu20) и скорости втрое.
        if (_config.AggressiveShapes)
        {
            var eyeBlock = FindByClass(board, color, random, point => IsEyeSpace(board, point, color.Opponent()));

            if (eyeBlock is not null)
            {
                return eyeBlock.Value;
            }

            var cut = FindByClass(board, color, random, point => IsCut(board, point, color));

            if (cut is not null)
            {
                return cut.Value;
            }

            var extend = FindByClass(board, color, random, point => HasNeighborStone(board, point, color));

            if (extend is not null)
            {
                return extend.Value;
            }
        }

        if (random.NextDouble() < _config.NeighborProbability)
        {
            var neighbor = FindByClass(board, color, random, point => HasNeighborStone(board, point));

            if (neighbor is not null)
            {
                return neighbor.Value;
            }
        }

        var sampled = Sample(board, color, random, eyesAllowed: _config.EyeSafe is false);

        if (sampled.Count > 0)
        {
            return PickByWeight(board, color, sampled, random);
        }

        // Вынужденный ход: безопасных ходов нет, разрешаем заполнить свой глаз.
        var forced = Sample(board, color, random, eyesAllowed: true);

        return forced.Count > 0 ? PickByWeight(board, color, forced, random) : Move.Pass(color);
    }

    /// <summary>Проверяет, что предложенный ход легален и безопасен.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="move">Предложенный ход или <c>null</c>.</param>
    /// <returns>Тот же ход, если он допустим, иначе <c>null</c>.</returns>
    private Move? Accept(Board board, StoneColor color, Move? move) =>
        move is not null && IsAllowed(board, color, move.Value) ? move : null;

    /// <summary>Ищет ход нужного класса среди точек рядом с камнями.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="random">Источник случайности.</param>
    /// <param name="matches">Условие класса хода.</param>
    /// <returns>Ход класса или <c>null</c>, если таких ходов нет.</returns>
    private Move? FindByClass(Board board, StoneColor color, Random random, Func<Point, bool> matches)
    {
        List<Move> candidates = [];

        // Сначала дешёвый отбор по фигуре, без проверок правил: они дороже всего в playout'е.
        foreach (var point in board.EmptyPoints())
        {
            if (candidates.Count >= ClassCandidateLimit)
            {
                break;
            }

            if (HasNeighborStone(board, point) && matches(point))
            {
                candidates.Add(Move.Play(point, color));
            }
        }

        // Затем несколько попыток подтвердить легальность выбранного по весу хода.
        for (var attempt = 0; attempt < ClassLegalityAttempts && candidates.Count > 0; attempt++)
        {
            var move = PickByWeight(board, color, candidates, random);

            if (IsAllowed(board, color, move))
            {
                return move;
            }

            candidates.Remove(move);
        }

        return null;
    }

    /// <summary>Собирает запасные ходы пробами случайных точек.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="random">Источник случайности.</param>
    /// <param name="eyesAllowed">Разрешить заполнение собственного глаза.</param>
    /// <returns>Найденные безопасные ходы; пустой список, если их нет.</returns>
    private List<Move> Sample(Board board, StoneColor color, Random random, bool eyesAllowed)
    {
        List<Move> found = [];

        for (var attempt = 0; attempt < SampleAttempts && found.Count < SampleLimit; attempt++)
        {
            var index = random.Next(board.Size.Area);
            var point = new Point((byte)(index % board.Size.Value), (byte)(index / board.Size.Value));

            if (!board.IsEmpty(point))
            {
                continue;
            }

            var move = Move.Play(point, color);

            if (IsAllowed(board, color, move, eyesAllowed))
            {
                found.Add(move);
            }
        }

        return found;
    }

    /// <summary>Проверяет, что ход легален, не заполняет свой глаз и не губит свою группу.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="move">Проверяемый ход.</param>
    /// <param name="eyesAllowed">Разрешить заполнение собственного глаза.</param>
    /// <returns><c>true</c>, если ход можно играть в playout'е.</returns>
    private bool IsAllowed(Board board, StoneColor color, Move move, bool eyesAllowed = false)
    {
        if (!board.IsLegal(move).IsSuccess)
        {
            return false;
        }

        if (!eyesAllowed && _config.EyeSafe && EyeDetector.IsEye(board, move.Point, color))
        {
            return false;
        }

        // Ход, оставляющий свою группу в атари, запрещён всегда — и в вынужденном случае тоже:
        // замеры T-019b показали, что такие ходы губят тактику.
        return !AtariHeuristics.LeavesOwnGroupInAtari(board, move);
    }

    /// <summary>Выбирает ход взвешенно-случайно по весам окрестностей.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="moves">Допустимые ходы.</param>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Выбранный ход.</returns>
    private Move PickByWeight(Board board, StoneColor color, List<Move> moves, Random random)
    {
        // Веса фигур по умолчанию не применяются: замеры T-019c показали, что ручная таблица
        // уводит playout в борьбу и снижает силу (5/10 → 2/10 против Kyu20).
        if (!_config.Patterns || moves.Count == 1)
        {
            return moves[random.Next(moves.Count)];
        }

        var total = 0.0;

        foreach (var move in moves)
        {
            total += Weight(board, color, move.Point);
        }

        var target = random.NextDouble() * total;

        foreach (var move in moves)
        {
            target -= Weight(board, color, move.Point);

            if (target <= 0)
            {
                return move;
            }
        }

        return moves[^1];
    }

    /// <summary>Считает вес хода по окрестности 3×3.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="point">Точка хода.</param>
    /// <returns>Вес фигуры, не меньше <see cref="PatternWeights.MinimalWeight"/>.</returns>
    private static double Weight(Board board, StoneColor color, Point point) =>
        Math.Max(PatternWeights.MinimalWeight, PatternWeights.WeightFor(Pattern3x3.FromBoard(board, point, color)));

    /// <summary>Проверяет, что рядом с точкой есть камень.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если хотя бы один сосед занят.</returns>
    private static bool HasNeighborStone(Board board, Point point)
    {
        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.At(neighbor) != StoneColor.Empty)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Проверяет, что рядом с точкой есть свой камень.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns><c>true</c>, если хотя бы один сосед — камень своего цвета.</returns>
    private static bool HasNeighborStone(Board board, Point point, StoneColor color)
    {
        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.At(neighbor) == color)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Проверяет, разрезает ли ход две группы противника.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns><c>true</c>, если точка граничит с двумя разными группами противника.</returns>
    private static bool IsCut(Board board, Point point, StoneColor color)
    {
        var opponent = color.Opponent();
        HashSet<Point> visited = [];
        var groups = 0;

        foreach (var neighbor in point.Neighbors(board.Size))
        {
            if (board.At(neighbor) != opponent || !visited.Add(neighbor))
            {
                continue;
            }

            groups++;

            if (groups >= 2)
            {
                return true;
            }

            visited.UnionWith(GroupTracker.FindGroup(board, neighbor).Stones);
        }

        return false;
    }

    /// <summary>Проверяет, что точка — место, где противник может сделать глаз.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет, чей глаз блокируется.</param>
    /// <returns><c>true</c>, если пустая область точки мала и окружена камнями этого цвета.</returns>
    private static bool IsEyeSpace(Board board, Point point, StoneColor color)
    {
        HashSet<Point> region = [point];
        Queue<Point> frontier = new();
        frontier.Enqueue(point);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            foreach (var neighbor in current.Neighbors(board.Size))
            {
                var stone = board.At(neighbor);

                if (stone == StoneColor.Empty)
                {
                    if (!region.Add(neighbor))
                    {
                        continue;
                    }

                    if (region.Count > MaxEyeSpace)
                    {
                        return false;
                    }

                    frontier.Enqueue(neighbor);
                    continue;
                }

                if (stone != color)
                {
                    return false;
                }
            }
        }

        return true;
    }
}