namespace GoEngine.Core;

/// <summary>Ход в партии.</summary>
/// <remarks>
/// Создаётся только фабриками <see cref="Play"/>, <see cref="Pass"/> и <see cref="Resign"/>:
/// так у хода не бывает пустого цвета. Значение <see cref="None"/> обозначает «нет хода»,
/// у него <see cref="Type"/> и <see cref="Color"/> равны <c>null</c>.
/// </remarks>
public readonly record struct Move
{
    private Move(MoveType type, Point point, StoneColor color)
    {
        Type = type;
        Point = point;
        Color = color;
    }

    /// <summary>Тип хода. <c>null</c> только у <see cref="None"/>.</summary>
    public MoveType Type { get; }

    /// <summary>Точка хода. Значима только для <see cref="MoveType.Play"/>.</summary>
    public Point Point { get; }

    /// <summary>Цвет игрока, делающего ход. <c>null</c> только у <see cref="None"/>.</summary>
    public StoneColor Color { get; }

    /// <summary>Признак отсутствующего хода.</summary>
    public bool IsNone => Type is null;

    /// <summary>Нет хода.</summary>
    public static Move None => default;

    /// <summary>Создаёт ход постановки камня.</summary>
    /// <param name="point">Точка постановки камня.</param>
    /// <param name="color">Цвет игрока: <see cref="StoneColor.Black"/> или <see cref="StoneColor.White"/>.</param>
    /// <returns>Ход типа <see cref="MoveType.Play"/>.</returns>
    /// <exception cref="DomainException">Цвет не является цветом игрока.</exception>
    public static Move Play(Point point, StoneColor color) =>
        new(MoveType.Play, point, RequirePlayerColor(color));

    /// <summary>Создаёт ход-пас.</summary>
    /// <param name="color">Цвет игрока: <see cref="StoneColor.Black"/> или <see cref="StoneColor.White"/>.</param>
    /// <returns>Ход типа <see cref="MoveType.Pass"/> с точкой <c>default(Point)</c>.</returns>
    /// <exception cref="DomainException">Цвет не является цветом игрока.</exception>
    public static Move Pass(StoneColor color) =>
        new(MoveType.Pass, default, RequirePlayerColor(color));

    /// <summary>Создаёт ход-сдачу.</summary>
    /// <param name="color">Цвет игрока: <see cref="StoneColor.Black"/> или <see cref="StoneColor.White"/>.</param>
    /// <returns>Ход типа <see cref="MoveType.Resign"/> с точкой <c>default(Point)</c>.</returns>
    /// <exception cref="DomainException">Цвет не является цветом игрока.</exception>
    public static Move Resign(StoneColor color) =>
        new(MoveType.Resign, default, RequirePlayerColor(color));

    /// <summary>Проверяет, что ход делает игрок, а не пустая точка.</summary>
    /// <param name="color">Проверяемый цвет.</param>
    /// <returns>Тот же цвет, если он допустим.</returns>
    /// <exception cref="DomainException">Передан <see cref="StoneColor.Empty"/>.</exception>
    private static StoneColor RequirePlayerColor(StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(color);

        return color == StoneColor.Empty
            ? throw new DomainException("Ход может сделать только игрок: цвет должен быть Black или White.")
            : color;
    }
}
