using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GoEngine.AI;
using GoEngine.App.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Доска партии и панель управления — общая часть окна и мобильного вида.</summary>
/// <remarks>
/// Вид связывает доску с моделью представления: щелчок по доске превращается в ход игрока,
/// после которого отвечает AI. Здесь же всё, что нужно и без окна: пас, отмена, возврат, новая
/// партия, выбор доски и уровня, сохранение и загрузка партии, настройки.
/// Раскладка выбирается по размеру вида (<see cref="BoardLayoutRules"/>): на телефоне доска
/// сверху и управление снизу, на широком экране — как в настольной версии. Настройки на
/// настольной системе показывает окно (<see cref="SettingsWindow"/>, подписка на
/// <see cref="SettingsRequested"/>), а если окна нет — тот же <see cref="SettingsView"/>
/// показывается поверх доски.
/// </remarks>
public sealed partial class BoardView : UserControl
{
    private readonly BoardControl? _boardControl;
    private readonly Grid? _layout;
    private readonly Border? _panel;
    private readonly Border? _overlay;
    private readonly SettingsView? _settingsView;
    private readonly TextBlock? _hint;

    /// <summary>Настройки, с которыми играет вид: их показывает панель настроек.</summary>
    private AppSettings _settings;

    /// <summary>Состояние списка размеров: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _sizeCombo = new();

    /// <summary>Состояние списка уровней: программное обновление не считается выбором игрока.</summary>
    private readonly ComboState _levelCombo = new();

    /// <summary>Текущая раскладка: <c>null</c> — ещё не выбрана.</summary>
    private bool? _narrow;

    /// <summary>Создаёт вид с настройками из файла и оценкой сети, заданной головой.</summary>
    public BoardView() : this(SettingsStore.Load(), global::GoEngine.App.App.Evaluator)
    {
    }

    /// <summary>Создаёт вид с готовыми настройками.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="evaluator">Оценка позиции нейросетью для уровней Дан.</param>
    /// <param name="viewModel">Готовая модель партии; <c>null</c> — создать свою.</param>
    /// <remarks>
    /// Готовая модель нужна оболочке режимов: она владеет моделью партии, поэтому переключение
    /// между партией и задачами не сбрасывает партию (<c>DECISIONS.md</c>, D-061). Без неё вид
    /// создаёт модель сам — так его по-прежнему можно собрать одним конструктором.
    /// </remarks>
    public BoardView(AppSettings settings, IPositionEvaluator? evaluator = null, MainViewModel? viewModel = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;

        AvaloniaXamlLoader.Load(this);

        ViewModel = viewModel ?? new MainViewModel(settings, Random.Shared, evaluator);
        DataContext = ViewModel;

        _boardControl = this.FindControl<BoardControl>("Board");
        _layout = this.FindControl<Grid>("Layout");
        _panel = this.FindControl<Border>("Panel");
        _overlay = this.FindControl<Border>("SettingsOverlay");
        _settingsView = this.FindControl<SettingsView>("SettingsArea");
        _hint = this.FindControl<TextBlock>("Hint");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        if (_settingsView is not null)
        {
            _settingsView.Accepted += OnSettingsAccepted;
            _settingsView.Cancelled += OnSettingsCancelled;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        WireButton("PassButton", OnPassClick);
        WireButton("UndoButton", OnUndoClick);
        WireButton("RedoButton", OnRedoClick);
        WireButton("NewGameButton", OnNewGameClick);
        WireButton("SettingsButton", OnSettingsClick);
        WireButton("SaveButton", OnSaveClick);
        WireButton("LoadButton", OnLoadClick);

        if (this.FindControl<ComboBox>("SizeBox") is { } sizeBox)
        {
            sizeBox.SelectionChanged += OnSizeChanged;
        }

        if (this.FindControl<ComboBox>("LevelBox") is { } levelBox)
        {
            levelBox.SelectionChanged += OnLevelChanged;
        }

        SizeChanged += OnViewSizeChanged;

        SyncCombos();
        ApplyLayout(Bounds.Width, Bounds.Height);
    }

    /// <summary>Запрошены настройки: окно, если оно есть, показывает диалог.</summary>
    /// <remarks>Без подписчика (мобильный вид) настройки показываются поверх доски.</remarks>
    public event EventHandler? SettingsRequested;

    /// <summary>Модель представления партии.</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>Показывает настройки: в окне, если окно есть, иначе поверх доски.</summary>
    public void ShowSettings()
    {
        if (SettingsRequested is not null)
        {
            SettingsRequested.Invoke(this, EventArgs.Empty);
            return;
        }

        _settingsView?.Initialize(_settings, ViewModel.HasModelFor);

        if (_overlay is not null)
        {
            _overlay.IsVisible = true;
        }
    }

    /// <summary>Спрашивает файл и записывает в него партию.</summary>
    /// <returns>Задача сохранения.</returns>
    /// <remarks>
    /// Пишем в поток, а не в путь: на телефоне у выбранного файла локального пути может не быть.
    /// </remarks>
    public async Task SaveGameAsync()
    {
        var file = await PickSaveFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            var saved = SgfStore.Save(ViewModel.ToSgfGame(), stream);

            ShowHint(saved.IsSuccess
                ? $"Партия сохранена: {file.Name}"
                : saved.Error ?? "Не удалось сохранить партию.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowHint($"Не удалось сохранить партию: {exception.Message}");
        }
    }

    /// <summary>Спрашивает файл и загружает из него партию.</summary>
    /// <returns>Задача загрузки.</returns>
    public async Task LoadGameAsync()
    {
        var file = await PickOpenFileAsync();

        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            var loaded = SgfStore.Load(stream);

            if (!loaded.IsSuccess)
            {
                ShowHint(loaded.Error ?? "Не удалось прочитать партию.");
                return;
            }

            _ = ViewModel.LoadGameAsync(loaded.Value);
            ShowHint($"Партия загружена: {file.Name}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowHint($"Не удалось прочитать партию: {exception.Message}");
        }
    }

    /// <summary>Выбирает файл для сохранения партии.</summary>
    /// <returns>Файл или <c>null</c>, если игрок отказался.</returns>
    private async Task<IStorageFile?> PickSaveFileAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } provider)
        {
            ShowHint("Система не даёт сохранить файл: нет доступа к выбору файлов.");
            return null;
        }

        return await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить партию",
            SuggestedFileName = "game",
            DefaultExtension = SgfStore.Extension,
            FileTypeChoices = [SgfFileType]
        });
    }

    /// <summary>Выбирает файл с партией для загрузки.</summary>
    /// <returns>Файл или <c>null</c>, если игрок отказался.</returns>
    private async Task<IStorageFile?> PickOpenFileAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } provider)
        {
            ShowHint("Система не даёт открыть файл: нет доступа к выбору файлов.");
            return null;
        }

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Загрузить партию",
            AllowMultiple = false,
            FileTypeFilter = [SgfFileType]
        });

        return files.Count > 0 ? files[0] : null;
    }

    /// <summary>Тип файла SGF для диалогов выбора файла.</summary>
    private static FilePickerFileType SgfFileType => new("Партия Go (SGF)")
    {
        Patterns = [$"*.{SgfStore.Extension}"]
    };

    /// <summary>Показывает сообщение под панелью.</summary>
    /// <param name="message">Текст сообщения.</param>
    private void ShowHint(string message)
    {
        if (_hint is null)
        {
            return;
        }

        _hint.Text = message;
        _hint.IsVisible = true;
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
    /// Узкий экран (телефон в портрете) — доска сверху квадратом, управление снизу под прокруткой.
    /// Широкий — как в настольной версии: доска слева, панель справа.
    /// </remarks>
    private void ApplyLayout(double width, double height)
    {
        if (_layout is null || _boardControl is null || _panel is null)
        {
            return;
        }

        var narrow = BoardLayoutRules.IsNarrow(width, height);

        if (_narrow == narrow)
        {
            return;
        }

        _narrow = narrow;

        _layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,320");
        _layout.RowDefinitions = new RowDefinitions(narrow ? "Auto,*" : "*");

        Grid.SetColumn(_boardControl, 0);
        Grid.SetRow(_boardControl, 0);
        Grid.SetColumn(_panel, narrow ? 0 : 1);
        Grid.SetRow(_panel, narrow ? 1 : 0);

        _boardControl.Height = narrow ? BoardLayoutRules.BoardHeight(width, height) : double.NaN;
        _panel.BorderThickness = narrow ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
    }

    /// <summary>Применяет настройки, выбранные в панели настроек, и убирает её.</summary>
    /// <param name="sender">Вид настроек.</param>
    /// <param name="e">Событие согласия.</param>
    private void OnSettingsAccepted(object? sender, EventArgs e)
    {
        if (_settingsView is null)
        {
            return;
        }

        _settings = _settingsView.Selected;

        // Партия начинается сразу, а первый ход соперника считается в фоне: иначе окно
        // замирало бы на время поиска, а анимация хода не была бы видна.
        _ = ViewModel.ApplySettingsAsync(_settings);

        // Неудачная запись настроек не мешает играть: значения уже применены к партии.
        _ = SettingsStore.Save(_settings);

        HideSettings();
    }

    /// <summary>Убирает панель настроек без изменений.</summary>
    /// <param name="sender">Вид настроек.</param>
    /// <param name="e">Событие отказа.</param>
    private void OnSettingsCancelled(object? sender, EventArgs e) => HideSettings();

    /// <summary>Прячет панель настроек.</summary>
    private void HideSettings()
    {
        if (_overlay is not null)
        {
            _overlay.IsVisible = false;
        }
    }

    /// <summary>Запускает анимацию, когда партия показала новый ход.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Событие изменения свойства.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LastMove))
        {
            _boardControl?.Animate(ViewModel.LastMove, ViewModel.LastCaptured, ViewModel.LastCapturedColor);
            return;
        }

        // Партия пересоздана (смена доски, уровня, настроек) — списки выбора должны это показать.
        if (e.PropertyName is nameof(MainViewModel.LevelLabels)
            or nameof(MainViewModel.SelectedLevelIndex)
            or nameof(MainViewModel.SelectedSizeIndex))
        {
            SyncCombos();
        }
    }

    /// <summary>Показывает в списках выбора то, чем играет партия сейчас.</summary>
    private void SyncCombos()
    {
        Sync(_sizeCombo, "SizeBox", ViewModel.SizeLabels, ViewModel.SelectedSizeIndex);
        Sync(_levelCombo, "LevelBox", ViewModel.LevelLabels, ViewModel.SelectedLevelIndex);
    }

    /// <summary>Показывает в одном списке то, что показывает модель представления.</summary>
    /// <param name="state">Состояние списка.</param>
    /// <param name="name">Имя списка в разметке.</param>
    /// <param name="labels">Подписи из модели представления.</param>
    /// <param name="index">Выбранный индекс.</param>
    /// <remarks>
    /// На время обновления изменения списка игнорируются: он сообщает о сбросе индекса, а не
    /// о выборе игрока. Подписи присваиваются только новым экземпляром: подмена того же самого
    /// списка сбрасывала бы выбранный уровень.
    /// </remarks>
    private void Sync(ComboState state, string name, IReadOnlyList<string> labels, int index)
    {
        var replace = state.BeginUpdate(labels, index);

        try
        {
            if (this.FindControl<ComboBox>(name) is not { } box)
            {
                return;
            }

            if (replace)
            {
                box.ItemsSource = labels;
            }

            box.SelectedIndex = index;
        }
        finally
        {
            state.EndUpdate();
        }
    }

    /// <summary>Применяет выбранный размер доски.</summary>
    /// <param name="sender">Список размеров.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || !_sizeCombo.TryAccept(box.SelectedIndex))
        {
            return;
        }

        ViewModel.SelectedSizeIndex = box.SelectedIndex;
        SyncCombos();
    }

    /// <summary>Применяет выбранный уровень.</summary>
    /// <param name="sender">Список уровней.</param>
    /// <param name="e">Событие смены выбора.</param>
    private void OnLevelChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || !_levelCombo.TryAccept(box.SelectedIndex))
        {
            return;
        }

        ViewModel.SelectedLevelIndex = box.SelectedIndex;
        SyncCombos();
    }

    /// <summary>Подписывает кнопку на обработчик.</summary>
    /// <param name="name">Имя кнопки.</param>
    /// <param name="handler">Обработчик нажатия.</param>
    private void WireButton(string name, EventHandler<RoutedEventArgs> handler)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += handler;
        }
    }

    /// <summary>Обрабатывает щелчок по доске.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка хода.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => _ = ViewModel.PlayMoveAsync(e.Point);

    /// <summary>Передаёт ход.</summary>
    /// <param name="sender">Кнопка «Пас».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnPassClick(object? sender, RoutedEventArgs e) => _ = ViewModel.PassAsync();

    /// <summary>Отменяет последний ход игрока вместе с ответом AI.</summary>
    /// <param name="sender">Кнопка «Отменить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnUndoClick(object? sender, RoutedEventArgs e) => ViewModel.Undo();

    /// <summary>Возвращает отменённый ход.</summary>
    /// <param name="sender">Кнопка «Вернуть».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnRedoClick(object? sender, RoutedEventArgs e) => ViewModel.Redo();

    /// <summary>Начинает новую партию по выбранным доске и уровню.</summary>
    /// <param name="sender">Кнопка «Новая партия».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnNewGameClick(object? sender, RoutedEventArgs e) => _ = ViewModel.StartNewGameAsync();

    /// <summary>Показывает настройки.</summary>
    /// <param name="sender">Кнопка «Настройки».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => ShowSettings();

    /// <summary>Сохраняет партию.</summary>
    /// <param name="sender">Кнопка «Сохранить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SaveGameAsync();

    /// <summary>Загружает партию.</summary>
    /// <param name="sender">Кнопка «Загрузить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLoadClick(object? sender, RoutedEventArgs e) => _ = LoadGameAsync();
}
