using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Система подсчёта: японская (основная) и китайская — и её влияние на итог.</summary>
/// <remarks>
/// Систему выбирает регламент турнира; в турнирах РФ чаще объявляют японскую, поэтому основной
/// в приложении объявлена она. Проверяется и сам элемент перечисления, и то, что выбор системы
/// действительно меняет победителя: разница между системами равна разнице числа выставленных
/// сторонами камней, и на несбалансированной позиции победители расходятся.
/// </remarks>
public sealed class ScoringRuleTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Система_Ищется_По_Имени()
    {
        Assert.Equal(ScoringRule.Chinese, Enumeration.FromName<ScoringRule>(nameof(ScoringRule.Chinese)));
    }

    [Fact]
    public void Неизвестное_Имя_Системы_Не_Найдено()
    {
        Assert.Null(Enumeration.TryFromName<ScoringRule>("Ing"));
    }

    [Fact]
    public void Системы_Различаются()
    {
        Assert.NotEqual(ScoringRule.Japanese, ScoringRule.Chinese);
    }

    [Fact]
    public void У_Системы_Есть_Описание_Для_Интерфейса()
    {
        Assert.NotNull(ScoringRule.Japanese.Descriptions);
    }

    [Fact]
    public void Итог_По_Умолчанию_Японский()
    {
        var final = Endgame.Finalize(new Board(Size), [], new Komi(0), 0, 0);

        Assert.Equal(ScoringRule.Japanese, final.Rule);
    }

    [Fact]
    public void Японская_Система_Называет_Победителя_По_Территории()
    {
        // Камней у чёрных больше, территории нет ни у кого: по площади они впереди, по территории — ничья.
        var final = Endgame.Finalize(Unbalanced(), [], new Komi(0), 0, 0, ScoringRule.Japanese);

        Assert.Equal(StoneColor.Empty, final.Winner);
    }

    [Fact]
    public void Китайская_Система_Называет_Победителя_По_Площади()
    {
        var final = Endgame.Finalize(Unbalanced(), [], new Komi(0), 0, 0, ScoringRule.Chinese);

        Assert.Equal(StoneColor.Black, final.Winner);
    }

    [Fact]
    public void Обе_Величины_Считаются_При_Любой_Системе()
    {
        // Система выбирает только победителя: величины остаются посчитанными обеими.
        var final = Endgame.Finalize(Unbalanced(), [], new Komi(0), 0, 0, ScoringRule.Japanese);

        Assert.Equal((5, 0), (final.BlackArea, final.BlackTerritoryPoints));
    }

    /// <summary>Строит позицию с перевесом чёрных по камням и без территории у обеих сторон.</summary>
    /// <returns>Доска 9×9: пять чёрных камней и два белых в открытой позиции.</returns>
    /// <remarks>
    /// Область здесь граничит и с чёрными, и с белыми, поэтому нейтральна целиком. Такая позиция
    /// и нужна: она показывает, что разница систем — это разница числа камней, а не ошибка подсчёта.
    /// </remarks>
    private static Board Unbalanced() =>
        TestPositions.StoneGrid(
            (2, 2, StoneColor.Black),
            (3, 3, StoneColor.Black),
            (4, 4, StoneColor.Black),
            (5, 5, StoneColor.Black),
            (6, 6, StoneColor.Black),
            (8, 6, StoneColor.White),
            (6, 8, StoneColor.White));
}
