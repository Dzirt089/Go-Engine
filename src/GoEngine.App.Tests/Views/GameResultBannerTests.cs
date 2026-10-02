using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты баннера итога партии: подключение к модели и кнопка «Новая партия».</summary>
/// <remarks>
/// Регрессия D-065 §6: оба баннера создаёт разметка конструктором без параметров, поэтому модель
/// приходит только через <see cref="GameResultBanner.Attach"/>. Без подключения баннер не показывал
/// исход партии, а его кнопка ничего не делала. Позиция взята готовая — та же, что в
/// <c>EndgameScoreTests</c>: доказанно мёртвая белая группа в углу и два паса подряд.
/// </remarks>
public sealed class GameResultBannerTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Без_Attach_Баннер_Не_Показывает_Итог()
    {
        // Так создаёт баннер разметка; вид зовёт Refresh, но модели у баннера нет.
        var banner = new GameResultBanner();

        banner.Refresh();

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void После_Attach_Баннер_Показывает_Итог_Завершённой_Партии()
    {
        var banner = new GameResultBanner();

        banner.Attach(FinishedModel());

        Assert.True(banner.IsVisible);
    }

    [Fact]
    public void Партия_Заканчивается_При_Подключённом_Баннере_И_Карточка_Появляется_Сама()
    {
        // Шага «Посчитать» больше нет: два паса завершают партию, и карточка показывается без
        // единого нажатия (жалоба пользователя 2026-10-02 «не хочу ещё куда-то тыкать»).
        var model = Create();
        var banner = new GameResultBanner();
        banner.Attach(model);

        _ = model.LoadGame(DeadCornerGame());

        Assert.True(banner.IsVisible);
    }

    [Fact]
    public void После_Attach_Заголовок_Баннера_Совпадает_С_Моделью()
    {
        var model = FinishedModel();
        var banner = new GameResultBanner();

        banner.Attach(model);

        Assert.Equal(model.ResultHeadline, Headline(banner));
    }

    [Fact]
    public void После_Attach_Подпись_Баннера_Совпадает_С_Моделью()
    {
        var model = FinishedModel();
        var banner = new GameResultBanner();

        banner.Attach(model);

        Assert.Equal(model.ResultDetail, Detail(banner));
    }

    [Fact]
    public void Баннер_Красится_Признаком_Победы_Игрока()
    {
        // В этой позиции выигрывают чёрные, а играет тест чёрными: тон — победа игрока,
        // значит карточка получает класс result-win.
        var model = FinishedModel();
        var banner = new GameResultBanner();

        banner.Attach(model);

        Assert.Contains("result-win", Card(banner).Classes);
    }

    [Fact]
    public void Кнопка_Баннера_Начинает_Новую_Партию()
    {
        var model = FinishedModel();
        var banner = new GameResultBanner();
        banner.Attach(model);

        ClickNewGame(banner);

        Assert.Equal("0", model.MoveNumber);
    }

    [Fact]
    public void Новая_Партия_Прячет_Баннер()
    {
        var model = FinishedModel();
        var banner = new GameResultBanner();
        banner.Attach(model);

        ClickNewGame(banner);

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void Повторный_Attach_Не_Удваивает_Обработчики()
    {
        // Одно нажатие = одна новая партия: каждый лишний обработчик кнопки запускал бы
        // партию ещё раз, и это видно по числу уведомлений о номере хода.
        var model = FinishedModel();
        var banner = new GameResultBanner();
        banner.Attach(model);
        banner.Attach(model);

        var starts = 0;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.MoveNumber))
            {
                starts++;
            }
        };

        ClickNewGame(banner);

        Assert.Equal(1, starts);
    }

    [Fact]
    public void Повторный_Attach_Показывает_Итог()
    {
        // Повторное подключение — не ошибка: раскладка и пересоздание вида могут позвать его снова.
        var model = FinishedModel();
        var banner = new GameResultBanner();

        banner.Attach(model);
        banner.Attach(model);

        Assert.True(banner.IsVisible);
    }

    [Fact]
    public void Кнопка_Баннера_Сначала_Настольного_Размера()
    {
        // Крупный размер — признак мобильной раскладки. Пока раскладка не выбрана, кнопка
        // настольная: класс touch ставит вид партии, а не разметка (UiStyles.Controls.axaml).
        var banner = new GameResultBanner();

        Assert.False(banner.IsTouch);
    }

    [Fact]
    public void Мобильная_Раскладка_Делает_Кнопку_Крупной()
    {
        var banner = new GameResultBanner();

        banner.IsTouch = true;

        Assert.True(banner.IsTouch);
    }

    [Fact]
    public void Возврат_К_Настольной_Раскладке_Убирает_Крупную_Кнопку()
    {
        var banner = new GameResultBanner();
        banner.IsTouch = true;

        banner.IsTouch = false;

        Assert.False(banner.IsTouch);
    }

    [Fact]
    public void Карточка_Стоит_По_Центру_Экрана()
    {
        // Жалоба пользователя 2026-10-02: блок с исходом стоял полосой сверху и уезжал от взгляда.
        var banner = new GameResultBanner();

        var card = Card(banner);

        Assert.Equal((HorizontalAlignment.Center, VerticalAlignment.Center), (card.HorizontalAlignment, card.VerticalAlignment));
    }

    [Fact]
    public void Карточка_Ограничена_По_Ширине()
    {
        // Без ограничения карточка растянулась бы в строку через весь монитор.
        var banner = new GameResultBanner();

        Assert.InRange(Card(banner).MaxWidth, 1, 900);
    }

    [Fact]
    public void Затемнение_Растянуто_На_Весь_Вид()
    {
        var banner = new GameResultBanner();

        var overlay = Overlay(banner);

        Assert.Equal((HorizontalAlignment.Stretch, VerticalAlignment.Stretch), (overlay.HorizontalAlignment, overlay.VerticalAlignment));
    }

    [Fact]
    public void Затемнение_Взято_Из_Темы()
    {
        // Цвет затемнения задаёт класс темы: жёсткий чёрный в тёмной теме сливался бы с фоном.
        var banner = new GameResultBanner();

        Assert.Contains("app-overlay", Overlay(banner).Classes);
    }

    [Fact]
    public void Кнопка_Закрыть_Прячет_Карточку()
    {
        var model = FinishedModel();
        var banner = new GameResultBanner();
        banner.Attach(model);

        ClickClose(banner);

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void Закрытая_Карточка_Не_Возвращается_От_Постороннего_Изменения()
    {
        // Закрытие не должно сниматься от перерисовки: иначе карточка мигала бы поверх доски,
        // которую игрок как раз рассматривает. Переключатель разметки итог не меняет.
        var model = FinishedModel();
        var banner = new GameResultBanner();
        banner.Attach(model);
        ClickClose(banner);

        model.ShowTerritory = false;

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void Закрытая_Карточка_Не_Возвращается_При_Том_Же_Итоге()
    {
        // Правило с двух сторон: молчит, пока итог тот же. Повторная загрузка той же
        // завершённой позиции итог не меняет — и карточка не появляется.
        var model = FinishedModel();
        var banner = new GameResultBanner();
        banner.Attach(model);
        ClickClose(banner);

        _ = model.LoadGame(DeadCornerGame());

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void Закрытая_Карточка_Возвращается_При_Другом_Итоге()
    {
        // «Закрыть» гасит карточку, пока итог тот же, а не навсегда: другая партия с другим
        // результатом обязана показаться снова.
        var model = FinishedModel();
        var banner = new GameResultBanner();
        banner.Attach(model);
        ClickClose(banner);

        _ = model.LoadGame(DeadCornerGame(new Komi(0.5)));

        Assert.True(banner.IsVisible);
    }

    [Fact]
    public void Мобильная_Раскладка_Делает_Крупной_И_Кнопку_Закрыть()
    {
        var banner = new GameResultBanner();

        banner.IsTouch = true;

        Assert.Contains("touch", CloseButton(banner).Classes);
    }

    [Fact]
    public void Подпись_Баннера_Не_Повторяет_Счёт()
    {
        // Жалоба пользователя 2026-10-02 №3: счёт показывался трижды, в том числе в баннере.
        // Баннер называет исход и перевес, счёт живёт в панели партии и в шторке.
        var model = FinishedModel();
        var banner = new GameResultBanner();

        banner.Attach(model);

        Assert.DoesNotContain(model.Score, Detail(banner), StringComparison.Ordinal);
    }

    /// <summary>Создаёт модель представления со случайным соперником: партия в тесте идёт быстро.</summary>
    /// <returns>Модель представления партии 9×9; построение — общее для тестов.</returns>
    private static MainViewModel Create() => TestViewModel.Create(null, DifficultyLevel.Kyu30, size: Size);

    /// <summary>Создаёт модель завершённой партии: баннеру есть что показывать.</summary>
    /// <returns>Модель, в которой партия закончилась двумя пасами.</returns>
    /// <remarks>
    /// Отдельного шага подтверждения подсчёта нет: два паса завершают партию, и итог называется
    /// сразу. Мёртвые группы при этом уже помечены перебором — их видно на доске.
    /// </remarks>
    private static MainViewModel FinishedModel()
    {
        var model = Create();
        _ = model.LoadGame(DeadCornerGame());

        return model;
    }

    /// <summary>Нажимает кнопку баннера «Новая партия» так же, как её нажимает игрок.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    private static void ClickNewGame(GameResultBanner banner) =>
        NewGameButton(banner).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Нажимает кнопку карточки «Закрыть» так же, как её нажимает игрок.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    private static void ClickClose(GameResultBanner banner) =>
        CloseButton(banner).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Кнопка «Закрыть» внутри карточки.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    /// <returns>Кнопка разметки; отсутствие кнопки — сломанная разметка, а не сценарий теста.</returns>
    private static Button CloseButton(GameResultBanner banner) =>
        banner.FindControl<Button>("CloseButton")!;

    /// <summary>Затемнение под карточкой: слой, на котором стоит сообщение.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    /// <returns>Рамка затемнения.</returns>
    private static Border Overlay(GameResultBanner banner) =>
        banner.FindControl<Border>("Overlay")!;

    /// <summary>Кнопка «Новая партия» внутри баннера.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    /// <returns>Кнопка разметки; отсутствие кнопки — сломанная разметка, а не сценарий теста.</returns>
    private static Button NewGameButton(GameResultBanner banner) =>
        banner.FindControl<Button>("NewGameButton")!;

    /// <summary>Крупный заголовок баннера.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    /// <returns>Текст заголовка.</returns>
    private static string Headline(GameResultBanner banner) =>
        banner.FindControl<TextBlock>("HeadlineText")!.Text ?? string.Empty;

    /// <summary>Строка под заголовком баннера.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    /// <returns>Текст подписи.</returns>
    private static string Detail(GameResultBanner banner) =>
        banner.FindControl<TextBlock>("DetailText")!.Text ?? string.Empty;

    /// <summary>Карточка баннера: по её классам видно тон итога.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    /// <returns>Рамка карточки.</returns>
    private static Border Card(GameResultBanner banner) =>
        banner.FindControl<Border>("BannerCard")!;

    /// <summary>Строит партию 9×9 с мёртвой белой группой в углу, завершённую двумя пасами.</summary>
    /// <param name="komi">Коми партии; <c>null</c> — обычное для 9×9 (5.5).</param>
    /// <returns>Прочитанная партия для <see cref="MainViewModel.LoadGame"/>.</returns>
    /// <remarks>
    /// Позиция: чёрные закрыли угол (2,0), (2,1), (0,2), (1,2), внутри остались два белых камня.
    /// Ход белых в любую из двух пустых точек угла проигрывает: чёрные отвечают и снимают группу.
    /// Коми — параметр: та же позиция с другим коми даёт другой перевес, а значит и другой итог.
    /// </remarks>
    private static SgfGame DeadCornerGame(Komi? komi = null) => new(
        Size,
        komi ?? Komi.For9x9,
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
