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
using Microsoft.Extensions.Time.Testing;

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

    /// <summary>Пункты меню партии: действия, которые не помещаются в ряд под доской.</summary>
    /// <remarks>
    /// «Настройка партии» называется так же, как экран, который она открывает: раньше это были
    /// «Настройки» вообще, а сведения о программе жили в них же (замечание 2026-10-06).
    /// </remarks>
    private static readonly string[] MenuCaptions =
    [
        "Закончить партию", "Отменить партию", "Сохранить партию", "Загрузить партию",
        "Настройка партии", "Закрыть"
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
        var layout = view.FindControl<Grid>("Layout")!;

        // Три величины — три блока: «стену» из японской и китайской строк читать нельзя
        // (жалоба пользователя 2026-10-11). Каждая строка стоит ровно один раз.
        Assert.Equal(1, CountTexts(layout, model.ScoreJapanese));
        Assert.Equal(1, CountTexts(layout, model.ScoreChinese));
        Assert.Equal(1, CountTexts(layout, model.ScoreDeadGroups));
        Assert.NotEqual(model.ScoreJapanese, model.ScoreChinese);
    }

    [Fact]
    public void Объяснение_Счёта_Стоит_Только_В_Подробностях_Шторки()
    {
        var (view, model) = Create();
        Finish(model);
        var sheet = view.FindControl<Grid>("MobileLayout")!;

        Assert.Equal(1, CountTexts(sheet, model.ScoreJapanese));
        Assert.Equal(1, CountTexts(sheet, model.ScoreChinese));
        Assert.Equal(1, CountTexts(sheet, model.ScoreDeadGroups));
    }

    [Fact]
    public void Карточка_Итога_Видна_В_Подробностях_Шторки()
    {
        // Жалоба пользователя 2026-10-11: «была когда синее объявление об исходе партии
        // в подробностях. Давай вернем». Карточка показывает исход словами и цветом.
        var (view, model) = Create();
        Finish(model);

        var card = view.FindControl<Border>("MobileResultCard")!;

        Assert.True(card.IsVisible, "карточка итога скрыта");
        Assert.Equal(model.ResultHeadline, view.FindControl<TextBlock>("MobileResultHeadline")!.Text);
        Assert.True(
            card.Classes.Contains("result-win") || card.Classes.Contains("result-loss") || card.Classes.Contains("result-draw"),
            "у карточки нет цвета исхода");
    }

    [Fact]
    public void После_Конца_Партии_Шапка_Телефона_Показывает_Окончание()
    {
        // Дефект 2 QA 2026-10-17: после диалога итога шапка пустела, и результат было
        // не посмотреть. После 2026-11-09 шапка короткая: «Партия окончена», а победитель
        // назван одной фразой в свёрнутой шторке — иначе строка обрезалась и дублировала итог.
        var (view, model) = Create();
        Finish(model);

        var text = view.FindControl<TextBlock>("MobileTurnText")!;

        Assert.True(text.IsVisible, "шапка молчит после конца партии");
        Assert.Equal(GameStatusLines.FinishedMark, text.Text);
        Assert.False(view.FindControl<TextBlock>("MobileClockText")!.IsVisible, "часы остались в шапке");
        Assert.Null(view.FindControl<Button>("MobilePauseButton"));

        // Итог одной фразой и счёт отдельной строкой — в свёрнутой шторке.
        var summary = view.FindControl<TextBlock>("SheetSummary")!.Text ?? string.Empty;

        Assert.Equal(
            GameStatusLines.FinalLine(true, model.NeedsScoreConfirmation, model.Outcome, model.Status),
            summary);
        Assert.Contains(model.Outcome, summary, StringComparison.Ordinal);
        Assert.DoesNotContain(GameStatusLines.FinishedMark, summary, StringComparison.Ordinal);
        Assert.Equal(
            GameStatusLines.ScoreLine(false, model.Score),
            view.FindControl<TextBlock>("SheetSummaryDetails")!.Text);
    }

    [Fact]
    public void В_Партии_С_ИИ_Показан_Тип_Партии()
    {
        var (view, _) = Create();

        Assert.Contains(
            "Партия с ИИ",
            view.FindControl<TextBlock>("DesktopStatusText")!.Text,
            StringComparison.Ordinal);
        Assert.False(view.FindControl<Button>("NewGameButton")!.IsVisible);

        // Выбор доски и уровня живёт в меню окна «Партия», а не на панели: панель без дублей.
        Assert.Null(view.FindControl<Grid>("LevelPanel"));
    }

    [Fact]
    public void Настольная_Панель_Не_Дублирует_Меню_Партии()
    {
        // Жалоба владельца 2026-11-12: выбор доски и уровня, «Настройки…», «Сохранить»
        // и «Загрузить» стояли и на панели, и в меню окна «Партия». У действия должно быть
        // одно место: на настольной раскладке — меню окна, на телефоне — меню партии.
        var (view, _) = Create();

        foreach (var name in new[] { "SizePanel", "LevelPanel", "AiFilesRow", "SettingsButton", "SaveButton", "LoadButton" })
        {
            Assert.Null(view.FindControl<Control>(name));
        }

        // Остальные входы те же и не пострадали: меню партии и файлы в шторке телефона.
        var menu = MenuOverlay(view);

        Assert.NotNull(menu.FindControl<Button>("MenuSaveButton"));
        Assert.NotNull(menu.FindControl<Button>("MenuLoadButton"));
        Assert.NotNull(menu.FindControl<Button>("MenuSettingsButton"));
        Assert.NotNull(view.FindControl<Button>("MobileSaveButton"));
        Assert.NotNull(view.FindControl<Button>("MobileLoadButton"));
    }

    [Fact]
    public void В_Партии_С_ИИ_Подробности_Не_Раскрываются_Принудительно()
    {
        var (view, model) = Create();
        var panel = view.FindControl<StackPanel>("DesktopDetailsPanel")!;

        Assert.False(panel.IsVisible);

        var toggle = view.FindControl<ToggleButton>("DesktopDetailsButton")!;
        toggle.IsChecked = true;
        Click(toggle);
        Assert.True(panel.IsVisible);

        model.StartGame();
        Assert.True(panel.IsVisible);
    }

    [Fact]
    public void В_Сетевой_Партии_Скрыты_Элементы_ИИ_И_Показан_Соперник()
    {
        // Замечание пользователя 2026-11-09: в игре с другом не должно быть уровня,
        // нейросети и прочих элементов, которые принадлежат только партии с ИИ.
        var (view, model) = Create();
        var client = new FakeNetworkClient();
        var started = model.StartNetworkGuest(
            client,
            new GoEngine.Multiplayer.GuestSessionStarted(
                "0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.White, 1, 90_000));

        Assert.True(started.IsSuccess, started.Error);
        Assert.Contains("Партия с другом", view.FindControl<TextBlock>("DesktopStatusText")!.Text, StringComparison.Ordinal);
        Assert.False(view.FindControl<Button>("NewGameButton")!.IsVisible);
        Assert.False(view.FindControl<Button>("UndoButton")!.IsVisible);
        Assert.False(view.FindControl<Button>("RedoButton")!.IsVisible);
        Assert.Equal(2, view.FindControl<Grid>("DesktopActionRow")!.ColumnDefinitions.Count);
        Assert.Null(view.FindControl<Button>("MobilePauseButton"));
        Assert.False(view.FindControl<Control>("MobileFilesPanel")!.IsVisible);
        Assert.False(view.FindControl<TextBlock>("MobileStatsText")!.IsVisible);
        Assert.False(view.FindControl<TextBlock>("MobileLevelHintText")!.IsVisible);
        Assert.Equal(2, view.FindControl<Grid>("MobileActionRow")!.ColumnDefinitions.Count);
        Assert.Equal(1, Grid.GetColumn(view.FindControl<Control>("MobileTerritoryButton")!));

        var opponent = view.FindControl<TextBlock>("NetworkOpponentText")!;

        Assert.True(opponent.IsVisible);
        Assert.Contains("Соперник", opponent.Text, StringComparison.Ordinal);
        Assert.Contains("ты: белые", opponent.Text, StringComparison.Ordinal);

        // Соперник на телефоне назван один раз — в свёрнутой шторке; в подробностях
        // повторной строки нет (жалоба 2026-11-09: имя и цвета дублировались).
        Assert.Null(view.FindControl<TextBlock>("MobileNetworkOpponentText"));

        var mobileSummary = view.FindControl<TextBlock>("MobileOpponentSummary")!;

        Assert.True(mobileSummary.IsVisible);
        Assert.Contains("Ты: белые", mobileSummary.Text, StringComparison.Ordinal);
        Assert.Contains("(чёрные)", mobileSummary.Text, StringComparison.Ordinal);

        // «Подробности» в сети не пропадают: в них разбор счёта и пленные, а не уровень ИИ.
        Assert.True(view.FindControl<ToggleButton>("DesktopDetailsButton")!.IsVisible);
        Assert.False(view.FindControl<TextBlock>("DesktopStatsText")!.IsVisible);
    }

    [Fact]
    public void После_Закрытия_Карточки_Итог_Остаётся_В_Панели()
    {
        // Дефект 2 QA 2026-10-17: итог жил только в закрываемом диалоге. «Закрыть» прячет
        // карточку, но не итог: признак конца, победитель и счёт остаются в панели.
        var (view, model) = Create();
        Finish(model);

        var banner = view.FindControl<GameResultBanner>("ResultBanner")!;
        Click(banner.FindControl<Button>("CloseButton")!);

        Assert.False(banner.IsVisible, "карточка итога не закрылась");

        var status = view.FindControl<TextBlock>("DesktopStatusText")!.Text ?? string.Empty;

        Assert.Contains(GameStatusLines.FinishedMark, status, StringComparison.Ordinal);
        Assert.Contains(model.Outcome, status, StringComparison.Ordinal);
        Assert.Contains(
            model.Score,
            view.FindControl<TextBlock>("DesktopScoreText")!.Text ?? string.Empty,
            StringComparison.Ordinal);

        // Способ завершения остаётся на экране, но в «Подробностях»: на главной панели
        // он дублировал «Партия окончена» третьей строкой (жалоба пользователя 2026-11-09).
        var reason = view.FindControl<TextBlock>("DesktopStatusReasonText")!;
        var details = view.FindControl<StackPanel>("DesktopDetailsPanel")!;

        Assert.Contains(reason, details.GetLogicalDescendants().OfType<TextBlock>());
        Assert.Equal(model.Status, reason.Text);
        Assert.False(details.IsVisible, "подробности раскрылись сами после итога");
    }

    [Fact]
    public void Отмена_Партии_Спрашивает_Подтверждение()
    {
        // Дефект 1 QA 2026-10-17: «Отменить партию» обнуляла партию мгновенно, без вопроса.
        var (view, model) = Create();
        model.StartGame();

        var board = model.Board;

        Click(view.FindControl<Button>("DesktopAbortButton")!);

        Assert.True(view.IsAbortConfirmShown, "подтверждение не появилось");
        Assert.True(model.IsGameStarted, "партия отменена до ответа");
        Assert.Same(board, model.Board);
    }

    [Fact]
    public void Отказ_В_Подтверждении_Оставляет_Партию()
    {
        // «Отмена» в вопросе возвращает к партии, а не обнуляет её.
        var (view, model) = Create();
        model.StartGame();

        var board = model.Board;

        Click(view.FindControl<Button>("DesktopAbortButton")!);
        Click(view.FindControl<Button>("AbortCancelButton")!);

        Assert.False(view.IsAbortConfirmShown, "вопрос остался на экране");
        Assert.True(model.IsGameStarted, "партия отменена по отказу");
        Assert.Same(board, model.Board);
    }

    [Fact]
    public void Подтверждение_Отменяет_Партию()
    {
        // Единственный путь к отмене — вторая кнопка вопроса.
        var (view, model) = Create();
        model.StartGame();
        Click(view.FindControl<Button>("DesktopAbortButton")!);

        Click(view.FindControl<Button>("AbortConfirmButton")!);

        Assert.False(view.IsAbortConfirmShown, "вопрос не закрылся");
        Assert.False(model.IsGameStarted, "партия не отменена по подтверждению");
    }

    [Fact]
    public void Меню_Спрашивает_Подтверждение_Отмены()
    {
        // Меню партии — второй вход к отмене: вопрос тот же, партия до ответа не меняется.
        var (view, model) = Create();
        model.StartGame();

        var board = model.Board;

        view.ShowMenu();
        Click(view.FindControl<Button>("MenuAbortButton")!);

        Assert.True(view.IsAbortConfirmShown, "меню отменило партию без вопроса");
        Assert.False(MenuOverlay(view).IsVisible, "меню осталось поверх вопроса");
        Assert.Same(board, model.Board);
    }

    [Fact]
    public void Строка_Сообщения_Не_Входит_В_Нижнюю_Карточку()
    {
        // Дефект 4 QA 2026-10-17: строка статуса стояла в нижней карточке, её появление
        // росло карточку и двигало доску на 29 px. Сообщение — оверлей в месте доски.
        var (view, _) = Create();
        var sheet = view.FindControl<Border>("Sheet")!;
        var hint = view.FindControl<TextBlock>("MobileHint")!;
        var boardRow = view.FindControl<Panel>("MobileBoardRow")!;

        Assert.DoesNotContain(hint, sheet.GetLogicalDescendants().OfType<TextBlock>());
        Assert.Contains(hint, boardRow.GetLogicalDescendants().OfType<TextBlock>());
    }

    [Fact]
    public void Плашка_Сообщения_Не_Ловит_Нажатия()
    {
        // Плашка висит над доской: если бы она ловила нажатия, часть доски стала бы мёртвой.
        var (view, _) = Create();

        Assert.False(view.FindControl<Border>("MobileHintPill")!.IsHitTestVisible);
    }

    [Fact]
    public void Сообщение_Показывается_Плашкой_Над_Доской()
    {
        var (view, _) = Create();

        view.ShowHint("Партия сохранена: game.sgf");

        Assert.True(view.FindControl<Border>("MobileHintPill")!.IsVisible, "плашка не показалась");
        Assert.Equal("Партия сохранена: game.sgf", view.FindControl<TextBlock>("MobileHint")!.Text);
    }

    [Fact]
    public void Подсказка_Паузы_Называет_Действительную_Кнопку()
    {
        // Дефект 5 QA 2026-10-17: подсказка звала нажать «Продолжить», а кнопка — «Дальше».
        var (view, model) = Create();
        model.StartGame();
        model.PauseGame();

        var label = view.FindControl<TextBlock>("MenuPauseText")!;

        Assert.Equal("Дальше", label.Text);
        Assert.Contains($"«{label.Text}»", BoardView.PausedHint, StringComparison.Ordinal);
        Assert.DoesNotContain("Продолжить", BoardView.PausedHint, StringComparison.Ordinal);
    }

    [Fact]
    public void После_Итога_Новая_Партия_Видна_В_Меню()
    {
        // Путь после закрытой итоговой карточки: кнопки «Начать партию» в шапке нет,
        // поэтому «Новая партия» должна появиться в меню партии.
        var (view, model) = Create();
        Finish(model);

        Assert.True(view.FindControl<Button>("MenuNewGameButton")!.IsVisible);
    }

    [Fact]
    public void Пленные_Стоят_Только_В_Подробностях_Шторки()
    {
        // Жалоба 2026-10-02 №3: одна и та же величина стояла и в свёрнутой шторке, и в подробностях.
        // С 2026-11-09 пленные — техническая строка: им место только в подробностях.
        var (view, model) = Create();
        Finish(model);

        var layout = view.FindControl<Grid>("MobileLayout")!;
        var prisoners = view.FindControl<TextBlock>("MobilePrisonersText")!;
        var summary = view.FindControl<StackPanel>("SheetSummaryPanel")!;
        var details = view.FindControl<ScrollViewer>("SheetDetails")!;

        Assert.Equal(1, CountTexts(layout, model.PrisonersLine));
        Assert.Contains(prisoners, details.GetLogicalDescendants().OfType<TextBlock>());
        Assert.DoesNotContain(prisoners, summary.GetLogicalDescendants().OfType<TextBlock>());
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
    public void Технические_Строки_Счёта_Стоят_Только_В_Подробностях_Шторки()
    {
        // Жалоба пользователя 2026-11-09: «Территория — …» и «Камни — …» стояли на главном
        // экране после каждой партии. Техническая разбивка — только в раскрываемых подробностях.
        var (view, _) = Create();

        var summary = view.FindControl<StackPanel>("SheetSummaryPanel")!;
        var details = view.FindControl<ScrollViewer>("SheetDetails")!;
        var breakdown = view.FindControl<TextBlock>("MobileBreakdownText")!;
        var terms = view.FindControl<TextBlock>("MobileTermsHint")!;

        Assert.True(breakdown.IsVisible);
        Assert.Contains(breakdown, details.GetLogicalDescendants().OfType<TextBlock>());
        Assert.DoesNotContain(breakdown, summary.GetLogicalDescendants().OfType<TextBlock>());
        Assert.DoesNotContain(terms, summary.GetLogicalDescendants().OfType<TextBlock>());
        Assert.False(view.FindControl<ScrollViewer>("SheetDetails")!.IsVisible);
    }

    [Fact]
    public void Технические_Строки_Счёта_Стоят_Только_В_Подробностях_Панели()
    {
        var (view, _) = Create();

        var details = view.FindControl<StackPanel>("DesktopDetailsPanel")!;
        var breakdown = view.FindControl<TextBlock>("DesktopBreakdownText")!;
        var prisoners = view.FindControl<TextBlock>("DesktopPrisonersText")!;

        Assert.Contains(breakdown, details.GetLogicalDescendants().OfType<TextBlock>());
        Assert.Contains(prisoners, details.GetLogicalDescendants().OfType<TextBlock>());
        Assert.False(details.IsVisible, "подробности раскрыты по умолчанию");
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
    public void Меню_Стоит_Шторкой_Внизу_Экрана()
    {
        // Карточка по центру заменена шторкой снизу: она ближе к большому пальцу и не закрывает
        // доску целиком (замечание пользователя 2026-10-06).
        var (view, _) = Create();

        var sheet = view.FindControl<Border>("MenuCard")!;

        Assert.Equal(
            (HorizontalAlignment.Stretch, VerticalAlignment.Bottom),
            (sheet.HorizontalAlignment, sheet.VerticalAlignment));
    }

    [Fact]
    public void Меню_Стоит_На_Затемнении()
    {
        var (view, _) = Create();

        Assert.Contains("app-backdrop", view.FindControl<Button>("MenuBackdrop")!.Classes);
    }

    [Fact]
    public void Меню_Закрывается_Нажатием_По_Затемнению()
    {
        var (view, _) = Create();
        view.ShowMenu();

        Click(view.FindControl<Button>("MenuBackdrop")!);

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Меню_Содержит_Пункты_Партии()
    {
        var (view, _) = Create();

        Assert.Equal(
            MenuCaptions,
            ButtonsIn(MenuOverlay(view))
                .Select(LabelOf)
                .Where(caption => MenuCaptions.Contains(caption))
                .ToArray());
    }

    [Fact]
    public void Шапка_Телефона_Открывает_Меню_Партии()
    {
        var (view, _) = Create();

        Click(view.FindControl<Button>("MobileMenuButton")!);

        Assert.True(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Шапка_Телефона_Закрывает_Меню_Повторным_Нажатием()
    {
        var (view, _) = Create();
        view.ShowMenu();

        Click(view.FindControl<Button>("MobileMenuButton")!);

        Assert.False(MenuOverlay(view).IsVisible);
    }

    [Fact]
    public void Шапка_Телефона_Не_Перегружена()
    {
        // После замечания 2026-11-09 шестерёнка и «Пауза» ушли из шапки: в 360 точках
        // они соседствовали с часами и статусом, и часть строки не читалась.
        var (view, _) = Create();

        Assert.Null(view.FindControl<Button>("MobileSettingsButton"));
        Assert.Null(view.FindControl<Button>("MobilePauseButton"));
        Assert.NotNull(view.FindControl<TextBlock>("MobileTurnText"));
        Assert.NotNull(view.FindControl<TextBlock>("MobileClockText"));
    }

    [Fact]
    public void Меню_Открывает_Настройку_Партии()
    {
        var (view, _) = Create();

        Click(view.FindControl<Button>("MenuSettingsButton")!);

        Assert.True(view.FindControl<Border>("SettingsOverlay")!.IsVisible);
    }

    [Fact]
    public void Подпись_Настройки_Называет_Настройку_Партии()
    {
        var (view, _) = Create();

        Assert.Equal("Настройка партии", LabelOf(view.FindControl<Button>("MenuSettingsButton")!));
    }

    [Fact]
    public void Экран_Настроек_Называется_Настройкой_Партии()
    {
        var (view, _) = Create();

        Assert.Equal("Настройка партии", view.FindControl<TextBlock>("SettingsTitle")!.Text);
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
    public void Новая_Партия_Из_Меню_Не_Открывает_Настройки()
    {
        // N6 отчёта QA 2026-10-17: «Новая партия» должна начинать игру, а не показывать
        // настройку партии. Настройки открывает отдельная строка меню — она и остаётся входом.
        var (view, _) = Create();
        view.ShowMenu();

        Click(view.FindControl<Button>("MenuNewGameButton")!);

        Assert.False(view.FindControl<Border>("SettingsOverlay")!.IsVisible);
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
    public void Экран_Настроек_Растянут_На_Весь_Вид()
    {
        // Полноценный экран, а не карточка по центру: ни ограничений ширины, ни скруглений.
        var (view, _) = Create();
        var card = view.FindControl<Border>("SettingsCard")!;

        Assert.Equal(
            (double.PositiveInfinity, double.PositiveInfinity, default(Avalonia.CornerRadius)),
            (card.MaxWidth, card.MaxHeight, card.CornerRadius));
    }

    [Fact]
    public void Экран_Настроек_Закрывается_Кнопкой_Сохранить()
    {
        // Регрессия 2026-10-03: у экрана настроек не было кнопок — игрок не мог ни применить
        // выбор, ни отказаться от него.
        var (view, _) = Create();

        Assert.Equal("Сохранить", SettingsButton(view, "SaveButton").Content);
    }

    [Fact]
    public void Экран_Настроек_Закрывается_Кнопкой_Отмена()
    {
        var (view, _) = Create();

        Assert.Equal("Отмена", SettingsButton(view, "CancelButton").Content);
    }

    [Fact]
    public void Новая_Партия_Из_Меню_Начинает_Игру()
    {
        // N6 отчёта QA 2026-10-17: «Новая партия» больше не открывает настройки, а сразу
        // начинает новую игру — иначе новичок видел настройки вместо партии.
        var (view, model) = Create();
        var before = model.Board;

        Click(view.FindControl<Button>("MenuNewGameButton")!);

        Assert.NotSame(before, model.Board);
        Assert.False(view.FindControl<Border>("SettingsOverlay")!.IsVisible);
    }

    [Fact]
    public void Новая_Партия_Из_Меню_Открывает_Подраздел_Партия()
    {
        var (view, _) = Create();

        Click(view.FindControl<Button>("MenuNewGameButton")!);

        Assert.Equal(0, SettingsTabs(view).SelectedIndex);
    }

    [Fact]
    public void Сохранение_Из_Меню_Начинает_Новую_Партию()
    {
        var (view, model) = Create();
        var board = model.Board;

        Click(view.FindControl<Button>("MenuNewGameButton")!);
        Click(SettingsButton(view, "SaveButton"));

        Assert.NotSame(board, model.Board);
    }

    [Fact]
    public void Новая_Партия_Из_Меню_Не_Меняет_Правило_Счёта()
    {
        var (view, model) = Create();
        var rule = model.ScoringRule;

        Click(view.FindControl<Button>("MenuNewGameButton")!);

        Assert.Equal(rule, model.ScoringRule);
    }

    [Fact]
    public void Сохранение_Из_Раздела_Партию_Не_Начинает()
    {
        // Размер доски, цвет и коми не менялись: партия продолжается, меняется только выбор
        // (жалоба 2026-10-03 «не синхронизирован уровень AI»).
        var (view, model) = Create();
        var board = model.Board;

        view.ShowSettings();
        Click(SettingsButton(view, "SaveButton"));

        Assert.Same(board, model.Board);
    }

    [Fact]
    public void Отмена_Из_Раздела_Партию_Не_Начинает()
    {
        var (view, model) = Create();
        var board = model.Board;

        view.ShowSettings();
        Click(SettingsButton(view, "CancelButton"));

        Assert.Same(board, model.Board);
    }

    [Fact]
    public void Настройки_Открываются_С_Настройками_Партии()
    {
        // Диалог обязан показывать то, чем играет партия: раньше он открывался со своей копией
        // настроек и показывал «25 кю», когда панель играла «10 кю» (регрессия 2026-10-03).
        var (view, model) = Create();
        model.ApplySettings(AppSettings.From(
            BoardSize.Size13, DifficultyLevel.Kyu20, StoneColor.White, Komi.For13x13, ScoringRule.Japanese));

        view.ShowSettings();

        Assert.Equal(BoardSize.Size13.Value, SettingsArea(view).Collect().BoardSize);
    }

    [Fact]
    public void Сохранение_Уровня_Меняет_Настройки_Партии()
    {
        var (view, model) = Create();
        view.ShowSettings();
        SettingsBox(view, "LevelBox").SelectedIndex = 1;

        Click(SettingsButton(view, "SaveButton"));

        Assert.Equal(SettingsArea(view).Collect().ToDifficultyLevel(), model.Settings.ToDifficultyLevel());
    }

    [Fact]
    public void Сохранение_Размера_Доски_Начинает_Новую_Партию()
    {
        // Размер доски — свойство позиции: продолжать партию на другой доске нельзя.
        var (view, model) = Create();
        var board = model.Board;
        view.ShowSettings();
        SettingsBox(view, "SizeBox").SelectedIndex = 1;

        Click(SettingsButton(view, "SaveButton"));

        Assert.NotSame(board, model.Board);
    }

    [Fact]
    public void Отмена_Возвращает_Систему_Подсчёта()
    {
        // Правило подсчёта действует сразу, пока экран открыт: «Отмена» обязана вернуть прежнее
        // (жалоба 2026-10-03 «не хватает кнопки отмена»).
        var (view, model) = Create();
        view.ShowSettings();
        ScoringBox(view).SelectedIndex = 1;

        Click(SettingsButton(view, "CancelButton"));

        Assert.Equal(ScoringRule.Japanese, model.ScoringRule);
    }

    [Fact]
    public void Смена_Системы_Подсчёта_В_Настройках_Действует_Сразу()
    {
        // Игрок переключает «японская/китайская» и ждёт, что победителя назовут по новой системе
        // в текущей партии, а не в следующей (жалоба пользователя 2026-10-03).
        var (view, model) = Create();
        view.ShowSettings();

        ScoringBox(view).SelectedIndex = 1;

        Assert.Equal(ScoringRule.Chinese, model.ScoringRule);
    }

    [Fact]
    public void Смена_Системы_Подсчёта_Меняет_Строку_Счёта_Панели()
    {
        var (view, model) = Create();
        Finish(model);
        view.ShowSettings();
        var before = view.FindControl<TextBlock>("DesktopScoreText")!.Text;

        ScoringBox(view).SelectedIndex = 1;

        Assert.NotEqual(before, view.FindControl<TextBlock>("DesktopScoreText")!.Text);
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
    public void После_Конца_Партии_На_Телефоне_Скрыты_Кнопки_Хода()
    {
        // Жалоба пользователя 2026-11-09: после итога на телефоне оставались две серые
        // кнопки «Пас» и «Территория». После финала ряд хода убирается целиком.
        var (view, model) = Create();
        Finish(model);

        Assert.False(view.FindControl<Button>("MobilePassButton")!.IsVisible);
        Assert.False(view.FindControl<ToggleButton>("MobileTerritoryButton")!.IsVisible);
        Assert.False(view.FindControl<Grid>("MobileActionRow")!.IsVisible);
    }

    [Fact]
    public void После_Конца_Партии_На_Панели_Скрыты_Кнопки_Хода()
    {
        var (view, model) = Create();
        Finish(model);

        Assert.False(view.FindControl<Button>("PassButton")!.IsVisible);
        Assert.False(view.FindControl<ToggleButton>("TerritoryButton")!.IsVisible);
        Assert.False(view.FindControl<Button>("UndoButton")!.IsVisible);
        Assert.False(view.FindControl<Button>("RedoButton")!.IsVisible);
    }

    [Fact]
    public void После_Итога_В_Партии_С_ИИ_Остаётся_Одна_Понятная_Кнопка_Новой_Партии()
    {
        // «Или оставить только осмысленное «Новая партия»»: после итога на телефоне —
        // одно крупное действие вместо ряда мёртвых кнопок.
        var (view, model) = Create();
        Finish(model);

        var postGame = view.FindControl<Grid>("MobilePostGameRow")!;

        Assert.True(postGame.IsVisible);
        Assert.True(view.FindControl<Button>("MobilePostGameNewGameButton")!.IsVisible);
        Assert.False(view.FindControl<Button>("MobilePostGameLeaveButton")!.IsVisible);
        Assert.True(view.FindControl<Button>("NewGameButton")!.IsVisible);
    }

    [Fact]
    public void После_Итога_В_Сетевой_Партии_Телефон_Предлагает_Выйти_Из_Сети()
    {
        // В сети новую партию начать нельзя, пока не вышел: осмысленное действие — выход.
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.Black);
        FinishNetworkGameByResign(client);

        var postGame = view.FindControl<Grid>("MobilePostGameRow")!;

        Assert.True(model.HasOutcome);
        Assert.True(postGame.IsVisible);
        Assert.True(view.FindControl<Button>("MobilePostGameLeaveButton")!.IsVisible);
        Assert.False(view.FindControl<Button>("MobilePostGameNewGameButton")!.IsVisible);
        Assert.False(view.FindControl<Grid>("MobileActionRow")!.IsVisible);
    }

    [Fact]
    public void После_Итога_Территория_Доступна_В_Подробностях()
    {
        // Разметка территории после итога не пропала: она переехала из ряда действий
        // в «Подробности», чтобы на главном экране не оставалось мёртвой кнопки.
        var (view, model) = Create();
        Finish(model);

        Assert.True(view.FindControl<ToggleButton>("MobileDetailsTerritoryButton")!.IsVisible);
        Assert.Contains(
            view.FindControl<ToggleButton>("MobileDetailsTerritoryButton")!,
            view.FindControl<ScrollViewer>("SheetDetails")!.GetLogicalDescendants().OfType<ToggleButton>());
    }

    [Fact]
    public void После_Итога_Статус_И_Счёт_Не_Дублируются_На_Телефоне()
    {
        // Жалоба пользователя 2026-11-09: на телефоне почти одновременно стояли
        // «Партия с другом · Партия окончена», «Завершена сдачей» и счёт. Осталось:
        // короткое «Партия окончена» в шапке и одна фраза итога со счётом в шторке.
        var (view, model) = Create();
        Finish(model);

        var layout = view.FindControl<Grid>("MobileLayout")!;

        Assert.Equal(0, CountTexts(layout, "Завершена"));
        Assert.Equal(0, CountTexts(layout, "Партия с другом · Партия окончена"));
    }

    [Fact]
    public void После_Итога_Счёт_Стоит_Одной_Строкой_В_Шторке()
    {
        var (view, model) = Create();
        Finish(model);

        var scoreLine = view.FindControl<TextBlock>("SheetSummaryDetails")!;

        Assert.True(scoreLine.IsVisible);
        Assert.Equal(GameStatusLines.ScoreLine(false, model.Score), scoreLine.Text);
        Assert.DoesNotContain("\n", scoreLine.Text ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void После_Итога_Соперник_Назван_Один_Раз_На_Телефоне()
    {
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.Black);
        FinishNetworkGameByResign(client);

        var layout = view.FindControl<Grid>("MobileLayout")!;
        var visible = layout.GetLogicalDescendants().OfType<TextBlock>()
            .Where(block => block.IsVisible
                && block.Text?.Contains("Соперник", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Single(visible);
        Assert.Same(view.FindControl<TextBlock>("MobileOpponentSummary"), visible[0]);
    }

    [Fact]
    public void Сдача_Соперника_Даёт_Короткий_Итог_О_Победе()
    {
        // Исходный случай со скриншота: сетевая партия закончена сдачей. Ребёнок должен
        // увидеть «Победили чёрные · сдача», а не три строки протокола подряд.
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.Black);
        FinishNetworkGameByResign(client);

        Assert.Equal("Победили чёрные · сдача", view.FindControl<TextBlock>("SheetSummary")!.Text);
        Assert.Equal(GameStatusLines.FinishedMark, view.FindControl<TextBlock>("MobileTurnText")!.Text);
    }

    [Fact]
    public void Итог_Называет_Способ_Завершения_Из_Состояния_Партии()
    {
        // Страховка от расхождения: слова состояния собирает модель представления,
        // а короткий способ завершения — чистая функция. Тест сверяет их на живой партии.
        var (view, model) = Create();
        Finish(model);

        Assert.Equal("два паса", GameStatusLines.FinishReason(model.Status));
        Assert.Contains(
            "· два паса",
            view.FindControl<TextBlock>("SheetSummary")!.Text ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Толкование_Терминов_Не_Показано_В_Свёрнутой_Шторке()
    {
        // Толкование нужно там, где видны сами термины, и один раз: в подробностях, а не
        // в конце каждой партии на главном экране (жалоба пользователя 2026-11-09).
        var (view, model) = Create();
        Finish(model);

        var summary = view.FindControl<StackPanel>("SheetSummaryPanel")!;
        var terms = view.FindControl<TextBlock>("MobileTermsHint")!;

        Assert.DoesNotContain(terms, summary.GetLogicalDescendants().OfType<TextBlock>());
        Assert.Equal(1, CountTexts(view.FindControl<Grid>("MobileLayout")!, "Пленные — камни"));
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

    [Fact]
    public void Меню_Предлагает_Закончить_Партию()
    {
        // Блокер B1 отчёта QA 2026-10-17: в меню партии не было способа её закончить, и игрок
        // не мог увидеть счёт. Строка доступна, пока партия идёт.
        var (view, model) = Create();
        model.StartGame();
        view.ShowMenu();

        var item = ButtonsIn(MenuOverlay(view))
            .FirstOrDefault(button => string.Equals(LabelOf(button), "Закончить партию", StringComparison.Ordinal));

        Assert.NotNull(item);
        Assert.True(item!.IsEnabled);
    }

    [Fact]
    public void Меню_Закончить_Партию_Спрашивает_Подтверждение()
    {
        // Одно неосторожное нажатие не должно завершать партию — вопрос тот же, что у отмены.
        var (view, model) = Create();
        model.StartGame();
        var board = model.Board;
        view.ShowMenu();

        Click(view.FindControl<Button>("MenuFinishButton")!);

        Assert.True(view.IsFinishConfirmShown, "подтверждение не появилось");
        Assert.False(MenuOverlay(view).IsVisible, "меню осталось поверх вопроса");
        Assert.False(model.HasOutcome, "партия завершена до ответа");
        Assert.Same(board, model.Board);
    }

    [Fact]
    public void Отказ_Заканчивать_Партию_Оставляет_Её_Идущей()
    {
        var (view, model) = Create();
        model.StartGame();
        Click(view.FindControl<Button>("MenuFinishButton")!);

        Click(view.FindControl<Button>("FinishCancelButton")!);

        Assert.False(view.IsFinishConfirmShown, "вопрос остался на экране");
        Assert.False(model.HasOutcome, "партия завершена по отказу");
        Assert.True(model.IsGameStarted);
    }

    [Fact]
    public void Подтверждение_Завершает_Партию_И_Показывает_Итог()
    {
        // После «Закончить партию» счёт посчитан, итог виден в панели и в баннере, а не только
        // в закрываемом диалоге (блокер B1 отчёта QA 2026-10-17).
        var (view, model) = Create();
        model.StartGame();
        Assert.True(model.PlayMove(new Point(4, 4)));
        Click(view.FindControl<Button>("MenuFinishButton")!);

        Click(view.FindControl<Button>("FinishConfirmButton")!);

        Assert.False(view.IsFinishConfirmShown, "вопрос не закрылся");
        Assert.True(model.HasOutcome);
        Assert.Equal("Завершена по согласию сторон", model.Status);
        Assert.Contains(GameStatusLines.FinishedMark, view.FindControl<TextBlock>("DesktopStatusText")!.Text, StringComparison.Ordinal);
        Assert.True(view.FindControl<GameResultBanner>("ResultBanner")!.IsVisible, "итог не показан");
    }

    [Fact]
    public void Переключатель_Территории_Доступен_До_Первого_Хода()
    {
        // Находка QA L7: кнопка «Территория» была серой и не объясняла почему. Предпросмотр
        // безопасен в любой момент — на пустой доске все точки просто нейтральны.
        var (view, _) = Create();

        Assert.True(view.FindControl<ToggleButton>("TerritoryButton")!.IsEnabled);
        Assert.True(view.FindControl<ToggleButton>("MobileTerritoryButton")!.IsEnabled);
    }

    [Fact]
    public void Подсказка_Терминов_Объясняет_Пленных_И_Коми_Один_Раз()
    {
        // Находки QA L3 и L4: «пленные» и «коми» стоят в счёте без расшифровки. Строка живёт
        // только в подробностях (там же, где числа) и звучит одинаково на обеих раскладках.
        var (view, _) = Create();
        var desktop = view.FindControl<TextBlock>("DesktopTermsHint")!;
        var mobile = view.FindControl<TextBlock>("MobileTermsHint")!;
        var text = desktop.Text ?? string.Empty;

        Assert.Contains("Пленные", text, StringComparison.Ordinal);
        Assert.Contains("Коми", text, StringComparison.Ordinal);
        Assert.Contains("чёрные ходят первыми", text, StringComparison.Ordinal);
        Assert.Equal(text, mobile.Text);
        Assert.Contains(desktop, view.FindControl<StackPanel>("DesktopDetailsPanel")!.GetLogicalDescendants().OfType<TextBlock>());
        Assert.Contains(mobile, view.FindControl<ScrollViewer>("SheetDetails")!.GetLogicalDescendants().OfType<TextBlock>());
    }

    [Fact]
    public void Подсказка_Паса_Называет_Способ_Закончить_Партию()
    {
        // Находки QA L5 и B1: пас проходил молча, а конца партии не было. Сообщение называет
        // и сам пас, и строку меню, которая подводит итог.
        Assert.Contains("пропустили", BoardView.PassHint, StringComparison.Ordinal);
        Assert.Contains("Закончить партию", BoardView.PassHint, StringComparison.Ordinal);
    }

    [Fact]
    public void В_Сетевой_Партии_Часы_ИИ_Не_Показаны()
    {
        // R-010: в сетевой партии единственный таймер — ход; часы партии ИИ только путали.
        var (view, model) = Create();
        _ = StartNetworkGuest(model, StoneColor.White);

        Assert.False(view.FindControl<TextBlock>("DesktopClockText")!.IsVisible);
        Assert.False(view.FindControl<TextBlock>("MobileClockText")!.IsVisible);
    }

    [Fact]
    public void Сетевое_Сообщение_О_Передаче_Хода_Видно()
    {
        var (view, model) = Create();
        _ = StartNetworkGuest(model, StoneColor.Black);

        var pill = view.FindControl<Border>("NetworkNoticePill")!;
        var text = view.FindControl<TextBlock>("NetworkNoticeText")!;

        Assert.True(pill.IsVisible);
        Assert.Contains("Твой ход", text.Text ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Сообщение_Об_Авто_Пасе_Видно_В_Плашке()
    {
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.White);

        client.RaiseMoveApplied(new GoEngine.Multiplayer.MoveAppliedEventArgs(
            1, Move.Pass(StoneColor.Black), autoPass: true, remainingMs: 90_000));

        var pill = view.FindControl<Border>("NetworkNoticePill")!;
        var text = view.FindControl<TextBlock>("NetworkNoticeText")!;

        Assert.True(pill.IsVisible);
        Assert.Contains("Время вышло", text.Text ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Обрыв_Показывает_Баннер_С_Отсчётом()
    {
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.Black);

        client.RaiseDisconnected();

        var overlay = view.FindControl<Border>("NetworkStateOverlay")!;

        Assert.True(overlay.IsVisible);
        Assert.Equal("Соперник потерял связь", view.FindControl<TextBlock>("NetworkStateTitle")!.Text);
        Assert.Contains("Ждём возвращения", view.FindControl<TextBlock>("NetworkStateText")!.Text ?? string.Empty, StringComparison.Ordinal);
        Assert.True(view.FindControl<Button>("NetworkStateWaitButton")!.IsVisible);
    }

    [Fact]
    public void Терминальный_Обрыв_Показывает_Выход_В_Меню()
    {
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.Black);

        client.RaiseFailed(new GoEngine.Multiplayer.LanErrorEventArgs(
            GoEngine.Multiplayer.LanErrorCodes.ReconnectTimeout,
            "Не удалось вернуться в партию за отведённое время."));

        var overlay = view.FindControl<Border>("NetworkStateOverlay")!;
        var leave = view.FindControl<Button>("NetworkStateLeaveButton")!;

        Assert.True(overlay.IsVisible);
        Assert.Equal("Партия прервана", view.FindControl<TextBlock>("NetworkStateTitle")!.Text);
        Assert.Equal("В меню", leave.Content);
        Assert.False(view.FindControl<Button>("NetworkStateWaitButton")!.IsVisible);
    }

    [Fact]
    public void Выход_Из_Терминального_Обрыва_Возвращает_На_Главную()
    {
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.Black);
        client.RaiseFailed(new GoEngine.Multiplayer.LanErrorEventArgs(
            GoEngine.Multiplayer.LanErrorCodes.Stalled,
            "Соперник не вернулся в игру."));
        var requested = false;
        view.NetworkReturnHomeRequested += (_, _) => requested = true;

        Click(view.FindControl<Button>("NetworkStateLeaveButton")!);

        Assert.False(model.IsNetworkGame);
        Assert.True(requested);
    }

    [Fact]
    public void Терминальный_Обрыв_Не_Перекрывает_Итог_Партии()
    {
        // B1 ревью P4: после обычного итога поздний выход соперника не показывает терминал.
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.White);
        client.RaiseMoveApplied(new GoEngine.Multiplayer.MoveAppliedEventArgs(
            1, Move.Pass(StoneColor.Black), autoPass: false, remainingMs: 90_000));
        client.RaiseMoveApplied(new GoEngine.Multiplayer.MoveAppliedEventArgs(
            2, Move.Pass(StoneColor.White), autoPass: false, remainingMs: 90_000));
        Assert.True(model.HasOutcome);

        client.RaiseFailed(new GoEngine.Multiplayer.LanErrorEventArgs(
            GoEngine.Multiplayer.LanErrorCodes.HostLeft,
            "Хозяин вышел из игры."));

        Assert.False(view.FindControl<Border>("NetworkStateOverlay")!.IsVisible);
        Assert.True(model.HasOutcome);
    }

    [Fact]
    public void Последние_Секунды_Подсвечивают_Плашку_В_Строке_Доски()
    {
        // M2+M3 ревью P4: последние 10 с — мягкое акцентирование, плашка в строке доски,
        // а не поверх нижней шторки.
        var time = new FakeTimeProvider();
        var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9);
        var model = new MainViewModel(settings, new Random(TestViewModel.Seed), time: time);
        var view = new BoardView(settings, null, model);
        var client = StartNetworkGuest(model, StoneColor.White);
        client.RaiseMoveApplied(new GoEngine.Multiplayer.MoveAppliedEventArgs(
            1, Move.Play(new Point(2, 2), StoneColor.Black), autoPass: false, remainingMs: 90_000));

        time.Advance(TimeSpan.FromSeconds(80));
        model.TickClock();

        var pill = view.FindControl<Border>("MobileNetworkNoticePill")!;
        var row = view.FindControl<Panel>("MobileBoardRow")!;

        Assert.True(pill.IsVisible);
        Assert.Contains("network-urgent", pill.Classes);
        Assert.Contains(pill, row.Children);
    }

    [Fact]
    public void Розыгрыш_Цвета_Показывает_Вопрос_Гостю()
    {
        // R-004: гость видит крупные кнопки «Чёт/Нечет», пока хозяин прячет камни.
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.White);
        var random = new Random(3);
        var nonce = GoEngine.Multiplayer.ColorDraw.CreateNonce(random);
        var commitment = GoEngine.Multiplayer.ColorDraw.Commitment(7, nonce);

        client.RaiseColorDrawOffer(commitment, 15_000);

        Assert.True(view.FindControl<Border>("ColorDrawOverlay")!.IsVisible);
        Assert.True(view.FindControl<Grid>("ColorDrawGuessRow")!.IsVisible);
        Assert.False(view.FindControl<Grid>("ColorDrawChoiceRow")!.IsVisible);
        Assert.Equal("Чёт", view.FindControl<Button>("ColorDrawEvenButton")!.Content);
        Assert.Equal("Нечет", view.FindControl<Button>("ColorDrawOddButton")!.Content);
    }

    [Fact]
    public async Task Розыгрыш_Цвета_Показывает_Выбор_Победителю()
    {
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.White);
        var random = new Random(4);
        var nonce = GoEngine.Multiplayer.ColorDraw.CreateNonce(random);
        var commitment = GoEngine.Multiplayer.ColorDraw.Commitment(7, nonce);
        client.RaiseColorDrawOffer(commitment, 15_000);

        // Гость сам проверяет победителя по своему ответу (F1): 7 — нечет, отвечаем «нечет».
        await model.GuessColorDrawAsync(odd: true);
        client.RaiseColorDrawReveal(7, nonce, winnerIsGuest: true);

        Assert.True(view.FindControl<Grid>("ColorDrawChoiceRow")!.IsVisible);
        Assert.False(view.FindControl<Grid>("ColorDrawGuessRow")!.IsVisible);
        Assert.Contains("Чёрные", view.FindControl<Button>("ColorDrawBlackButton")!.Content?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Розыгрыш_Цвета_Скрывается_После_Итоговых_Цветов()
    {
        var (view, model) = Create();
        var client = StartNetworkGuest(model, StoneColor.White);
        var random = new Random(6);
        var nonce = GoEngine.Multiplayer.ColorDraw.CreateNonce(random);
        var commitment = GoEngine.Multiplayer.ColorDraw.Commitment(7, nonce);
        client.RaiseColorDrawOffer(commitment, 15_000);
        client.RaiseColorDrawReveal(7, nonce, winnerIsGuest: true);

        client.RaiseColorDrawDone(StoneColor.Black, StoneColor.White);

        Assert.False(view.FindControl<Border>("ColorDrawOverlay")!.IsVisible);
        Assert.Equal("белые", model.NetworkPlayerColorText);
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

    /// <summary>Подключает партию к сетевому сопернику на стороне гостя.</summary>
    /// <param name="model">Модель представления партии.</param>
    /// <param name="playerColor">Цвет гостя в сетевой партии.</param>
    /// <returns>Подставной клиент: через него приходят подтверждённые ходы хозяина.</returns>
    private static FakeNetworkClient StartNetworkGuest(MainViewModel model, StoneColor playerColor)
    {
        // События сети модель перекладывает в захваченный SynchronizationContext. В тесте
        // цикла сообщений нет: без этого подтверждённые ходы оставались бы в очереди,
        // и партия не завершалась бы (как в MainViewModelNetworkTests.CreateModel).
        SynchronizationContext.SetSynchronizationContext(null);

        var client = new FakeNetworkClient();
        var started = model.StartNetworkGuest(
            client,
            new GoEngine.Multiplayer.GuestSessionStarted(
                "0123456789abcdef0123456789abcdef", 9, 5.5, playerColor, 1, 90_000));

        Assert.True(started.IsSuccess, started.Error);

        return client;
    }

    /// <summary>Завершает сетевую партию сдачей соперника: чёрные сыграли, белые сдались.</summary>
    /// <param name="client">Подставной клиент, подключённый к партии.</param>
    private static void FinishNetworkGameByResign(FakeNetworkClient client)
    {
        client.RaiseMoveApplied(new GoEngine.Multiplayer.MoveAppliedEventArgs(
            1, Move.Play(new Point(4, 4), StoneColor.Black), autoPass: false, remainingMs: 90_000));
        client.RaiseMoveApplied(new GoEngine.Multiplayer.MoveAppliedEventArgs(
            2, Move.Resign(StoneColor.White), autoPass: false, remainingMs: 90_000));
    }

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

    /// <summary>Вид настроек внутри вида партии.</summary>
    /// <param name="view">Вид партии.</param>
    /// <returns>Вид настроек; его отсутствие — сломанная разметка, а не сценарий теста.</returns>
    private static SettingsView SettingsArea(BoardView view) => view.FindControl<SettingsView>("SettingsArea")!;

    /// <summary>Кнопка низа экрана настроек: «Сохранить» или «Отмена».</summary>
    /// <param name="view">Вид партии.</param>
    /// <param name="name">Имя кнопки в разметке вида настроек.</param>
    /// <returns>Кнопка разметки.</returns>
    private static Button SettingsButton(BoardView view, string name) =>
        SettingsArea(view).FindControl<Button>(name)!;

    /// <summary>Список настроек по имени: доска, уровень или система подсчёта.</summary>
    /// <param name="view">Вид партии.</param>
    /// <param name="name">Имя списка в разметке вида настроек.</param>
    /// <returns>Список разметки.</returns>
    private static ComboBox SettingsBox(BoardView view, string name) =>
        SettingsArea(view).FindControl<ComboBox>(name)!;

    /// <summary>Список систем подсчёта внутри вида настроек.</summary>
    /// <param name="view">Вид партии.</param>
    /// <returns>Список выбора системы подсчёта.</returns>
    private static ComboBox ScoringBox(BoardView view) => SettingsBox(view, "ScoringBox");

    /// <summary>Список подразделов настроек внутри вида партии.</summary>
    /// <param name="view">Вид партии.</param>
    /// <returns>Переключатель подразделов.</returns>
    private static TabControl SettingsTabs(BoardView view) =>
        view.FindControl<SettingsView>("SettingsArea")!.FindControl<TabControl>("SettingsTabs")!;

    /// <summary>Карточка меню партии: слой затемнения.</summary>
    /// <param name="view">Вид партии.</param>
    /// <returns>Рамка меню.</returns>
    private static Border MenuOverlay(BoardView view) => view.FindControl<Border>("MenuOverlay")!;

    /// <summary>Все кнопки части разметки.</summary>
    /// <param name="root">Корень разметки.</param>
    /// <returns>Кнопки в порядке обхода дерева.</returns>
    /// <summary>Подпись строки: текст внутри кнопки, а не её содержимое.</summary>
    /// <param name="button">Строка меню.</param>
    /// <returns>Текст подписи или пустая строка, если подписи нет.</returns>
    /// <remarks>
    /// Строки меню несут значок и подпись, поэтому содержимое кнопки — не строка, а разметка.
    /// </remarks>
    private static string LabelOf(Button button) => button
        .GetLogicalDescendants()
        .OfType<TextBlock>()
        .Select(text => text.Text ?? string.Empty)
        .FirstOrDefault(string.Empty);

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
