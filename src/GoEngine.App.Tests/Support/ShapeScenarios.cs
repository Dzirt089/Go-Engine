using GoEngine.App.ViewModels;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Позиции с несколькими формами: ищутся по встроенному контенту, а не собираются вручную.</summary>
/// <remarks>
/// Форма появляется только из настоящего хода: распознаватель смотрит на группы камней и на
/// историю позиции. Поэтому сценарий берётся из библиотеки задач и партий — так же, как это
/// делает игрок, — и работает при любых правках содержания.
/// </remarks>
internal static class ShapeScenarios
{
    /// <summary>Задача, в которой ходы решения построили больше одной независимой формы.</summary>
    /// <returns>Модель, остановленная на позиции с несколькими формами.</returns>
    /// <exception cref="InvalidOperationException">Во встроенной библиотеке нет такой задачи.</exception>
    public static ProblemViewModel ProblemWithSeveralShapes()
    {
        foreach (var problem in ProblemLibrary.All)
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
                return model;
            }
        }

        throw new InvalidOperationException("во встроенной библиотеке нет задачи с двумя формами");
    }

    /// <summary>Ход разбора партии, где на доске больше одной независимой формы.</summary>
    /// <returns>Модель, стоящая на таком ходе.</returns>
    /// <exception cref="InvalidOperationException">Во встроенных партиях нет такого хода.</exception>
    /// <remarks>
    /// Вопросы разбора проходятся так же, как игроком: ответ на вопрос — ход партии, после
    /// которого разбор идёт дальше. Без этого обход останавливается на первом же вопросе.
    /// </remarks>
    public static ReviewViewModel ReviewWithSeveralShapes()
    {
        for (var gameIndex = 0; gameIndex < ReviewLibrary.All.Count; gameIndex++)
        {
            var model = new ReviewViewModel();
            var game = ReviewLibrary.All[gameIndex];
            model.SelectGame(gameIndex);
            var guard = 0;

            while (guard++ < 200)
            {
                if (model.HasSeveralShapes)
                {
                    return model;
                }

                // Вопрос разбора идёт первым: пока он не отвечен, CanGoNext может быть false,
                // и обход остановился бы на первом же вопросе, не дойдя до ходов с формами.
                if (model.IsQuiz)
                {
                    model.Play(game.Moves[model.MoveNumber].Point);
                    continue;
                }

                if (!model.CanGoNext)
                {
                    break;
                }

                model.Next();
            }
        }

        throw new InvalidOperationException("во встроенных партиях нет хода с двумя формами");
    }
}
