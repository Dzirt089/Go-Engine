using System.Diagnostics;
using System.Runtime.InteropServices;
using GoEngine.App.Services.Updates;
using GoEngine.Core;

namespace GoEngine.App.Updates;

/// <summary>Установка обновления на настольных системах.</summary>
/// <remarks>
/// Windows умеет тихую установку: скачанный <c>GoEngine-Setup.exe</c> — это Inno Setup, он
/// обновляет приложение поверх прежнего. На Linux и macOS заменить работающее приложение без прав
/// пользователя нельзя, поэтому приложение честно открывает папку с файлом и объясняет, что делать,
/// а не делает вид, что установка произошла.
/// </remarks>
public sealed class DesktopUpdateInstaller : IUpdateInstaller
{
    /// <inheritdoc />
    public string PlatformKey
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "win-x64";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux-x64";
            }

            // Сборки macOS различаются процессором: arm64 и x64 — разные файлы в манифесте.
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }
    }

    /// <inheritdoc />
    public Result<string> Install(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return Result<string>.Fail("Скачанный файл обновления не найден.");
        }

        return OperatingSystem.IsWindows()
            ? InstallOnWindows(filePath)
            : RevealForManualInstall(filePath);
    }

    /// <inheritdoc />
    public Result<string> OpenDownloadPage(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        try
        {
            _ = Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

            return Result<string>.Ok("Страница загрузки открыта в браузере.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Result<string>.Fail($"Не удалось открыть браузер: {exception.Message}. Скачайте обновление вручную: {url}");
        }
    }

    /// <summary>Запускает установщик Windows в тихом режиме.</summary>
    /// <param name="filePath">Путь к <c>GoEngine-Setup.exe</c>.</param>
    /// <returns>Сообщение игроку.</returns>
    /// <remarks>
    /// Ключи Inno Setup: <c>/SILENT</c> — без вопросов, <c>/SUPPRESSMSGBOXES</c> — без окон,
    /// <c>/NORESTART</c> — без перезагрузки. Установщик закрывает приложение сам, поэтому игроку
    /// достаточно дождаться окончания установки.
    /// </remarks>
    private static Result<string> InstallOnWindows(string filePath)
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo(filePath)
            {
                UseShellExecute = true,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART"
            });

            return Result<string>.Ok("Установщик запущен: приложение закроется и обновится само. После установки запустите Go Engine заново.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Result<string>.Fail($"Не удалось запустить установщик: {exception.Message}. Файл лежит рядом: {filePath}");
        }
    }

    /// <summary>Открывает папку с файлом: на Linux и macOS установка требует действий игрока.</summary>
    /// <param name="filePath">Путь к скачанному архиву.</param>
    /// <returns>Сообщение игроку с путём к файлу.</returns>
    private static Result<string> RevealForManualInstall(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? filePath;

        try
        {
            var command = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            var arguments = OperatingSystem.IsMacOS() ? $"-R \"{filePath}\"" : $"\"{directory}\"";

            _ = Process.Start(new ProcessStartInfo(command) { Arguments = arguments, UseShellExecute = false });

            return Result<string>.Ok($"Обновление скачано. Заменить приложение можно только вручную: распакуйте архив и замените папку приложения. Файл: {filePath}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Result<string>.Ok($"Обновление скачано: {filePath}. Распакуйте архив и замените папку приложения вручную.");
        }
    }
}
