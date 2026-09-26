namespace GoEngine.Core;

/// <summary>Хеш расположения камней на доске: Zobrist-хеш позиции.</summary>
/// <param name="Hash">Значение хеша.</param>
/// <remarks>
/// Позиция для сравнения — только расположение камней: очерёдность хода и коми не учитываются
/// (<c>GO_RULES.md</c>, п. 6). Таблица Zobrist строится один раз при первом обращении к типу
/// и заполняется SplitMix64 от постоянного зерна, поэтому хеши одинаковы на любой машине
/// и в любом запуске — в отличие от <see cref="Random"/>, чей алгоритм не гарантирован
/// между версиями .NET.
/// </remarks>
public readonly record struct PositionHash(ulong Hash)
{
    /// <summary>Зерно генератора таблицы Zobrist. Менять нельзя: смена зерна меняет все хеши.</summary>
    private const ulong Seed = 0x0047_6F45_6E67_696EUL;

    /// <summary>Приращение SplitMix64 — дробная часть золотого сечения.</summary>
    private const ulong GoldenGamma = 0x9E37_79B9_7F4A_7C15UL;

    /// <summary>Индекс чёрных камней в таблице Zobrist.</summary>
    private const int BlackIndex = 0;

    /// <summary>Индекс белых камней в таблице Zobrist.</summary>
    private const int WhiteIndex = 1;

    /// <summary>
    /// Таблица случайных значений: два цвета × максимальное число точек доски (19×19).
    /// Индексируется номером цвета и плоским индексом точки конкретной доски.
    /// </summary>
    private static readonly ulong[,] Zobrist = CreateZobristTable();

    /// <summary>Считает хеш позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Хеш расположения камней; пустые точки в хеш не входят.</returns>
    /// <exception cref="DomainException">В позиции встретился цвет, не являющийся камнем.</exception>
    public static PositionHash From(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var hash = 0UL;

        foreach (var point in board.AllPoints())
        {
            var color = board.At(point);

            if (color == StoneColor.Empty)
            {
                continue;
            }

            hash ^= Zobrist[ColorIndex(color), FlatIndex(board.Size, point)];
        }

        return new PositionHash(hash);
    }

    /// <summary>Возвращает номер цвета в таблице Zobrist.</summary>
    /// <param name="color">Цвет камня: <see cref="StoneColor.Black"/> или <see cref="StoneColor.White"/>.</param>
    /// <returns>Номер строки таблицы.</returns>
    /// <exception cref="DomainException">Цвет не является камнем.</exception>
    private static int ColorIndex(StoneColor color)
    {
        if (color == StoneColor.Black)
        {
            return BlackIndex;
        }

        if (color == StoneColor.White)
        {
            return WhiteIndex;
        }

        throw new DomainException($"Цвет {color.Name} не является камнем: хешируются только Black и White.");
    }

    /// <summary>Вычисляет плоский индекс точки на доске указанного размера.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="point">Точка доски.</param>
    /// <returns>Индекс в строке таблицы Zobrist.</returns>
    private static int FlatIndex(BoardSize size, Point point) => (point.Y * size.Value) + point.X;

    /// <summary>Заполняет таблицу Zobrist значениями SplitMix64 от постоянного зерна.</summary>
    /// <returns>Таблица «цвет × точка».</returns>
    private static ulong[,] CreateZobristTable()
    {
        var table = new ulong[2, BoardSize.Size19.Area];
        var state = Seed;

        for (var color = 0; color < 2; color++)
        {
            for (var index = 0; index < table.GetLength(1); index++)
            {
                table[color, index] = NextValue(ref state);
            }
        }

        return table;
    }

    /// <summary>Возвращает следующее значение SplitMix64.</summary>
    /// <param name="state">Состояние генератора.</param>
    /// <returns>Очередное 64-битное значение.</returns>
    private static ulong NextValue(ref ulong state)
    {
        state += GoldenGamma;
        var value = state;
        value = (value ^ (value >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D0_49BB_1331_11EBUL;

        return value ^ (value >> 31);
    }
}
