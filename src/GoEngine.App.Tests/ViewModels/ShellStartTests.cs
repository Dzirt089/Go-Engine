using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Skia;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты стартового состояния приложения: раздел партии и карточка «Меню» над ней.</summary>
/// <remarks>
/// <para>
/// Регрессия 2026-10-03: приложение открывалось на экране настроек — на телефоне по признаку
/// одновидового времени жизни (<c>startOnSettings</c>). Жалоба пользователя (правки для Android):
/// «окно меню должно быть первым активным окном над партией. Сейчас первое окно при запуске — это
/// настройки». Теперь оболочка открывается на разделе «Партия», экран настроек закрыт, а на телефоне
/// над доской сразу показана карточка «Меню» (<see cref="BoardView.ShowMenu"/>): игрок сам решает,
/// начинать партию или менять настройки. Настольное окно стартует без карточки — у него есть
/// меню-бар и мышь, а карточка поверх доски только мешала бы; там «Меню» открывает кнопка.
/// </para>
/// <para>
/// Здесь же — вторая жалоба: «в начале матча загорается кнопка территория, которая не отключается».
/// На пустой доске переключатель выключен и недоступен (<see cref="MainViewModel.CanToggleTerritory"/>
/// ложно): территории без камней не бывает, включать нечего.
/// </para>
/// <para>
/// Вид собирается без окна: библиотека <c>Avalonia.Headless</c> в проект не добавлена и новых
/// пакетов проект не заводит, а графическое окно в этой среде одно на машину (<c>AGENTS.md</c>,
/// п. 14). Раскладка выбирается по размеру вида, поэтому вид измеряется и укладывается вручную —
/// так же, как это делает окно. Проверяются состояние вида и разметки, а не пиксели.
/// </para>
/// <para>
/// Здесь же разделы нижней навигации, «О программе» и подтверждение выхода — с порядком обработки
/// системной кнопки «Назад» (<see cref="ShellView.HandleBack"/>): верхнего уровня в тестах нет,
/// поэтому порядок проверяется вызовом самого решения, а сам обработчик только передаёт ему событие.
/// </para>
/// </remarks>
public sealed class ShellStartTests
{
    /// <summary>Размер телефона: узкая раскладка из живой приёмки.</summary>
    private const double PhoneWidth = 380;
    private const double PhoneHeight = 780;

    /// <summary>Размер настольного окна: ширина окна партии по умолчанию (<c>MainWindow</c>).</summary>
    private const double WindowWidth = 920;
    private const double WindowHeight = 720;

    /// <summary>Готовит платформу Skia один раз на всю тестовую сборку, до первого теста.</summary>
    /// <remarks>
    /// <para>
    /// Значки нижней навигации — фигуры <c>Path</c>: их геометрия создаётся платформенным
    /// рендерером, и без него оболочка не собирается вовсе («Unable to locate
    /// IPlatformRenderInterface»). Skia уже подключена к приложению (<c>Avalonia.Skia</c>),
    /// поэтому новых пакетов не нужно, а окно и пиксели проверке по-прежнему не требуются:
    /// <c>Avalonia.Headless</c> в проект не добавляется.
    /// </para>
    /// <para>
    /// Инициализация вынесена в <see cref="ModuleInitializerAttribute"/>, а не в тело теста:
    /// xUnit гоняет классы параллельно, и инициализация «по первому обращению» попадала в момент,
    /// когда другой класс грузил свою разметку, — прогон становился плавающим (нашёл ui:
    /// <c>BoardViewTests</c> падал NRE после загрузки вида, повторный прогон был зелёным).
    /// Так к первому тесту платформа уже готова и гонки нет.
    /// </para>
    /// </remarks>
    [ModuleInitializer]
    internal static void InitializeSkia() => SkiaPlatform.Initialize();

    [Fact]
    public void На_Телефоне_При_Запуске_Выбран_Раздел_Партии()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void На_Телефоне_При_Запуске_Экран_Настроек_Скрыт()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.False(SettingsOverlay(view).IsVisible);
    }

    [Fact]
    public void На_Телефоне_При_Запуске_Открыта_Карточка_Меню()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.True(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void В_Настольном_Окне_При_Запуске_Выбран_Раздел_Партии()
    {
        var (view, shell) = Start();
        InWindow(view);

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void В_Настольном_Окне_При_Запуске_Экран_Настроек_Скрыт()
    {
        var (view, _) = Start();
        InWindow(view);

        Assert.False(SettingsOverlay(view).IsVisible);
    }

    [Fact]
    public void В_Настольном_Окне_При_Запуске_Карточки_Меню_Нет()
    {
        // Настольному окну карточка при запуске не нужна: у него есть меню-бар и мышь,
        // а «Меню» в нижней навигации — элемент мобильной раскладки (требование Lead 2026-10-03).
        var (view, _) = Start();
        InWindow(view);

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void На_Телефоне_В_Навигации_Четыре_Раздела()
    {
        // Разделы: партия, задачи, обучение и общее меню. Настроек среди них нет — «Настройка
        // партии» открывается из шапки партии (замечания пользователя 2026-10-06 и 2026-10-07).
        var (view, _) = Start();
        OnPhone(view);

        Assert.True(view.FindControl<Button>("NavGameButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("NavProblemButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("NavLessonButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("NavMenuButton")!.IsVisible);
        Assert.Null(view.FindControl<Button>("NavSettingsButton"));
    }

    [Fact]
    public void Кнопка_Обучения_Открывает_Раздел_Уроков()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.FindControl<Button>("NavLessonButton")!);

        Assert.True(shell.IsLessonSection);
    }

    [Fact]
    public void Раздел_Обучения_Показывает_Урок_А_Не_Партию()
    {
        var (view, _) = Start();
        OnPhone(view);

        Click(view.FindControl<Button>("NavLessonButton")!);

        Assert.False(view.GameArea!.IsVisible);
        Assert.True(view.LessonArea!.IsVisible);
    }

    [Fact]
    public void Словарь_Терминов_Открывается_Из_Урока()
    {
        // Словарь — вложенный экран раздела «Обучение»: открывается кнопкой панели урока,
        // как «О программе» у раздела «Меню» (замечание пользователя 2026-10-07).
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);

        Click(view.LessonArea!.FindControl<Button>("GlossaryButton")!);

        Assert.True(view.IsGlossaryShown);
    }

    [Fact]
    public void Словарь_Терминов_Закрывается_Кнопкой_Назад()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);
        Click(view.LessonArea!.FindControl<Button>("GlossaryButton")!);

        Click(view.FindControl<Button>("GlossaryBackButton")!);

        Assert.False(view.IsGlossaryShown);
    }

    [Fact]
    public void Назад_Закрывает_Словарь_Терминов()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);
        Click(view.LessonArea!.FindControl<Button>("GlossaryButton")!);

        view.HandleBack();

        Assert.False(view.IsGlossaryShown);
    }

    [Fact]
    public void Словарь_Убирает_Содержимое_Раздела_Из_Кадра()
    {
        // Пока открыт полный экран, содержимое под ним не рисуется: на экране 120 Гц лишняя
        // работа видна как рывки прокрутки словаря (замечание пользователя 2026-10-07).
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);

        Click(view.LessonArea!.FindControl<Button>("GlossaryButton")!);

        Assert.False(view.FindControl<Panel>("ModeContent")!.IsVisible);
    }

    [Fact]
    public void Закрытие_Словаря_Возвращает_Содержимое_В_Кадр()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);
        Click(view.LessonArea!.FindControl<Button>("GlossaryButton")!);

        Click(view.FindControl<Button>("GlossaryBackButton")!);

        Assert.True(view.FindControl<Panel>("ModeContent")!.IsVisible);
    }

    [Fact]
    public void Словарь_Терминов_Содержит_Термины()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.NotEmpty(view.FindControl<GlossaryView>("GlossaryArea")!.ViewModel.Terms);
    }

    [Fact]
    public void В_Настольной_Строке_Есть_Кнопка_Обучения()
    {
        var (view, shell) = Start();
        InWindow(view);

        Click(view.FindControl<ToggleButton>("LessonModeButton")!);

        Assert.True(shell.IsLessonMode);
    }

    [Fact]
    public void Кнопка_Меню_Открывает_Раздел_Общего_Меню()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.FindControl<Button>("NavMenuButton")!);

        Assert.True(shell.IsMenuSection);
    }

    [Fact]
    public void Кнопка_Партии_Закрывает_Общее_Меню()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.FindControl<Button>("NavMenuButton")!);
        Click(view.FindControl<Button>("NavGameButton")!);

        Assert.True(shell.IsGameSection);
        Assert.False(shell.IsMenuSection);
    }

    [Fact]
    public void Экран_О_Программе_Скрыт_При_Запуске()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.False(view.IsAboutShown);
    }

    [Fact]
    public void Экран_О_Программе_Открывается_Из_Общего_Меню()
    {
        // «О программе» — вложенный экран раздела «Меню»: раньше это был подраздел настроек,
        // а настройки принадлежат партии (замечание пользователя 2026-10-06).
        var (view, _) = Start();
        OnPhone(view);

        Click(view.MenuArea!.FindControl<Button>("AboutRow")!);

        Assert.True(view.IsAboutShown);
    }

    [Fact]
    public void Экран_О_Программе_Закрывается_Кнопкой_Назад()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.MenuArea!.FindControl<Button>("AboutRow")!);

        view.HandleBack();

        Assert.False(view.IsAboutShown);
    }

    [Fact]
    public void Подтверждение_Выхода_Скрыто_При_Запуске()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.False(view.IsExitConfirmShown);
    }

    [Fact]
    public void Выход_Из_Меню_Спрашивает_Подтверждение()
    {
        // Выход закрывает программу: случайное нажатие не должно уносить несохранённую партию.
        var (view, _) = Start();
        OnPhone(view);

        Click(view.MenuArea!.FindControl<Button>("ExitRow")!);

        Assert.True(view.IsExitConfirmShown);
    }

    [Fact]
    public void Подтверждение_Выхода_Отменяется()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.MenuArea!.FindControl<Button>("ExitRow")!);

        Click(view.FindControl<Button>("ExitCancelButton")!);

        Assert.False(view.IsExitConfirmShown);
    }

    [Fact]
    public void Назад_Закрывает_Подтверждение_Выхода()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.MenuArea!.FindControl<Button>("ExitRow")!);

        view.HandleBack();

        Assert.False(view.IsExitConfirmShown);
    }

    [Fact]
    public void Назад_Закрывает_Шторку_Меню_Партии()
    {
        var (view, _) = Start();
        OnPhone(view);

        view.HandleBack();

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Назад_Возвращает_Из_Общего_Меню_В_Партию()
    {
        var (view, shell) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavMenuButton")!);

        view.HandleBack();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Назад_В_Партии_Ничего_Не_Закрывает()
    {
        // Закрывать нечего — событие остаётся системе: она решает, выходить ли из приложения.
        var (view, _) = Start();
        OnPhone(view);
        view.GameArea!.CloseMenu();

        Assert.False(view.HandleBack());
    }

    [Fact]
    public void Закрытие_Меню_Прячет_Карточку()
    {
        var (view, _) = Start();
        OnPhone(view);

        view.GameArea!.CloseMenu();

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Закрытие_Меню_Оставляет_Партию_На_Экране()
    {
        var (view, shell) = Start();
        OnPhone(view);

        view.GameArea!.CloseMenu();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Закрытие_Меню_Не_Начинает_Партию()
    {
        // За карточкой — готовая, но не начатая партия: закрытие меню не делает ни хода,
        // ни экрана настроек, игрок видит пустую доску и ждёт его хода.
        var (view, shell) = Start();
        OnPhone(view);

        view.GameArea!.CloseMenu();

        Assert.True(shell.Game.IsFirstMove);
    }

    [Fact]
    public void На_Пустой_Доске_Переключатель_Территории_Выключен()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.False(MobileTerritory(view).IsChecked);
    }

    [Fact]
    public void На_Пустой_Доске_Переключатель_Территории_Недоступен()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.False(MobileTerritory(view).IsEnabled);
    }

    [Fact]
    public void На_Пустой_Доске_Разметка_Территории_Выключена_В_Модели()
    {
        var (_, shell) = Start();

        Assert.False(shell.Game.ShowTerritory);
    }

    /// <summary>Собирает оболочку так же, как её собирают головы, — но с детерминированной партией.</summary>
    /// <returns>Вид оболочки и её модель.</returns>
    /// <remarks>
    /// Вид получает ту же модель партии, что и оболочка, — так же устроено окно
    /// (<c>MainWindow</c>): иначе тест проверял бы не тот вид, который видит игрок. Настройки
    /// и зерно фиксированы (<c>AGENTS.md</c>, п. 6 и 9). Размер вида задаёт тест: до измерения
    /// раскладка неизвестна, и стартовое «Меню» ещё не решено.
    /// </remarks>
    private static (ShellView View, ShellViewModel Shell) Start()
    {
        var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9);
        var game = new MainViewModel(settings, new Random(TestViewModel.Seed));
        var shell = new ShellViewModel(settings, game, new ProblemViewModel());

        return (new ShellView(shell), shell);
    }

    /// <summary>Ставит оболочку в размер телефона: узкая раскладка.</summary>
    /// <param name="view">Вид оболочки.</param>
    private static void OnPhone(ShellView view) => Place(view, PhoneWidth, PhoneHeight);

    /// <summary>Ставит оболочку в размер настольного окна: широкая раскладка.</summary>
    /// <param name="view">Вид оболочки.</param>
    private static void InWindow(ShellView view) => Place(view, WindowWidth, WindowHeight);

    /// <summary>Измеряет и укладывает вид в заданный размер — как это делает окно.</summary>
    /// <param name="view">Вид оболочки.</param>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <remarks>
    /// Окно в тестах не открывается (<c>AGENTS.md</c>, п. 14), а раскладка выбирается по размеру
    /// вида (<see cref="BoardLayoutRules.DecideLayout"/>), поэтому размер задаётся укладкой вручную.
    /// </remarks>
    private static void Place(ShellView view, double width, double height)
    {
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
    }

    /// <summary>Карточка меню партии: слой затемнения с действиями партии.</summary>
    /// <param name="view">Вид оболочки.</param>
    /// <returns>Рамка меню.</returns>
    private static Border MenuOverlay(ShellView view) =>
        view.GameArea!.FindControl<Border>("MenuOverlay")!;

    /// <summary>Экран настроек вида партии: на телефоне он показывается поверх доски.</summary>
    /// <param name="view">Вид оболочки.</param>
    /// <returns>Рамка экрана настроек.</returns>
    private static Border SettingsOverlay(ShellView view) =>
        view.GameArea!.FindControl<Border>("SettingsOverlay")!;

    /// <summary>Переключатель «Территория» мобильного ряда действий.</summary>
    /// <param name="view">Вид оболочки.</param>
    /// <returns>Переключатель.</returns>
    private static ToggleButton MobileTerritory(ShellView view) =>
        view.GameArea!.FindControl<ToggleButton>("MobileTerritoryButton")!;

    /// <summary>Нажимает кнопку так же, как её нажимает игрок.</summary>
    /// <param name="button">Кнопка разметки.</param>
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
