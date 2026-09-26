namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Окрестность 3×3 вокруг точки хода: код из девяти клеток по два бита.</summary>
/// <remarks>
/// Клетки кодируются по строкам сверху вниз: 0 — пусто, 1 — свой камень, 2 — чужой, 3 — край доски.
/// Центральная клетка — всегда свой камень (в неё ходим). Код устойчив: одна и та же позиция
/// даёт один и тот же код, поэтому по коду можно искать вес в таблице
/// (<see cref="PatternWeights"/>). Восемь симметрий (повороты и отражения) сводятся
/// к каноническому коду — так таблица весов остаётся компактной.
/// </remarks>
public readonly record struct Pattern3x3(ushort Code)
{
    /// <summary>Сколько клеток в окрестности.</summary>
    public const int Cells = 9;

    /// <summary>Сколько бит занимает одна клетка.</summary>
    private const int BitsPerCell = 2;

    /// <summary>Код пустой клетки.</summary>
    private const int EmptyCell = 0;

    /// <summary>Код своего камня.</summary>
    private const int OwnCell = 1;

    /// <summary>Код чужого камня.</summary>
    private const int OpponentCell = 2;

    /// <summary>Код края доски.</summary>
    private const int EdgeCell = 3;

    /// <summary>Сколько всего симметрий окрестности: четыре поворота и отражение.</summary>
    private const int Symmetries = 8;

    /// <summary>Строит окрестность вокруг точки хода.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="center">Точка хода — центр окрестности.</param>
    /// <param name="me">Цвет, который ходит.</param>
    /// <returns>Код окрестности 3×3.</returns>
    /// <exception cref="DomainException">Точка находится вне доски.</exception>
    public static Pattern3x3 FromBoard(Board board, Point center, StoneColor me)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(me);

        // At проверяет принадлежность доске: вне доски окрестность не определена.
        _ = board.At(center);

        var opponent = me.Opponent();
        var code = 0;

        for (var index = 0; index < Cells; index++)
        {
            var value = CellValue(board, center, (index % 3) - 1, (index / 3) - 1, me, opponent);
            code |= value << (BitsPerCell * index);
        }

        return new Pattern3x3((ushort)code);
    }

    /// <summary>Возвращает канонический код: наименьший из восьми симметрий.</summary>
    /// <returns>Окрестность, не зависящая от поворотов и отражений.</returns>
    public Pattern3x3 Canonical()
    {
        var best = Code;

        for (var symmetry = 1; symmetry < Symmetries; symmetry++)
        {
            var transformed = Transform(symmetry).Code;

            if (transformed < best)
            {
                best = transformed;
            }
        }

        return new Pattern3x3(best);
    }

    /// <summary>Поворачивает и отражает окрестность.</summary>
    /// <param name="symmetry">Номер симметрии от 0 до 7: младшие два бита — повороты, третий — отражение.</param>
    /// <returns>Преобразованная окрестность.</returns>
    public Pattern3x3 Transform(int symmetry)
    {
        var flip = (symmetry & 4) != 0;
        var rotations = symmetry & 3;
        var code = 0;

        for (var index = 0; index < Cells; index++)
        {
            var x = index % 3;
            var y = index / 3;

            if (flip)
            {
                x = 2 - x;
            }

            for (var step = 0; step < rotations; step++)
            {
                (x, y) = (2 - y, x);
            }

            var value = (Code >> (BitsPerCell * ((y * 3) + x))) & 3;
            code |= value << (BitsPerCell * index);
        }

        return new Pattern3x3((ushort)code);
    }

    /// <summary>Определяет значение клетки окрестности.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="center">Центр окрестности.</param>
    /// <param name="dx">Смещение по столбцу.</param>
    /// <param name="dy">Смещение по строке.</param>
    /// <param name="me">Цвет, который ходит.</param>
    /// <param name="opponent">Цвет противника.</param>
    /// <returns>Код клетки: пусто, свой, чужой или край.</returns>
    private static int CellValue(Board board, Point center, int dx, int dy, StoneColor me, StoneColor opponent)
    {
        var x = center.X + dx;
        var y = center.Y + dy;

        if (x < 0 || x >= board.Size.Value || y < 0 || y >= board.Size.Value)
        {
            return EdgeCell;
        }

        var color = board.At(new Point((byte)x, (byte)y));

        if (color == me)
        {
            return OwnCell;
        }

        return color == opponent ? OpponentCell : EmptyCell;
    }
}