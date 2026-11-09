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

/// <summary>Тесты стартового состояния приложения и переходов между экранами оболочки.</summary>
/// <remarks>
/// <para>
/// Регрессия 2026-11-09: приложение запускалось прямо в партию, и ребёнок не понимал, что уже
/// играет с ИИ; «Партия с другом» была спрятана в меню. Теперь оболочка открывается на главном
/// экране — выборе режима крупными карточками (BadukPop, Go Quest и OGS делают так же), партия
/// сама не начинается, а часы стоят на нуле.
/// </para>
/// <para>
/// Экранов три: главный, партия с ИИ и обучение; задачи — третий подраздел обучения, а не
/// отдельный раздел. «Партия с другом» — сетевое лобби поверх содержимого.
/// </para>
/// <para>
/// Вид собирается без окна: библиотека <c>Avalonia.Headless</c> в проект не добавлена и новых
/// пакетов проект не заводит, а графическое окно в этой среде одно на машину (<c>AGENTS.md</c>,
/// п. 14). Раскладка выбирается по размеру вида, поэтому вид измеряется и укладывается вручную —
/// так же, как это делает окно. Проверяются состояние вида и разметки, а не пиксели.
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
    /// Значки карточек и навигации — фигуры <c>Path</c>: их геометрия создаётся платформенным
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

    // --- Главный экран ---------------------------------------------------------------

    [Fact]
    public void На_Телефоне_При_Запуске_Показан_Главный_Экран()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Assert.True(shell.IsHomeSection);
        Assert.True(view.HomeArea!.IsVisible);
        Assert.False(view.GameArea!.IsVisible);
    }

    [Fact]
    public void В_Настольном_Окне_При_Запуске_Показан_Главный_Экран()
    {
        var (view, shell) = Start();
        InWindow(view);

        Assert.True(shell.IsHomeSection);
        Assert.True(view.HomeArea!.IsVisible);
    }

    [Fact]
    public void Главный_Экран_Показывает_Три_Карточки()
    {
        // Три режима, без отдельной карточки задач: они внутри обучения (уточнение 2026-11-09).
        var (view, _) = Start();
        OnPhone(view);

        Assert.NotNull(view.HomeArea!.FindControl<Button>("GameCard"));
        Assert.NotNull(view.HomeArea!.FindControl<Button>("NetworkCard"));
        Assert.NotNull(view.HomeArea!.FindControl<Button>("LessonCard"));
        Assert.Null(view.HomeArea!.FindControl<Button>("ProblemCard"));
    }

    [Fact]
    public void Главный_Экран_Не_Начинает_Партию()
    {
        // Партия стартует только первым ходом игрока после явного выбора режима.
        var (view, shell) = Start();
        OnPhone(view);

        Assert.False(shell.Game.IsGameStarted);
        Assert.Equal("00:00", shell.Game.ClockDisplay);
        Assert.True(shell.Game.IsFirstMove);
    }

    [Fact]
    public void На_Телефоне_Карточки_В_Один_Столбец()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.True(view.HomeArea!.IsCompactLayout);
        Assert.Equal(PhoneWidth - 40, view.HomeArea.FindControl<StackPanel>("HomeContent")!.Width);
    }

    [Fact]
    public void В_Настольном_Окне_Карточки_В_Два_Столбца()
    {
        var (view, _) = Start();
        InWindow(view);

        Assert.False(view.HomeArea!.IsCompactLayout);
        Assert.Equal(640, view.HomeArea.FindControl<StackPanel>("HomeContent")!.Width);
    }

    [Fact]
    public void Карточка_Партии_С_ИИ_Открывает_Доску()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.HomeArea!.FindControl<Button>("GameCard")!);

        Assert.True(shell.IsGameSection);
        Assert.True(view.GameArea!.IsVisible);
        Assert.False(view.HomeArea!.IsVisible);
    }

    [Fact]
    public void Карточка_Партии_С_ИИ_Не_Начинает_Партию()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.HomeArea!.FindControl<Button>("GameCard")!);

        Assert.False(shell.Game.IsGameStarted);
        Assert.Equal("00:00", shell.Game.ClockDisplay);
    }

    [Fact]
    public void Переход_В_Партию_Не_Открывает_Шторку_Меню()
    {
        // Главный экран заменил стартовую шторку меню партии: действия партии — за кнопкой ☰.
        var (view, _) = Start();
        OnPhone(view);

        Click(view.HomeArea!.FindControl<Button>("GameCard")!);

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Карточка_Обучения_Открывает_Обучение()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.HomeArea!.FindControl<Button>("LessonCard")!);

        Assert.True(shell.IsLessonSection);
        Assert.True(view.LessonArea!.IsVisible);
        Assert.True(view.FindControl<Border>("StudySwitch")!.IsVisible);
    }

    [Fact]
    public void Карточка_Партии_С_Другом_Поднимает_Событие()
    {
        // Сетевое лобби поднимает сама оболочка: у карточки проверяется событие, без сети.
        var home = new HomeView();
        var requested = false;
        home.NetworkRequested += (_, _) => requested = true;

        Click(home.FindControl<Button>("NetworkCard")!);

        Assert.True(requested);
    }

    [Fact]
    public void Строка_Меню_На_Главном_Экране_Открывает_Общее_Меню()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.HomeArea!.FindControl<Button>("MenuButton")!);

        Assert.True(shell.IsMenuSection);
        Assert.True(view.MenuArea!.IsVisible);
    }

    // --- Навигация -------------------------------------------------------------------

    [Fact]
    public void На_Телефоне_В_Навигации_Четыре_Пункта()
    {
        // Главный экран в навигацию не входит: на него возвращает «Главная» общего меню.
        // «Задачи» — подраздел обучения, а не отдельный пункт (уточнение 2026-11-09).
        var (view, _) = Start();
        OnPhone(view);

        Assert.True(view.FindControl<Button>("NavGameButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("NavNetworkButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("NavLessonButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("NavMenuButton")!.IsVisible);
        Assert.Null(view.FindControl<Button>("NavProblemButton"));
        Assert.Null(view.FindControl<Button>("NavHomeButton"));
    }

    [Fact]
    public void Кнопка_Партии_С_ИИ_Открывает_Доску()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.FindControl<Button>("NavGameButton")!);

        Assert.True(shell.IsGameSection);
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
    public void В_Настольной_Строке_Три_Режима()
    {
        // Задачи больше не отдельный режим: в строке «Партия с ИИ», «Партия с другом», «Обучение».
        var (view, _) = Start();
        InWindow(view);

        Assert.NotNull(view.FindControl<ToggleButton>("GameModeButton"));
        Assert.NotNull(view.FindControl<ToggleButton>("NetworkModeButton"));
        Assert.NotNull(view.FindControl<ToggleButton>("LessonModeButton"));
        Assert.Null(view.FindControl<ToggleButton>("ProblemModeButton"));
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
    public void В_Настольной_Строке_Есть_Кнопка_Игры_С_Другом()
    {
        var (view, _) = Start();
        InWindow(view);

        var button = view.FindControl<ToggleButton>("NetworkModeButton");

        Assert.NotNull(button);
        Assert.Equal("Партия с другом", button.Content);
    }

    // --- Обучение: три подраздела ----------------------------------------------------

    [Fact]
    public void В_Обучении_Сначала_Открыты_Уроки()
    {
        var (view, shell) = Start();
        OnPhone(view);

        Click(view.FindControl<Button>("NavLessonButton")!);

        Assert.True(shell.IsLessonSection);
        Assert.True(shell.IsLessonsStudy);
        Assert.True(view.LessonArea!.IsVisible);
        Assert.False(view.ReviewArea!.IsVisible);
        Assert.False(view.ProblemArea!.IsVisible);
        Assert.True(view.FindControl<Border>("StudySwitch")!.IsVisible);
    }

    [Fact]
    public void Переключатель_Обучения_Открывает_Партии()
    {
        // Практика после уроков: разбор партий живёт в том же разделе «Обучение»
        // (замечание пользователя 2026-10-09).
        var (view, shell) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);

        Click(view.FindControl<ToggleButton>("StudyReviewsButton")!);

        Assert.True(shell.IsReviewsStudy);
        Assert.False(view.LessonArea!.IsVisible);
        Assert.True(view.ReviewArea!.IsVisible);
    }

    [Fact]
    public void Переключатель_Обучения_Открывает_Задачи()
    {
        // Задачи — третий подраздел обучения, после уроков и партий (уточнение 2026-11-09).
        var (view, shell) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);

        Click(view.FindControl<ToggleButton>("StudyProblemsButton")!);

        Assert.True(shell.IsProblemsStudy);
        Assert.True(view.ProblemArea!.IsVisible);
        Assert.False(view.LessonArea!.IsVisible);
        Assert.False(view.ReviewArea!.IsVisible);
    }

    [Fact]
    public void Переключатель_Обучения_Возвращает_Уроки()
    {
        var (view, shell) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);
        Click(view.FindControl<ToggleButton>("StudyReviewsButton")!);

        Click(view.FindControl<ToggleButton>("StudyLessonsButton")!);

        Assert.True(shell.IsLessonsStudy);
        Assert.True(view.LessonArea!.IsVisible);
        Assert.False(view.ReviewArea!.IsVisible);
    }

    [Fact]
    public void Переключатель_Обучения_Скрыт_В_Других_Разделах()
    {
        var (view, _) = Start();
        OnPhone(view);

        Assert.False(view.FindControl<Border>("StudySwitch")!.IsVisible);
        Assert.False(view.ReviewArea!.IsVisible);
        Assert.False(view.ProblemArea!.IsVisible);
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
    public void Словарь_Открывается_Из_Разбора_Партии()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);
        Click(view.FindControl<ToggleButton>("StudyReviewsButton")!);

        Click(view.ReviewArea!.FindControl<Button>("GlossaryButton")!);

        Assert.True(view.IsGlossaryShown);
    }

    [Fact]
    public void Разбор_Партии_Прячется_Под_Полным_Экраном()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavLessonButton")!);
        Click(view.FindControl<ToggleButton>("StudyReviewsButton")!);

        Click(view.ReviewArea!.FindControl<Button>("GlossaryButton")!);

        Assert.False(view.FindControl<Panel>("ModeContent")!.IsVisible);
    }

    // --- Общее меню и «О программе» --------------------------------------------------

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
    public void Главная_В_Меню_Возвращает_На_Выбор_Режима()
    {
        // Из игры на главный экран возвращает строка «Главная» общего меню (замечание 2026-11-09).
        var (view, shell) = Start();
        OnPhone(view);
        Click(view.HomeArea!.FindControl<Button>("GameCard")!);
        Click(view.FindControl<Button>("NavMenuButton")!);

        Click(view.MenuArea!.FindControl<Button>("HomeRow")!);

        Assert.True(shell.IsHomeSection);
        Assert.False(shell.IsMenuSection);
        Assert.True(view.HomeArea!.IsVisible);
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

    // --- Системная кнопка «Назад» ----------------------------------------------------

    [Fact]
    public void Назад_Закрывает_Подтверждение_Отмены_Партии()
    {
        // Дефект 1 QA 2026-10-17: отмена партии спрашивает подтверждение, как и выход.
        // Системная «Назад» закрывает вопрос и оставляет партию, а не приложение.
        var (view, shell) = Start();
        OnPhone(view);
        shell.Game.StartGame();
        view.GameArea!.ShowAbortConfirm();

        view.HandleBack();

        Assert.False(view.GameArea!.IsAbortConfirmShown);
        Assert.True(shell.Game.IsGameStarted, "партия отменена по кнопке «Назад»");
    }

    [Fact]
    public void Назад_Закрывает_Подтверждение_Конца_Партии()
    {
        // «Назад» закрывает вопрос, а не заканчивает партию: так же, как у подтверждения отмены.
        var (view, shell) = Start();
        OnPhone(view);
        shell.Game.StartGame();
        view.GameArea!.ShowFinishConfirm();

        view.HandleBack();

        Assert.False(view.GameArea!.IsFinishConfirmShown);
        Assert.True(shell.Game.CanFinishGame, "партия закончена по кнопке «Назад»");
    }

    [Fact]
    public void Назад_Закрывает_Шторку_Меню_Партии()
    {
        var (view, _) = Start();
        OnPhone(view);
        view.GameArea!.ShowMenu();

        view.HandleBack();

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Назад_Возвращает_Из_Общего_Меню_В_Текущий_Раздел()
    {
        // «Меню» режим не меняет: закрыв его, игрок возвращается на главный экран.
        var (view, shell) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavMenuButton")!);

        view.HandleBack();

        Assert.True(shell.IsHomeSection);
    }

    [Fact]
    public void Назад_На_Главном_Экране_Ничего_Не_Закрывает()
    {
        // Закрывать нечего — событие остаётся системе: она решает, выходить ли из приложения.
        var (view, _) = Start();
        OnPhone(view);

        Assert.False(view.HandleBack());
    }

    [Fact]
    public void Назад_В_Партии_Ничего_Не_Закрывает()
    {
        var (view, _) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavGameButton")!);
        view.GameArea!.CloseMenu();

        Assert.False(view.HandleBack());
    }

    // --- Меню партии ----------------------------------------------------------------

    [Fact]
    public void Закрытие_Меню_Прячет_Карточку()
    {
        var (view, _) = Start();
        OnPhone(view);

        view.GameArea!.ShowMenu();
        view.GameArea!.CloseMenu();

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Закрытие_Меню_Оставляет_Партию_На_Экране()
    {
        var (view, shell) = Start();
        OnPhone(view);
        Click(view.FindControl<Button>("NavGameButton")!);

        view.GameArea!.ShowMenu();
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
        Click(view.FindControl<Button>("NavGameButton")!);

        view.GameArea!.ShowMenu();
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
    public void На_Пустой_Доске_Переключатель_Территории_Доступен_Для_Предпросмотра()
    {
        var (view, _) = Start();
        OnPhone(view);

        // Предпросмотр территории доступен всегда (D-115, находка QA-новичка 2026-10-17).
        Assert.True(MobileTerritory(view).IsEnabled);
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
    /// раскладка неизвестна.
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

    /// <summary>Переключатель «Территория» мобильного ряда действий.</summary>
    /// <param name="view">Вид оболочки.</param>
    /// <returns>Переключатель.</returns>
    private static ToggleButton MobileTerritory(ShellView view) =>
        view.GameArea!.FindControl<ToggleButton>("MobileTerritoryButton")!;

    /// <summary>Нажимает кнопку так же, как её нажимает игрок.</summary>
    /// <param name="button">Кнопка разметки.</param>
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
