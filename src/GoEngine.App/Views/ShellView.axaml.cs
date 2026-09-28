using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Оболочка приложения: переключатель режимов «Партия» и «Задачи».</summary>
/// <remarks>
/// Оба режима живут одновременно: виды не пересоздаются, меняется только видимость. Поэтому
/// переключение режима не сбрасывает партию — вернувшись в «Партию», игрок видит ту же позицию.
/// Тот же вид используется настольным окном и мобильной головой (<c>GoEngine.App.Android</c>).
/// </remarks>
public sealed partial class ShellView : UserControl
{
    private readonly Panel? _content;
    private readonly ToggleButton? _gameButton;
    private readonly ToggleButton? _problemButton;

    /// <summary>Создаёт оболочку с настройками из файла и оценкой сети, заданной головой.</summary>
    public ShellView() : this(ShellViewModel.Create(SettingsStore.Load(), global::GoEngine.App.App.Evaluator))
    {
    }

    /// <summary>Создаёт оболочку с готовой моделью.</summary>
    /// <param name="shell">Модель оболочки: обе модели режимов и настройки партии.</param>
    public ShellView(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        Shell = shell;

        AvaloniaXamlLoader.Load(this);

        DataContext = shell;

        _content = this.FindControl<Panel>("ModeContent");
        _gameButton = this.FindControl<ToggleButton>("GameModeButton");
        _problemButton = this.FindControl<ToggleButton>("ProblemModeButton");

        if (_content is not null)
        {
            // Модели берутся у оболочки: у вида партии и вида задач они те же, что в модели оболочки.
            GameArea = new BoardView(shell.Settings, global::GoEngine.App.App.Evaluator, shell.Game);
            ProblemArea = new ProblemView(shell.Problems);

            _content.Children.Add(GameArea);
            _content.Children.Add(ProblemArea);
        }

        if (_gameButton is not null)
        {
            _gameButton.Click += OnGameClick;
        }

        if (_problemButton is not null)
        {
            _problemButton.Click += OnProblemClick;
        }

        shell.PropertyChanged += OnShellPropertyChanged;

        UpdateMode();
    }

    /// <summary>Модель оболочки.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Вид партии: его же использует меню окна.</summary>
    public BoardView? GameArea { get; }

    /// <summary>Вид задач.</summary>
    public ProblemView? ProblemArea { get; }

    /// <summary>Показывает режим партии.</summary>
    /// <param name="sender">Кнопка режима.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnGameClick(object? sender, RoutedEventArgs e) => Shell.ShowGame();

    /// <summary>Показывает режим задач.</summary>
    /// <param name="sender">Кнопка режима.</param>
    /// <param name="e">Событие нажатия.</param>
    private void OnProblemClick(object? sender, RoutedEventArgs e) => Shell.ShowProblems();

    /// <summary>Обновляет видимость режимов, когда модель сообщает о переключении.</summary>
    /// <param name="sender">Модель оболочки.</param>
    /// <param name="e">Имя изменившегося свойства.</param>
    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsGameMode) or nameof(ShellViewModel.IsProblemMode))
        {
            UpdateMode();
        }
    }

    /// <summary>Показывает выбранный режим и приводит кнопки в согласованное состояние.</summary>
    private void UpdateMode()
    {
        if (GameArea is not null)
        {
            GameArea.IsVisible = Shell.IsGameMode;
        }

        if (ProblemArea is not null)
        {
            ProblemArea.IsVisible = Shell.IsProblemMode;
        }

        if (_gameButton is not null)
        {
            _gameButton.IsChecked = Shell.IsGameMode;
        }

        if (_problemButton is not null)
        {
            _problemButton.IsChecked = Shell.IsProblemMode;
        }
    }
}
