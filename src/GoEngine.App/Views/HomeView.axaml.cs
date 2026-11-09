using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace GoEngine.App.Views;

/// <summary>Главный экран приложения: выбор режима крупными карточками.</summary>
/// <remarks>
/// <para>
/// Первый экран вместо доски: приложение запускалось прямо в партию, и ребёнку было непонятно,
/// что игра с ИИ уже идёт, а «Партия с другом» спрятана в меню (замечание пользователя 2026-11-09).
/// Ориентир — главные экраны популярных Го-приложений (BadukPop, Go Quest, OGS): один явный
/// главный ход и два понятных продолжения, без перегруза сверху.
/// </para>
/// <para>
/// Своих решений вид не принимает: карточки поднимают события, а показывает нужный раздел
/// оболочка (<see cref="ShellView"/>). Так переходы проверяются тестом без окна, и у экрана
/// нет второго места, где меняется режим приложения.
/// </para>
/// </remarks>
public sealed partial class HomeView : UserControl
{
    /// <summary>Ширина, ниже которой карточки встают в один столбец: телефон и узкое окно.</summary>
    /// <remarks>
    /// 600 точек — между телефоном (360) и настольным окном (760 и больше): двум карточкам
    /// рядом нужно по 280–300 точек на подпись и значок, а на телефоне они не помещаются.
    /// </remarks>
    private const double CompactWidth = 600;

    /// <summary>Отступ страницы от краёв: тот же, что у остальных экранов приложения.</summary>
    private const double PageMargin = 20;

    /// <summary>Ширина колонки карточек на большом экране: дальше карточки только растягивались бы.</summary>
    private const double MaximumPageWidth = 640;

    private readonly StackPanel? _page;
    private readonly Button? _gameCard;
    private readonly Button? _networkCard;
    private readonly Button? _lessonCard;
    private readonly Button? _menuButton;
    private readonly UniformGrid? _secondaryCards;
    private readonly TextBlock? _title;

    /// <summary>Раскладка уже применена: до первого вызова размеров экрана ещё нет.</summary>
    private bool _layoutApplied;

    /// <summary>Карточки стоят в один столбец: раскладка телефона.</summary>
    private bool _compact;

    /// <summary>Создаёт главный экран с карточками режимов.</summary>
    public HomeView()
    {
        AvaloniaXamlLoader.Load(this);

        _title = this.FindControl<TextBlock>("TitleText");
        _page = this.FindControl<StackPanel>("HomeContent");
        _secondaryCards = this.FindControl<UniformGrid>("SecondaryCards");
        _gameCard = this.FindControl<Button>("GameCard");
        _networkCard = this.FindControl<Button>("NetworkCard");
        _lessonCard = this.FindControl<Button>("LessonCard");
        _menuButton = this.FindControl<Button>("MenuButton");

        WireButton("GameCard", OnGameClick);
        WireButton("NetworkCard", OnNetworkClick);
        WireButton("LessonCard", OnLessonClick);
        WireButton("MenuButton", OnMenuClick);

        // До первого измерения размер неизвестен: оставляем настольную раскладку из разметки.
        ApplyLayout(0);
    }

    /// <summary>Выбрана партия с ИИ.</summary>
    public event EventHandler? GameRequested;

    /// <summary>Выбрана партия с другом: открывается сетевое лобби.</summary>
    public event EventHandler? NetworkRequested;

    /// <summary>Выбрано обучение: уроки, разбор партий и задачи.</summary>
    public event EventHandler? LessonsRequested;

    /// <summary>Открыто общее меню приложения.</summary>
    public event EventHandler? MenuRequested;

    /// <summary>Карточки стоят в один столбец: раскладка телефона.</summary>
    /// <remarks>
    /// Признак нужен проверкам: раскладка выбирается по ширине экрана, а не по платформе,
    /// поэтому её видно и без окна.
    /// </remarks>
    public bool IsCompactLayout => _compact;

    /// <summary>Применяет раскладку по ширине экрана.</summary>
    /// <param name="width">Ширина вида; 0 — размер ещё не известен.</param>
    /// <remarks>
    /// Решение принимает оболочка по настоящему размеру вида и передаёт его сюда: у экрана
    /// нет своего измерения, чтобы порог раскладки не разошёлся с порогом оболочки.
    /// </remarks>
    public void ApplyLayout(double width)
    {
        var compact = width > 0 && width < CompactWidth;
        var layoutChanged = !_layoutApplied || compact != _compact;

        _layoutApplied = true;
        _compact = compact;

        // Ширина колонки задаётся явно: StackPanel по центру иначе сжался бы до ширины
        // подписей, и на настольном экране карточки стояли бы узкой колонкой вместо страницы.
        if (_page is not null)
        {
            _page.Width = width > 0
                ? Math.Clamp(width - (2 * PageMargin), 0, MaximumPageWidth)
                : double.NaN;
        }

        if (!layoutChanged)
        {
            return;
        }

        if (_title is not null)
        {
            _title.FontSize = compact ? 24 : 28;
        }

        foreach (var card in new[] { _gameCard, _networkCard, _lessonCard, _menuButton })
        {
            card?.Classes.Set("compact", compact);
        }

        if (_secondaryCards is not null)
        {
            // Два продолжения: рядом на широком экране, друг под другом на телефоне.
            _secondaryCards.Columns = compact ? 1 : 2;
            _secondaryCards.Rows = compact ? 2 : 1;
        }

        PlaceCards(compact);
    }

    /// <summary>Расставляет карточки по выбранной раскладке.</summary>
    /// <param name="compact">Один столбец вместо двух.</param>
    /// <remarks>
    /// Расстояние между карточками — отступами, а не RowSpacing/ColumnSpacing: этих свойств
    /// у Grid в Avalonia 11.2 нет (проверено сборкой).
    /// </remarks>
    private void PlaceCards(bool compact)
    {
        if (_gameCard is not null)
        {
            _gameCard.Margin = new Thickness(0);
        }

        // UniformGrid раскладывает детей по порядку: строки и столбцы задаются его сеткой,
        // а отступы на карточках задают расстояние между ними.
        if (_networkCard is not null)
        {
            _networkCard.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 6, 0);
        }

        if (_lessonCard is not null)
        {
            _lessonCard.Margin = compact ? new Thickness(0) : new Thickness(6, 0, 0, 0);
        }
    }

    /// <summary>Подписывает карточку на обработчик нажатия.</summary>
    /// <param name="name">Имя карточки в разметке.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void WireButton(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }

    /// <summary>Игрок выбрал партию с ИИ.</summary>
    /// <param name="sender">Карточка «Партия с ИИ».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGameClick(object? sender, RoutedEventArgs e) => GameRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Игрок выбрал партию с другом.</summary>
    /// <param name="sender">Карточка «Партия с другом».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNetworkClick(object? sender, RoutedEventArgs e) => NetworkRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Игрок выбрал обучение.</summary>
    /// <param name="sender">Карточка «Обучение».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLessonClick(object? sender, RoutedEventArgs e) => LessonsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Игрок открыл общее меню.</summary>
    /// <param name="sender">Строка «Меню».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnMenuClick(object? sender, RoutedEventArgs e) => MenuRequested?.Invoke(this, EventArgs.Empty);
}
