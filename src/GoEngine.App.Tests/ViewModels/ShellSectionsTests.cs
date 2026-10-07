using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты разделов нижней навигации телефона: партия, задачи и общее меню.</summary>
/// <remarks>
/// <para>
/// Разделов три, и настройки среди них больше нет: «Настройка партии» — экран внутри партии,
/// он открывается из меню партии и шестерёнкой в шапке (замечание пользователя 2026-10-06,
/// <c>DECISIONS.md</c>, D-075). Общее меню — раздел приложения: в нём выбор режима, «О программе»
/// и выход, и режим он не меняет, поэтому закрывается возвратом туда же, где игрок был.
/// </para>
/// <para>
/// Подсветку раздела показывает модель оболочки, а не вид: так поведение проверяется без интерфейса.
/// Стартовое состояние — раздел «Партия», закрытая шторка меню партии и закрытое «О программе» —
/// проверяет <c>ShellStartTests</c>: регрессия 2026-10-03 состояла в том, что приложение
/// открывалось на разделе настроек.
/// </para>
/// </remarks>
public sealed class ShellSectionsTests
{
    private static ShellViewModel Shell() =>
        ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null);

    [Fact]
    public void Вначале_Выбран_Раздел_Партии()
    {
        var shell = Shell();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Вначале_Раздел_Меню_Не_Выбран()
    {
        var shell = Shell();

        Assert.False(shell.IsMenuSection);
    }

    [Fact]
    public void Раздел_Меню_Выделяет_Свой_Раздел()
    {
        var shell = Shell();

        shell.ShowMenu();

        Assert.True(shell.IsMenuSection);
    }

    [Fact]
    public void Раздел_Меню_Снимает_Подсветку_Партии()
    {
        var shell = Shell();

        shell.ShowMenu();

        Assert.False(shell.IsGameSection);
    }

    [Fact]
    public void Раздел_Меню_Не_Меняет_Режим_Партии()
    {
        // Меню — раздел приложения, а не третий режим: партия за ним остаётся на месте.
        var shell = Shell();

        shell.ShowMenu();

        Assert.True(shell.IsGameMode);
    }

    [Fact]
    public void Раздел_Меню_Из_Задач_Не_Меняет_Режим_Задач()
    {
        var shell = Shell();

        shell.ShowProblems();
        shell.ShowMenu();

        Assert.True(shell.IsProblemMode);
    }

    [Fact]
    public void Раздел_Меню_Из_Задач_Снимает_Подсветку_Задач()
    {
        var shell = Shell();

        shell.ShowProblems();
        shell.ShowMenu();

        Assert.False(shell.IsProblemSection);
    }

    [Fact]
    public void Переход_В_Партию_Из_Меню_Возвращает_Раздел_Партии()
    {
        var shell = Shell();

        shell.ShowMenu();
        shell.ShowGame();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Переход_В_Задачи_Из_Меню_Выделяет_Их_Раздел()
    {
        var shell = Shell();

        shell.ShowMenu();
        shell.ShowProblems();

        Assert.True(shell.IsProblemSection);
    }

    [Fact]
    public void Раздел_Обучения_Выделяет_Свой_Раздел()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.True(shell.IsLessonSection);
    }

    [Fact]
    public void Раздел_Обучения_Снимает_Подсветку_Партии()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.False(shell.IsGameSection);
    }

    [Fact]
    public void Раздел_Обучения_Выбирает_Свой_Режим()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.True(shell.IsLessonMode);
    }

    [Fact]
    public void Раздел_Меню_Из_Обучения_Не_Меняет_Режим_Обучения()
    {
        var shell = Shell();

        shell.ShowLessons();
        shell.ShowMenu();

        Assert.True(shell.IsLessonMode);
        Assert.False(shell.IsLessonSection);
    }

    [Fact]
    public void Переход_В_Задачи_Из_Обучения_Выделяет_Их_Раздел()
    {
        var shell = Shell();

        shell.ShowLessons();
        shell.ShowProblems();

        Assert.True(shell.IsProblemSection);
    }

    [Fact]
    public void Подсказка_Обучения_Называет_Уроки()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.Contains("урок", shell.ModeHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Переход_В_Задачи_Выделяет_Их_Раздел()
    {
        var shell = Shell();

        shell.ShowProblems();

        Assert.True(shell.IsProblemSection);
    }

    [Fact]
    public void Переход_В_Партию_Из_Задач_Возвращает_Раздел_Партии()
    {
        var shell = Shell();

        shell.ShowProblems();
        shell.ShowGame();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Переход_В_Партию_Из_Задач_Снимает_Раздел_Задач()
    {
        var shell = Shell();

        shell.ShowProblems();
        shell.ShowGame();

        Assert.False(shell.IsProblemSection);
    }

    [Fact]
    public void Смена_Раздела_Сообщает_Обо_Всех_Свойствах_Навигации()
    {
        var shell = Shell();
        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.ShowMenu();

        Assert.Contains(nameof(ShellViewModel.IsGameSection), changed);
        Assert.Contains(nameof(ShellViewModel.IsProblemSection), changed);
        Assert.Contains(nameof(ShellViewModel.IsMenuSection), changed);
    }

    [Fact]
    public void Смена_Режима_Сообщает_О_Подсказке()
    {
        // Подсказка режима стоит строкой в настольной оболочке и меняется вместе с режимом.
        var shell = Shell();
        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.ShowProblems();

        Assert.Contains(nameof(ShellViewModel.ModeHint), changed);
    }

    [Fact]
    public void Повторный_Выбор_Раздела_Ничего_Не_Сообщает()
    {
        // Иначе вид перерисовывался бы на каждое нажатие уже выбранной кнопки.
        var shell = Shell();
        var changed = 0;
        shell.PropertyChanged += (_, _) => changed++;

        shell.ShowGame();

        Assert.Equal(0, changed);
    }
}
