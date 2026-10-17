using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Распознавание формы хода: что игрок построил своим камнем.</summary>
/// <remarks>
/// Замечание пользователя 2026-10-11: «форма — нужна ли отдельная строка вида „форма: косуми, два
/// камня по диагонали“. Хорошая эта форма или нет, чем хороша\плоха тут в перспективе». Проверки
/// идут на позициях из уроков: каждая форма должна называться своим именем и получать оценку.
/// </remarks>
public sealed class MoveShapeTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Камень_В_Линию_Называется_Ноби()
    {
        var (board, move) = Play(
            (new Point(3, 3), StoneColor.Black),
            (new Point(3, 4), StoneColor.Black));

        var shape = MoveShape.Of(board, move);

        Assert.Equal("ноби", shape.Name);
        Assert.True(shape.Good);
    }

    [Fact]
    public void Камень_По_Диагонали_Называется_Косуми()
    {
        var (board, move) = Play(
            (new Point(3, 3), StoneColor.Black),
            (new Point(4, 4), StoneColor.Black));

        var shape = MoveShape.Of(board, move);

        Assert.Equal("косуми", shape.Name);
        Assert.True(shape.Good);
    }

    [Fact]
    public void Камень_Через_Точку_Называется_Тоби()
    {
        var (board, move) = Play(
            (new Point(3, 3), StoneColor.Black),
            (new Point(5, 3), StoneColor.Black));

        Assert.Equal("тоби", MoveShape.Of(board, move).Name);
    }

    [Fact]
    public void Ход_Конём_Называется_Кейма()
    {
        var (board, move) = Play(
            (new Point(3, 3), StoneColor.Black),
            (new Point(5, 4), StoneColor.Black));

        Assert.Equal("кейма", MoveShape.Of(board, move).Name);
    }

    [Fact]
    public void Три_Камня_Вокруг_Точки_Дают_Пасть_Тигра()
    {
        var (board, move) = Play(
            (new Point(3, 3), StoneColor.Black),
            (new Point(4, 4), StoneColor.Black),
            (new Point(5, 3), StoneColor.Black));

        var shape = MoveShape.Of(board, move);

        Assert.Equal("пасть тигра", shape.Name);
        Assert.True(shape.Good);
    }

    [Fact]
    public void Угол_Из_Двух_Камней_С_Пустой_Точкой_Внутри_Плохая_Форма()
    {
        // Классика уроков: три камня буквой Г, а четвёртая точка внутри пуста.
        var (board, move) = Play(
            (new Point(3, 3), StoneColor.Black),
            (new Point(3, 4), StoneColor.Black),
            (new Point(4, 4), StoneColor.Black));

        var shape = MoveShape.Of(board, move);

        Assert.Equal("пустой треугольник", shape.Name);
        Assert.False(shape.Good);
        Assert.Contains("тяжёлая форма", shape.Why, StringComparison.Ordinal);
    }

    [Fact]
    public void Форма_Жизни_В_Углу_Не_Называется_Пустым_Треугольником()
    {
        // В углу три камня A8, B8, B9 и пустая точка A9 — это глаз, а не пустой треугольник:
        // называть такую форму плохой нельзя, группа именно так и живёт.
        var (board, move) = Play(
            (new Point(0, 7), StoneColor.Black),
            (new Point(1, 8), StoneColor.Black),
            (new Point(1, 7), StoneColor.Black));

        var shape = MoveShape.Of(board, move);

        Assert.NotEqual("пустой треугольник", shape.Name);
        Assert.True(shape.Good, $"форма названа плохой: {shape.Name}");
    }

    [Fact]
    public void Бамбук_Из_Двух_Пар_Камней_Крепкая_Форма()
    {
        // Бамбук из урока: пара D5, E5 и вторая пара F6, G6 со сдвигом на одну точку.
        var (board, move) = Play(
            (new Point(3, 4), StoneColor.Black),
            (new Point(4, 4), StoneColor.Black),
            (new Point(5, 5), StoneColor.Black),
            (new Point(6, 5), StoneColor.Black));

        var shape = MoveShape.Of(board, move);

        Assert.Equal("бамбук", shape.Name);
        Assert.True(shape.Good);
    }

    [Fact]
    public void Квадрат_Из_Четырёх_Камней_Плохая_Форма()
    {
        // Кирпич: четыре камня квадратом — внутри ни глаза, ни территории.
        var (board, move) = Play(
            (new Point(4, 4), StoneColor.Black),
            (new Point(5, 4), StoneColor.Black),
            (new Point(4, 5), StoneColor.Black),
            (new Point(5, 5), StoneColor.Black));

        var shape = MoveShape.Of(board, move);

        Assert.Equal("кирпич", shape.Name);
        Assert.False(shape.Good);
    }

    [Fact]
    public void Каждая_Задача_Получает_Название_Формы()
    {
        List<string> errors = [];

        foreach (var problem in ProblemLibrary.All)
        {
            var session = new ProblemSession(problem);
            var guard = 0;

            while (session.State != ProblemState.Solved && guard++ < 30 && session.AcceptedMoves.Count > 0)
            {
                _ = session.Play(session.AcceptedMoves[0].Point);
            }

            if (session.SolverMoves.Count == 0)
            {
                continue;
            }

            var shape = MoveShape.Of(session.Board, session.SolverMoves[^1]);

            if (shape.Name.Length == 0 || shape.Why.Length < 20)
            {
                errors.Add($"{problem.Id}: форма не названа или не объяснена — «{shape.Name}», «{shape.Why}»");
            }
        }

        Assert.Empty(errors);
    }

    /// <summary>Играет камни по порядку и возвращает позицию и последний ход.</summary>
    /// <param name="stones">Точки с цветами.</param>
    /// <returns>Позиция после последнего камня и сам ход.</returns>
    private static (Board Board, Move Move) Play(params (Point Point, StoneColor Color)[] stones)
    {
        var board = new Board(Size);
        Move last = Move.None;

        foreach (var (point, color) in stones)
        {
            last = Move.Play(point, color);
            board = board.ApplyMove(last);
        }

        return (board, last);
    }
}
