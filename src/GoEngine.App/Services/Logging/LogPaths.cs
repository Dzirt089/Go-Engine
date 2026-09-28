namespace GoEngine.App.Services.Logging;

/// <summary>Каталог файловых логов приложения.</summary>
/// <remarks>
/// Каталог берётся из того же корня, что и настройки (<see cref="AppDataPaths"/>), и лишь
/// у macOS свой: там принято хранить логи в <c>~/Library/Logs</c>, а не рядом с настройками.
/// Логи не должны мешать игре, поэтому каталог только вычисляется: создаёт его писатель логов,
/// и любая ошибка ввода-вывода там молча пропускается.
/// </remarks>
public static class LogPaths
{
    /// <summary>Имя подпапки логов в каталоге данных приложения.</summary>
    public const string FolderName = "logs";

    /// <summary>Возвращает каталог логов для заданной системы.</summary>
    /// <param name="userProfile">Домашний каталог пользователя.</param>
    /// <param name="applicationData">Каталог данных приложений (используется в Windows).</param>
    /// <param name="isWindows">Система — Windows.</param>
    /// <param name="isMacOs">Система — macOS.</param>
    /// <returns>Полный путь к каталогу логов.</returns>
    /// <exception cref="ArgumentException">Домашний каталог не задан.</exception>
    public static string DirectoryFor(string userProfile, string applicationData, bool isWindows, bool isMacOs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userProfile);

        return isMacOs
            ? Path.Combine(userProfile, "Library", "Logs", AppDataPaths.FolderName)
            : Path.Combine(AppDataPaths.RootFor(userProfile, applicationData, isWindows, isMacOs), FolderName);
    }

    /// <summary>Каталог логов для текущей системы.</summary>
    public static string DefaultDirectory => DirectoryFor(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        OperatingSystem.IsWindows(),
        OperatingSystem.IsMacOS());
}
