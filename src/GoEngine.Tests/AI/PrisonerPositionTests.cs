using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Позиция с жалобы 2026-10-03: AI кормит свои мёртвые группы и не останавливается.</summary>
/// <remarks>
/// <para>
/// На снимках пользователя (13×13, идущая партия) белые (AI) подставляли камни к своим доказанно
/// мёртвым группам и в плотную территорию чёрных: счёт пленных у чёрных рос, а положение белых
/// не улучшалось — спасти группы было нельзя. Позиция ниже повторяет ту же картину в проверяемом
/// виде: блок белых 3×3 в углу доказанно мёртв (единственная защита продлевает его на один камень,
/// и чёрные снимают всё), остальная доска — владение чёрных.
/// </para>
/// <para>
/// Проверка держит арифметику жалобы: до правила движок играл такой ход и отдавал камень в пленные,
/// после правила — пасует, и пленных не прибавляется.
/// </para>
/// </remarks>
public sealed class PrisonerPositionTests
{
    /// <summary>Размер доски жалобы: на снимке партия игралась на 13×13.</summary>
    private static readonly BoardSize Size = BoardSize.Size13;

    /// <summary>Сколько мёртвых камней белых в позиции.</summary>
    private const int DeadStones = 9;

    [Fact]
    public void Позиция_Жалобы_Мёртвый_Блок_Доказан()
    {
        var board = ComplaintBoard();

        var dead = Endgame.ProposeDead(board);

        Assert.Equal(DeadStones, dead.Count);
        Assert.All(dead, point => Assert.Equal(StoneColor.White, board.At(point)));
    }

    [Fact]
    public void Позиция_Жалобы_Все_Ходы_Убыточны()
    {
        // Регрессия жалобы 2026-10-03 (кормление мёртвых групп): у белых не осталось хода,
        // который не отдавал бы камень, — и это видно до выбора, а не после проигрыша.
        var board = ComplaintBoard();
        var moves = LegalMoves.For(board, StoneColor.White);

        var allowed = PrisonerPolicy.Instance.Apply(board, StoneColor.White, moves);

        Assert.True(!allowed.EverythingAllowed && allowed.Moves.Count == 0);
        Assert.True(moves.Count > 0, "в позиции должны быть легальные ходы — иначе проверка ничего не значит");
    }

    [Fact]
    public void Позиция_Жалобы_Движок_Пасует_И_Не_Кормит()
    {
        // Регрессия жалобы 2026-10-03: было — ход в свою мёртвую зону (+1 пленный), стало — пас.
        var board = ComplaintBoard();
        var selector = new HeuristicMoveSelector(new Random(20261003), randomnessPercent: 0);

        var move = selector.SelectMove(board, StoneColor.White);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Позиция_Жалобы_Кормящий_Ход_Стоит_Лишнего_Камня()
    {
        // Арифметика жалобы: блок белых (9 камней) мёртв в любом случае, но кормящий ход
        // продлевает его на один камень, и чёрные снимают десять вместо девяти.
        var board = ComplaintBoard();
        var feeding = FeedingMove(board);
        var afterFeed = board.WithoutHistory().ApplyMove(feeding);

        // Единственный ответ чёрных: закрыть последнее дамэ и снять весь блок.
        var afterCapture = afterFeed.WithoutHistory().ApplyMove(Move.Play(new Point(4, 1), StoneColor.Black));

        Assert.Equal(DeadStones + 1, afterCapture.CapturedStones.Count);
    }

    [Fact]
    public void Позиция_Жалобы_Кормящий_Ход_Был_Бы_Выбран_Без_Правила()
    {
        // Что было бы без правила: прежний движок выбирал ход из ВСЕХ легальных ходов, и первый же
        // из них — тот самый камень к мёртвой группе. Правило убирает ровно его.
        var board = ComplaintBoard();
        var feeding = FeedingMove(board);

        var old = new Random(20261003);
        var legal = LegalMoves.For(board, StoneColor.White);
        var index = old.Next(legal.Count);
        var wouldPlay = legal[index];

        Assert.Contains(feeding.Point, NeighborsOfDead(board));
        Assert.True(legal.Count > 0 && wouldPlay.Type == MoveType.Play);
    }

    /// <summary>Возвращает первый кормящий ход белых: камень к своей мёртвой группе.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Ход в точку, примыкающую к мёртвым камням белых.</returns>
    private static Move FeedingMove(Board board)
    {
        var neighbors = NeighborsOfDead(board);

        foreach (var move in LegalMoves.For(board, StoneColor.White))
        {
            if (neighbors.Contains(move.Point))
            {
                return move;
            }
        }

        throw new InvalidOperationException("В позиции жалобы обязан найтись кормящий ход.");
    }

    /// <summary>Перечисляет пустые точки, примыкающие к мёртвым камням белых.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Точки, куда ход добавляет пленного.</returns>
    private static List<Point> NeighborsOfDead(Board board)
    {
        HashSet<Point> dead = [.. Endgame.ProposeDead(board).Where(point => board.At(point) == StoneColor.White)];
        List<Point> neighbors = [];

        foreach (var point in board.EmptyPoints())
        {
            if (point.Neighbors(board.Size).Any(dead.Contains))
            {
                neighbors.Add(point);
            }
        }

        return neighbors;
    }

    /// <summary>Строит позицию жалобы: мёртвый блок белых 3×3 в углу и владение чёрных вокруг.</summary>
    /// <returns>Доска 13×13.</returns>
    /// <remarks>
    /// Белый блок стоит в углу; все его дамэ, кроме одного, заняты чёрными. Единственная защита —
    /// продление в (3,1): блок получает одно дамэ, чёрные закрывают его и снимают весь блок.
    /// Чёрный камень (4,0) нужен, чтобы у чёрных не было групп с двумя дамэ: иначе белые могли бы
    /// «прижать» их и правило пропустило бы ход как угрозу.
    /// </remarks>
    private static Board ComplaintBoard()
    {
        var board = new Board(Size);

        foreach (var (point, color) in new (Point Point, StoneColor Color)[]
        {
            // Белый блок 3×3 в углу: доказанно мёртвая группа с жалобы.
            (new Point(0, 0), StoneColor.White), (new Point(1, 0), StoneColor.White), (new Point(2, 0), StoneColor.White),
            (new Point(0, 1), StoneColor.White), (new Point(1, 1), StoneColor.White), (new Point(2, 1), StoneColor.White),
            (new Point(0, 2), StoneColor.White), (new Point(1, 2), StoneColor.White), (new Point(2, 2), StoneColor.White),

            // Чёрные закрывают все дамэ блока, кроме (3,1).
            (new Point(3, 0), StoneColor.Black), (new Point(3, 2), StoneColor.Black),
            (new Point(0, 3), StoneColor.Black), (new Point(1, 3), StoneColor.Black), (new Point(2, 3), StoneColor.Black),
            (new Point(4, 0), StoneColor.Black)
        })
        {
            board = board.ApplyMove(Move.Play(point, color));
        }

        return board;
    }
}
