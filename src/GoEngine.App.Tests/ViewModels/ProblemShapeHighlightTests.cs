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
    public void Каждая_Задача_Даёт_Подсветку_После_Решения()
    {
        List<string> errors = [];

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

            if (model.ShapePoints.Count == 0)
            {
                errors.Add(problem.Id);
            }
        }

        Assert.True(errors.Count == 0, $"без подсветки: {string.Join(", ", errors)}");
    }
}
