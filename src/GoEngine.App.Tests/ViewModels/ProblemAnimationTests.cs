using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты данных анимации в режиме задач: что появилось и что снято.</summary>
/// <remarks>
/// В задачах снятие камней — обычное дело (задачи на захват), и оно должно показываться так же,
/// как в партии: камни исчезают постепенно, а не рывком.
/// </remarks>
public sealed class ProblemAnimationTests
{
    /// <summary>Позиция с захватом: два белых камня в углу под атари, чёрные снимают их первым ходом.</summary>
    /// <remarks>
    /// Своя позиция нужна потому, что библиотека теперь состоит из задач из источника, и в их
    /// линиях снятия не видно: у ts-001 чёрные жертвуют камень, который белые снимают ответом, а разница
    /// досок до и после пары ходов такую жертву не показывает. Проверяем анимацию там, где снятие есть.
    /// </remarks>
    private const string CaptureProblem =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Захват в углу]GC[Белые два камня в углу в атари.]"
        + "GE[capture]GD[30]GX[3]GW[3]PL[B]GT[ai]AB[ah]AB[bh]AW[ai]AW[bi](;B[ci])(;B[di];W[ci];B[ch]))";

    [Fact]
    public void При_Снятии_Камней_Анимация_Показывает_Снятые()
    {
        var parsed = ProblemSgf.Parse(CaptureProblem, "capture-in-corner");

        Assert.True(parsed.IsSuccess, parsed.Error);

        var model = new ProblemViewModel([parsed.Value!]);
        var hint = model.AcceptedMoves[0];

        model.Play(hint.Point);

        Assert.NotEmpty(model.LastCaptured);
        Assert.NotEqual(StoneColor.Empty, model.LastCapturedColor);

        foreach (var point in model.LastCaptured)
        {
            Assert.True(model.Board.IsEmpty(point), "снятый камень обязан исчезнуть с доски");
        }
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
