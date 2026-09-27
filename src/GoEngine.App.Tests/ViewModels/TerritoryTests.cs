using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Разметка территории: что показывается, что считается и когда включается.</summary>
/// <remarks>
/// Заход 1: пользователь просил видеть, кому принадлежат пустые точки, и различать территорию
/// чёрных, белых и нейтральные точки. Считает её <see cref="Scorer.Ownership"/>; тесты держат
/// и состав разметки, и расшифровку в панели партии.
/// </remarks>
public sealed class TerritoryTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Территория_Скрыта_Пока_Партия_Идёт()
    {
        var model = Create();

        Assert.False(model.HasTerritory);
        Assert.Null(model.Territory);
        Assert.Empty(model.TerritorySummary);
    }

    [Fact]
    public void Территория_Включается_Переключателем_Во_Время_Партии()
    {
        var model = Create();

        model.ShowTerritory = true;

        Assert.True(model.HasTerritory);
        Assert.NotNull(model.Territory);
        Assert.Equal(Size.Area, model.Territory!.Count);
        Assert.NotEmpty(model.TerritorySummary);

        model.ShowTerritory = false;

        Assert.False(model.HasTerritory);
        Assert.Null(model.Territory);
    }

    [Fact]
    public void Территория_Показывается_Сама_После_Двух_Пасов()
    {
        var model = Create();
        _ = model.LoadGame(new SgfGame(Size, Komi.For9x9, [Move.Pass(StoneColor.Black), Move.Pass(StoneColor.White)], null));

        Assert.NotEqual("Идёт", model.Status);
        Assert.True(model.HasTerritory);
        Assert.NotNull(model.Territory);
    }

    [Fact]
    public void Разметка_Отличает_Территорию_От_Нейтральных_Точек()
    {
        // Чёрные замкнули угол: точка (0,0) окружена только их камнями, остальная доска спорная.
        var model = Create(StoneColor.White);
        _ = model.LoadGame(CornerGame());
        model.ShowTerritory = true;

        var territory = model.Territory!;

        Assert.Equal(StoneColor.Black, territory[0]);
        Assert.Equal(StoneColor.Empty, territory[(4 * Size.Value) + 4]);
    }

    [Fact]
    public void Расшифровка_Считает_Площадь_Сторон_И_Нейтральные_Точки()
    {
        var model = Create(StoneColor.White);
        _ = model.LoadGame(CornerGame());
        model.ShowTerritory = true;

        // Три чёрных камня и замкнутый угол — 4; два белых камня — 2; остальное нейтрально.
        Assert.Contains("чёрные 4", model.TerritorySummary, StringComparison.Ordinal);
        Assert.Contains("белые 2", model.TerritorySummary, StringComparison.Ordinal);
        Assert.Contains("нейтрально 75", model.TerritorySummary, StringComparison.Ordinal);
        Assert.Contains("коми 5.5", model.TerritorySummary, StringComparison.Ordinal);
    }

    /// <summary>Партия с замкнутым углом чёрных и двумя белыми камнями в открытой позиции.</summary>
    /// <returns>Партия, где ход белых: после загрузки AI не ходит.</returns>
    private static SgfGame CornerGame() =>
        new(
            Size,
            Komi.For9x9,
            [
                Move.Play(new Point(1, 0), StoneColor.Black),
                Move.Play(new Point(8, 8), StoneColor.White),
                Move.Play(new Point(0, 1), StoneColor.Black),
                Move.Play(new Point(7, 8), StoneColor.White),
                Move.Play(new Point(1, 1), StoneColor.Black)
            ],
            null);

    /// <summary>Создаёт модель представления с фиксированным зерном.</summary>
    /// <param name="color">Цвет игрока.</param>
    /// <returns>Модель представления партии 9×9.</returns>
    private static MainViewModel Create(StoneColor? color = null)
    {
        var settings = AppSettings.From(Size, DifficultyLevel.Kyu30, color ?? StoneColor.Black, Komi.For9x9);

        return new MainViewModel(settings, new Random(20260926));
    }
}
