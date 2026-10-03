using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using GoEngine.AI;
using GoEngine.App.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты вида партии: один счёт на экране, ряд действий телефона и баннер итога.</summary>
/// <remarks>
/// <para>
/// Регрессии жалоб пользователя 2026-10-02. №2,а: блок с сообщением (итог партии, приглашение
/// обновления) стоял полосой сверху — теперь он оверлеем по центру экрана. №2,б: панель действий
/// телефона была «плавающей» карточкой по центру доски, заезжала за её нижний край и оставляла
/// пустую колонку, когда «Территория» была скрыта. №3: счёт показывался дважды — в свёрнутой шторке
/// и в «Подробностях».
/// </para>
/// <para>
/// Замечания 2026-10-03: крестики мёртвых групп и знаки территории — только по кнопке «Территория»
/// (доска чистая, числа посчитанного видны всегда), а действия партии («Начать партию», отмена,
/// файлы, настройки) переехали из ряда и меню «Ещё» в карточку «Меню» по центру экрана.
/// </para>
/// <para>
/// Проверки идут по разметке и логическому дереву: окно в этой среде не открывается
/// (<c>AGENTS.md</c>, п. 14), а сторона доски и ширина панели считаются чистыми функциями
/// <c>BoardLayoutRules</c> — их проверяет <c>MobileLayoutRulesTests</c>.
/// </para>
/// </remarks>
public sealed class BoardViewTests
{
    /// <summary>Колонки ряда действий телефона: четыре действия партии, по одному на колонку.</summary>
    private static readonly int[] ActionColumns = [0, 1, 2, 3];

    /// <summary>Пункты меню партии: пять действий, которые не помещаются в ряд телефона.</summary>
    private static readonly string[] MenuCaptions =
    [
        "Начать партию", "Отменить партию", "Сохранить партию", "Загрузить партию", "Настройки"
    ];

    [Fact]
    public void В_Настольной_Панели_Счёт_Показан_Один_Раз()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.Equal(1, CountTexts(view.FindControl<Grid>("Layout")!, model.Score));
    }

    [Fact]
    public void В_Шторке_Телефона_Счёт_Показан_Один_Раз()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.Equal(1, CountTexts(view.FindControl<Grid>("MobileLayout")!, model.Score));
    }

    [Fact]
    public void Объяснение_Счёта_Стоит_Только_В_Подробностях_Панели()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.Equal(1, CountTexts(view.FindControl<Grid>("Layout")!, model.ScoreDetail));
    }

    [Fact]
    public void Объяснение_Счёта_Стоит_Только_В_Подробностях_Шторки()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.Equal(1, CountTexts(view.FindControl<Grid>("MobileLayout")!, model.ScoreDetail));
    }

    [Fact]
    public void Пленные_Показаны_Один_Раз_На_Телефоне()
    {
        // Жалоба 2026-10-02 №3: одна и та же величина стояла и в свёрнутой шторке, и в подробностях.
        var (view, model) = Create();
        Finish(model);

        Assert.Equal(1, CountTexts(view.FindControl<Grid>("MobileLayout")!, model.PrisonersLine));
    }

    [Fact]
    public void Пленные_Убраны_Из_Подробностей_Шторки()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<TextBlock>("MobilePrisonersText"));
    }

    [Fact]
    public void Счёт_Убран_Из_Подробностей_Шторки()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<TextBlock>("MobileScoreText"));
    }

    [Fact]
    public void Счёт_Убран_Из_Строки_Над_Подробностями_Панели()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<TextBlock>("DesktopScoreDetailText"));
    }

    [Fact]
    public void Панель_Согласования_Убрана_Из_Панели_Партии()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<Border>("DesktopCountingPanel"));
    }

    [Fact]
    public void Полоса_Согласования_Убрана_С_Доски_Телефона()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<Border>("MobileCountingBar"));
    }

    [Fact]
    public void Панель_Согласования_Убрана_Из_Подробностей_Шторки()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<Border>("SheetCountingPanel"));
    }

    [Fact]
    public void Кнопки_Посчитать_Больше_Нет()
    {
        // Игрок не подтверждает подсчёт кнопкой: оценка приходит сама (жалоба 2026-10-02).
        var (view, _) = Create();

        Assert.Empty(Buttons(view, "Посчитать"));
    }

    [Fact]
    public void После_Конца_Партии_Приглашения_Проверить_Пометки_Нет()
    {
        // Шага согласования больше нет: после двух пасов на экране только итог, а не просьба
        // подтвердить пометки (жалоба пользователя 2026-10-02 «не хочу ещё куда-то тыкать»).
        var (view, model) = Create();
        Finish(model);

        Assert.Equal(0, CountTexts(view, "Проверьте пометки"));
    }

    [Fact]
    public void После_Конца_Партии_Строки_Согласования_Нет()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.Equal(0, CountTexts(view, "Согласование подсчёта"));
    }

    [Fact]
    public void Разметка_Территории_Выключена_По_Умолчанию_На_Телефоне()
    {
        // Доска чистая до нажатия: крестики и знаки территории игрок просит сам (D-071).
        var (view, _) = Create();

        Assert.False(view.FindControl<ToggleButton>("MobileTerritoryButton")!.IsChecked);
    }

    [Fact]
    public void Разметка_Территории_Выключена_По_Умолчанию_В_Панели_Партии()
    {
        var (view, _) = Create();

        Assert.False(view.FindControl<ToggleButton>("TerritoryButton")!.IsChecked);
    }

    [Fact]
    public void Числа_Разметки_Видны_Даже_При_Выключенной_Разметке()
    {
        // Переключатель прячет картинку на доске, а не счёт: числа посчитанного видны всегда.
        var (view, _) = Create();

        Assert.True(view.FindControl<TextBlock>("MobileBreakdownText")!.IsVisible);
    }

    [Fact]
    public void Территория_Не_Показана_Пока_Разметка_Выключена()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<BoardControl>("Board")!.Territory);
    }

    [Fact]
    public void Крестики_Мёртвых_Групп_Не_Рисуются_Пока_Разметка_Выключена()
    {
        // Оценку перебор даёт и без разметки, но крестики — часть картинки: пока игрок не нажал
        // «Территорию», доска остаётся чистой (замечание пользователя 2026-10-03).
        var (view, model) = Create();
        Finish(model);

        Assert.Null(view.FindControl<BoardControl>("Board")!.DeadPoints);
    }

    [Fact]
    public void Крестики_Мёртвых_Групп_Видны_При_Включённой_Разметке()
    {
        var (view, model) = Create();
        Finish(model);

        model.ShowTerritory = true;

        Assert.NotEmpty(view.FindControl<BoardControl>("Board")!.DeadPoints!);
    }

    [Fact]
    public void Переключатель_Включает_Разметку_Территории()
    {
        var (view, model) = Create();

        model.ShowTerritory = true;

        Assert.NotNull(view.FindControl<BoardControl>("Board")!.Territory);
    }

    [Fact]
    public void Кнопки_Новая_Партия_В_Мобильном_Ряду_Больше_Нет()
    {
        // Действия переехали в меню: ряд стал короче и ровнее (замечание 2026-10-03).
        var (view, _) = Create();

        Assert.Null(view.FindControl<Button>("MobileNewGameButton"));
    }

    [Fact]
    public void Кнопки_Ещё_В_Мобильном_Ряду_Больше_Нет()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<Button>("MobileMoreButton"));
    }

    [Fact]
    public void Меню_Скрыто_При_Запуске()
    {
        var (view, _) = Create();

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Меню_Открывается()
    {
        var (view, _) = Create();

        view.ShowMenu();

        Assert.True(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Меню_Закрывается()
    {
        var (view, _) = Create();
        view.ShowMenu();

        view.CloseMenu();

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Меню_Стоит_По_Центру_Экрана()
    {
        var (view, _) = Create();

        var card = view.FindControl<Border>("MenuCard")!;

        Assert.Equal((HorizontalAlignment.Center, VerticalAlignment.Center), (card.HorizontalAlignment, card.VerticalAlignment));
    }

    [Fact]
    public void Меню_Наложено_На_Затемнение()
    {
        var (view, _) = Create();

        Assert.Contains("app-overlay", MenuOverlay(view).Classes);
    }

    [Fact]
    public void Меню_Содержит_Пять_Пунктов()
    {
        var (view, _) = Create();

        Assert.Equal(
            MenuCaptions,
            ButtonsIn(MenuOverlay(view)).Select(button => button.Content as string).Where(MenuCaptions.Contains).ToArray());
    }

    [Fact]
    public void Меню_Закрывается_Перед_Действием()
    {
        // Нажали «Начать партию» в меню — карточка уходит, игрок смотрит на доску.
        var (view, _) = Create();
        view.ShowMenu();

        Click(view.FindControl<Button>("MenuNewGameButton")!);

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Кнопка_Закрыть_Убирает_Меню()
    {
        var (view, _) = Create();
        view.ShowMenu();

        Click(view.FindControl<Button>("MenuCloseButton")!);

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Меню_Повторным_Нажатием_Закрывается()
    {
        var (view, _) = Create();
        view.ShowMenu();

        view.ToggleMenu();

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Ряд_Действий_Телефона_Делится_На_Четыре_Колонки()
    {
        var (view, _) = Create();

        Assert.Equal(4, ActionRow(view).ColumnDefinitions.Count);
    }

    [Fact]
    public void Колонки_Ряда_Действий_Телефона_Одинаковой_Ширины()
    {
        var (view, _) = Create();

        Assert.True(ActionRow(view).ColumnDefinitions.All(column => column.Width.IsStar));
    }

    [Fact]
    public void Ряд_Действий_Телефона_Заполнен_Целиком()
    {
        // Пустой колонки быть не должно: раньше «Территория» пряталась по признаку доступности,
        // и ряд выглядел сломанным (жалоба 2026-10-02).
        var (view, _) = Create();

        Assert.Equal(
            ActionColumns,
            ActionRow(view).Children.Select(Grid.GetColumn).OrderBy(column => column).ToArray());
    }

    [Fact]
    public void Переключатель_Территории_Остаётся_В_Ряду_Телефона_После_Конца_Партии()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.True(view.FindControl<ToggleButton>("MobileTerritoryButton")!.IsVisible);
    }

    [Fact]
    public void Переключатель_Территории_Остаётся_В_Ряду_Панели_После_Конца_Партии()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.True(view.FindControl<ToggleButton>("TerritoryButton")!.IsVisible);
    }

    [Fact]
    public void Панель_Действий_Растянута_По_Ширине_Вида()
    {
        var (view, _) = Create();

        Assert.Equal(HorizontalAlignment.Stretch, ActionBar(view).HorizontalAlignment);
    }

    [Fact]
    public void Отступы_Панели_Действий_Симметричны()
    {
        var (view, _) = Create();
        var margin = ActionBar(view).Margin;

        Assert.Equal(margin.Left, margin.Right);
    }

    [Fact]
    public void Между_Доской_И_Панелью_Действий_Есть_Зазор()
    {
        var (view, _) = Create();

        Assert.True(ActionBar(view).Margin.Top > 0);
    }

    [Fact]
    public void Строки_Итога_Над_Доской_Больше_Нет()
    {
        var (view, _) = Create();

        Assert.Null(view.FindControl<Border>("MobileResultHost"));
    }

    [Fact]
    public void Баннер_Итога_Один_На_Обе_Раскладки()
    {
        var (view, _) = Create();

        Assert.Single(view.GetLogicalDescendants().OfType<GameResultBanner>());
    }

    [Fact]
    public void Баннер_Итога_Лежит_Поверх_Доски()
    {
        // Оверлей обязан стоять в разметке после раскладок: иначе карточка ушла бы под доску.
        var (view, _) = Create();
        var root = view.FindControl<Panel>("Root")!;
        var banner = view.FindControl<GameResultBanner>("ResultBanner")!;

        Assert.True(root.Children.IndexOf(banner) > root.Children.IndexOf(view.FindControl<Grid>("MobileLayout")!));
    }

    /// <summary>Создаёт вид партии 9×9 вместе с его моделью представления.</summary>
    /// <returns>Вид и модель, которой он показывает партию.</returns>
    /// <remarks>
    /// Модель передаётся виду готовая: тесты доводят партию до конца через ту же модель,
    /// а вид обязан показать это без отдельной команды.
    /// </remarks>
    private static (BoardView View, MainViewModel Model) Create()
    {
        var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9);
        var model = new MainViewModel(settings, new Random(TestViewModel.Seed));

        return (new BoardView(settings, null, model), model);
    }

    /// <summary>Доводит партию до итога: два паса завершают её, подтверждать нечего.</summary>
    /// <param name="model">Модель представления партии.</param>
    private static void Finish(MainViewModel model) => _ = model.LoadGame(DeadCornerGame());

    /// <summary>Считает строки разметки, в которых встречается текст.</summary>
    /// <param name="root">Корень части разметки: раскладка или шторка.</param>
    /// <param name="text">Текст, который ищется в строках.</param>
    /// <returns>Число строк, где текст встречается.</returns>
    /// <remarks>
    /// Сравнение по вхождению, а не по равенству: строка счёта обрамляется словами
    /// («Счёт: …», «Предварительный счёт: …»), и точное равенство пропустило бы дубль.
    /// </remarks>
    private static int CountTexts(ILogical root, string text) =>
        root.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Count(block => block.Text?.Contains(text, StringComparison.Ordinal) == true);

    /// <summary>Карточка меню партии: слой затемнения.</summary>
    /// <param name="view">Вид партии.</param>
    /// <returns>Рамка меню.</returns>
    private static Border MenuOverlay(BoardView view) => view.FindControl<Border>("MenuOverlay")!;

    /// <summary>Все кнопки части разметки.</summary>
    /// <param name="root">Корень разметки.</param>
    /// <returns>Кнопки в порядке обхода дерева.</returns>
    private static IReadOnlyList<Button> ButtonsIn(ILogical root) =>
        [.. root.GetLogicalDescendants().OfType<Button>()];

    /// <summary>Нажимает кнопку так же, как её нажимает игрок.</summary>
    /// <param name="button">Кнопка разметки.</param>
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Кнопки разметки с такой подписью.</summary>
    /// <param name="root">Корень разметки: вид целиком или его часть.</param>
    /// <param name="caption">Подпись кнопки.</param>
    /// <returns>Кнопки, у которых подпись совпала.</returns>
    private static IReadOnlyList<Button> Buttons(ILogical root, string caption) =>
        [.. root.GetLogicalDescendants()
            .OfType<Button>()
            .Where(button => string.Equals(button.Content as string, caption, StringComparison.Ordinal))];

    /// <summary>Ряд действий телефона.</summary>
    /// <param name="view">Вид партии.</param>
    /// <returns>Сетка ряда; отсутствие сетки — сломанная разметка, а не сценарий теста.</returns>
    private static Grid ActionRow(BoardView view) => view.FindControl<Grid>("MobileActionRow")!;

    /// <summary>Панель действий телефона.</summary>
    /// <param name="view">Вид партии.</param>
    /// <returns>Рамка панели.</returns>
    private static Border ActionBar(BoardView view) => view.FindControl<Border>("MobileActionBar")!;

    /// <summary>Строит партию 9×9 с мёртвой белой группой в углу, завершённую двумя пасами.</summary>
    /// <returns>Прочитанная партия для <see cref="MainViewModel.LoadGame"/>.</returns>
    /// <remarks>
    /// Та же позиция, что в <c>GameResultBannerTests</c>: один сценарий конца партии на все тесты
    /// вида. Позиция: чёрные закрыли угол (2,0), (2,1), (0,2), (1,2), внутри остались два белых камня.
    /// </remarks>
    private static SgfGame DeadCornerGame() => new(
        BoardSize.Size9,
        Komi.For9x9,
        [
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 1), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(1, 2), StoneColor.Black),
            Move.Play(new Point(8, 7), StoneColor.White),
            Move.Pass(StoneColor.Black),
            Move.Pass(StoneColor.White)
        ],
        null);
}
