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

/// <summary>Вид обучения: доска урока и панель с текстом шага и переходами.</summary>
/// <remarks>
/// <para>
/// Отдельный от партии и задач вид: своя модель (<see cref="LessonViewModel"/>), своя панель,
/// но та же доска <see cref="BoardControl"/>, что и в партии, — второго рендера нет. Раскладка
/// выбирается по размеру вида (<see cref="BoardLayoutRules"/>), поэтому обучение работает
/// и на телефоне.
/// </para>
/// <para>
/// Словарь терминов открывает оболочка (<see cref="ShellView"/>): это отдельный экран, и вид
/// только сообщает о нажатии — тем же способом, что «О программе» в общем меню.
/// </para>
/// </remarks>
public sealed partial class LessonView : UserControl
{
    private readonly BoardControl? _boardControl;
    private readonly Grid? _layout;
    private readonly Border? _panel;
    private readonly ComboBox? _lessonBox;
    private readonly TextBlock? _heading;
    private readonly TextBlock? _lessonTitle;
    private readonly TextBlock? _lessonSummary;
    private readonly TextBlock? _message;

    /// <summary>Состояние списка уроков: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _lessonCombo = new();

    /// <summary>Раскладка «доска и панель»: общая с задачами, уроками и разбором партий.</summary>
    private readonly BoardPanelLayout? _layoutRules;

    /// <summary>Создаёт вид обучения со встроенной библиотекой уроков.</summary>
    public LessonView()
        : this(new LessonViewModel())
    {
    }

    /// <summary>Создаёт вид обучения с готовой моделью представления.</summary>
    /// <param name="viewModel">Модель представления обучения.</param>
    public LessonView(LessonViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;

        AvaloniaXamlLoader.Load(this);

        DataContext = viewModel;

        _boardControl = this.FindControl<BoardControl>("Board");
        _layout = this.FindControl<Grid>("Layout");
        _panel = this.FindControl<Border>("Panel");
        _lessonBox = this.FindControl<ComboBox>("LessonBox");
        _heading = this.FindControl<TextBlock>("Heading");
        _lessonTitle = this.FindControl<TextBlock>("LessonTitleText");
        _lessonSummary = this.FindControl<TextBlock>("LessonSummaryText");
        _message = this.FindControl<TextBlock>("MessageText");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        if (_lessonBox is not null)
        {
            _lessonBox.SelectionChanged += OnLessonChanged;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        Wire("HintButton", OnHintClick);
        Wire("BackButton", OnBackClick);
        Wire("ResetButton", OnResetClick);
        Wire("NextButton", OnNextClick);
        Wire("PreviousLessonButton", OnPreviousLessonClick);
        Wire("NextLessonButton", OnNextLessonClick);
        Wire("GlossaryButton", OnGlossaryClick);

        if (_layout is not null && _boardControl is not null && _panel is not null)
        {
            _layoutRules = new BoardPanelLayout(
                _layout,
                _boardControl,
                _panel,
                [.. new Control?[] { _heading, _lessonTitle, _lessonSummary }.OfType<Control>()]);
        }

        SizeChanged += OnViewSizeChanged;
        ApplyLayout(Bounds.Width, Bounds.Height);
        SyncLessonBox();
        RefreshMessage();
    }

    /// <summary>Запрошен словарь терминов: экран показывает оболочка.</summary>
    public event EventHandler? GlossaryRequested;

    /// <summary>Модель представления обучения.</summary>
    public LessonViewModel ViewModel { get; }

    /// <summary>Обновляет список уроков и цвет сообщения, когда модель сообщает об изменениях.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LessonViewModel.SelectedIndex))
        {
            SyncLessonBox();
        }

        if (e.PropertyName is nameof(LessonViewModel.LastMove))
        {
            // Та же анимация, что и в партии: поставленный камень и снятые не должны появляться
            // рывком — жалоба «всё происходит резко в один миг» касается и уроков.
            _boardControl?.Animate(
                ViewModel.LastMove,
                ViewModel.LastCaptured,
                ViewModel.LastCapturedColor);
        }

        if (e.PropertyName is nameof(LessonViewModel.Message) or nameof(LessonViewModel.IsMessageGood))
        {
            RefreshMessage();
        }
    }

    /// <summary>Играет ход игрока по щелчку в доску.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка щелчка.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => ViewModel.Play(e.Point);

    /// <summary>Показывает подсказку шага.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnHintClick(object? sender, RoutedEventArgs e) => ViewModel.ShowHint();

    /// <summary>Возвращает предыдущий шаг.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnBackClick(object? sender, RoutedEventArgs e) => ViewModel.Back();

    /// <summary>Начинает урок заново.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnResetClick(object? sender, RoutedEventArgs e) => ViewModel.Restart();

    /// <summary>Переходит к следующему шагу.</summary>
    /// <param name="sender">Кнопка.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNextClick(object? sender, RoutedEventArgs e) => ViewModel.Next();

    /// <summary>Открывает предыдущий урок списка.</summary>
    /// <param name="sender">Стрелка «назад» в шапке панели.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnPreviousLessonClick(object? sender, RoutedEventArgs e) => ViewModel.PreviousLesson();

    /// <summary>Открывает следующий урок списка.</summary>
    /// <param name="sender">Стрелка «вперёд» в шапке панели.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNextLessonClick(object? sender, RoutedEventArgs e) => ViewModel.NextLesson();

    /// <summary>Просит показать словарь терминов.</summary>
    /// <param name="sender">Кнопка «Словарь терминов».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGlossaryClick(object? sender, RoutedEventArgs e) =>
        GlossaryRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Переключает урок по выбору в списке.</summary>
    /// <param name="sender">Список уроков.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnLessonChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || !_lessonCombo.TryAccept(box.SelectedIndex))
        {
            return;
        }

        ViewModel.SelectLesson(box.SelectedIndex);
    }

    /// <summary>Показывает в списке текущий урок.</summary>
    private void SyncLessonBox()
    {
        if (_lessonBox is null)
        {
            return;
        }

        var replace = _lessonCombo.BeginUpdate(ViewModel.Labels, ViewModel.SelectedIndex);

        try
        {
            if (replace)
            {
                _lessonBox.ItemsSource = ViewModel.Labels;
            }

            _lessonBox.SelectedIndex = ViewModel.SelectedIndex;
        }
        finally
        {
            _lessonCombo.EndUpdate();
        }
    }

    /// <summary>Красит сообщение шага: похвала акцентом, отказ — красным.</summary>
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
