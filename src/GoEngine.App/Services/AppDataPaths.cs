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

    /// <summary>Переменная окружения: свой каталог данных вместо профиля пользователя.</summary>
    /// <remarks>
    /// Нужна для переносимой установки и для проверок: живой прогон запускает приложение
    /// из-под песочницы, и запись в профиль пользователя ей запрещена — без своего каталога
    /// прогресс и оформление не сохраняются, и «продолжить после перезапуска» проверить нельзя
    /// (проверено 2026-10-09: каталог данных не пополнялся, хотя приложение работало).
    /// </remarks>
    public const string DataDirectoryVariable = "GOENGINE_DATA";

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
    /// <remarks>
    /// Свой каталог из <see cref="DataDirectoryVariable"/> важнее профиля пользователя: так
    /// работает переносимая установка и так проверяется сохранение прогресса в живом прогоне.
    /// </remarks>
    public static string Root => RootOf(
        Environment.GetEnvironmentVariable(DataDirectoryVariable),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        OperatingSystem.IsWindows(),
        OperatingSystem.IsMacOS());

    /// <summary>Возвращает корневой каталог данных с учётом своего каталога.</summary>
    /// <param name="dataDirectory">Свой каталог; пусто — каталог профиля пользователя.</param>
    /// <param name="userProfile">Домашний каталог пользователя.</param>
    /// <param name="applicationData">Каталог данных приложений (используется в Windows).</param>
    /// <param name="isWindows">Система — Windows.</param>
    /// <param name="isMacOs">Система — macOS.</param>
    /// <returns>Полный путь к каталогу приложения.</returns>
    /// <exception cref="ArgumentException">Домашний каталог не задан, а своего каталога нет.</exception>
    public static string RootOf(
        string? dataDirectory,
        string userProfile,
        string applicationData,
        bool isWindows,
        bool isMacOs) =>
        string.IsNullOrWhiteSpace(dataDirectory)
            ? RootFor(userProfile, applicationData, isWindows, isMacOs)
            : dataDirectory;
}
