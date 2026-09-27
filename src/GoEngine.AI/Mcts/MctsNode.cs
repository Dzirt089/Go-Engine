namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Узел дерева MCTS: позиция, ход в неё и статистика посещений.</summary>
/// <remarks>
/// Узел хранит позицию после своего хода и ещё не разобранные ходы из неё, поэтому дерево
/// не пересчитывает правила при каждом спуске. <see cref="Wins"/> считается с точки зрения
/// игрока, сделавшего <see cref="Move"/>: это позволяет выбирать лучший ход сравнением
/// оценок детей одного родителя.
/// </remarks>
public sealed class MctsNode
{
    private readonly List<Move> _untriedMoves;

    /// <summary>Создаёт узел.</summary>
    /// <param name="move">Ход, которым пришли в узел; у корня — <see cref="Move.None"/>.</param>
    /// <param name="parent">Родительский узел или <c>null</c> у корня.</param>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="toMove">Цвет, который ходит из этой позиции.</param>
    /// <param name="untriedMoves">Ещё не разобранные ходы из этой позиции.</param>
    /// <exception cref="ArgumentNullException">Позиция, цвет или список ходов не заданы.</exception>
    public MctsNode(Move move, MctsNode? parent, Board board, StoneColor toMove, IEnumerable<Move> untriedMoves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(untriedMoves);

        Move = move;
        Parent = parent;
        Board = board;
        ToMove = toMove;
        _untriedMoves = [.. untriedMoves];
    }

    /// <summary>Ход, которым пришли в узел.</summary>
    public Move Move { get; }

    /// <summary>Родительский узел или <c>null</c> у корня.</summary>
    public MctsNode? Parent { get; }

    /// <summary>Позиция после <see cref="Move"/>.</summary>
    public Board Board { get; }

    /// <summary>Цвет, который ходит из этой позиции.</summary>
    public StoneColor ToMove { get; }

    /// <summary>Дети узла — позиции после ходов из этой.</summary>
    public List<MctsNode> Children { get; } = [];

    /// <summary>Сколько раз узел участвовал в playout'ах.</summary>
    public int Visits { get; private set; }

    /// <summary>Сколько playout'ов из узла выиграл игрок, сделавший <see cref="Move"/>.</summary>
    public double Wins { get; private set; }

    /// <summary>Вероятность хода по мнению сети: приоритет исследования.</summary>
    /// <remarks>
    /// Ноль у деревьев без оценки сети: тогда выбор идёт по чистому UCB1 (или UCB1 с RAVE).
    /// </remarks>
    public double Prior { get; internal set; }

    /// <summary>Сколько симуляций видели ход этого узла где угодно (статистика RAVE).</summary>
    public int RaveVisits { get; private set; }

    /// <summary>Сколько RAVE-симуляций выиграл игрок, сделавший <see cref="Move"/>.</summary>
    public double RaveWins { get; private set; }

    /// <summary>Ходы, которые из этой позиции ещё не пробовали.</summary>
    public IReadOnlyList<Move> UntriedMoves => _untriedMoves.AsReadOnly();

    /// <summary>Проверяет, что все ходы из позиции уже попробованы.</summary>
    public bool IsFullyExpanded => _untriedMoves.Count == 0;

    /// <summary>Считает оценку UCB1 — баланс выигрышей и исследования.</summary>
    /// <param name="c">Коэффициент исследования: больше — шире поиск.</param>
    /// <returns>Оценка узла; у непосещённого узла — бесконечность, поэтому он выбирается первым.</returns>
    public double Ucb1(double c)
    {
        // Непосещённый узел обязан быть выбран раньше посещённых, иначе часть ходов никогда не проверится.
        if (Visits == 0 || Parent is null || Parent.Visits == 0)
        {
            return double.PositiveInfinity;
        }

        return (Wins / Visits) + (c * Math.Sqrt(Math.Log(Parent.Visits) / Visits));
    }

    /// <summary>Считает вес доверия к статистике RAVE.</summary>
    /// <param name="raveK">Постоянная RAVE.</param>
    /// <returns>β от 1 при малом числе посещений до 0 при большом.</returns>
    /// <remarks>Формула β = √(k / (3n + k)): пока посещений мало, верим RAVE, дальше — обычной статистике.</remarks>
    public double RaveBeta(double raveK) => Math.Sqrt(raveK / ((3.0 * Visits) + raveK));

    /// <summary>Считает оценку UCB1 с поправкой RAVE.</summary>
    /// <param name="c">Коэффициент исследования.</param>
    /// <param name="raveK">Постоянная RAVE; 0 отключает поправку.</param>
    /// <returns>Оценка узла; у непосещённого узла — бесконечность.</returns>
    public double Ucb1Rave(double c, double raveK)
    {
        if (raveK <= 0 || RaveVisits == 0)
        {
            return Ucb1(c);
        }

        if (Visits == 0 || Parent is null || Parent.Visits == 0)
        {
            return double.PositiveInfinity;
        }

        var beta = RaveBeta(raveK);
        var own = Wins / Visits;
        var rave = RaveWins / RaveVisits;
        var mixed = ((1 - beta) * own) + (beta * rave);

        return mixed + (c * Math.Sqrt(Math.Log(Parent.Visits) / Visits));
    }

    /// <summary>Учитывает результат симуляции, в которой ход узла игрался где угодно.</summary>
    /// <param name="winner">Победитель партии.</param>
    internal void RegisterRaveResult(StoneColor winner)
    {
        RaveVisits++;

        if (Move.Color == winner)
        {
            RaveWins += 1;
        }
    }

    /// <summary>Забирает очередной неразобранный ход.</summary>
    /// <returns>Последний из неразобранных ходов.</returns>
    /// <remarks>Ход берётся с конца списка: удаление из конца не сдвигает остальные элементы.</remarks>
    internal Move TakeUntriedMove()
    {
        var last = _untriedMoves.Count - 1;
        var move = _untriedMoves[last];
        _untriedMoves.RemoveAt(last);

        return move;
    }

    /// <summary>Забирает указанный неразобранный ход.</summary>
    /// <param name="move">Ход из числа неразобранных.</param>
    /// <returns>Тот же ход.</returns>
    /// <exception cref="AiException">Хода нет среди неразобранных.</exception>
    /// <remarks>
    /// Нужен поиску с сетью: порядок разбора задаёт политика сети, а не порядок списка ходов.
    /// </remarks>
    internal Move TakeUntriedMove(Move move)
    {
        if (!_untriedMoves.Remove(move))
        {
            throw new AiException($"Ход {move} не найден среди неразобранных.");
        }

        return move;
    }

    /// <summary>Учитывает оценку позиции с точки зрения того, кто в неё ходил.</summary>
    /// <param name="winForItsMover">Шансы на победу игрока, сделавшего <see cref="Move"/>, от 0 до 1.</param>
    /// <remarks>
    /// Оценка сети — вероятность, а не «победил или нет»: узел получает её целиком.
    /// Из партии playout'а сюда приходит 1 или 0, поэтому обе оценки складываются одинаково.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Вероятность вне диапазона 0…1.</exception>
    internal void RegisterResult(double winForItsMover)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(winForItsMover);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(winForItsMover, 1.0);

        Visits++;
        Wins += winForItsMover;
    }

    /// <summary>Учитывает результат партии.</summary>
    /// <param name="winner">Победитель партии.</param>
    internal void RegisterResult(StoneColor winner) => RegisterResult(Move.Color == winner ? 1.0 : 0.0);

    /// <summary>Считает оценку PUCT — UCB1 с приоритетом хода из политики сети.</summary>
    /// <param name="c">Коэффициент исследования.</param>
    /// <returns>Оценка узла.</returns>
    /// <remarks>
    /// Формула <c>Q + c · P · √N / (1 + n)</c>. В отличие от <see cref="Ucb1"/>, непосещённый
    /// узел не получает бесконечность: иначе поиск обязан был бы перебрать все ходы подряд,
    /// и политика сети ни на что не влияла бы. Пока посещений нет, <c>Q</c> равно нулю,
    /// и узлы различаются только вероятностью хода — то есть мнением сети. Дальше это мнение
    /// проверяется оценкой позиций, и решает уже средняя оценка.
    /// </remarks>
    public double Puct(double c)
    {
        var parentVisits = Parent?.Visits ?? 0;

        if (parentVisits == 0)
        {
            return double.PositiveInfinity;
        }

        var average = Visits == 0 ? 0.0 : Wins / Visits;
        var exploration = c * Prior * Math.Sqrt(parentVisits) / (1 + Visits);

        return average + exploration;
    }
}
