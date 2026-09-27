using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты списка уровней: состав, подписи и замена недоступного выбора (D-038, D-039, D-048).</summary>
/// <remarks>
/// Список уровней собирается в одном месте для панели партии и экрана настроек. Подпись панели
/// называет движок и размер доски, подпись настроек короче: там размер ещё выбирается.
/// </remarks>
public sealed class LevelListViewTests
{
    [Fact]
    public void Состав_Списка_Партии_Совпадает_С_Доступными_Уровнями()
    {
        var view = LevelListView.ForGame(BoardSize.Size9, modelAvailable: true);

        Assert.Equal(LevelChooser.Available(BoardSize.Size9, true), view.Levels);
    }

    [Fact]
    public void Подпись_Списка_Партии_Совпадает_С_Описанием_Уровня()
    {
        // Регрессия D-048: список обязан называть движок, который действительно сядет играть.
        var view = LevelListView.ForGame(BoardSize.Size13, modelAvailable: true);

        for (var index = 0; index < view.Levels.Count; index++)
        {
            Assert.Equal(
                LevelChooser.Describe(view.Levels[index], BoardSize.Size13, true),
                view.Labels[index]);
        }
    }

    [Fact]
    public void Подпись_Экрана_Настроек_Идёт_Без_Размера_Доски()
    {
        var view = LevelListView.ForSettings(BoardSize.Size13, modelAvailable: true);

        for (var index = 0; index < view.Levels.Count; index++)
        {
            Assert.Equal(LevelChooser.Label(view.Levels[index]), view.Labels[index]);
        }
    }

    [Fact]
    public void Без_Модели_Сетевые_Уровни_В_Список_Не_Попадают()
    {
        var view = LevelListView.ForGame(BoardSize.Size9, modelAvailable: false);

        Assert.DoesNotContain(DifficultyLevel.Kyu1, view.Levels);
        Assert.DoesNotContain(DifficultyLevel.Dan5, view.Levels);
        Assert.Equal(-1, view.IndexOf(DifficultyLevel.Dan5));
    }

    [Fact]
    public void Недоступный_Уровень_Заменяется_Доступным()
    {
        // Модели для 9×9 нет: «5 дан» играть не сможет, выбор откатывается на 10 кю (D-038).
        var view = LevelListView.ForGame(BoardSize.Size9, modelAvailable: false);
        var index = view.IndexOfAvailable(DifficultyLevel.Dan5);

        Assert.Equal(LevelChooser.Resolve(DifficultyLevel.Dan5, BoardSize.Size9, false), view.At(index));
        Assert.Equal(LevelChooser.Fallback, view.At(index));
    }

    [Fact]
    public void Индекс_Вне_Списка_Даёт_Уровень_Сброса()
    {
        var view = LevelListView.ForGame(BoardSize.Size9, modelAvailable: false);

        Assert.Equal(LevelChooser.Fallback, view.At(-1));
        Assert.Equal(LevelChooser.Fallback, view.At(view.Levels.Count));
    }

    [Fact]
    public void С_Моделью_Список_Идёт_До_Пятого_Дана()
    {
        var view = LevelListView.ForSettings(BoardSize.Size19, modelAvailable: true);

        Assert.Equal(DifficultyLevel.Dan5, view.Levels[^1]);
        Assert.Equal(view.Levels.Count - 1, view.IndexOfAvailable(DifficultyLevel.Dan5));
    }
}
