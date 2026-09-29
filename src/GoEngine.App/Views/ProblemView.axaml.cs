using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Views;

/// <summary>Вид режима задач: доска задачи и панель с вердиктами и переходами.</summary>
/// <remarks>
/// Отдельный от партии вид: своя модель (<see cref="ProblemViewModel"/>), своя панель, но та же
/// доска <see cref="BoardControl"/>, что и в партии, — второго рендера нет. Раскладка выбирается
/// по размеру вида (<see cref="BoardLayoutRules"/>), поэтому режим работает и на телефоне.
/// </remarks>
public sealed partial class ProblemView : UserControl
{
    /// <summary>Наименьшая высота панели в вертикальной раскладке.</summary>
    /// <remarks>
    /// Вердикт и два ряда кнопок занимают около 150 точек; остальное — описание задачи, и оно
    /// прокручивается. Меньше этой границы панель отдавать нельзя: кнопки режима уехали бы
    /// за край экрана, и игрок не смог бы ни подсказать ход, ни отменить его.
    /// </remarks>
    private const double MinimumPanelHeight = 280;

    /// <summary>Высота доски на совсем низком экране, где половина высоты меньше разумного минимума.</summary>
    private const double MinimumBoardHeightForTinyScreen = 120;

    private readonly BoardControl? _boardControl;
    private readonly Grid? _layout;
    private readonly Border? _panel;
    private readonly ComboBox? _problemBox;
    private readonly TextBlock? _heading;
    private readonly Expander? _details;

    /// <summary>Состояние списка задач: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _problemCombo = new();

    /// <summary>Текущая раскладка: <c>null</c> — ещё не выбрана.</summary>
    private bool? _narrow;

    /// <summary>Создаёт вид задач со встроенной библиотекой.</summary>
    public ProblemView() : this(new ProblemViewModel())
    {
    }

    /// <summary>Создаёт вид задач с готовой моделью представления.</summary>
    /// <param name="viewModel">Модель представления задач.</param>
    public ProblemView(ProblemViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;

        AvaloniaXamlLoader.Load(this);

        DataContext = viewModel;

        _boardControl = this.FindControl<BoardControl>("Board");
        _layout = this.FindControl<Grid>("Layout");
        _panel = this.FindControl<Border>("Panel");
        _problemBox = this.FindControl<ComboBox>("ProblemBox");
        _heading = this.FindControl<TextBlock>("Heading");
        _details = this.FindControl<Expander>("Details");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        if (_problemBox is not null)
        {
            _problemBox.SelectionChanged += OnProblemChanged;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        Wire("HintButton", OnHintClick);
        Wire("BackButton", OnBackClick);
        Wire("ResetButton", OnResetClick);
        Wire("PreviousButton", OnPreviousClick);
        Wire("NextButton", OnNextClick);

        SizeChanged += OnViewSizeChanged;
        ApplyLayout(Bounds.Width, Bounds.Height);
        SyncProblemBox();
    }

    /// <summary>Модель представления задач.</summary>
    public ProblemViewModel ViewModel { get; }

    /// <summary>Обновляет список задач, когда модель сообщает об изменениях.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProblemViewModel.SelectedIndex))
        {
            SyncProblemBox();
        }

        if (e.PropertyName is nameof(ProblemViewModel.LastMove))
        {
            // Та же анимация, что и в партии: ответ соперника и снятие камней не должны
            // появляться рывком — жалоба «всё происходит резко в один миг» касается и задач.
            _boardControl?.Animate(
                ViewModel.LastMove,
                ViewModel.LastCaptured,
                ViewModel.LastCapturedColor);
        }
    }

    /// <summary>Играет ход игрока по щелчку в доску.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка щелчка.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => ViewModel.Play(e.Point);

    /// <summary>Показывает подсказку: ход словом и точкой на доске.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnHintClick(object? sender, RoutedEventArgs e) => ViewModel.ShowHint();

    /// <summary>Отменяет последний ход.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnBackClick(object? sender, RoutedEventArgs e) => ViewModel.Back();

    /// <summary>Возвращает задачу в начальную позицию.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnResetClick(object? sender, RoutedEventArgs e) => ViewModel.Reset();

    /// <summary>Переходит к предыдущей задаче.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnPreviousClick(object? sender, RoutedEventArgs e) => ViewModel.Previous();

    /// <summary>Переходит к следующей задаче.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNextClick(object? sender, RoutedEventArgs e) => ViewModel.Next();

    /// <summary>Переключает задачу по выбору в списке.</summary>
    /// <param name="sender">Список задач.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnProblemChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || !_problemCombo.TryAccept(box.SelectedIndex))
        {
            return;
        }

        ViewModel.SelectedIndex = box.SelectedIndex;
    }

    /// <summary>Показывает в списке текущую задачу.</summary>
    private void SyncProblemBox()
    {
        if (_problemBox is null)
        {
            return;
        }

        var replace = _problemCombo.BeginUpdate(ViewModel.Labels, ViewModel.SelectedIndex);

        try
        {
            if (replace)
            {
                _problemBox.ItemsSource = ViewModel.Labels;
            }

            _problemBox.SelectedIndex = ViewModel.SelectedIndex;
        }
        finally
        {
            _problemCombo.EndUpdate();
        }
    }

    /// <summary>Перестраивает раскладку при изменении размера вида.</summary>
    /// <param name="sender">Вид.</param>
    /// <param name="e">Новый размер.</param>
    private void OnViewSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ApplyLayout(e.NewSize.Width, e.NewSize.Height);

    /// <summary>Раскладывает доску и панель по размеру вида.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <remarks>
    /// Правила те же, что у доски партии: на телефоне доска сверху, панель снизу. Высота доски
    /// пересчитывается при каждом изменении размера, а не только при смене раскладки: окно
    /// на телефоне меняется и в повороте, и когда появляется экранная клавиатура.
    /// </remarks>
    private void ApplyLayout(double width, double height)
    {
        if (_layout is null || _boardControl is null || _panel is null)
        {
            return;
        }

        var narrow = BoardLayoutRules.IsNarrow(width, height);

        if (_narrow != narrow)
        {
            _narrow = narrow;

            _layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,320");
            _layout.RowDefinitions = new RowDefinitions(narrow ? "Auto,*" : "*");

            Grid.SetColumn(_boardControl, 0);
            Grid.SetRow(_boardControl, 0);
            Grid.SetColumn(_panel, narrow ? 0 : 1);
            Grid.SetRow(_panel, narrow ? 1 : 0);

            _panel.BorderThickness = narrow ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
        }

        if (_heading is not null)
        {
            // На телефоне заголовок панели дублирует подпись раздела в нижней навигации,
            // а его высота нужна описанию задачи.
            _heading.IsVisible = !narrow;
        }

        if (_details is not null)
        {
            // На телефоне подробности свёрнуты: развёрнутое описание не помещается в панель
            // и обрывалось на середине строки прямо над кнопками.
            _details.IsExpanded = !narrow;
        }

        _boardControl.Height = narrow ? NarrowBoardHeight(width, height) : double.NaN;
    }

    /// <summary>Считает высоту доски в вертикальной раскладке.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <returns>Высота доски в точках.</returns>
    /// <remarks>
    /// Берётся обычная высота доски (<see cref="BoardLayoutRules.BoardHeight"/>), но не больше той,
    /// при которой панели остаётся <see cref="MinimumPanelHeight"/>. На низком экране доска
    /// уступает панели: без кнопок режим бесполезен, а доска меньше минимума — нечитаема.
    /// </remarks>
    private static double NarrowBoardHeight(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return double.NaN;
        }

        var available = height - MinimumPanelHeight;

        if (available < BoardLayoutRules.MinimumBoardHeight)
        {
            // Экран ниже, чем доска вместе с панелью (телефон в повороте): делим высоту пополам,
            // но панель не отдаём целиком — иначе кнопки снова уедут за край, а без них режим
            // бесполезен. Совсем низкий экран — исключение из правила, а не его отмена.
            return Math.Max(height / 2, MinimumBoardHeightForTinyScreen);
        }

        return Math.Min(BoardLayoutRules.BoardHeight(width, height), available);
    }

    /// <summary>Подписывает кнопку на обработчик, если она есть в разметке.</summary>
    /// <param name="name">Имя кнопки.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void Wire(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }
}
