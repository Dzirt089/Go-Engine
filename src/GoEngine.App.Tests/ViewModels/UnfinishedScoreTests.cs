using GoEngine.AI;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Счёт на явно недоигранной позиции: территория не отдаётся, итог не принимается.</summary>
/// <remarks>
/// Регрессия N2 отчёта QA 2026-10-17: после двух пасов на почти пустой доске (один белый камень)
/// наивный подсчёт отдавал белым всю доску — «Чёрные 0 : 85.5 Белые». По правилам территория —
/// это окружённые области, а пустое поле с одним камнем территорией не является. Пока вторая
/// сторона не поставила ни камня, позиция считается недоигранной: счёт показывается без территории
/// (только пленные и коми), победитель не называется, а игроку предлагается доиграть партию.
/// </remarks>
public sealed class UnfinishedScoreTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Два_Паса_На_Почти_Пустой_Доске_Дают_Ноль_Против_Коми()
    {
        var model = CreateFinishedOnOneWhiteStone();

        Assert.Equal("Чёрные 0 : 5.5 Белые", model.Score);
    }

    [Fact]
    public void Недоигранная_Позиция_Помечена_В_Разборе_Счёта()
    {
        var model = CreateFinishedOnOneWhiteStone();

        Assert.Contains("Партия недоиграна: отметьте мёртвые камни или доиграйте", model.ScoreDetail, StringComparison.Ordinal);
        Assert.Contains("территория 0", model.ScoreDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void Недоигранная_Партия_Не_Называет_Победителя()
    {
        var model = CreateFinishedOnOneWhiteStone();

        Assert.Equal("Счёт не принят", model.Outcome);
        Assert.Equal(GameTone.None, model.ResultTone);
        Assert.Equal("Партия недоиграна", model.ResultHeadline);
    }

    [Fact]
    public void Недоигранный_Счёт_Требует_Продолжения()
    {
        var model = CreateFinishedOnOneWhiteStone();

        Assert.True(model.NeedsScoreConfirmation);
        Assert.True(model.ResumeFinishedGame());
        Assert.Equal("Идёт", model.Status);
        Assert.False(model.NeedsScoreConfirmation);
    }

    [Fact]
    public void После_Продолжения_Партию_Можно_Доиграть()
    {
        // Позиция и очередь не меняются: игрок просто возвращается к доске и может ходить.
        var model = CreateFinishedOnOneWhiteStone();

        _ = model.ResumeFinishedGame();

        Assert.True(model.PlayMove(new Point(2, 2)));
    }

    [Fact]
    public void Оба_Цвета_Играли_Счёт_Считается_Обычно()
    {
        // Та же пустая доска, но оба камня на месте: территории нет ни у кого, и хватает коми.
        var model = TestViewModel.Create(StoneColor.Black, DifficultyLevel.Kyu30, size: Size);
        _ = model.LoadGame(new SgfGame(
            Size,
            Komi.For9x9,
            [
                Move.Play(new Point(4, 4), StoneColor.Black),
                Move.Play(new Point(5, 5), StoneColor.White),
                Move.Pass(StoneColor.Black),
                Move.Pass(StoneColor.White)
            ],
            null));

        Assert.False(model.NeedsScoreConfirmation);
        Assert.Equal("Чёрные 0 : 5.5 Белые", model.Score);
    }

    /// <summary>Строит партию, закрытую двумя пасами после одного белого камня.</summary>
    /// <returns>Модель с завершённой партией 9×9; чёрные только пасовали.</returns>
    /// <remarks>
    /// Последовательность как у QA: чёрные пасуют, белые ставят камень, чёрные пасуют снова,
    /// белые пасуют — два паса подряд закрывают партию. Чёрные не поставили ни камня, поэтому
    /// позиция явно недоиграна.
    /// </remarks>
    private static MainViewModel CreateFinishedOnOneWhiteStone()
    {
        var model = TestViewModel.Create(StoneColor.Black, DifficultyLevel.Kyu30, size: Size);

        _ = model.LoadGame(new SgfGame(
            Size,
            Komi.For9x9,
            [
                Move.Pass(StoneColor.Black),
                Move.Play(new Point(4, 4), StoneColor.White),
                Move.Pass(StoneColor.Black),
                Move.Pass(StoneColor.White)
            ],
            null));

        return model;
    }
}
