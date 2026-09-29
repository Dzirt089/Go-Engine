using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Предложение мёртвых групп: ограниченный перебор форсированного захвата.</summary>
/// <remarks>
/// Проверяется <see cref="Endgame.ProposeDead"/>. Перебор обязан быть приговором, а не догадкой:
/// предлагается только та группа, которую не спасает ни один ход защиты. Поэтому здесь есть и
/// мёртвые группы, и живые — с двумя глазами и с одним дамэ, из которого защита успевает уйти.
/// </remarks>
public sealed class DeadGroupTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void ProposeDead_Пустая_Доска_Ничего_Не_Предлагает()
    {
        Assert.Empty(Endgame.ProposeDead(new Board(Size)));
    }

    [Fact]
    public void ProposeDead_Мёртвая_Группа_В_Углу_Предложена()
    {
        // Белая группа {(0,0), (1,0)}: единственное дамэ (2,0) закрыто чёрными со всех сторон,
        // ход в него — самоубийство. Защиты нет, значит группа мёртвая.
        var dead = Endgame.ProposeDead(CornerDeadWhite());

        Assert.Equal([new Point(0, 0), new Point(1, 0)], dead);
    }

    [Fact]
    public void ProposeDead_Предлагает_Всю_Группу()
    {
        // Угловая группа из трёх камней: предложение возвращает все её камни, а не одну точку.
        var dead = Endgame.ProposeDead(CornerDeadWhiteThree());

        Assert.Equal(3, dead.Count);
    }

    [Fact]
    public void ProposeDead_Группа_С_Двумя_Глазами_Не_Предлагается()
    {
        // Чёрная группа в белой стене имеет ровно два дамэ — два настоящих глаза. Заполнить их
        // белый не может (самоубийство), а защищающийся всегда может спасти пасом.
        var dead = Endgame.ProposeDead(TwoEyedBlack());

        Assert.Empty(dead);
    }

    [Fact]
    public void ProposeDead_Защита_Успевает_Группа_Не_Предлагается()
    {
        // Чёрная группа в атари, но её единственное дамэ — не тупик: продлившись, она уходит
        // от захвата за горизонт перебора. Неполный перебор мёртвой её не называет.
        var dead = Endgame.ProposeDead(BlackEscapesAtari());

        Assert.Empty(dead);
    }

    [Fact]
    public void ProposeDead_Сэки_Ничего_Не_Предлагает()
    {
        // GO_RULES.md, 13.7: у стен в сэки много общих дамэ, обе группы живы.
        Assert.Empty(Endgame.ProposeDead(TestPositions.Seki()));
    }

    [Fact]
    public void ProposeDead_Не_Меняет_Доску()
    {
        var board = CornerDeadWhite();
        var before = Occupied(board);

        _ = Endgame.ProposeDead(board);

        Assert.Equal(before, Occupied(board));
    }

    [Fact]
    public void ProposeDead_Не_Портит_Историю_Партии()
    {
        // Примерочные ходы перебора не должны попадать в историю суперко: иначе запрет за
        // повторение позиции распространится на варианты, которых в партии не было.
        var game = GameWithDeadCorner();
        var before = game.History.Count;

        _ = Endgame.ProposeDead(game.Board);

        Assert.Equal(before, game.History.Count);
    }

    [Fact]
    public void ProposeDead_Находит_Группу_В_Партии()
    {
        var game = GameWithDeadCorner();

        var dead = Endgame.ProposeDead(game.Board);

        Assert.Equal([new Point(0, 0), new Point(1, 0)], dead);
    }

    [Fact]
    public void ProposeDead_Без_Доски_Отказ()
    {
        Assert.Throws<ArgumentNullException>(() => Endgame.ProposeDead(null!));
    }

    /// <summary>Строит позицию: белая группа в углу в атари, остальное — чёрная стена.</summary>
    /// <returns>Доска 9×9 с белыми камнями (0,0), (1,0) без спасения.</returns>
    private static Board CornerDeadWhite()
    {
        var board = new Board(Size);

        board = Place(board, StoneColor.White, (0, 0), (1, 0));

        return Place(board, StoneColor.Black, (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (3, 0), (4, 0));
    }

    /// <summary>Строит позицию: угловая группа белых из трёх камней с одним дамэ-самоубийством.</summary>
    /// <returns>Доска 9×9: белые (0,0), (1,0), (0,1); чёрные закрывают все выходы.</returns>
    private static Board CornerDeadWhiteThree()
    {
        var board = new Board(Size);

        board = Place(board, StoneColor.White, (0, 0), (1, 0), (0, 1));

        return Place(board, StoneColor.Black, (1, 1), (2, 0), (1, 2), (0, 3), (2, 1));
    }

    /// <summary>Строит позицию: чёрная группа с двумя настоящими глазами внутри белой стены.</summary>
    /// <returns>Доска 9×9: чёрное кольцо с глазами (2,2) и (3,3) и белая стена вокруг него.</returns>
    private static Board TwoEyedBlack()
    {
        var board = new Board(Size);

        board = Place(board, StoneColor.Black, (1, 1), (2, 1), (3, 1), (4, 1), (1, 2), (4, 2), (1, 3), (4, 3));
        board = Place(board, StoneColor.Black, (1, 4), (2, 4), (3, 4), (4, 4), (2, 3), (3, 2));

        // Стена вокруг кольца: она забирает у чёрных все внешние дамэ, оставляя ровно два глаза.
        return Place(
            board,
            StoneColor.White,
            (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0),
            (0, 1), (5, 1), (0, 2), (5, 2), (0, 3), (5, 3), (0, 4), (5, 4),
            (0, 5), (1, 5), (2, 5), (3, 5), (4, 5), (5, 5));
    }

    /// <summary>Строит позицию: чёрная группа в атари, из которой защита уходит продлением.</summary>
    /// <returns>Доска 9×9: чёрные (0,0), (1,0) с единственным дамэ (2,0).</returns>
    private static Board BlackEscapesAtari() =>
        Place(Place(new Board(Size), StoneColor.Black, (0, 0), (1, 0)), StoneColor.White, (0, 1), (1, 1));

    /// <summary>Строит партию, в которой у белых в углу остаётся мёртвая группа.</summary>
    /// <returns>Партия с историей ходов и белой группой (0,0), (1,0) в атари.</returns>
    /// <remarks>
    /// Ходы чередуются, как в настоящей партии: иначе проверить, что перебор не портит историю,
    /// было бы не на чем. Чёрные камни собраны в одну группу с четырьмя дамэ — перебор её
    /// мёртвой не назовёт, и предложение останется ровно про белую группу.
    /// </remarks>
    private static GameState GameWithDeadCorner()
    {
        var game = GameState.NewGame(Size, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(2, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(8, 8), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 2), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(8, 7), StoneColor.White));

        return game;
    }

    /// <summary>Ставит камни одного цвета в перечисленные точки.</summary>
    /// <param name="board">Исходная позиция.</param>
    /// <param name="color">Цвет камней.</param>
    /// <param name="points">Точки постановки.</param>
    /// <returns>Позиция с поставленными камнями.</returns>
    private static Board Place(Board board, StoneColor color, params (int X, int Y)[] points)
    {
        foreach (var (x, y) in points)
        {
            board = board.ApplyMove(Move.Play(new Point((byte)x, (byte)y), color));
        }

        return board;
    }

    /// <summary>Снимает с доски список занятых точек.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Точки с камнями в порядке обхода доски.</returns>
    private static List<Point> Occupied(Board board)
    {
        List<Point> points = [];

        foreach (var point in board.AllPoints())
        {
            if (board.At(point) != StoneColor.Empty)
            {
                points.Add(point);
            }
        }

        return points;
    }
}
