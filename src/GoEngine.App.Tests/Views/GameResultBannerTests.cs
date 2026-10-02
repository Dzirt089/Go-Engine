using Avalonia.Controls;
using Avalonia.Interactivity;
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
    public void До_Подтверждения_Подсчёта_Баннер_Молчит()
    {
        // Согласование мёртвых групп: итог ещё не подтверждён, победитель не называется.
        var banner = new GameResultBanner();
        banner.Attach(CountingModel());

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void Подтверждение_Подсчёта_Показывает_Баннер_Без_Отдельной_Команды()
    {
        var model = CountingModel();
        var banner = new GameResultBanner();
        banner.Attach(model);

        model.ConfirmScore();

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

    /// <summary>Создаёт модель представления со случайным соперником: партия в тесте идёт быстро.</summary>
    /// <returns>Модель представления партии 9×9; построение — общее для тестов.</returns>
    private static MainViewModel Create() => TestViewModel.Create(null, DifficultyLevel.Kyu30, size: Size);

    /// <summary>Создаёт модель, в которой партия завершена двумя пасами, а мёртвые не подтверждены.</summary>
    /// <returns>Модель в состоянии согласования мёртвых групп.</returns>
    private static MainViewModel CountingModel()
    {
        var model = Create();
        _ = model.LoadGame(DeadCornerGame());

        return model;
    }

    /// <summary>Создаёт модель с подтверждённым итогом: баннеру есть что показывать.</summary>
    /// <returns>Модель завершённой партии.</returns>
    private static MainViewModel FinishedModel()
    {
        var model = CountingModel();
        model.ConfirmScore();

        return model;
    }

    /// <summary>Нажимает кнопку баннера «Новая партия» так же, как её нажимает игрок.</summary>
    /// <param name="banner">Баннер с разметкой.</param>
    private static void ClickNewGame(GameResultBanner banner) =>
        NewGameButton(banner).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
    /// <returns>Прочитанная партия для <see cref="MainViewModel.LoadGame"/>.</returns>
    /// <remarks>
    /// Позиция: чёрные закрыли угол (2,0), (2,1), (0,2), (1,2), внутри остались два белых камня.
    /// Ход белых в любую из двух пустых точек угла проигрывает: чёрные отвечают и снимают группу.
    /// </remarks>
    private static SgfGame DeadCornerGame() => new(
        Size,
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
