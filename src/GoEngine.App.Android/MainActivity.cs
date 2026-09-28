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

        AndroidModelStartup.Prepare(this);
        ConfigureUpdates();

        base.OnCreate(savedInstanceState);
    }

    /// <summary>Каталог логов: личные файлы приложения, доступные без разрешений.</summary>
    /// <returns>Путь к каталогу логов или <c>null</c>, если каталог недоступен.</returns>
    private string? LogsDirectory() =>
        FilesDir?.AbsolutePath is { Length: > 0 } root ? Path.Combine(root, "logs") : null;

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
