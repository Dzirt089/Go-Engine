using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GoEngine.AI;
using GoEngine.App.Rendering;
using GoEngine.App.Services.Logging;
using GoEngine.App.Services.Updates;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App;

/// <summary>Приложение Go Engine: окно партии и его жизненный цикл.</summary>
/// <remarks>
/// Слой <c>App</c> — единственный, которому разрешены Avalonia и SkiaSharp
/// (<c>PROJECT.md</c>, <c>AGENTS.md</c>, п. 1). Бизнес-логики здесь нет: правила живут в <c>Core</c>,
/// выбор хода — в <c>AI</c>.
/// На настольных системах приложение открывает окно с меню, на Android — единственный вид
/// с нижней навигацией: жизненный цикл там <see cref="ISingleViewApplicationLifetime"/>.
/// Стартовое состояние у обоих одно: раздел «Партия», поверх доски — карточка «Меню»
/// (замечание пользователя 2026-10-03 «окно меню должно быть первым активным окном над партией»).
/// </remarks>
public sealed partial class App : Application
{
    /// <summary>Оценка позиции нейросетью для уровней Дан.</summary>
    /// <remarks>
    /// Головы задают её до запуска: библиотека интерфейса не знает про ONNX
    /// (<c>DECISIONS.md</c>, D-034), а модель загружает та голова, у которой есть файл.
    /// <c>null</c> — уровни Дан недоступны, играют уровни кю.
    /// </remarks>
    public static IPositionEvaluator? Evaluator { get; set; }

    /// <summary>Каталог с файлами моделей: его задаёт голова, зная, где лежат файлы.</summary>
    /// <remarks>Без каталога уровни Дан недоступны — как и без оценщика (D-039).</remarks>
    public static string? ModelsDirectory { get; set; }

    /// <summary>Стороны доски, для которых нашлась модель.</summary>
    /// <remarks>
    /// Заполняет голова: она знает каталог и таблицу профилей (D-039). Пустое множество
    /// означает, что уровни Дан недоступны ни на одной доске, и играют уровни кю.
    /// </remarks>
    public static IReadOnlySet<int> ModelSizes { get; set; } = new HashSet<int>();

    /// <summary>Строка о состоянии моделей для экрана настроек.</summary>
    /// <remarks>
    /// Заполняет голова: только она знает, откуда берутся модели и что помешало их загрузить.
    /// Пусто — голова о моделях не сообщала, и экран настроек честно скажет, что состояние
    /// неизвестно. Без этой строки отказ виден лишь по отсутствию уровней с сетью, а причину
    /// на телефоне узнать нечем.
    /// </remarks>
    public static string? ModelsStatus { get; set; }

    /// <summary>Установщик обновлений платформы.</summary>
    /// <remarks>
    /// Задаёт голова: библиотека интерфейса не знает, чем ставить обновление — тихой установкой
    /// Windows, системным установщиком Android или ничем на Linux и macOS. <c>null</c> — проверка
    /// обновлений в приложении недоступна, и экран настроек честно об этом скажет.
    /// </remarks>
    public static IUpdateInstaller? UpdateInstaller { get; set; }

    /// <summary>Каталог, куда складываются скачанные обновления.</summary>
    /// <remarks>Задаёт голова: на Android это каталог приложения, на настольных системах — временный.</remarks>
    public static string? UpdateDownloadDirectory { get; set; }

    /// <summary>Открывает папку с логами: задаёт голова, знающая возможности системы.</summary>
    /// <remarks>
    /// На настольных системах это проводник или файловый менеджер, на Android открывать нечего —
    /// там голова обработчик не задаёт, и экран настроек показывает только путь к файлам.
    /// </remarks>
    public static Action<string>? OpenLogsDirectory { get; set; }

    /// <summary>Выход из приложения: задаёт голова, знающая, чем закрывается программа.</summary>
    /// <remarks>
    /// На настольной системе это остановка цикла сообщений (<c>desktop.Shutdown</c>), на Android —
    /// завершение задачи приложения. Головы задают обработчик при запуске; <c>null</c> — закрывать
    /// нечем, и выход из общего меню ничего не делает (так ведут себя проверки без окна).
    /// </remarks>
    public static Action? ExitRequested { get; set; }

    /// <summary>Проигрыватель звука ходов.</summary>
    /// <remarks>
    /// Задаёт голова: библиотека интерфейса не знает, чем играть WAV — <c>winmm</c> на Windows,
    /// внешним проигрывателем на Linux, <c>SoundPool</c> на Android. По умолчанию звука нет
    /// (<see cref="SilentSoundPlayer"/>): так приложение работает и на системе без звуковой
    /// подсистемы, и в тестах, а вид не обращается к проигрывателю напрямую.
    /// </remarks>
    public static ISoundPlayer Sound { get; set; } = new SilentSoundPlayer();

    /// <summary>Закрывает программу: выход из общего меню.</summary>
    /// <remarks>
    /// Спрашивать подтверждение — дело вида (<see cref="GoEngine.App.Views.ShellView"/>): здесь
    /// только само закрытие. Обработчика нет — выходим из игры молча: проверочные режимы и тесты
    /// работают без окна, и падать на нажатии «Выход» они не должны.
    /// </remarks>
    public static void RequestExit() => ExitRequested?.Invoke();

    /// <summary>Создаёт службу обновления, если голова её настроила.</summary>
    /// <returns>Служба обновления или <c>null</c>, если установщик или каталог не заданы.</returns>
    public static UpdateService? CreateUpdateService() =>
        UpdateInstaller is { } installer && !string.IsNullOrWhiteSpace(UpdateDownloadDirectory)
            ? UpdateService.CreateDefault(installer, UpdateDownloadDirectory)
            : null;

    /// <summary>Модель обновления: общая для приглашения в оболочке и блока обновлений в настройках.</summary>
    /// <remarks>
    /// Одна на приложение: иначе проверка в настройках и приглашение при запуске показывали бы
    /// разные состояния одного и того же обновления, а скачивание шло бы дважды. Собирается при
    /// первом обращении, а не при загрузке типа: головы задают установщик и каталог после старта,
    /// и раньше этого собрать службу не из чего.
    /// </remarks>
    public static UpdateViewModel Updates => _updates.Value;

    /// <summary>Отложенная сборка модели обновления.</summary>
    private static readonly Lazy<UpdateViewModel> _updates = new(
        static () => new UpdateViewModel(CreateUpdateService()),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    /// <remarks>
    /// Подписка на исключения диспетчера ставится здесь, а не в точке входа: обращение к
    /// <c>Dispatcher.UIThread</c> до запуска платформы привязывает диспетчер к заглушке,
    /// и цикл сообщений падает с <c>PlatformNotSupportedException</c> (проверено живым запуском).
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) => CrashReporter.Report(e.Exception, "Dispatcher.UIThread");

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow();

                // На настольной системе программа закрывается остановкой цикла сообщений. Голова
                // может задать свой обработчик раньше — тогда остаётся он.
                if (ExitRequested is null)
                {
                    ExitRequested = () => desktop.Shutdown();
                }
                break;

            case ISingleViewApplicationLifetime mobile:
                // На телефоне — та же оболочка с двумя режимами, что и в окне: раздел «Партия»
                // и карточка «Меню» над доской. Экран настроек при запуске не открывается:
                // его выбирает игрок, а не запуск (замечание пользователя 2026-10-03).
                mobile.MainView = new ShellView();
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
