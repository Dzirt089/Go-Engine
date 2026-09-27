using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты списка размеров доски: состав, подписи, индексы и правило коми (D-051).</summary>
/// <remarks>
/// Размеры доски, их подписи и коми берутся из одного места: раньше список был продублирован
/// в модели представления и в виде настроек, и смена доски не пересчитывала коми.
/// </remarks>
public sealed class BoardSizesTests
{
    [Fact]
    public void Состав_Размеров_Идёт_По_Возрастанию()
    {
        Assert.Equal<IEnumerable<BoardSize>>(
            [BoardSize.Size9, BoardSize.Size13, BoardSize.Size19],
            BoardSizes.All);
    }

    [Fact]
    public void Подписи_Размеров_Читаются_Как_Стороны_Доски()
    {
        Assert.Equal<IEnumerable<string>>(["9×9", "13×13", "19×19"], BoardSizes.Labels);
    }

    [Fact]
    public void Список_Подписей_Не_Пересоздаётся()
    {
        // Список выбора подписан на это свойство: новая коллекция на каждый вызов сбрасывала бы выбор.
        var first = BoardSizes.Labels;
        var second = BoardSizes.Labels;

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(13, 1)]
    [InlineData(19, 2)]
    public void Индекс_Размера_Совпадает_С_Порядком_Списка(int side, int expected)
    {
        Assert.Equal(expected, BoardSizes.IndexOf(new BoardSize((byte)side)));
    }

    [Fact]
    public void Каждый_Размер_Списка_Находится_По_Индексу()
    {
        for (var index = 0; index < BoardSizes.All.Count; index++)
        {
            Assert.Equal(index, BoardSizes.IndexOf(BoardSizes.All[index]));
        }
    }

    [Fact]
    public void Размер_По_Индексу_Берётся_Из_Списка()
    {
        Assert.Equal(BoardSize.Size9, BoardSizes.At(0));
        Assert.Equal(BoardSize.Size19, BoardSizes.At(2));
    }

    [Fact]
    public void Индекс_Вне_Списка_Даёт_Размер_Сброса()
    {
        Assert.Equal(BoardSizes.Fallback, BoardSizes.At(-1));
        Assert.Equal(BoardSizes.Fallback, BoardSizes.At(3));
        Assert.Equal(BoardSizes.All[0], BoardSizes.Fallback);
    }

    [Fact]
    public void Подпись_Размера_Собирается_Из_Стороны()
    {
        Assert.Equal(BoardSize.Size13.ToString(), BoardSizes.Label(BoardSize.Size13));
        Assert.Equal("13×13", BoardSizes.Label(BoardSize.Size13));
    }

    [Theory]
    [InlineData(9, 5.5)]
    [InlineData(13, 5.5)]
    [InlineData(19, 7.5)]
    public void Коми_Берётся_По_Правилам_Доски(int side, double expected)
    {
        var size = new BoardSize((byte)side);

        Assert.Equal(expected, BoardSizes.KomiFor(size).Value, 3);
        Assert.Equal(Komi.For(size), BoardSizes.KomiFor(size));
    }

    [Fact]
    public void Смена_Размера_Доски_Берёт_Коми_По_Правилу()
    {
        // Регрессия D-051: смена доски в настройках не трогала поле коми, и в файл настроек
        // уезжала пара «9×9 + 7.5» — значение, оставшееся от доски 19×19.
        var saved = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, new Komi(7.5));

        // Игрок выбрал 19×19: правило совпадает с прежним значением, поэтому подмена незаметна.
        Assert.Equal(7.5, BoardSizes.KomiFor(BoardSize.Size19).Value, 3);

        // Игрок вернулся на 9×9: правило даёт 5.5, а не 7.5 из сохранённых настроек.
        var applied = AppSettings.From(
            BoardSize.Size9,
            saved.ToDifficultyLevel(),
            saved.ToPlayerColor(),
            BoardSizes.KomiFor(BoardSize.Size9));

        Assert.Equal(5.5, applied.Komi, 3);
        Assert.Equal(5.5, applied.ToKomi().Value, 3);
    }

    [Fact]
    public void Сохранённое_Коми_Не_Затирается_Правилом_При_Открытии_Настроек()
    {
        // Осознанно выставленное игроком коми живёт в настройках: вид применяет правило только
        // на смену размера доски, а при открытии окна показывает сохранённое значение.
        var saved = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, new Komi(6.5));

        Assert.Equal(6.5, saved.ToKomi().Value, 3);
        Assert.Equal(5.5, BoardSizes.KomiFor(saved.ToBoardSize()).Value, 3);
    }
}
