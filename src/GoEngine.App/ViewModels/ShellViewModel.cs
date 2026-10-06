using System.ComponentModel;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.Problems;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.ViewModels;

/// <summary>Оболочка приложения: разделы «Партия», «Задачи» и общее меню.</summary>
/// <remarks>
/// <para>
/// Режимы не смешиваются: партия живёт в <see cref="MainViewModel"/> и своём виде, задачи —
/// в <see cref="ProblemViewModel"/> и своём. Оболочка только показывает одно или другое и владеет
/// обоими моделями, поэтому переключение режима партию не пересоздаёт: вернувшись в «Партию»,
/// игрок видит ту же позицию (<c>DECISIONS.md</c>, D-061).
/// </para>
/// <para>
/// Разделов нижней навигации телефона три: «Партия», «Задачи» и «Меню». Настройки и меню партии
/// переехали внутрь раздела «Партия»: они принадлежат партии и в чужих разделах были бы
/// недоступны или бессмысленны (замечание пользователя 2026-10-06). Общее меню — раздел
/// приложения, а не партии: в нём выбор режима, «О программе» и выход из игры
/// (<c>DECISIONS.md</c>, D-075).
/// </para>
/// </remarks>
public sealed class ShellViewModel : INotifyPropertyChanged
{
    private static readonly string[] PropertyNames =
    [
        nameof(IsGameMode), nameof(IsProblemMode), nameof(ModeHint),
        nameof(IsGameSection), nameof(IsProblemSection), nameof(IsMenuSection)
    ];

    private bool _problemsMode;

    /// <summary>Открыт раздел общего меню: режим при этом не меняется, содержимое уступает ему место.</summary>
    private bool _menuSection;

    /// <summary>Проверка обновления при запуске уже начата: второй раз она не идёт.</summary>
    private bool _updateCheckStarted;

    /// <summary>Логгер режимов: в логе видно, чем игрок занимался до сбоя.</summary>
    private readonly ILogger _log = AppLog.For<ShellViewModel>();

    /// <summary>Создаёт оболочку с готовыми моделями режимов.</summary>
    /// <param name="settings">Настройки партии: их же получает вид партии.</param>
    /// <param name="game">Модель представления партии.</param>
    /// <param name="problems">Модель представления задач.</param>
    /// <param name="updates">
    /// Модель обновления: приглашение и ход загрузки. <c>null</c> — общая модель приложения.
    /// </param>
    public ShellViewModel(
        AppSettings settings,
        MainViewModel game,
        ProblemViewModel problems,
        UpdateViewModel? updates = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(problems);

        Settings = settings;
        Game = game;
        Problems = problems;
        Updates = updates ?? global::GoEngine.App.App.Updates;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Настройки партии: их показывает «Настройка партии».</summary>
    public AppSettings Settings { get; }

    /// <summary>Модель представления партии: живёт, пока приложение открыто.</summary>
    public MainViewModel Game { get; }

    /// <summary>Модель представления задач: живёт, пока приложение открыто.</summary>
    public ProblemViewModel Problems { get; }

    /// <summary>Приглашение обновления и ход загрузки: общее с экраном «О программе» состояние.</summary>
    /// <remarks>
    /// Модель приходит снаружи и по умолчанию берётся у приложения: проверка из «О программе» и
    /// приглашение в оболочке должны говорить об одном обновлении, а не о двух разных.
    /// </remarks>
    public UpdateViewModel Updates { get; }

    /// <summary>Показан режим партии.</summary>
    public bool IsGameMode => !_problemsMode;

    /// <summary>Показан режим задач.</summary>
    public bool IsProblemMode => _problemsMode;

    /// <summary>Выбран раздел партии в нижней навигации.</summary>
    /// <remarks>
    /// Разделы нижней навигации телефона: партия, задачи и общее меню. Настройки — не раздел,
    /// а экран внутри партии, поэтому выбранным может быть только один из трёх.
    /// </remarks>
    public bool IsGameSection => !_problemsMode && !_menuSection;

    /// <summary>Выбран раздел задач в нижней навигации.</summary>
    public bool IsProblemSection => _problemsMode && !_menuSection;

    /// <summary>Открыт раздел общего меню: режим, «О программе» и выход.</summary>
    public bool IsMenuSection => _menuSection;

    /// <summary>Подсказка о текущем режиме — чтобы игрок понимал, где он находится.</summary>
    /// <remarks>
    /// Одна короткая строка: полное описание режима занимало пол-экрана и повторяло то, что
    /// и так видно (жалоба 2026-09-30 — «очень много старого текста»).
    /// </remarks>
    public string ModeHint => _problemsMode
        ? "Задачи: решите позицию, подсказка покажет ход."
        : "Щёлкните по доске, чтобы сделать ход.";

    /// <summary>Собирает оболочку с настройками из файла и оценкой сети, заданной головой.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="evaluator">Оценка позиции нейросетью для уровней Дан; <c>null</c> — без сети.</param>
    /// <param name="problems">Задачи; <c>null</c> — взять встроенную библиотеку.</param>
    /// <param name="updates">Модель обновления; <c>null</c> — общая модель приложения.</param>
    /// <returns>Оболочку с двумя режимами и общим меню.</returns>
    public static ShellViewModel Create(
        AppSettings settings,
        IPositionEvaluator? evaluator = null,
        IReadOnlyList<Problem>? problems = null,
        UpdateViewModel? updates = null) =>
        new(
            settings,
            new MainViewModel(settings, Random.Shared, evaluator),
            problems is null ? new ProblemViewModel() : new ProblemViewModel(problems),
            updates);

    /// <summary>Проверяет обновление при первом показе оболочки: один раз, в фоне и молча.</summary>
    /// <returns>Задача проверки; повторные вызовы ничего не делают.</returns>
    /// <remarks>
    /// Приложение offline-first: проверка не задерживает ни запуск, ни первый ход игрока, а её
    /// отказ (нет сети, нет манифеста) — обычное дело, которое видно в логе, а не на экране.
    /// Приглашение появляется, только если обновление действительно есть. Возвращаемая задача
    /// нужна проверкам: они ждут её, чтобы утверждать об исходе; вид её не ожидает.
    /// </remarks>
    public async Task CheckUpdatesOnStartAsync()
    {
        if (_updateCheckStarted)
        {
            return;
        }

        _updateCheckStarted = true;

        await Updates.CheckAsync(silent: true).ConfigureAwait(true);
    }

    /// <summary>Показывает режим партии.</summary>
    public void ShowGame() => SetSection(problems: false, menu: false);

    /// <summary>Показывает режим задач.</summary>
    public void ShowProblems() => SetSection(problems: true, menu: false);

    /// <summary>Открывает общее меню: режим, «О программе» и выход из игры.</summary>
    /// <remarks>
    /// Режим меню не меняет: закрыв его, игрок возвращается туда же, где был. Так «Меню» остаётся
    /// разделом приложения, а не третьим режимом, и выбор режима внутри него — обычный переход.
    /// </remarks>
    public void ShowMenu() => SetSection(problems: _problemsMode, menu: true);

    /// <summary>Переключает раздел и сообщает об этом виду.</summary>
    /// <param name="problems">Показывать задачи.</param>
    /// <param name="menu">Показать общее меню вместо содержимого режима.</param>
    /// <remarks>
    /// Повторный выбор того же раздела ничего не меняет и ни о чём не сообщает: иначе вид
    /// перерисовывался бы на каждое нажатие выбранной кнопки.
    /// </remarks>
    private void SetSection(bool problems, bool menu)
    {
        if (_problemsMode == problems && _menuSection == menu)
        {
            return;
        }

        AppLogMessages.ModeChanged(_log, SectionName(problems, menu));

        _problemsMode = problems;
        _menuSection = menu;

        if (problems)
        {
            // Партия уходит с экрана: поиск хода соперника больше не нужен, а его результат
            // не должен примениться, пока игрок решает задачи.
            Game.CancelThinking();
        }

        NotifyAll();
    }

    /// <summary>Называет раздел для журнала: в логе видно, что игрок открыл перед сбоем.</summary>
    /// <param name="problems">Показаны задачи.</param>
    /// <param name="menu">Показано общее меню.</param>
    /// <returns>Название раздела.</returns>
    private static string SectionName(bool problems, bool menu) => menu
        ? "Меню"
        : problems ? "Задачи" : "Партия";

    /// <summary>Сообщает виду обо всех свойствах оболочки.</summary>
    /// <remarks>
    /// Сообщаем обо всех: разделы нижней навигации зависят и от режима, и от общего меню,
    /// а вычислять, что именно изменилось, здесь нечего — свойств шесть.
    /// </remarks>
    private void NotifyAll()
    {
        foreach (var name in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
