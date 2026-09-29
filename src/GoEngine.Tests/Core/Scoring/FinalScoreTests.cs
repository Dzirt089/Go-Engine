using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Итог подсчёта: обе величины рядом — по площади и по территории с пленными.</summary>
/// <remarks>
/// Величины держатся отдельно от <see cref="Score"/>: тот отвечает только за площадь
/// (<c>GO_RULES.md</c>, п. 9), а итог партии обязан показать и пленных, и нейтральные точки,
/// и коми. Коми прибавляется белым в момент сравнения и внутрь площадей не входит.
/// </remarks>
public sealed class FinalScoreTests
{
    /// <summary>Итог с заданными площадями и без пленных: коми нулевое.</summary>
    private static FinalScore Area(int blackStones, int blackTerritory, int whiteStones, int whiteTerritory) =>
        new(blackStones, blackTerritory, whiteStones, whiteTerritory, 0, 0, 0, new Komi(0));

    [Fact]
    public void Площадь_ЭтоКамни_Плюс_Территория()
    {
        var score = Area(3, 4, 2, 1);

        Assert.Equal(7, score.BlackArea);
    }

    [Fact]
    public void Площадь_Белых_Без_Коми()
    {
        var score = new FinalScore(0, 0, 2, 1, 0, 0, 0, Komi.For9x9);

        Assert.Equal(3, score.WhiteArea);
    }

    [Fact]
    public void Территориальные_Очки_ЭтоТерритория_Плюс_Пленные()
    {
        var score = new FinalScore(0, 5, 0, 2, 0, 3, 4, new Komi(0));

        Assert.Equal(8, score.BlackTerritoryPoints);
    }

    [Fact]
    public void Территориальные_Очки_Белых_Считают_Их_Пленных()
    {
        var score = new FinalScore(0, 5, 0, 2, 0, 3, 4, new Komi(0));

        Assert.Equal(6, score.WhiteTerritoryPoints);
    }

    [Fact]
    public void Победитель_По_Площади_Учитывает_Коми()
    {
        // Площади равны, но коми 5.5 выводит белых вперёд — так же считает Scorer.Calculate.
        var score = new FinalScore(10, 0, 10, 0, 0, 0, 0, Komi.For9x9);

        Assert.Equal(StoneColor.White, score.AreaWinner);
    }

    [Fact]
    public void Победитель_По_Территории_Учитывает_Коми()
    {
        var score = new FinalScore(0, 1, 0, 1, 0, 1, 1, Komi.For9x9);

        Assert.Equal(StoneColor.White, score.TerritoryWinner);
    }

    [Fact]
    public void Основной_Победитель_Считается_По_Японской_Системе()
    {
        // Японская система объявлена основной: камни на доске очков не приносят, поэтому
        // на такой позиции первенство по площади победителем не становится.
        var score = new FinalScore(5, 0, 2, 0, 74, 0, 0, new Komi(0));

        Assert.Equal(StoneColor.Empty, score.Winner);
    }

    [Fact]
    public void По_Китайской_Системе_Победитель_Считается_По_Площади()
    {
        var score = new FinalScore(5, 0, 2, 0, 74, 0, 0, new Komi(0)) { Rule = ScoringRule.Chinese };

        Assert.Equal(StoneColor.Black, score.Winner);
    }

    [Fact]
    public void Система_По_Умолчанию_Японская()
    {
        Assert.Equal(ScoringRule.Japanese, new FinalScore(0, 0, 0, 0, 81, 0, 0, new Komi(0)).Rule);
    }

    [Fact]
    public void Ничья_Даёт_Пустой_Цвет()
    {
        var score = Area(4, 0, 4, 0);

        Assert.Equal(StoneColor.Empty, score.Winner);
    }

    [Fact]
    public void Коми_Не_Входит_В_Площадь_Белых()
    {
        var score = new FinalScore(1, 1, 1, 1, 0, 0, 0, Komi.For9x9);

        Assert.Equal(2, score.WhiteArea);
    }

    [Fact]
    public void Все_Точки_Доски_Сходятся()
    {
        // Площадь обеих сторон плюс нейтральные точки — ровно доска 9×9.
        var score = new FinalScore(3, 4, 2, 1, 71, 0, 0, Komi.For9x9);

        Assert.Equal(BoardSize.Size9.Area, score.Total);
    }

    [Fact]
    public void Строка_По_Японской_Системе_Показывает_Территорию_С_Пленными()
    {
        // Территориальные очки: у чёрных 4 территории и один пленный, у белых 1 территория и коми.
        var score = new FinalScore(3, 4, 2, 1, 0, 1, 0, Komi.For9x9);

        Assert.Equal("Чёрные 5 : 6.5 Белые", score.ToString());
    }

    [Fact]
    public void Строка_По_Китайской_Системе_Показывает_Площадь()
    {
        var score = new FinalScore(3, 4, 2, 1, 0, 1, 0, Komi.For9x9) { Rule = ScoringRule.Chinese };

        Assert.Equal("Чёрные 7 : 8.5 Белые", score.ToString());
    }

    [Fact]
    public void Разница_По_Площади_Без_Знака()
    {
        var score = new FinalScore(3, 4, 2, 1, 0, 0, 0, new Komi(0));

        Assert.Equal(4, score.AreaMargin);
    }
}
