using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Доска партии и панель статуса — общая часть окна и мобильного вида.</summary>
/// <remarks>
/// Вид связывает доску с моделью представления: щелчок по доске превращается в ход игрока,
/// после которого отвечает AI. Меню и файловые диалоги остались в окне (<see cref="MainWindow"/>):
/// на Android их нет, а доска и панель нужны те же — поэтому они вынесены сюда.
/// </remarks>
public sealed partial class BoardView : UserControl
{
    private readonly BoardControl? _boardControl;

    /// <summary>Создаёт вид с настройками из файла.</summary>
    public BoardView() : this(SettingsStore.Load())
    {
    }

    /// <summary>Создаёт вид с готовыми настройками.</summary>
    /// <param name="settings">Настройки партии.</param>
    public BoardView(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AvaloniaXamlLoader.Load(this);

        ViewModel = new MainViewModel(settings, Random.Shared);
        DataContext = ViewModel;

        _boardControl = this.FindControl<BoardControl>("Board");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        WireButton("UndoButton", OnUndoClick);
        WireButton("RedoButton", OnRedoClick);
    }

    /// <summary>Модель представления партии.</summary>
    public MainViewModel ViewModel { get; }

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

    /// <summary>Обрабатывает щелчок по доске.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка хода.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => ViewModel.PlayMove(e.Point);

    /// <summary>Отменяет последний ход игрока вместе с ответом AI.</summary>
    /// <param name="sender">Кнопка «Отменить».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnUndoClick(object? sender, RoutedEventArgs e) => ViewModel.Undo();

    /// <summary>Возвращает отменённый ход.</summary>
    /// <param name="sender">Кнопка «Вернуть».</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnRedoClick(object? sender, RoutedEventArgs e) => ViewModel.Redo();
}
