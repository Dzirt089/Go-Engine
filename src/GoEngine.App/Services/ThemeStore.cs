namespace GoEngine.App.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using GoEngine.Core;

/// <summary>Чтение и запись выбранного оформления.</summary>
/// <remarks>
/// <para>
/// Оформление лежит отдельным файлом рядом с настройками партии: тема — свойство приложения,
/// а не партии, и смешивать их в одном файле значило бы сбрасывать тему при каждой правке
/// настроек на доске.
/// </para>
/// <para>
/// Ошибки ввода-вывода — ожидаемый исход, а не нарушение инварианта (<c>AGENTS.md</c>, п. 4):
/// отсутствие или порча файла дают системную тему, а не исключение.
/// </para>
/// </remarks>
public static class ThemeStore
{
    /// <summary>Имя файла оформления.</summary>
    private const string FileName = "theme.json";

    /// <summary>Параметры записи: отступы, чтобы файл читался человеком.</summary>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Возвращает путь к файлу оформления в профиле пользователя.</summary>
    public static string DefaultPath => Path.Combine(AppDataPaths.Root, FileName);

    /// <summary>Читает оформление из файла.</summary>
    /// <param name="path">Путь к файлу; <c>null</c> — путь по умолчанию.</param>
    /// <returns>Выбор игрока или системная тема, если файла нет или он не разбирается.</returns>
    public static ThemeChoice Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        if (!File.Exists(file))
        {
            return ThemeChoice.System;
        }

        try
        {
            var document = JsonSerializer.Deserialize<ThemeDocument>(File.ReadAllText(file));

            if (document?.Theme is not { Length: > 0 } name)
            {
                return ThemeChoice.System;
            }

            // Поиск по имени бросает на неизвестном имени: файл правлен человеком, и «Neon»
            // в нём — испорченные данные, а не нарушение инварианта.
            return ThemeChoice.FromName<ThemeChoice>(name);
        }
        catch (JsonException)
        {
            return ThemeChoice.System;
        }
        catch (DomainException)
        {
            return ThemeChoice.System;
        }
        catch (IOException)
        {
            return ThemeChoice.System;
        }
        catch (UnauthorizedAccessException)
        {
            return ThemeChoice.System;
        }
    }

    /// <summary>Записывает оформление в файл.</summary>
    /// <param name="theme">Выбранная тема.</param>
    /// <param name="path">Путь к файлу; <c>null</c> — путь по умолчанию.</param>
    /// <returns>Успех или причина отказа словами.</returns>
    public static Result Save(ThemeChoice theme, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var file = path ?? DefaultPath;

        try
        {
            var folder = Path.GetDirectoryName(file);

            if (!string.IsNullOrEmpty(folder))
            {
                _ = Directory.CreateDirectory(folder);
            }

            File.WriteAllText(file, JsonSerializer.Serialize(new ThemeDocument { Theme = theme.Name }, WriteOptions));

            return Result.Ok();
        }
        catch (IOException exception)
        {
            return Result.Fail($"Не удалось сохранить оформление: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Fail($"Нет доступа к файлу оформления: {exception.Message}");
        }
    }

    /// <summary>Файл оформления: одно поле, чтобы формат было видно целиком.</summary>
    private sealed record ThemeDocument
    {
        /// <summary>Имя выбранной темы: <c>System</c>, <c>Light</c> или <c>Dark</c>.</summary>
        [JsonPropertyName("theme")]
        public string Theme { get; init; } = nameof(ThemeChoice.System);
    }
}
