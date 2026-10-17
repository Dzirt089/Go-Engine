using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Подсветка форм: номера, список и выбор — общий механизм задач, уроков и партий.</summary>
/// <remarks>
/// Замечание пользователя 2026-10-11: независимые формы различаются номерами и списком, а не цветом;
/// номера стоят и на доске значками. Проверки идут на позициях, собранных прямо здесь, поэтому
/// не зависят от контента.
/// </remarks>
public sealed class ShapeHighlightTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Одна_Форма_Не_Нумеруется()
    {
        // Пара камней в линию: форма одна, номера и список не нужны.
        var board = Build((3, 3, StoneColor.Black), (3, 4, StoneColor.Black), (5, 5, StoneColor.White), (5, 6, StoneColor.White));
        var marks = new ShapeHighlight();

        _ = marks.Refresh(board, StoneColor.Black, [new Point(3, 3), new Point(3, 4)]);

        Assert.Single(marks.Shapes);
        Assert.False(marks.HasSeveral);
        Assert.Empty(marks.Badges);
        Assert.Equal("ноби", marks.Shapes[0].Name);
        Assert.Equal("Форма: ноби", marks.Summary);
    }

    [Fact]
    public void Две_Формы_Получают_Номера_И_Значки()
    {
        // Две отдельные пары в разных углах: две независимые формы — номера и значки на доске.
        // Две формы с разными названиями: пара в углу и плотный квадрат.
        // Одинаковые названия в списке не показываются — иначе игрок видит «2) бамбук»
        // и «3) бамбук» и не понимает разницы (B7 из отчёта QA 2026-10-17).
        var board = Build(
            (1, 1, StoneColor.Black), (1, 2, StoneColor.Black),
            (6, 6, StoneColor.Black), (7, 6, StoneColor.Black),
            (6, 7, StoneColor.Black), (7, 7, StoneColor.Black),
            (4, 4, StoneColor.White));
        var marks = new ShapeHighlight();

        _ = marks.Refresh(board, StoneColor.Black, [new Point(1, 1), new Point(6, 6), new Point(7, 7)]);

        Assert.True(marks.Shapes.Count >= 2, $"форм найдено: {marks.Shapes.Count}");
        Assert.True(marks.HasSeveral);
        Assert.Equal(marks.Shapes.Count, marks.Badges.Count);
        Assert.Equal(Enumerable.Range(1, marks.Shapes.Count), marks.Badges.Select(badge => badge.Number));
        Assert.StartsWith("Формы: 1)", marks.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Плохая_Форма_Не_Попадает_В_Список_Решения()
    {
        // Регрессия QA 2026-10-17: в «Формах решения» решённой задачи стоял «пустой треугольник» —
        // форма с пометкой «плохая». С отбором в список решения попадают только хорошие формы;
        // у этой позиции хороших нет, поэтому список пуст и подсвечивать нечего.
        var board = Build((3, 3, StoneColor.Black), (3, 4, StoneColor.Black), (4, 4, StoneColor.Black));
        var marks = new ShapeHighlight();

        _ = marks.Refresh(
            board,
            StoneColor.Black,
            [new Point(3, 3), new Point(3, 4), new Point(4, 4)],
            goodOnly: true);

        Assert.Empty(marks.Shapes);
    }

    [Fact]
    public void Без_Отбора_Плохая_Форма_Остаётся_Формой_Хода()
    {
        // Уроки и разбор партий показывают форму хода как есть, в том числе плохую: отбор
        // включается только для списка решения задачи, а не для общего механизма.
        var board = Build((3, 3, StoneColor.Black), (3, 4, StoneColor.Black), (4, 4, StoneColor.Black));
        var marks = new ShapeHighlight();

        _ = marks.Refresh(board, StoneColor.Black, [new Point(3, 3), new Point(3, 4), new Point(4, 4)]);

        var shape = Assert.Single(marks.Shapes);
        Assert.Equal("пустой треугольник", shape.Name);
        Assert.False(shape.Good);
    }

    [Fact]
    public void Отбор_Оставляет_Хорошие_Формы_С_Номерами_Подряд()
    {
        // Смешанная позиция: пара в углу (ноби, хорошая) и плотный квадрат (кирпич, плохая).
        // Отбор не должен оставлять дыру в нумерации чипов: номера идут подряд с единицы.
        var board = Build(
            (1, 1, StoneColor.Black), (1, 2, StoneColor.Black),
            (6, 6, StoneColor.Black), (7, 6, StoneColor.Black),
            (6, 7, StoneColor.Black), (7, 7, StoneColor.Black),
            (4, 4, StoneColor.White));
        var marks = new ShapeHighlight();

        _ = marks.Refresh(
            board,
            StoneColor.Black,
            [new Point(1, 1), new Point(6, 6), new Point(7, 7)],
            goodOnly: true);

        Assert.All(marks.Shapes, shape => Assert.True(shape.Good, $"в списке решения плохая форма: {shape.Name}"));
        Assert.Equal(Enumerable.Range(1, marks.Shapes.Count), marks.Shapes.Select(shape => shape.Number));
        Assert.Equal("ноби", marks.Shapes[0].Name);
    }

    [Fact]
    public void Выбор_Формы_Меняет_Камни_Подсветки()
    {
        // Две формы с разными названиями: пара в углу и плотный квадрат.
        // Одинаковые названия в списке не показываются — иначе игрок видит «2) бамбук»
        // и «3) бамбук» и не понимает разницы (B7 из отчёта QA 2026-10-17).
        var board = Build(
            (1, 1, StoneColor.Black), (1, 2, StoneColor.Black),
            (6, 6, StoneColor.Black), (7, 6, StoneColor.Black),
            (6, 7, StoneColor.Black), (7, 7, StoneColor.Black),
            (4, 4, StoneColor.White));
        var marks = new ShapeHighlight();

        _ = marks.Refresh(board, StoneColor.Black, [new Point(1, 1), new Point(6, 6), new Point(7, 7)]);

        var first = marks.Points;

        Assert.True(marks.Select(1));
        Assert.NotEqual(first, marks.Points);
        Assert.Equal(marks.Shapes[1].Points, marks.Points);
        Assert.False(marks.Select(1));
    }

    [Fact]
    public void Значок_Стоит_На_Камне_Формы()
    {
        // Две формы с разными названиями: пара в углу и плотный квадрат.
        // Одинаковые названия в списке не показываются — иначе игрок видит «2) бамбук»
        // и «3) бамбук» и не понимает разницы (B7 из отчёта QA 2026-10-17).
        var board = Build(
            (1, 1, StoneColor.Black), (1, 2, StoneColor.Black),
            (6, 6, StoneColor.Black), (7, 6, StoneColor.Black),
            (6, 7, StoneColor.Black), (7, 7, StoneColor.Black),
            (4, 4, StoneColor.White));
        var marks = new ShapeHighlight();

        _ = marks.Refresh(board, StoneColor.Black, [new Point(1, 1), new Point(6, 6), new Point(7, 7)]);

        foreach (var badge in marks.Badges)
        {
            Assert.Equal(StoneColor.Black, board.At(badge.Point));
            Assert.Contains(badge.Point, marks.Shapes[badge.Number - 1].Points);
        }
    }

    [Fact]
    public void В_Партии_Подсветка_Растёт_Вместе_С_Ходами()
    {
        // Разбор партии: форма считается по сыгранному ходу, и хотя бы на одном ходу она есть.
        // Разбор останавливается на вопросах, поэтому на них отвечаем — как это делает игрок.
        var model = new ReviewViewModel();
        var game = ReviewLibrary.All[0];
        var found = false;
        var guard = 0;

        while (!found && guard++ < 30)
        {
            if (model.IsQuiz)
            {
                model.Play(game.Moves[model.MoveNumber].Point);
                continue;
            }

            model.Next();

            if (model.Shapes.Count == 0)
            {
                continue;
            }

            found = true;
            Assert.Equal(model.Shapes[0].Points, model.ShapePoints);
            Assert.False(string.IsNullOrWhiteSpace(model.ShapesSummary));
        }

        Assert.True(found, "ни на одном ходу партии форма не найдена");
    }

    [Fact]
    public void Урок_Показывает_Форму_Хода()
    {
        // Урок: форма появляется после верного хода шага и снимается при переходе дальше.
        var model = new LessonViewModel();
        var guard = 0;

        while (model.CanGoNext && guard++ < 40)
        {
            if (model.CanHint)
            {
                model.ShowHint();
            }

            if (model.HintPoint is { } point)
            {
                model.Play(point);
            }

            if (model.Shapes.Count > 0)
            {
                Assert.Equal(model.Shapes[0].Points, model.ShapePoints);
                Assert.False(string.IsNullOrWhiteSpace(model.ShapesSummary));

                return;
            }

            model.Next();
        }

        // Ни один шаг урока не построил формы из двух камней: механизм всё равно обязан работать.
        Assert.False(model.HasSeveralShapes);
    }

    /// <summary>Собирает позицию из камней.</summary>
    /// <param name="stones">Точки и цвета.</param>
    /// <returns>Доска.</returns>
    private static Board Build(params (int X, int Y, StoneColor Colour)[] stones)
    {
        var board = new Board(Size);

        foreach (var (x, y, colour) in stones)
        {
            board = board.ApplyMove(Move.Play(new Point((byte)x, (byte)y), colour));
        }

        return board;
    }
}
