using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Правило пленных: движок не играет ходы, которые заведомо только кормят соперника.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-03: противник (AI) ставил камни к своим доказанно мёртвым группам
/// и в плотную территорию чёрных, увеличивая счёт пленных. Здесь проверяются обе границы правила:
/// убыточный ход отбрасывается, а ход, который что-то даёт, — остаётся.
/// </remarks>
public sealed class PrisonerPolicyTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Правило_Отбрасывает_Ход_К_Своей_Мёртвой_Группе()
    {
        // Регрессия жалобы 2026-10-03 (кормление мёртвых групп): белые спасают свою группу
        // в атари, но перебор доказал, что её не спасти, — камень уходит в пленные чёрных.
        var board = DoomedCornerBoard();
        var move = Move.Play(new Point(0, 1), StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, [move]);

        Assert.Empty(allowed.Moves);
    }

    [Fact]
    public void Правило_Оставляет_Ход_В_Другом_Месте()
    {
        // Тот же ход, но вдали от мёртвой группы и от чужой территории: правило молчит.
        var board = DoomedCornerBoard();
        var move = Move.Play(new Point(6, 6), StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, [move]);

        Assert.Single(allowed.Moves);
    }

    [Fact]
    public void Правило_Отбрасывает_Ход_В_Чужой_Территории()
    {
        // Плотная территория соперника: точка окружена только чёрными камнями, своих живых групп
        // рядом нет — камень там умирает сразу и становится пленным.
        // Точка (2,3) далека от мёртвой пары: её отбрасывает именно правило плотной территории.
        var board = WallBoard();
        var move = Move.Play(new Point(2, 3), StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, [move]);

        Assert.Empty(allowed.Moves);
    }

    [Fact]
    public void Правило_Молчит_В_Середине_Партии()
    {
        // Регрессия ложного срабатывания: в открытой позиции нет ни мёртвых групп, ни плотной
        // чужой территории, поэтому правило не сужает список ходов.
        var board = MiddleGameBoard();
        var moves = LegalMoves.For(board, StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, moves);

        Assert.True(allowed.EverythingAllowed && allowed.Moves.Count == moves.Count);
    }

    [Fact]
    public void Правило_Разрешает_Снятие_В_Чужой_Территории()
    {
        // Ход внутри чужой территории, который снимает группу, — это не кормление: такие ходы
        // правило обязано пропускать, иначе движок отказывался бы от убийства групп.
        var board = CaptureInBoxBoard();
        var move = Move.Play(new Point(2, 1), StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, [move]);

        Assert.Single(allowed.Moves);
    }

    [Fact]
    public void Правило_Разрешает_Угрозу_Чужой_Группе()
    {
        // Ход в чужой территории, прижимающий чужую группу до одного дамэ, — угроза, а не заполнение.
        var board = PressBoard();
        var move = Move.Play(new Point(4, 4), StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, [move]);

        Assert.Single(allowed.Moves);
    }

    [Fact]
    public void Правило_Отбрасывает_Все_Ходы_В_Безнадёжной_Позиции()
    {
        // Белым ходить некуда: у них только доказанно мёртвая группа, а вся остальная доска —
        // плотная территория чёрных. Пустой список означает пас.
        var board = DoomedOnlyBoard();
        var moves = LegalMoves.For(board, StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, moves);

        Assert.True(!allowed.EverythingAllowed && allowed.Moves.Count == 0);
    }

    [Fact]
    public void Эвристика_Пасует_Когда_Все_Ходы_Убыточны()
    {
        // Регрессия жалобы 2026-10-03: раньше движок играл такой ход и увеличивал пленные.
        var selector = new HeuristicMoveSelector(new Random(20261003), randomnessPercent: 0);

        var move = selector.SelectMove(DoomedOnlyBoard(), StoneColor.White);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Случайный_Уровень_Пасует_Когда_Все_Ходы_Убыточны()
    {
        var selector = new RandomMoveSelector(new Random(20261003));

        var move = selector.SelectMove(DoomedOnlyBoard(), StoneColor.White);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Случайный_Уровень_Не_Кормит_Свою_Мёртвую_Группу()
    {
        // В безнадёжной позиции ходов не остаётся вовсе; проверим и позицию, где полезные ходы есть:
        // ни один выбранный ход не должен примыкать к своей мёртвой группе.
        var board = DoomedCornerBoard();
        var selector = new RandomMoveSelector(new Random(20261003));

        var move = selector.SelectMove(board, StoneColor.White);

        Assert.DoesNotContain(move.Point, DoomedNeighbors(board));
    }

    [Fact]
    public void Эвристика_Не_Кормит_Свою_Мёртвую_Группу()
    {
        // Эвристика «спасти свою группу в атари» раньше выбирала именно ход к мёртвой группе:
        // теперь этот ход отброшен правилом, и движок играет в другом месте.
        var board = DoomedCornerBoard();
        var selector = new HeuristicMoveSelector(new Random(20261003), randomnessPercent: 0);

        var move = selector.SelectMove(board, StoneColor.White);

        Assert.DoesNotContain(move.Point, DoomedNeighbors(board));
    }

    [Fact]
    public void Перебор_Считает_Пленных_Меньше_После_Правила()
    {
        // Арифметика жалобы: кормящий ход добавлял камень в пленные, пас — нет.
        var board = DoomedOnlyBoard();
        var withoutRule = Endgame.Finalize(board, Endgame.ProposeDead(board), Komi.For9x9, 0, 0);
        var feeding = board.WithoutHistory().ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));
        var fed = Endgame.Finalize(
            feeding,
            Endgame.ProposeDead(feeding),
            Komi.For9x9,
            0,
            0);

        Assert.True(fed.BlackPrisoners > withoutRule.BlackPrisoners);
    }

    /// <summary>Точки, примыкающие к доказанно мёртвым камням белых.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Соседи мёртвых камней в порядке обхода доски.</returns>
    private static List<Point> DoomedNeighbors(Board board)
    {
        HashSet<Point> dead = [.. Endgame.ProposeDead(board).Where(point => board.At(point) == StoneColor.White)];
        List<Point> neighbors = [];

        foreach (var point in board.AllPoints())
        {
            if (board.At(point) == StoneColor.Empty && point.Neighbors(board.Size).Any(dead.Contains))
            {
                neighbors.Add(point);
            }
        }

        return neighbors;
    }

    /// <summary>Строит позицию: белая группа в углу в атари, чёрные её не спасают.</summary>
    /// <returns>Доска 9×9, где перебор доказывает смерть белых (0,0) и (1,0).</returns>
    /// <remarks>
    /// Классическая форма из тестов конца партии: чёрные (2,0), (2,1), (0,2), (1,2) закрывают угол.
    /// Единственная «защита» белых — продление в (0,1) или (1,1): перебор доказал, что и после неё
    /// группа снимается, поэтому такие ходы только добавляют пленных.
    /// </remarks>
    private static Board DoomedCornerBoard()
    {
        var board = new Board(Size);

        foreach (var (point, color) in new (Point Point, StoneColor Color)[]
        {
            (new Point(0, 0), StoneColor.White),
            (new Point(1, 0), StoneColor.White),
            (new Point(2, 0), StoneColor.Black),
            (new Point(2, 1), StoneColor.Black),
            (new Point(0, 2), StoneColor.Black),
            (new Point(1, 2), StoneColor.Black),
            (new Point(8, 8), StoneColor.White),
            (new Point(8, 7), StoneColor.White)
        })
        {
            board = board.ApplyMove(Move.Play(point, color));
        }

        return board;
    }

    /// <summary>Строит позицию: только мёртвая группа белых, остальная доска — владение чёрных.</summary>
    /// <returns>Доска 9×9, где белым некуда идти, кроме как в пленные.</returns>
    /// <remarks>
    /// Белых камней, кроме доказанно мёртвой пары в углу, нет: после её снятия вся пустая доска
    /// граничит только с чёрными, то есть плотная территория чёрных. Любой ход белых — потеря камня.
    /// </remarks>
    private static Board DoomedOnlyBoard() => DeadOnly(DoomedCornerBoard());

    /// <summary>Убирает с доски живые белые камни, оставляя только мёртвую группу.</summary>
    /// <param name="board">Позиция с живыми и мёртвыми белыми.</param>
    /// <returns>Доска без живых белых камней.</returns>
    private static Board DeadOnly(Board board)
    {
        List<Point> live =
        [
            .. board.OccupiedPoints(StoneColor.White).Where(point => point.X > 4)
        ];

        return Endgame.ClearedBoard(board, live);
    }

    /// <summary>Строит позицию: плотная стена чёрных, за которой ход белых убыточен.</summary>
    /// <returns>Доска 9×9 с доказанно мёртвой парой белых и чёрной стеной по четвёртой линии.</returns>
    /// <remarks>
    /// Мёртвая пара нужна как условие включения правила: без доказанных мёртвых групп правило
    /// молчит (иначе в открытой позиции убыточным выглядел бы любой ход).
    /// </remarks>
    private static Board WallBoard()
    {
        var board = DoomedCornerBoard();

        // Стена чёрных по четвёртой линии отгораживает левый верхний угол: его пустые точки
        // граничат только с чёрными и краем, значит принадлежат чёрным.
        foreach (var index in Enumerable.Range(0, BoardSize.Size9.Value))
        {
            board = Place(board, new Point(4, (byte)index), StoneColor.Black);
            board = Place(board, new Point((byte)index, 4), StoneColor.Black);
        }

        return board;
    }

    /// <summary>Строит позицию: чёрная коробка, внутри которой ход белых снимает чёрную группу.</summary>
    /// <returns>Доска 9×9 с чёрной группой в атари внутри своей территории.</returns>
    /// <remarks>
    /// Коробка окружена белыми камнями, поэтому у чёрных остаётся ровно одно дамэ — внутренняя
    /// точка. Ход белых в неё снимает всю чёрную группу: это и есть «ход, который что-то даёт».
    /// </remarks>
    private static Board CaptureInBoxBoard()
    {
        var board = new Board(Size);

        // Чёрная коробка 3×3 (периметр) и белое окружение вокруг неё.
        board = Box(board, new Point(1, 1), 3, StoneColor.Black);
        board = Surround(board, new Point(1, 1), 3, StoneColor.White);

        return board;
    }

    /// <summary>Строит позицию: у чёрной группы внутри коробки ровно два дамэ.</summary>
    /// <returns>Доска 9×9, где ход белых в (3,2) прижимает чёрную группу до одного дамэ.</returns>
    private static Board PressBoard()
    {
        var board = new Board(Size);

        // Коробка 5×3 с тремя внутренними точками: один чёрный камень внутри оставляет два дамэ.
        board = Box(board, new Point(1, 1), 5, StoneColor.Black);

        // Внутренний камень соединяется с периметром: у группы остаётся два дамэ — (3,2) и (4,2).
        board = board.ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black));
        board = Surround(board, new Point(1, 1), 5, StoneColor.White);

        return board;
    }

    /// <summary>Ставит чёрное кольцо со стороной <paramref name="side"/> вокруг точки.</summary>
    /// <param name="board">Доска.</param>
    /// <param name="corner">Левый верхний угол кольца.</param>
    /// <param name="side">Сторона кольца.</param>
    /// <param name="color">Цвет камней.</param>
    /// <returns>Доска с кольцом.</returns>
    private static Board Box(Board board, Point corner, int side, StoneColor color)
    {
        for (var offset = 0; offset < side; offset++)
        {
            board = Place(board, new Point((byte)(corner.X + offset), corner.Y), color);
            board = Place(board, new Point((byte)(corner.X + offset), (byte)(corner.Y + side - 1)), color);
            board = Place(board, new Point(corner.X, (byte)(corner.Y + offset)), color);
            board = Place(board, new Point((byte)(corner.X + side - 1), (byte)(corner.Y + offset)), color);
        }

        return board;
    }

    /// <summary>Ставит камень, если точка свободна: помогает собирать кольца без повторов.</summary>
    /// <param name="board">Доска.</param>
    /// <param name="point">Точка.</param>
    /// <param name="color">Цвет камня.</param>
    /// <returns>Доска с камнем.</returns>
    private static Board Place(Board board, Point point, StoneColor color) =>
        board.At(point) == StoneColor.Empty ? board.ApplyMove(Move.Play(point, color)) : board;

    /// <summary>Окружает кольцо камнями: у внутренней группы не остаётся внешних дамэ.</summary>
    /// <param name="board">Доска с кольцом.</param>
    /// <param name="corner">Левый верхний угол кольца.</param>
    /// <param name="side">Сторона кольца.</param>
    /// <param name="color">Цвет окружающих камней.</param>
    /// <returns>Доска с окружением.</returns>
    private static Board Surround(Board board, Point corner, int side, StoneColor color)
    {
        for (var x = corner.X - 1; x <= corner.X + side; x++)
        {
            for (var y = corner.Y - 1; y <= corner.Y + side; y++)
            {
                if (x < 0 || y < 0 || x >= Size.Value || y >= Size.Value)
                {
                    continue;
                }

                // Только рамка вокруг коробки: диагонали — это уже внутренние точки, и камень там
                // был бы самоубийственным.
                var onFrame = x == corner.X - 1
                    || x == corner.X + side
                    || y == corner.Y - 1
                    || y == corner.Y + side;

                if (onFrame)
                {
                    board = Place(board, new Point((byte)x, (byte)y), color);
                }
            }
        }

        return board;
    }

    /// <summary>Строит открытую позицию середины партии: у обеих сторон живые группы.</summary>
    /// <returns>Доска 9×9 без доказанно мёртвых групп и без плотных территорий.</returns>
    private static Board MiddleGameBoard()
    {
        var board = new Board(Size);

        foreach (var (point, color) in new (Point Point, StoneColor Color)[]
        {
            (new Point(2, 2), StoneColor.Black),
            (new Point(3, 3), StoneColor.White),
            (new Point(6, 2), StoneColor.Black),
            (new Point(6, 6), StoneColor.White),
            (new Point(2, 6), StoneColor.Black),
            (new Point(4, 5), StoneColor.White),
            (new Point(5, 4), StoneColor.Black),
            (new Point(3, 6), StoneColor.White)
        })
        {
            board = board.ApplyMove(Move.Play(point, color));
        }

        return board;
    }
}
