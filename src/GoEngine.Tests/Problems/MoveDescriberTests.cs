using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Строка о ходе: игрок говорит, что он сделал и зачем, по правилам и позиции.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-11: «должна быть обучающая информация, а не пустота… как будто каждый
/// игрок говорит вслух — „я занимаю такую-то позицию для такой-то тактики“». Здесь проверяется, что
/// строка говорит правду: координата как на доске, линия и область, снятия, атари, соединение
/// и разрезание — и что она есть для каждого хода каждой партии.
/// </remarks>
public sealed class MoveDescriberTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Строка_Говорит_За_Игрока_О_Месте_Хода()
    {
        var move = Move.Play(new Point(2, 2), StoneColor.Black);
        var before = new Board(Size);
        var after = before.ApplyMove(move);

        var text = MoveDescriber.Describe(MoveDescriber.FactsOf(before, after, move, 1), Size);

        Assert.Contains("Чёрные: C3", text, StringComparison.Ordinal);
        Assert.Contains("угол", text, StringComparison.Ordinal);
        Assert.Contains("третьей линии", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Строка_Называет_Снятые_Камни()
    {
        // Белый камень (0,0) в атари: чёрные закрывают последнее дыхание ходом (0,1).
        var moves = new[]
        {
            Move.Play(new Point(1, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(4, 4), StoneColor.White),
            Move.Play(new Point(0, 1), StoneColor.Black)
        };

        var before = Position(moves, 4);
        var after = before.ApplyMove(moves[4]);

        var text = MoveDescriber.Describe(MoveDescriber.FactsOf(before, after, moves[4], 5), Size);

        Assert.Contains("снимаю 1 камень", text, StringComparison.Ordinal);
        Assert.Contains("пленные", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Строка_Объявляет_Атари_На_Группу()
    {
        // Белая группа из двух камней: чёрные закрывают третье дыхание, оставляя одно.
        var moves = new[]
        {
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(8, 8), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 1), StoneColor.Black)
        };

        var before = Position(moves, 4);
        var after = before.ApplyMove(moves[4]);
        var facts = MoveDescriber.FactsOf(before, after, moves[4], 5);

        Assert.Single(facts.Atari);
        Assert.Equal(StoneColor.White, facts.Atari[0].Color);
        Assert.Equal(2, facts.Atari[0].Stones);
        Assert.Contains("ставлю белую группу в атари", MoveDescriber.Describe(facts, Size), StringComparison.Ordinal);
    }

    [Fact]
    public void Строка_Различает_Соединение()
    {
        // Чёрные соединяют свои камни B2 и D2 ходом C2: рядом две свои группы.
        var moves = new[]
        {
            Move.Play(new Point(1, 1), StoneColor.Black),
            Move.Play(new Point(1, 3), StoneColor.White),
            Move.Play(new Point(3, 1), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White)
        };

        var move = Move.Play(new Point(2, 1), StoneColor.Black);
        var before = Position(moves, 4);
        var after = before.ApplyMove(move);
        var facts = MoveDescriber.FactsOf(before, after, move, 5);

        Assert.True(facts.Connects);
        Assert.Equal(2, facts.OwnGroups);
        Assert.Contains("соединяю свои группы", MoveDescriber.Describe(facts, Size), StringComparison.Ordinal);
    }

    [Fact]
    public void Строка_Различает_Разрезание()
    {
        // Чёрные встают между двумя белыми группами: они больше не соединены.
        var moves = new[]
        {
            Move.Play(new Point(1, 1), StoneColor.White),
            Move.Play(new Point(8, 8), StoneColor.Black),
            Move.Play(new Point(3, 1), StoneColor.White),
            Move.Play(new Point(8, 7), StoneColor.Black)
        };

        var move = Move.Play(new Point(2, 1), StoneColor.Black);
        var before = Position(moves, 4);
        var after = before.ApplyMove(move);
        var facts = MoveDescriber.FactsOf(before, after, move, 5);

        Assert.True(facts.Cuts);
        Assert.Contains("разрезаю", MoveDescriber.Describe(facts, Size), StringComparison.Ordinal);
    }

    [Fact]
    public void Пас_Объясняется_Словами()
    {
        var board = new Board(Size);

        var text = MoveDescriber.Describe(MoveDescriber.FactsOf(board, board, Move.Pass(StoneColor.Black), 4), Size);

        Assert.Contains("Чёрные: пас", text, StringComparison.Ordinal);
        Assert.Contains("подсчёт", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Каждый_Ход_Каждой_Партии_Получает_Строку()
    {
        // Панель не молчит: у каждого хода каждой партии есть объяснение (жалоба 2026-10-11).
        foreach (var game in ReviewLibrary.All)
        {
            var session = new ReviewSession(game);

            for (var index = 0; index < game.Moves.Count; index++)
            {
                // Позиция выставляется напрямую: вопрос в этой точке не должен мешать проверке строки.
                Assert.True(session.GoTo(index + 1), $"{game.Id}: переход к ходу {index + 1} не выполнен");

                Assert.False(
                    string.IsNullOrWhiteSpace(session.MoveInfo),
                    $"{game.Id}, ход {index + 1}: пустая строка о ходе");
            }
        }
    }

    [Fact]
    public void Строка_Партий_Говорит_ОСоединении_ТолькоПриДвухГруппах()
    {
        // Находка живого прогона 2026-10-17: «соединяю свои группы» на ходе, который лишь
        // продолжает одну группу. Число групп считается по доске до хода, независимо от фактов.
        foreach (var game in ReviewLibrary.All)
        {
            var board = game.StartPosition();

            for (var index = 0; index < game.Moves.Count; index++)
            {
                var move = game.Moves[index];
                var before = board;
                board = board.ApplyMove(move);

                if (move.Type != MoveType.Play)
                {
                    continue;
                }

                var text = MoveDescriber.Describe(MoveDescriber.FactsOf(before, board, move, index + 1), game.Size);

                if (AdjacentOwnGroups(before, move.Point, move.Color) <= 1)
                {
                    Assert.DoesNotContain("соединяю свои группы", text, StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public void Строка_О_Ходе_Меняется_От_Хода_К_Ходу()
    {
        var game = ReviewLibrary.All[0];
        var session = new ReviewSession(game);
        HashSet<string> lines = [];

        for (var index = 1; index <= 12; index++)
        {
            _ = session.GoTo(index);
            _ = lines.Add(session.MoveInfo);
        }

        Assert.True(lines.Count >= 10, $"разных строк за 12 ходов: {lines.Count}");
    }

    /// <summary>Сколько разных своих групп стоит вплотную к точке до хода.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="point">Точка хода.</param>
    /// <param name="color">Цвет хода.</param>
    /// <returns>Число групп; две и больше — ход их соединяет.</returns>
    private static int AdjacentOwnGroups(Board board, Point point, StoneColor color)
    {
        HashSet<Point> seen = [];
        var groups = 0;

        foreach (var neighbour in point.Neighbors(board.Size))
        {
            if (board.At(neighbour) != color || seen.Contains(neighbour))
            {
                continue;
            }

            groups++;

            foreach (var stone in GroupTracker.FindGroup(board, neighbour).Stones)
            {
                _ = seen.Add(stone);
            }
        }

        return groups;
    }

    /// <summary>Строит позицию после первых ходов.</summary>
    /// <param name="moves">Ходы по порядку.</param>
    /// <param name="count">Сколько ходов сыграть.</param>
    /// <returns>Доска после них.</returns>
    private static Board Position(IReadOnlyList<Move> moves, int count)
    {
        var board = new Board(Size);

        for (var index = 0; index < count; index++)
        {
            board = board.ApplyMove(moves[index]);
        }

        return board;
    }
}
