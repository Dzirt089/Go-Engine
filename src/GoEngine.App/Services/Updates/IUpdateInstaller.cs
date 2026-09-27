using GoEngine.Core;

namespace GoEngine.App.Services.Updates;

/// <summary>Установка обновления средствами платформы.</summary>
/// <remarks>
/// Установка отличается от системы к системе, поэтому интерфейс живёт в библиотеке, а реализации —
/// в головах: настольная умеет тихую установку Windows, Android открывает системный установщик,
/// а на Linux и macOS замена работающего приложения требует действий игрока.
/// </remarks>
public interface IUpdateInstaller
{
    /// <summary>Ключ платформы в манифесте обновления.</summary>
    string PlatformKey { get; }

    /// <summary>Устанавливает скачанный файл обновления.</summary>
    /// <param name="filePath">Полный путь к скачанному файлу.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    Result<string> Install(string filePath);

    /// <summary>Открывает страницу загрузки, когда установка из приложения невозможна.</summary>
    /// <param name="url">Адрес страницы выпуска.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    Result<string> OpenDownloadPage(Uri url);
}
