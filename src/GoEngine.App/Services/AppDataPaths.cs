namespace GoEngine.App.Services;

/// <summary>Каталог данных приложения в профиле пользователя: общий для настроек и логов.</summary>
/// <remarks>
/// Путь свой для каждой системы: Windows хранит данные в <c>%AppData%</c>, Linux — в
/// <c>~/.config</c>, macOS — в <c>~/Library/Application Support</c>. Windows и Linux описаны
/// здесь один раз, чтобы настройки и логи не разъезжались; у логов macOS свой каталог
/// (<c>~/Library/Logs</c>), и это единственное расхождение — оно в <see cref="Logging.LogPaths"/>.
/// </remarks>
public static class AppDataPaths
{
    /// <summary>Имя папки приложения в профиле пользователя.</summary>
    public const string FolderName = "GoEngine";

    /// <summary>Возвращает корневой каталог данных приложения в профиле пользователя.</summary>
    /// <param name="userProfile">Домашний каталог пользователя.</param>
    /// <param name="applicationData">Каталог данных приложений (используется в Windows).</param>
    /// <param name="isWindows">Система — Windows.</param>
    /// <param name="isMacOs">Система — macOS.</param>
    /// <returns>Полный путь к каталогу приложения.</returns>
    /// <exception cref="ArgumentException">Домашний каталог не задан.</exception>
    public static string RootFor(string userProfile, string applicationData, bool isWindows, bool isMacOs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userProfile);

        if (isWindows && !string.IsNullOrEmpty(applicationData))
        {
            return Path.Combine(applicationData, FolderName);
        }

        if (isMacOs)
        {
            return Path.Combine(userProfile, "Library", "Application Support", FolderName);
        }

        return Path.Combine(userProfile, ".config", FolderName.ToLowerInvariant());
    }

    /// <summary>Корневой каталог данных приложения для текущей системы.</summary>
    public static string Root => RootFor(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        OperatingSystem.IsWindows(),
        OperatingSystem.IsMacOS());
}
