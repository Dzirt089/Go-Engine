using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты экранов оболочки и подразделов обучения.</summary>
/// <remarks>
/// <para>
/// Экранов три: главный — выбор режима, партия с ИИ, обучение. «Партия с другом» — сетевое лобби
/// поверх содержимого: режим оболочки оно не меняет. Задачи — третий подраздел обучения, а не
/// отдельный режим (уточнение интерфейса 2026-11-09, ориентир — главные экраны BadukPop,
/// Go Quest и OGS: один явный главный ход и понятные продолжения).
/// </para>
/// <para>
/// Подсветку экрана показывает модель оболочки, а не вид: так поведение проверяется без
/// интерфейса. Стартовое состояние и карточки главного экрана проверяет <c>ShellStartTests</c>.
/// </para>
/// </remarks>
public sealed class ShellSectionsTests
{
    [Fact]
    public void Вначале_Выбран_Главный_Экран()
    {
        var shell = Shell();

        Assert.True(shell.IsHomeSection);
    }

    [Fact]
    public void Вначале_Выбран_Режим_Главного_Экрана()
    {
        var shell = Shell();

        Assert.True(shell.IsHomeMode);
        Assert.False(shell.IsGameMode);
        Assert.False(shell.IsLessonMode);
    }

    [Fact]
    public void Вначале_Раздел_Меню_Не_Выбран()
    {
        var shell = Shell();

        Assert.False(shell.IsMenuSection);
    }

    [Fact]
    public void Вначале_Обучение_Не_Выбрано()
    {
        var shell = Shell();

        Assert.False(shell.IsLessonSection);
    }

    [Fact]
    public void Вначале_В_Обучении_Выбраны_Уроки()
    {
        Assert.True(Shell().IsLessonsStudy);
    }

    [Fact]
    public void Раздел_Меню_Выделяет_Свой_Раздел()
    {
        var shell = Shell();

        shell.ShowMenu();

        Assert.True(shell.IsMenuSection);
    }

    [Fact]
    public void Раздел_Меню_Снимает_Подсветку_Главного_Экрана()
    {
        var shell = Shell();

        shell.ShowMenu();

        Assert.False(shell.IsHomeSection);
    }

    [Fact]
    public void Раздел_Меню_Не_Меняет_Режим_Главного_Экрана()
    {
        // Меню — раздел приложения, а не третий режим: за ним остаётся выбранный экран.
        var shell = Shell();

        shell.ShowMenu();

        Assert.True(shell.IsHomeMode);
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
    public void Переход_В_Партию_Из_Меню_Возвращает_Раздел_Партии()
    {
        var shell = Shell();

        shell.ShowMenu();
        shell.ShowGame();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Переход_В_Обучение_Из_Меню_Выделяет_Его_Раздел()
    {
        var shell = Shell();

        shell.ShowMenu();
        shell.ShowLessons();

        Assert.True(shell.IsLessonSection);
    }

    [Fact]
    public void Переход_На_Главную_Из_Задач_Возвращает_Главный_Экран()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);
        shell.ShowHome();

        Assert.True(shell.IsHomeSection);
    }

    [Fact]
    public void Переход_На_Главную_Снимает_Режим_Обучения()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);
        shell.ShowHome();

        Assert.False(shell.IsLessonMode);
    }

    [Fact]
    public void Раздел_Обучения_Выделяет_Свой_Раздел()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.True(shell.IsLessonSection);
    }

    [Fact]
    public void Раздел_Обучения_Снимает_Подсветку_Главного_Экрана()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.False(shell.IsHomeSection);
    }

    [Fact]
    public void Раздел_Обучения_Выбирает_Свой_Режим()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.True(shell.IsLessonMode);
    }

    [Fact]
    public void Подсказка_Обучения_Называет_Уроки()
    {
        var shell = Shell();

        shell.ShowLessons();

        Assert.Contains("урок", shell.ModeHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Обучение_Открывает_Разбор_Партий()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Reviews);

        Assert.True(shell.IsReviewsStudy);
        Assert.False(shell.IsLessonsStudy);
    }

    [Fact]
    public void Обучение_Открывает_Задачи()
    {
        // Задачи — третий подраздел обучения, а не отдельный экран (уточнение интерфейса
        // 2026-11-09): порядок подразделов уроки → партии → задачи.
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);

        Assert.True(shell.IsProblemsStudy);
        Assert.True(shell.IsLessonSection);
    }

    [Fact]
    public void Задачи_Не_Меняют_Режим_Обучения()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);

        Assert.True(shell.IsLessonMode);
        Assert.False(shell.IsGameMode);
    }

    [Fact]
    public void Подсказка_Задач_Называет_Задачи()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);

        Assert.Contains("Задачи", shell.ModeHint, StringComparison.Ordinal);
    }

    [Fact]
    public void Возврат_К_Урокам_Из_Задач()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);
        shell.ShowStudy(StudyPart.Lessons);

        Assert.True(shell.IsLessonsStudy);
        Assert.False(shell.IsProblemsStudy);
    }

    [Fact]
    public void Переход_В_Партию_Из_Обучения_Возвращает_Раздел_Партии()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);
        shell.ShowGame();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Переход_В_Партию_Из_Обучения_Снимает_Раздел_Обучения()
    {
        var shell = Shell();

        shell.ShowStudy(StudyPart.Problems);
        shell.ShowGame();

        Assert.False(shell.IsLessonSection);
    }

    [Fact]
    public void Смена_Раздела_Сообщает_Обо_Всех_Свойствах_Навигации()
    {
        var shell = Shell();
        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.ShowMenu();

        Assert.Contains(nameof(ShellViewModel.IsHomeSection), changed);
        Assert.Contains(nameof(ShellViewModel.IsGameSection), changed);
        Assert.Contains(nameof(ShellViewModel.IsLessonSection), changed);
        Assert.Contains(nameof(ShellViewModel.IsMenuSection), changed);
    }

    [Fact]
    public void Смена_Экрана_Сообщает_О_Подсказке()
    {
        // Подсказка экрана стоит строкой в настольной оболочке и меняется вместе с экраном.
        var shell = Shell();
        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.ShowStudy(StudyPart.Problems);

        Assert.Contains(nameof(ShellViewModel.ModeHint), changed);
    }

    [Fact]
    public void Смена_Подраздела_Сообщает_О_Подразделах()
    {
        var shell = Shell();
        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.ShowStudy(StudyPart.Reviews);

        Assert.Contains(nameof(ShellViewModel.IsLessonsStudy), changed);
        Assert.Contains(nameof(ShellViewModel.IsReviewsStudy), changed);
        Assert.Contains(nameof(ShellViewModel.IsProblemsStudy), changed);
    }

    [Fact]
    public void Повторный_Выбор_Главного_Экрана_Ничего_Не_Сообщает()
    {
        // Иначе вид перерисовывался бы на каждое нажатие уже выбранной кнопки.
        var shell = Shell();
        var changed = 0;
        shell.PropertyChanged += (_, _) => changed++;

        shell.ShowHome();

        Assert.Equal(0, changed);
    }

    [Fact]
    public void Повторный_Выбор_Подраздела_Ничего_Не_Сообщает()
    {
        var shell = Shell();
        shell.ShowStudy(StudyPart.Reviews);
        var changed = 0;
        shell.PropertyChanged += (_, _) => changed++;

        shell.ShowStudy(StudyPart.Reviews);

        Assert.Equal(0, changed);
    }

    /// <summary>Собирает оболочку с детерминированной партией и встроенными библиотеками.</summary>
    /// <returns>Модель оболочки.</returns>
    private static ShellViewModel Shell() =>
        ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null);
}
