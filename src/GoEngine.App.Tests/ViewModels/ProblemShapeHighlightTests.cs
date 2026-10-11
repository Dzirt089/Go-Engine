using GoEngine.App.ViewModels;

namespace GoEngine.App.Tests;

/// <summary>Подсветка формы в задаче: камни появляются, когда задача решена.</summary>
/// <remarks>Замечание пользователя 2026-10-11: подсвечивать форму на доске при решении задачи.</remarks>
public sealed class ProblemShapeHighlightTests
{
    [Fact]
    public void До_Решения_Подсветки_Нет()
    {
        var model = new ProblemViewModel();

        Assert.Empty(model.ShapePoints);
    }

    [Fact]
    public void После_Решения_Подсвечены_Камни_Формы()
    {
        var model = new ProblemViewModel();
        var guard = 0;

        while (!model.IsSolved && guard++ < 30 && model.AcceptedMoves.Count > 0)
        {
            model.Play(model.AcceptedMoves[0].Point);
        }

        Assert.True(model.IsSolved, "задача не решена");
        Assert.NotEmpty(model.ShapePoints);
        Assert.All(model.ShapePoints, point => Assert.NotEqual(GoEngine.Core.StoneColor.Empty, model.Board.At(point)));
    }

    [Fact]
    public void Подсветку_Дают_Почти_Все_Задачи()
    {
        // Пять задач (ts-007, ts-011, ts-019, ts-031, ts-033) решаются одиночными камнями:
        // узнаваемой формы из двух и более камней в них нет, и подсвечивать нечего — это честно,
        // а не пропуск. Проверка сторожит, что таких задач не становится больше.
        List<string> without = [];
        var total = 0;

        foreach (var problem in GoEngine.Problems.ProblemLibrary.All)
        {
            var model = new ProblemViewModel();
            var index = model.Problems.ToList().FindIndex(item => item.Id == problem.Id);

            if (index < 0)
            {
                continue;
            }

            model.SelectedIndex = index;
            var guard = 0;

            while (!model.IsSolved && guard++ < 30 && model.AcceptedMoves.Count > 0)
            {
                model.Play(model.AcceptedMoves[0].Point);
            }

            total++;

            if (model.ShapePoints.Count == 0)
            {
                without.Add(problem.Id);
            }
            else
            {
                Assert.All(model.ShapePoints, point => Assert.NotEqual(GoEngine.Core.StoneColor.Empty, model.Board.At(point)));
            }
        }

        Assert.True(total >= 36, $"задач проверено: {total}");
        Assert.True(without.Count <= 5, $"без формы оказались: {string.Join(", ", without)}");
    }

    [Fact]
    public void Независимые_Формы_Различаются_Номерами()
    {
        // Замечание пользователя 2026-10-11: несколько форм различаются номерами и списком.
        // Таких задач в библиотеке большинство — проверяем на первой, где форм больше одной.
        ProblemViewModel? found = null;

        foreach (var problem in GoEngine.Problems.ProblemLibrary.All)
        {
            var model = new ProblemViewModel();
            var index = model.Problems.ToList().FindIndex(item => item.Id == problem.Id);

            if (index < 0)
            {
                continue;
            }

            model.SelectedIndex = index;
            var guard = 0;

            while (!model.IsSolved && guard++ < 30 && model.AcceptedMoves.Count > 0)
            {
                model.Play(model.AcceptedMoves[0].Point);
            }

            if (model.Shapes.Count > 1)
            {
                found = model;
                break;
            }
        }

        Assert.NotNull(found);
        Assert.True(found!.HasSeveralShapes);
        Assert.Equal(Enumerable.Range(1, found.Shapes.Count), found.Shapes.Select(shape => shape.Number));
        Assert.Equal(found.Shapes[0].Points, found.ShapePoints);

        // Выбор другой формы меняет подсветку: на доске остаются только её камни.
        found.SelectShape(1);

        Assert.Equal(found.Shapes[1].Points, found.ShapePoints);
        // Формы могут делить камни (пасть тигра и ноби на одном участке), но это разные наборы:
        // подсветка переключается на камни выбранной формы.
        Assert.NotEqual(found.Shapes[0].Points, found.Shapes[1].Points);
    }
}
