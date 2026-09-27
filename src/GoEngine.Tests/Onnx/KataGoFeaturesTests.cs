using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты кодирования позиции в признаки KataGo — T-032.</summary>
public sealed class KataGoFeaturesTests
{
    /// <summary>Сторона доски, на которой работает модель.</summary>
    private const int Side = 19;

    /// <summary>Число точек на плоскости.</summary>
    private const int Plane = Side * Side;

    [Fact]
    public void Кодировщик_Пустая_Доска_Заполнена_Только_Плоскость_Доски()
    {
        var spatial = KataGoFeatures.EncodeSpatial(new Board(BoardSize.Size19), StoneColor.Black, []);

        Assert.Equal(Plane, spatial.Take(Plane).Sum());
    }

    [Fact]
    public void Кодировщик_Число_Значений_Соответствует_Формату()
    {
        var spatial = KataGoFeatures.EncodeSpatial(new Board(BoardSize.Size19), StoneColor.Black, []);

        Assert.Equal(KataGoFeatures.SpatialPlanes * Plane, spatial.Length);
    }

    [Fact]
    public void Кодировщик_Пустая_Доска_Даёт_Девятнадцать_Глобальных_Признаков()
    {
        var global = KataGoFeatures.EncodeGlobal(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        Assert.Equal(KataGoFeatures.GlobalFeatures, global.Length);
    }

    [Fact]
    public void Кодировщик_Коми_Делится_На_Двадцать()
    {
        // Раскладка модели (kaya-go/katago-onnx, features.py): признак 5 — selfKomi / 20.
        var global = KataGoFeatures.EncodeGlobal(new Board(BoardSize.Size19), new Komi(10), StoneColor.White, []);

        Assert.Equal(0.5f, global[5]);
    }

    [Fact]
    public void Кодировщик_Пас_Завершает_Партию_Признак_Четырнадцать()
    {
        List<Move> moves = [Move.Pass(StoneColor.Black)];
        var global = KataGoFeatures.EncodeGlobal(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.White, moves);

        Assert.Equal(1f, global[14]);
    }

    [Fact]
    public void Кодировщик_Волна_Чётности_Коми_Признак_Восемнадцать()
    {
        // Коми 7,5 за белых на доске 19×19: до ближайшей границы, где ничьи возможны, пол-очка —
        // это вершина волны.
        var global = KataGoFeatures.EncodeGlobal(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.White, []);

        Assert.Equal(0.5f, global[18]);
    }

    [Fact]
    public void Кодировщик_Камень_Игрока_И_Его_Дамэ()
    {
        var board = new Board(BoardSize.Size19).ApplyMove(Move.Play(new Point(9, 9), StoneColor.Black));

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, []);

        Assert.Equal(1f, spatial[(1 * Plane) + (9 * Side) + 9]);
    }

    [Fact]
    public void Кодировщик_Камень_С_Тремя_Дамэ()
    {
        // Соседний чужой камень отнимает одно дамэ: у чёрного камня их становится три.
        var board = new Board(BoardSize.Size19)
            .ApplyMove(Move.Play(new Point(9, 9), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(10, 9), StoneColor.White));

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, []);

        Assert.Equal(1f, spatial[(5 * Plane) + (9 * Side) + 9]);
    }

    [Fact]
    public void Кодировщик_Угловой_Камень_С_Двумя_Дамэ()
    {
        var board = new Board(BoardSize.Size19).ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, []);

        // Плоскость 4 — камни ровно с двумя дамэ: угловой камень.
        Assert.Equal(1f, spatial[(4 * Plane) + 0]);
    }

    [Fact]
    public void Кодировщик_Камень_Противника_В_Своей_Плоскости()
    {
        var board = new Board(BoardSize.Size19).ApplyMove(Move.Play(new Point(3, 3), StoneColor.White));

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, []);

        Assert.Equal(1f, spatial[(2 * Plane) + (3 * Side) + 3]);
    }

    [Fact]
    public void Кодировщик_История_Отмечает_Последний_Ход()
    {
        List<Move> moves =
        [
            Move.Play(new Point(3, 3), StoneColor.Black),
            Move.Play(new Point(15, 15), StoneColor.White)
        ];
        var board = BoardOf(moves);

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, moves);

        Assert.Equal(1f, spatial[(9 * Plane) + (15 * Side) + 15]);
    }

    [Fact]
    public void Кодировщик_Отмечает_Запрет_Суперко()
    {
        List<Move> moves =
        [
            Move.Play(new Point(2, 2), StoneColor.Black),
            Move.Play(new Point(1, 2), StoneColor.White),
            Move.Play(new Point(3, 2), StoneColor.White),
            Move.Play(new Point(2, 3), StoneColor.White),
            Move.Play(new Point(1, 1), StoneColor.Black),
            Move.Play(new Point(3, 1), StoneColor.Black),
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(2, 1), StoneColor.White)
        ];
        var board = BoardOf(moves, withHistory: true);

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, moves);

        Assert.Equal(1f, spatial[(6 * Plane) + (2 * Side) + 2]);
    }

    [Fact]
    public void Кодировщик_Территория_В_Углу()
    {
        var board = new Board(BoardSize.Size19)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black));

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, []);

        Assert.Equal(1f, spatial[(18 * Plane) + 0]);
    }

    [Fact]
    public void Кодировщик_Коми_С_Точки_Зрения_Чёрных_Отрицательно()
    {
        var global = KataGoFeatures.EncodeGlobal(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        Assert.True(global[5] < 0);
    }

    [Fact]
    public void Кодировщик_Правила_Отражены_В_Глобальных_Признаках()
    {
        var global = KataGoFeatures.EncodeGlobal(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        // Позиционное суперко — 1 и 0,5; самоубийство запрещено — 0; подсчёт по площади — 0.
        Assert.Equal([1f, 0.5f, 0f, 0f], new[] { global[6], global[7], global[8], global[9] });
    }

    [Fact]
    public void Кодировщик_Пас_Отмечен_В_Глобальном_Признаке()
    {
        List<Move> moves = [Move.Pass(StoneColor.Black)];
        var board = new Board(BoardSize.Size19);

        var global = KataGoFeatures.EncodeGlobal(board, Komi.For19x19, StoneColor.White, moves);

        Assert.Equal(1f, global[0]);
    }

    [Fact]
    public void Кодировщик_Считает_Признаки_Для_Доски_9x9()
    {
        // Размер доски модель принимает динамически, поэтому признаки считаются для любого размера.
        var spatial = KataGoFeatures.EncodeSpatial(new Board(BoardSize.Size9), StoneColor.Black, []);

        Assert.Equal(KataGoFeatures.SpatialPlanes * 9 * 9, spatial.Length);
    }

    /// <summary>Строит доску по ходам партии.</summary>
    /// <param name="moves">Ходы.</param>
    /// <param name="withHistory">Вести ли историю позиций (нужна для суперко).</param>
    /// <returns>Доска 19×19.</returns>
    private static Board BoardOf(IReadOnlyList<Move> moves, bool withHistory = false)
    {
        var board = new Board(BoardSize.Size19);

        if (withHistory)
        {
            board = board.WithHistory(new PositionHistory());
        }

        foreach (var move in moves)
        {
            board = board.ApplyMove(move);
        }

        return board;
    }
}