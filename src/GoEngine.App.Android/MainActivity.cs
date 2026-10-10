using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;

namespace GoEngine.App.Android;

/// <summary>Точка входа приложения на Android: доска и панель статуса без меню.</summary>
/// <remarks>
/// Вид берётся общий с настольной версией (<c>GoEngine.App.Views.BoardView</c>), поэтому
/// правила и модель представления на телефоне те же. Модели нейросети копируются из пакета
/// в каталог приложения до создания вида: список уровней строится в конструкторе вида,
/// и уровни с сетью должны быть уже доступны.
/// </remarks>
[Activity(
    Label = "Go Engine",
    Icon = "@mipmap/ic_launcher",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity<global::GoEngine.App.App>
{
    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Логи и обработчики падений — раньше остальных: падение на старте тоже обязано
        // оставить файл, который игрок сможет прислать.
        global::GoEngine.App.Services.Logging.AppLog.Initialize(LogsDirectory());
        global::GoEngine.App.Services.Logging.CrashReporter.Install();

        // Копирование моделей из пакета и загрузка нейросети переехали в фоновую подготовку:
        // раньше они шли до создания интерфейса, и первые секунды после запуска телефон показывал
        // пустой экран — игрок думал, что приложение зависло (жалоба пользователя 2026-10-10).
        // Экран загрузки показывает оболочка (App): она открывает его первым и убирает,
        // когда подготовка закончена.
        var activity = this;

        global::GoEngine.App.App.Prepare = () => AndroidModelStartup.Prepare(activity);
        global::GoEngine.App.App.Sound = new AndroidSoundPlayer(this);
        global::GoEngine.App.App.ExitRequested = ExitGame;
        ConfigureUpdates();

        base.OnCreate(savedInstanceState);
    }

    /// <summary>Закрывает приложение: выход из общего меню.</summary>
    /// <remarks>
    /// <para>
    /// На Android «закрыть программу» — это завершить задачу приложения: система не даёт
    /// приложению выйти самому, но задачу можно убрать целиком. FinishAffinity закрывает
    /// все активити задачи, а процесс завершается следом: без этого приложение осталось бы висеть
    /// в недавних с фоновыми потоками поиска хода. Подтверждение выхода спрашивает вид
    /// (ShellView) — сюда приходит уже решённое «выходим».
    /// </para>
    /// <para>
    /// Журнал закрывается до завершения процесса: иначе последние записи остались бы в буфере,
    /// и по логу нельзя было бы понять, чем закончился сеанс.
    /// </para>
    /// </remarks>
    private void ExitGame()
    {
        global::GoEngine.App.Services.Logging.AppLog.Shutdown(0);

        FinishAffinity();
        Process.KillProcess(Process.MyPid());
    }

    /// <summary>Каталог логов: видимые файлы приложения на внешнем носителе.</summary>
    /// <returns>Путь к каталогу логов или <c>null</c>, если каталог недоступен.</returns>
    /// <remarks>
    /// <para>
    /// Логи пишутся в каталог приложения на внешнем носителе
    /// (<c>Android/data/&lt;пакет&gt;/files/logs</c>), а не в личные файлы: личные файлы
    /// на телефоне игроку не видны никаким файловым менеджером, и «пришлите лог» превращалось
    /// в «найдите несуществующий путь» (жалоба пользователя 2026-10-10: «отсутствует на андроид
    /// запись логов, записанный путь логов в игре не существует на телефоне»). Разрешений
    /// этот каталог не требует: он принадлежит приложению.
    /// </para>
    /// <para>
    /// Порядок: внешний каталог приложения, затем личные файлы, затем кэш. Совсем без каталога
    /// (<c>null</c>) логирование выключится, но игра запустится: отказ записи не должен ронять
    /// приложение.
    /// </para>
    /// </remarks>
    private string? LogsDirectory()
    {
        var root = GetExternalFilesDir(null)?.AbsolutePath
            ?? FilesDir?.AbsolutePath
            ?? CacheDir?.AbsolutePath;

        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var directory = Path.Combine(root, "logs");

        try
        {
            // Каталог создаётся сразу: «О программе» показывает путь, и он обязан существовать
            // ещё до первой записи (иначе игрок снова ищет то, чего нет).
            _ = Directory.CreateDirectory(directory);
        }
        catch (IOException)
        {
            // Каталог недоступен — логирование выключится само, путь останется честным.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return directory;
    }

    /// <summary>Отдаёт интерфейсу установщик обновлений и каталог для скачанных пакетов.</summary>
    /// <remarks>
    /// Пакет скачивается в личные файлы приложения на внешнем носителе: этот каталог виден
    /// <c>FileProvider</c>, а значит системный установщик сможет прочитать APK.
    /// </remarks>
    private void ConfigureUpdates()
    {
        var root = GetExternalFilesDir(null)?.AbsolutePath ?? FilesDir?.AbsolutePath ?? CacheDir?.AbsolutePath;

        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        global::GoEngine.App.App.UpdateInstaller = new Updates.AndroidUpdateInstaller();
        global::GoEngine.App.App.UpdateDownloadDirectory = Path.Combine(root, "updates");
    }
}
