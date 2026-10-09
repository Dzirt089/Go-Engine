using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Вид разбора партии: доска партии и панель с разбором и вопросами.</summary>
/// <remarks>
/// <para>
/// Отдельный от партии, задач и уроков вид: своя модель (<see cref="ReviewViewModel"/>), своя
/// панель, но та же доска <see cref="BoardControl"/>, что и везде, — второго рендера нет.
/// Раскладка общая с уроками (<see cref="BoardPanelLayout"/>), поэтому разбор работает
/// и на телефоне.
/// </para>
/// <para>
/// Словарь терминов открывает оболочка (<see cref="ShellView"/>): это отдельный экран, и вид
/// только сообщает о нажатии — тем же способом, что урок.
/// </para>
/// </remarks>
public sealed partial class ReviewView : UserControl
{
    private readonly BoardControl? _boardControl;
    private readonly Grid? _layout;
    private readonly Border? _panel;
    private readonly ComboBox? _gameBox;
    private readonly ScrollViewer? _noteScroll;
    private readonly TextBlock? _heading;
    private readonly TextBlock? _gameTitle;
    private readonly TextBlock? _gameSummary;
    private readonly TextBlock? _players;
    private readonly TextBlock? _result;
    private readonly TextBlock? _message;

    /// <summary>Состояние списка партий: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _gameCombo = new();

    /// <summary>Раскладка «доска и панель»: общая с задачами, уроками и партией.</summary>
    private readonly BoardPanelLayout? _layoutRules;

    /// <summary>Создаёт вид разбора со встроенной библиотекой партий.</summary>
    public ReviewView()
        : this(new ReviewViewModel())
    {
    }

    /// <summary>Создаёт вид разбора с готовой моделью представления.</summary>
    /// <param name="viewModel">Модель представления разбора.</param>
    public ReviewView(ReviewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;

        AvaloniaXamlLoader.Load(this);

        DataContext = viewModel;

        _boardControl = this.FindControl<BoardControl>("Board");
        _layout = this.FindControl<Grid>("Layout");
        _panel = this.FindControl<Border>("Panel");
        _gameBox = this.FindControl<ComboBox>("GameBox");
        _noteScroll = this.FindControl<ScrollViewer>("NoteScroll");
        _heading = this.FindControl<TextBlock>("Heading");
        _gameTitle = this.FindControl<TextBlock>("GameTitleText");
        _gameSummary = this.FindControl<TextBlock>("GameSummaryText");
        _players = this.FindControl<TextBlock>("PlayersText");
        _result = this.FindControl<TextBlock>("ResultText");
        _message = this.FindControl<TextBlock>("MessageText");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        if (_gameBox is not null)
        {
            _gameBox.SelectionChanged += OnGameChanged;
        }

        if (_layout is not null && _boardControl is not null && _panel is not null)
        {
            _layoutRules = new BoardPanelLayout(
                _layout,
                _boardControl,
                _panel,
                [.. new Control?[] { _heading, _gameTitle, _gameSummary, _players, _result }.OfType<Control>()]);
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        Wire("HintButton", OnHintClick);
        Wire("BackButton", OnBackClick);
        Wire("ResetButton", OnResetClick);
        Wire("NextButton", OnNextClick);
        Wire("GlossaryButton", OnGlossaryClick);

        SizeChanged += OnViewSizeChanged;
        ApplyLayout(Bounds.Width, Bounds.Height);
        SyncGameBox();
        RefreshMessage();
    }

    /// <summary>Запрошен словарь терминов: экран показывает оболочка.</summary>
    public event EventHandler? GlossaryRequested;

    /// <summary>Модель представления разбора.</summary>
    public ReviewViewModel ViewModel { get; }

    /// <summary>Обновляет список партий и цвет сообщения, когда модель сообщает об изменениях.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReviewViewModel.SelectedIndex))
        {
            SyncGameBox();
        }

        if (e.PropertyName is nameof(ReviewViewModel.MoveNumber) or nameof(ReviewViewModel.SelectedIndex))
        {
            _noteScroll?.ScrollToHome();
        }

        if (e.PropertyName is nameof(ReviewViewModel.LastMove))
        {
            // Та же анимация, что и в партии: поставленный камень и снятые не должны появляться
            // рывком.
            _boardControl?.Animate(
                ViewModel.LastMove,
                ViewModel.LastCaptured,
                ViewModel.LastCapturedColor);
        }

        if (e.PropertyName is nameof(ReviewViewModel.Message) or nameof(ReviewViewModel.IsMessageGood))
        {
            RefreshMessage();
        }
    }

    /// <summary>Принимает ход игрока по щелчку в доску.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка щелчка.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => ViewModel.Play(e.Point);

    /// <summary>Показывает подсказку к вопросу.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnHintClick(object? sender, RoutedEventArgs e) => ViewModel.ShowHint();

    /// <summary>Возвращает разбор на ход назад.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnBackClick(object? sender, RoutedEventArgs e) => ViewModel.Back();

    /// <summary>Начинает разбор заново.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnResetClick(object? sender, RoutedEventArgs e) => ViewModel.Restart();

    /// <summary>Играет следующий ход партии.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNextClick(object? sender, RoutedEventArgs e) => ViewModel.Next();

    /// <summary>Просит показать словарь терминов.</summary>
    /// <param name="sender">Кнопка «Словарь терминов».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGlossaryClick(object? sender, RoutedEventArgs e) =>
        GlossaryRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Переключает партию по выбору в списке.</summary>
    /// <param name="sender">Список партий.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnGameChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || !_gameCombo.TryAccept(box.SelectedIndex))
        {
            return;
        }

        ViewModel.SelectGame(box.SelectedIndex);
    }

    /// <summary>Показывает в списке текущую партию.</summary>
    private void SyncGameBox()
    {
        if (_gameBox is null)
        {
            return;
        }

        var replace = _gameCombo.BeginUpdate(ViewModel.Labels, ViewModel.SelectedIndex);

        try
        {
            if (replace)
            {
                _gameBox.ItemsSource = ViewModel.Labels;
            }

            _gameBox.SelectedIndex = ViewModel.SelectedIndex;
        }
        finally
        {
            _gameCombo.EndUpdate();
        }
    }

    /// <summary>Красит сообщение: похвала акцентом, отказ — красным.</summary>
    /// <remarks>
    /// Цвет ставит код вида, а не привязка: значение зависит от вердикта, а кисти берутся
    /// из темы — на тёмной теме красный и акцент остаются читаемыми.
    /// </remarks>
    private void RefreshMessage()
    {
        if (_message is null)
        {
            return;
        }

        _message.Foreground = this.FindResource(ViewModel.IsMessageGood ? "AppAccentBrush" : "AppDangerBrush")
            as Avalonia.Media.IBrush;
    }

    /// <summary>Перестраивает раскладку при изменении размера вида.</summary>
    /// <param name="sender">Вид.</param>
    /// <param name="e">Новый размер.</param>
    private void OnViewSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ApplyLayout(e.NewSize.Width, e.NewSize.Height);

    /// <summary>Раскладывает доску и панель по размеру вида.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    private void ApplyLayout(double width, double height) => _layoutRules?.Apply(width, height);

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
