using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты выбора уровней AI для доски — T-034, T-035 (D-038, D-039).</summary>
/// <remarks>
/// Уровни Дан предлагаются тогда и только тогда, когда для выбранной доски есть модель:
/// основная сеть годится для 13×13 и 19×19, для 9×9 нужна специализированная. Проверяется
/// именно это правило, а не конкретный размер доски.
/// </remarks>
public sealed class LevelChooserTests
{
    [Fact]
    public void Уровень_Дан_Доступен_Когда_Для_Размера_Есть_Модель()
    {
        Assert.Contains(DifficultyLevel.Dan5, LevelChooser.Available(BoardSize.Size19, true));
    }

    [Fact]
    public void Уровень_Дан_Доступен_И_На_9x9_Если_Модель_Есть()
    {
        // Специализированная модель делает 9×9 таким же полноправным размером (D-039).
        Assert.Contains(DifficultyLevel.Dan5, LevelChooser.Available(BoardSize.Size9, true));
    }

    [Fact]
    public void Без_Модели_Для_Размера_Уровень_Дан_Скрыт()
    {
        Assert.DoesNotContain(DifficultyLevel.Dan5, LevelChooser.Available(BoardSize.Size9, false));
    }

    [Fact]
    public void Уровни_Кю_Доступны_На_Любой_Доске_И_Без_Модели()
    {
        Assert.Contains(DifficultyLevel.Kyu5, LevelChooser.Available(BoardSize.Size9, false));
    }

    [Fact]
    public void Недоступный_Уровень_Сбрасывается_На_10_Кю()
    {
        Assert.Equal(DifficultyLevel.Kyu10, LevelChooser.Resolve(DifficultyLevel.Dan5, BoardSize.Size9, false));
    }

    [Fact]
    public void Доступный_Уровень_Остаётся_Выбранным()
    {
        Assert.Equal(DifficultyLevel.Kyu15, LevelChooser.Resolve(DifficultyLevel.Kyu15, BoardSize.Size9, false));
    }

    [Fact]
    public void Уровень_Дан_Остаётся_Выбранным_При_Наличии_Модели()
    {
        Assert.Equal(DifficultyLevel.Dan5, LevelChooser.Resolve(DifficultyLevel.Dan5, BoardSize.Size19, true));
    }

    [Fact]
    public void Подпись_Уровня_Дан_Читается_Как_Дан()
    {
        Assert.Equal("5 дан", LevelChooser.Label(DifficultyLevel.Dan5));
    }

    [Fact]
    public void Подпись_Уровня_Кю_Читается_Как_Кю()
    {
        Assert.Equal("10 кю", LevelChooser.Label(DifficultyLevel.Kyu10));
    }
}
