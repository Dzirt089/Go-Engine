using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.AI;
using GoEngine.App.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Доска партии и панель управления — общая часть окна и мобильного вида.</summary>
/// <remarks>
/// Вид связывает доску с моделью представления: щелчок по доске превращается в ход игрока,
/// после которого отвечает AI. Пас, отмена, возврат и новая партия не требуют окна и работают
/// здесь же; диалог настроек и выбор файлов партии — события <see cref="SettingsRequested"/>,
/// <see cref="SaveRequested"/> и <see cref="LoadRequested"/>, на которые подписывается окно
/// (<see cref="MainWindow"/>): на Android их нет, поэтому кнопки скрыты до вызова
/// <see cref="EnableDesktopActions"/>.
/// </remarks>
public sealed partial class BoardView : UserControl
{
    private readonly BoardControl? _boardControl;

    /// <summary>Создаёт вид с настройками из файла и оценкой сети, заданной головой.</summary>
    public BoardView() : this(SettingsStore.Load(), global::GoEngine.App.App.Evaluator)
    {
    }

    /// <summary>Создаёт вид с готовыми настройками.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="evaluator">Оценка позиции нейросетью для уровней Дан.</param>
    public BoardView(AppSettings settings, IPositionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AvaloniaXamlLoader.Load(this);

        ViewModel = new MainViewModel(settings, Random.Shared, evaluator);
        DataContext = ViewModel;

        _boardControl = this.FindControl<BoardControl>("Board");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        WireButton("PassButton", OnPassClick);
        WireButton("UndoButton", OnUndoClick);
        WireButton("RedoButton", OnRedoClick);
        WireButton("NewGameButton", OnNewGameClick);
        WireButton("SettingsButton", OnSettingsClick);
        WireButton("SaveButton", OnSaveClick);
        WireButton("LoadButton", OnLoadClick);
    }

    /// <summary>Запрошен диалог настроек: его показывает окно.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Запрошено сохранение партии: файл выбирает окно.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Запрошена загрузка партии: файл выбирает окно.</summary>
    public event EventHandler? LoadRequested;

    /// <summary>Модель представления партии.</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>Показывает кнопки, которым нужны окно и файловая система.</summary>
    /// <remarks>Вызывает окно: на мобильном виде этих действий нет, и кнопки остаются скрытыми.</remarks>
    public void EnableDesktopActions()
    {
        SetVisible("SettingsButton");
        SetVisible("SaveButton");
        SetVisible("LoadButton");
    }

    /// <summary>Запускает анимацию, когда партия показала новый ход.</summary>
    /// <param name="sender">Модель представления.</param>
    /// <param name="e">Событие изменения свойства.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.LastMove))
        {
            return;
        }

        _boardControl?.Animate(ViewModel.LastMove, ViewModel.LastCaptured, ViewModel.LastCapturedColor);
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

    /// <summary>Показывает кнопку, если она есть в разметке.</summary>
    /// <param name="name">Имя кнопки.</param>
    private void SetVisible(string name)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.IsVisible = true;
        }
    }

    /// <summary>Обрабатывает щелчок по доске.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка хода.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => ViewModel.PlayMove(e.Point);

    /// <summary>Передаёт ход.</summary>
    /// <param name="sender">Кнопка «Пас».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnPassClick(object? sender, RoutedEventArgs e) => ViewModel.Pass();

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
    private void OnNewGameClick(object? sender, RoutedEventArgs e) => ViewModel.StartNewGame();

    /// <summary>Просит окно показать настройки.</summary>
    /// <param name="sender">Кнопка «Настройки».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Просит окно сохранить партию.</summary>
    /// <param name="sender">Кнопка «Сохранить партию».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnSaveClick(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Просит окно загрузить партию.</summary>
    /// <param name="sender">Кнопка «Загрузить партию».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnLoadClick(object? sender, RoutedEventArgs e) => LoadRequested?.Invoke(this, EventArgs.Empty);
}
