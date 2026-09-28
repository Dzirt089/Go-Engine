using System.ComponentModel;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Problems;

namespace GoEngine.App.ViewModels;

/// <summary>Оболочка приложения: переключатель двух режимов — партии и задач.</summary>
/// <remarks>
/// Режимы не смешиваются: партия живёт в <see cref="MainViewModel"/> и своём виде, задачи —
/// в <see cref="ProblemViewModel"/> и своём. Оболочка только показывает одно или другое и владеет
/// обоими моделями, поэтому переключение режима партию не пересоздаёт: вернувшись в «Партию»,
/// игрок видит ту же позицию (<c>DECISIONS.md</c>, D-061).
/// </remarks>
public sealed class ShellViewModel : INotifyPropertyChanged
{
    private static readonly string[] PropertyNames =
    [
        nameof(IsGameMode), nameof(IsProblemMode), nameof(ModeHint)
    ];

    private bool _problemsMode;

    /// <summary>Создаёт оболочку с готовыми моделями режимов.</summary>
    /// <param name="settings">Настройки партии: их же получает вид партии.</param>
    /// <param name="game">Модель представления партии.</param>
    /// <param name="problems">Модель представления задач.</param>
    public ShellViewModel(AppSettings settings, MainViewModel game, ProblemViewModel problems)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(problems);

        Settings = settings;
        Game = game;
        Problems = problems;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Настройки партии: их показывает экран настроек.</summary>
    public AppSettings Settings { get; }

    /// <summary>Модель представления партии: живёт, пока приложение открыто.</summary>
    public MainViewModel Game { get; }

    /// <summary>Модель представления задач: живёт, пока приложение открыто.</summary>
    public ProblemViewModel Problems { get; }

    /// <summary>Показан режим партии.</summary>
    public bool IsGameMode => !_problemsMode;

    /// <summary>Показан режим задач.</summary>
    public bool IsProblemMode => _problemsMode;

    /// <summary>Подсказка о текущем режиме — чтобы игрок понимал, где он находится.</summary>
    public string ModeHint => _problemsMode
        ? "Задачи на жизнь и смерть: решите позицию, подсказка покажет первый правильный ход."
        : "Партия с соперником: щёлкните по доске, чтобы сделать ход.";

    /// <summary>Собирает оболочку с настройками из файла и оценкой сети, заданной головой.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="evaluator">Оценка позиции нейросетью для уровней Дан; <c>null</c> — без сети.</param>
    /// <param name="problems">Задачи; <c>null</c> — взять встроенную библиотеку.</param>
    /// <returns>Оболочка с двумя режимами.</returns>
    public static ShellViewModel Create(
        AppSettings settings,
        IPositionEvaluator? evaluator = null,
        IReadOnlyList<Problem>? problems = null) =>
        new(
            settings,
            new MainViewModel(settings, Random.Shared, evaluator),
            problems is null ? new ProblemViewModel() : new ProblemViewModel(problems));

    /// <summary>Показывает режим партии.</summary>
    public void ShowGame() => SetMode(problems: false);

    /// <summary>Показывает режим задач.</summary>
    public void ShowProblems() => SetMode(problems: true);

    /// <summary>Переключает режим и сообщает об этом виду.</summary>
    /// <param name="problems">Показывать задачи.</param>
    private void SetMode(bool problems)
    {
        if (_problemsMode == problems)
        {
            return;
        }

        _problemsMode = problems;

        foreach (var name in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
