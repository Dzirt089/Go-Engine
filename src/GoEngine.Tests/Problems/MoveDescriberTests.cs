using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Описание хода словами: координата, линия, область, снятия и атари.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-10: «в основном информация отсутствует о сделанном ходе противника
/// или тобой… необходимо добавлять пояснения о сделанных ходах». Здесь проверяется, что строка
/// говорит правду и называет точку так же, как доска.
/// </remarks>
public sealed class MoveDescriberTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Описание_Называет_Цвет_Точку_Линию_И_Область()
    {
        var board = BoardAfter(Move.Play(new Point(2, 2), StoneColor.Black));

        var text = MoveDescriber.Describe(MoveDescriber.FactsOf(board, Move.Play(new Point(2, 2), StoneColor.Black), 1), Size);

        Assert.Contains("Ход 1", text, StringComparison.Ordinal);
        Assert.Contains("чёрные C3", text, StringComparison.Ordinal);
        Assert.Contains("угол", text, StringComparison.Ordinal);
        Assert.Contains("третья линия", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Описание_Считает_Снятые_Камни()
    {
        // Белый камень (0,0) в атари: чёрные закрывают последнее дыхание ходом (0,1).
        var board = BoardAfter(
            Move.Play(new Point(1, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(4, 4), StoneColor.White),
            Move.Play(new Point(0, 1), StoneColor.Black));

        var facts = MoveDescriber.FactsOf(board, Move.Play(new Point(0, 1), StoneColor.Black), 5);

        Assert.Equal(1, facts.Captured);
        Assert.Contains("Снято камней: 1", MoveDescriber.Describe(facts, Size), StringComparison.Ordinal);
    }

    [Fact]
    public void Описание_Замечает_Атари_Соперника()
    {
        // Белая группа из двух камней: чёрные закрывают третье дыхание, оставляя одно.
        var board = BoardAfter(
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(8, 8), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 1), StoneColor.Black));

        var facts = MoveDescriber.FactsOf(board, Move.Play(new Point(0, 1), StoneColor.Black), 5);

        Assert.Single(facts.Atari);
        Assert.Equal(StoneColor.White, facts.Atari[0].Color);
        Assert.Contains("В атари попала белая группа (2 камня)", MoveDescriber.Describe(facts, Size), StringComparison.Ordinal);
    }

    [Fact]
    public void Пас_Описывается_Словом()
    {
        var board = BoardAfter();

        var text = MoveDescriber.Describe(MoveDescriber.FactsOf(board, Move.Pass(StoneColor.Black), 4), Size);

        Assert.Contains("чёрные", text, StringComparison.Ordinal);
        Assert.Contains("пас", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Описание_Хода_Партии_Не_Пустое_Для_Каждого_Хода()
    {
        // Каждый ход каждой обучающей партии обязан получить строку: иначе панель снова молчит.
        foreach (var game in ReviewLibrary.All)
        {
            var board = game.StartPosition();

            for (var index = 0; index < game.Moves.Count; index++)
            {
                var move = game.Moves[index];
                board = board.ApplyMove(move);

                var text = MoveDescriber.Describe(MoveDescriber.FactsOf(board, move, index + 1), game.Size);

                Assert.False(string.IsNullOrWhiteSpace(text), $"{game.Id}, ход {index + 1}: пустое описание");
            }
        }
    }

    /// <summary>Строит позицию ходами.</summary>
    /// <param name="moves">Ходы по порядку.</param>
    /// <returns>Доска после них.</returns>
    private static Board BoardAfter(params Move[] moves)
    {
        var board = new Board(Size);

        foreach (var move in moves)
        {
            board = board.ApplyMove(move);
        }

        return board;
    }
}