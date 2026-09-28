namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Задача на жизнь и смерть: позиция, цель и дерево решения.</summary>
/// <remarks>
/// <para>
/// Задача не зависит от интерфейса и от партии: она знает только доску, цель и принимаемые ходы.
/// Камни хранятся схемой, а позиция собирается легальными ходами (<see cref="ProblemSetup"/>),
/// поэтому сборка может отказать — это проверяет <see cref="ProblemChecker"/>.
/// </para>
/// <para>
/// Дерево подчиняется правилу согласованности: в узле решающего принимаются <b>все</b> ходы,
/// выигрывающие на остатке заявленного горизонта (<see cref="CheckDepth"/>), и только они.
/// Иначе правильный ход игрока получил бы вердикт «неверно» — ложный отказ недопустим.
/// Дерево генерируется перебором, руками не пишется; подсказка — самый быстрый выигрывающий ход,
/// при равенстве — первый по каноническому обходу доски (как <see cref="Board.AllPoints"/>).
/// </para>
/// </remarks>
public sealed class Problem
{
    /// <summary>Порог слабой задачи: принимаемых первых ходов больше этого числа.</summary>
    /// <remarks>
    /// Задача с большим набором принимаемых первых ходов слабая: она не заставляет искать
    /// единственный ход. Порог — характеристика качества контента, а не правило движка.
    /// </remarks>
    public const int WeakAcceptedFirstMoveCount = 3;

    /// <summary>Радиус объявленного окна поиска по умолчанию: шахматное расстояние от камней целевой группы.</summary>
    /// <remarks>
    /// Окно — ограничение нашего перебора, а не правило игры: за его пределами ничего не заявляется
    /// («проверка не проводилась»). Ход, который по правилам сразу выполняет цель, принимается
    /// независимо от окна — это второе основание accept в <see cref="ProblemSession"/>.
    /// </remarks>
    public const int DefaultWindowRadius = 3;

    private readonly IReadOnlyList<Point> _targetPoints;
    private readonly IReadOnlyList<Move> _acceptedFirstMoves;

    /// <summary>Создаёт задачу.</summary>
    /// <param name="id">Идентификатор задачи.</param>
    /// <param name="name">Название для игрока.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="komi">Коми позиции.</param>
    /// <param name="solverColor">Цвет, который решает задачу.</param>
    /// <param name="goal">Цель задачи.</param>
    /// <param name="stones">Начальная расстановка камней.</param>
    /// <param name="target">Точка целевой группы: камень цвета цели, вокруг которого строится задача.</param>
    /// <param name="rank">Номинальная сложность в кю: больше — проще.</param>
    /// <param name="description">Описание для игрока.</param>
    /// <param name="solution">Корень дерева решения.</param>
    /// <param name="checkDepth">Заявленный горизонт проверки цели в полуходах.</param>
    /// <param name="windowRadius">Объявленный радиус окна поиска: шахматное расстояние от камней целевой группы.</param>
    /// <exception cref="DomainException">Нарушен инвариант задачи.</exception>
    public Problem(
        string id,
        string name,
        BoardSize size,
        Komi komi,
        StoneColor solverColor,
        ProblemGoal goal,
        IReadOnlyList<ProblemStone> stones,
        Point target,
        int rank,
        string description,
        ProblemNode solution,
        int checkDepth = 0,
        int windowRadius = DefaultWindowRadius)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(solverColor);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(stones);
        ArgumentNullException.ThrowIfNull(solution);

        if (stones.Count == 0)
        {
            throw new DomainException($"Задача {id}: расстановка пуста.");
        }

        if (solution.IsLeaf)
        {
            throw new DomainException($"Задача {id}: дерево решения пусто.");
        }

        if (!target.IsOnBoard(size))
        {
            throw new DomainException($"Задача {id}: целевая точка {target} вне доски {size}.");
        }

        // Целевая группа у соперника, когда её убивают (Capture и Dead), и своя, когда её спасают (Live).
        var targetColor = goal == ProblemGoal.Live ? solverColor : solverColor.Opponent();

        if (!stones.Any(stone => stone.Point == target && stone.Color == targetColor))
        {
            throw new DomainException($"Задача {id}: в целевой точке {target} нет камня цвета {targetColor.Name}.");
        }

        Id = id;
        Name = name;
        Size = size;
        Komi = komi;
        SolverColor = solverColor;
        Goal = goal;
        Stones = stones;
        Target = target;
        Rank = rank;
        Description = description;
        Solution = solution;
        CheckDepth = checkDepth;
        WindowRadius = windowRadius > 0 ? windowRadius : DefaultWindowRadius;
        _targetPoints = GroupOf(stones, target, targetColor);

        List<Move> accepted = [];

        foreach (var move in solution.Moves)
        {
            accepted.Add(move.Move);
        }

        _acceptedFirstMoves = accepted.AsReadOnly();
    }

    /// <summary>Идентификатор задачи.</summary>
    public string Id { get; }

    /// <summary>Название задачи для игрока.</summary>
    public string Name { get; }

    /// <summary>Размер доски.</summary>
    public BoardSize Size { get; }

    /// <summary>Коми позиции.</summary>
    public Komi Komi { get; }

    /// <summary>Цвет, который решает задачу.</summary>
    public StoneColor SolverColor { get; }

    /// <summary>Цель задачи.</summary>
    public ProblemGoal Goal { get; }

    /// <summary>Начальная расстановка камней.</summary>
    public IReadOnlyList<ProblemStone> Stones { get; }

    /// <summary>Точка целевой группы.</summary>
    public Point Target { get; }

    /// <summary>Цвет целевой группы: у решающего или у соперника, в зависимости от цели.</summary>
    public StoneColor TargetColor => Goal == ProblemGoal.Live ? SolverColor : SolverColor.Opponent();

    /// <summary>Камни целевой группы в начальной позиции.</summary>
    public IReadOnlyList<Point> TargetPoints => _targetPoints;

    /// <summary>Номинальная сложность в кю: больше — проще.</summary>
    public int Rank { get; }

    /// <summary>Описание задачи для игрока.</summary>
    public string Description { get; }

    /// <summary>Корень дерева решения.</summary>
    public ProblemNode Solution { get; }

    /// <summary>Заявленный горизонт проверки задачи в полуходах: 0 — горизонт не задан.</summary>
    /// <remarks>
    /// <para>
    /// Для <see cref="ProblemGoal.Dead"/> это граница, за которую атакующий форсирует захват:
    /// «мертва» доказывается перебором сопротивления на конечную глубину, и без числа эту границу
    /// нельзя ни проверить, ни честно описать игроку.
    /// </para>
    /// <para>
    /// Для <see cref="ProblemGoal.Capture"/> и <see cref="ProblemGoal.Live"/> это горизонт, на котором
    /// подтверждён лист и на котором принимаемый набор равен множеству выигрывающих ходов.
    /// Ноль не ставится: он читается как «проверка не делалась».
    /// </para>
    /// </remarks>
    public int CheckDepth { get; }

    /// <summary>Объявленный радиус окна поиска (SGF <c>GW</c>): шахматное расстояние от камней целевой группы.</summary>
    /// <remarks>
    /// Принимаемый набор каждого узла — все выигрывающие ходы в этом окне на остатке горизонта.
    /// Окно объявляется письменно, чтобы проверка была воспроизводима, а «за окном» честно читалось
    /// как «проверка не проводилась», а не как «ход проигрывает».
    /// </remarks>
    public int WindowRadius { get; }

    /// <summary>Первый принимаемый ход решающего: подсказка.</summary>
    /// <returns>Ход или <c>null</c>, если дерево пусто.</returns>
    /// <remarks>Подсказка — самый быстрый выигрывающий ход; при равенстве — канонический по обходу доски.</remarks>
    public Move? Hint => Solution.Moves.Count > 0 ? Solution.Moves[0].Move : null;

    /// <summary>Принимаемые первые ходы решающего: все выигрывающие ходы за заявленный горизонт.</summary>
    /// <remarks>
    /// Характеристика качества задачи, а не служебная деталь: интерфейсу она нужна, чтобы честно
    /// показать, сколько решений у задачи. Первый ход списка — <see cref="Hint"/>.
    /// </remarks>
    public IReadOnlyList<Move> AcceptedFirstMoves => _acceptedFirstMoves;

    /// <summary>Число принимаемых первых ходов решающего.</summary>
    public int AcceptedFirstMoveCount => _acceptedFirstMoves.Count;

    /// <summary>Задача слабая: принимаемых первых ходов больше <see cref="WeakAcceptedFirstMoveCount"/>.</summary>
    /// <remarks>Слабую задачу честно помечать в интерфейсе, а не выдавать за задачу с единственным решением.</remarks>
    public bool IsWeak => AcceptedFirstMoveCount > WeakAcceptedFirstMoveCount;

    /// <summary>Собирает связную группу камней, начиная с точки.</summary>
    private static IReadOnlyList<Point> GroupOf(IReadOnlyList<ProblemStone> stones, Point start, StoneColor color)
    {
        List<Point> group = [start];
        Queue<Point> queue = new();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var stone in stones)
            {
                if (stone.Color != color || group.Contains(stone.Point))
                {
                    continue;
                }

                if (IsAdjacent(current, stone.Point))
                {
                    group.Add(stone.Point);
                    queue.Enqueue(stone.Point);
                }
            }
        }

        return group.AsReadOnly();
    }

    private static bool IsAdjacent(Point left, Point right) =>
        (left.X == right.X && Math.Abs(left.Y - right.Y) == 1)
        || (left.Y == right.Y && Math.Abs(left.X - right.X) == 1);
}
