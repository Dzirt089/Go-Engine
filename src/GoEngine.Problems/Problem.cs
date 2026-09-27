namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Задача на жизнь и смерть: позиция, цель и дерево решения.</summary>
/// <remarks>
/// Задача не зависит от интерфейса и от партии: она знает только доску, цель и принимаемые ходы.
/// Камни хранятся схемой, а позиция собирается легальными ходами (<see cref="ProblemSetup"/>),
/// поэтому сборка может отказать — это проверяет <see cref="ProblemChecker"/>.
/// </remarks>
public sealed class Problem
{
    private readonly IReadOnlyList<Point> _targetPoints;

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
        ProblemNode solution)
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

        var targetColor = goal == ProblemGoal.Capture ? solverColor.Opponent() : solverColor;

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
        _targetPoints = GroupOf(stones, target, targetColor);
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
    public StoneColor TargetColor => Goal == ProblemGoal.Capture ? SolverColor.Opponent() : SolverColor;

    /// <summary>Камни целевой группы в начальной позиции.</summary>
    public IReadOnlyList<Point> TargetPoints => _targetPoints;

    /// <summary>Номинальная сложность в кю: больше — проще.</summary>
    public int Rank { get; }

    /// <summary>Описание задачи для игрока.</summary>
    public string Description { get; }

    /// <summary>Корень дерева решения.</summary>
    public ProblemNode Solution { get; }

    /// <summary>Первый принимаемый ход решающего: подсказка.</summary>
    /// <returns>Ход или <c>null</c>, если дерево пусто.</returns>
    public Move? Hint => Solution.Moves.Count > 0 ? Solution.Moves[0].Move : null;

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
