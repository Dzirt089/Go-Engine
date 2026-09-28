using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты данных анимации в режиме задач: что появилось и что снято.</summary>
/// <remarks>
/// В задачах снятие камней — обычное дело (задачи на захват), и оно должно показываться так же,
/// как в партии: камни исчезают постепенно, а не рывком.
/// </remarks>
public sealed class ProblemAnimationTests
{
    [Fact]
    public void При_Снятии_Камней_Анимация_Показывает_Снятые()
    {
        var model = new ProblemViewModel();

        for (var index = 0; index < model.Problems.Count; index++)
        {
            model.SelectedIndex = index;

            var hint = model.AcceptedMoves.Count > 0 ? model.AcceptedMoves[0] : Move.None;

            if (hint.IsNone)
            {
                continue;
            }

            model.Play(hint.Point);

            if (model.LastCaptured.Count == 0)
            {
                continue;
            }

            Assert.NotEqual(StoneColor.Empty, model.LastCapturedColor);

            foreach (var point in model.LastCaptured)
            {
                Assert.True(model.Board.IsEmpty(point), "снятый камень обязан исчезнуть с доски");
            }

            return;
        }

        Assert.Fail("ни одна задача библиотеки не снимает камни первым правильным ходом");
    }

    [Fact]
    public void Неверный_Ход_Не_Меняет_Анимацию()
    {
        var model = new ProblemViewModel();
        var capturedBefore = model.LastCaptured.Count;

        // Точка, которой нет среди принимаемых: ход не решает задачу.
        var wrong = model.Board.AllPoints()
            .First(point => model.Board.IsEmpty(point) && model.AcceptedMoves.All(move => move.Point != point));

        model.Play(wrong);

        Assert.Equal(ProblemViewModel.WrongText, model.Verdict);
        Assert.Equal(capturedBefore, model.LastCaptured.Count);
        Assert.Null(model.LastMove);
    }

    [Fact]
    public void Сброс_И_Переход_Очищают_Анимацию()
    {
        var model = new ProblemViewModel();
        var hint = model.AcceptedMoves[0];

        model.Play(hint.Point);
        model.Reset();

        Assert.Null(model.LastMove);
        Assert.Empty(model.LastCaptured);
        Assert.Equal(StoneColor.Empty, model.LastCapturedColor);

        model.Play(hint.Point);
        model.Next();

        Assert.Null(model.LastMove);
        Assert.Empty(model.LastCaptured);
    }
}
