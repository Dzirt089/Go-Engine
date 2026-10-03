using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты вида настроек: три подраздела и никакой кнопки «Начать партию».</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-03: настройки были одним длинным списком, а кнопка «Начать партию»
/// внутри них означала «применить и начать». Теперь подразделов три — «Партия», «Подсчёт»
/// и «О программе», — а значения применяются при закрытии экрана: начинает партию тот, кто экран
/// открыл. Проверки идут по разметке: окно в этой среде не открывается (<c>AGENTS.md</c>, п. 14).
/// </remarks>
public sealed class SettingsViewTests
{
    /// <summary>Заголовки подразделов настроек в порядке показа.</summary>
    private static readonly string[] SectionHeaders = ["Партия", "Подсчёт", "О программе"];

    /// <summary>Строки пояснений по системам подсчёта: обе системы и разница между ними.</summary>
    private static readonly string[] ScoringTexts = ["JapaneseText", "ChineseText", "DifferenceText"];

    /// <summary>Поля подраздела «Партия»: чем играем, как звучит ход и когда что вступает в силу.</summary>
    private static readonly string[] GameControls =
        ["SizeBox", "LevelBox", "ColorBox", "KomiBox", "SoundBox", "NewGameHintText"];

    /// <summary>Поля подраздела «Подсчёт»: выбор системы и пояснения к обеим.</summary>
    private static readonly string[] ScoringControls =
        ["ScoringBox", "ScoringHintText", "JapaneseText", "ChineseText", "DifferenceText"];

    /// <summary>Поля подраздела «О программе»: версия, модели, логи и обновление.</summary>
    private static readonly string[] AboutControls =
        ["VersionText", "ModelsText", "LogsText", "OpenLogsButton", "CheckUpdatesButton"];

    [Fact]
    public void Настройки_Состоят_Из_Трёх_Подразделов()
    {
        var view = new SettingsView(AppSettings.Default);

        Assert.Equal(3, Tabs(view).Items.Count);
    }

    [Fact]
    public void Подразделы_Называются_Партия_Подсчёт_О_Программе()
    {
        var view = new SettingsView(AppSettings.Default);

        Assert.Equal(
            SectionHeaders,
            Tabs(view).Items.OfType<TabItem>().Select(item => item.Header as string).ToArray());
    }

    [Fact]
    public void Подраздел_Партия_Содержит_Доску_Уровень_Цвет_Коми_И_Звук()
    {
        var view = new SettingsView(AppSettings.Default);

        AssertInTab(view, 0, GameControls);
    }

    [Fact]
    public void Подраздел_Подсчёт_Содержит_Системы_И_Пояснения()
    {
        var view = new SettingsView(AppSettings.Default);

        AssertInTab(view, 1, ScoringControls);
    }

    [Fact]
    public void Подраздел_О_Программе_Содержит_Версию_Модели_Логи_И_Обновление()
    {
        var view = new SettingsView(AppSettings.Default);

        AssertInTab(view, 2, AboutControls);
    }

    [Fact]
    public void В_Настройках_Нет_Кнопки_Начать_Партию()
    {
        var view = new SettingsView(AppSettings.Default);

        Assert.DoesNotContain(
            view.GetLogicalDescendants().OfType<Button>(),
            button => string.Equals(button.Content as string, "Начать партию", StringComparison.Ordinal));
    }

    [Fact]
    public void Пояснения_По_Системам_Подсчёта_Заполнены()
    {
        var view = new SettingsView(AppSettings.Default);

        Assert.Equal(
            [false, false, false],
            ScoringTexts.Select(name => string.IsNullOrWhiteSpace(view.FindControl<TextBlock>(name)!.Text)));
    }

    [Fact]
    public void Collect_Возвращает_Выбранные_Настройки()
    {
        var current = AppSettings.From(BoardSize.Size13, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.White, Komi.For13x13);
        var view = new SettingsView(current);

        Assert.Equal(current, view.Collect());
    }

    [Fact]
    public void Смена_Системы_Подсчёта_Сообщается_Сразу()
    {
        // Правило подсчёта — не свойство партии: оно действует немедленно, поэтому о смене
        // узнаёт хозяин вида, а не только файл настроек (жалоба пользователя 2026-10-03).
        var view = new SettingsView(AppSettings.Default);
        var changes = 0;
        view.ScoringRuleChanged += (_, _) => changes++;

        view.FindControl<ComboBox>("ScoringBox")!.SelectedIndex = 1;

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Событие_Смены_Системы_Несёт_Выбранное_Правило()
    {
        // Правило идёт в самом событии: у выбора одна точка правды, и хозяин вида не читает
        // список повторно (H1: настольное окно показывает свой экземпляр вида).
        var view = new SettingsView(AppSettings.Default);
        ScoringRule? reported = null;
        view.ScoringRuleChanged += (_, rule) => reported = rule;

        view.FindControl<ComboBox>("ScoringBox")!.SelectedIndex = 1;

        Assert.Equal(ScoringRule.Chinese, reported);
    }

    [Fact]
    public void Смена_Системы_Подсчёта_Попадает_В_Выбранные_Настройки()
    {
        var view = new SettingsView(AppSettings.Default);

        view.FindControl<ComboBox>("ScoringBox")!.SelectedIndex = 1;

        Assert.Equal(ScoringRule.Chinese, view.Selected.ToScoringRule());
    }

    [Fact]
    public void Подраздел_Партия_Подсказывает_Когда_Настройки_Вступают_В_Силу()
    {
        var view = new SettingsView(AppSettings.Default);

        Assert.Contains(
            "Сохранить",
            view.FindControl<TextBlock>("NewGameHintText")!.Text ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Внизу_Экрана_Есть_Кнопка_Сохранить()
    {
        // Регрессия 2026-10-03: у экрана настроек не было ни сохранения, ни отмены.
        var view = new SettingsView(AppSettings.Default);

        Assert.Equal("Сохранить", view.FindControl<Button>("SaveButton")!.Content);
    }

    [Fact]
    public void Внизу_Экрана_Есть_Кнопка_Отмена()
    {
        var view = new SettingsView(AppSettings.Default);

        Assert.Equal("Отмена", view.FindControl<Button>("CancelButton")!.Content);
    }

    [Fact]
    public void Кнопка_Сохранить_Сообщает_О_Согласии()
    {
        var view = new SettingsView(AppSettings.Default);
        var accepted = false;
        view.Accepted += (_, _) => accepted = true;

        Click(view.FindControl<Button>("SaveButton")!);

        Assert.True(accepted);
    }

    [Fact]
    public void Кнопка_Отмена_Сообщает_Об_Отказе()
    {
        var view = new SettingsView(AppSettings.Default);
        var cancelled = false;
        view.Cancelled += (_, _) => cancelled = true;

        Click(view.FindControl<Button>("CancelButton")!);

        Assert.True(cancelled);
    }

    [Fact]
    public void Подписка_Применяет_Смену_Системы_К_Партии()
    {
        // Общий шов обоих хозяев настроек: и вид партии на телефоне, и настольное окно применяют
        // выбранное правило через него (H1). Само окно в тестах не построить — оконной платформы
        // нет, — поэтому проверяется тот же шов на виде настроек.
        var view = new SettingsView(AppSettings.Default);
        var game = new MainViewModel(
            AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            new Random(TestViewModel.Seed));
        SettingsView.ApplyScoringRuleOnChange(view, game);

        view.FindControl<ComboBox>("ScoringBox")!.SelectedIndex = 1;

        Assert.Equal(ScoringRule.Chinese, game.ScoringRule);
    }

    [Fact]
    public void Accept_Сообщает_О_Согласии()
    {
        var view = new SettingsView(AppSettings.Default);
        var accepted = false;
        view.Accepted += (_, _) => accepted = true;

        view.Accept();

        Assert.True(accepted);
    }

    /// <summary>Проверяет, что все названные поля лежат в указанном подразделе.</summary>
    /// <param name="view">Вид настроек.</param>
    /// <param name="index">Номер подраздела.</param>
    /// <param name="names">Имена полей подраздела.</param>
    /// <remarks>Имя поля идёт сообщением проверки: по нему сразу видно, что потерялось.</remarks>
    private static void AssertInTab(SettingsView view, int index, string[] names)
    {
        var tab = TabAt(view, index);
        var inside = tab.GetLogicalDescendants().ToHashSet();

        Assert.All(
            names,
            name => Assert.True(
                view.FindControl<Control>(name) is { } control && inside.Contains(control),
                name));
    }

    /// <summary>Возвращает подраздел настроек по номеру.</summary>
    /// <param name="view">Вид настроек.</param>
    /// <param name="index">Номер подраздела.</param>
    /// <returns>Вкладка настроек.</returns>
    private static TabItem TabAt(SettingsView view, int index) => Tabs(view).Items.OfType<TabItem>().ElementAt(index);

    /// <summary>Список подразделов настроек.</summary>
    /// <param name="view">Вид настроек.</param>
    /// <returns>Переключатель подразделов.</returns>
    private static TabControl Tabs(SettingsView view) => view.FindControl<TabControl>("SettingsTabs")!;

    /// <summary>Нажимает кнопку так же, как её нажимает игрок.</summary>
    /// <param name="button">Кнопка разметки.</param>
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Находит элемент управления настроек по имени.</summary>
    /// <param name="view">Вид настроек.</param>
    /// <param name="name">Имя элемента.</param>
    /// <returns>Элемент разметки; отсутствие элемента — сломанная разметка, а не сценарий теста.</returns>
    private static Control Control(SettingsView view, string name) => view.FindControl<Control>(name)!;
}
