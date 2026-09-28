using System.Globalization;
using System.Text;

namespace GoEngine.App.Services.Logging;

/// <summary>Запись о сбое: что случилось, когда и какой файл прислать разработчику.</summary>
/// <param name="Moment">Момент сбоя.</param>
/// <param name="Version">Версия сборки.</param>
/// <param name="CrashFile">Копия лога, снятая при сбое.</param>
/// <param name="Reason">Краткая причина.</param>
public readonly record struct CrashReport(DateTimeOffset Moment, string Version, string CrashFile, string Reason)
{
    /// <summary>Имя файла-признака сбоя в каталоге логов.</summary>
    public const string MarkerFileName = "ПОСЛЕДНИЙ_СБОЙ.txt";

    /// <summary>Строка для экрана настроек.</summary>
    /// <remarks>Точка в конце причины убирается: иначе в сообщении получается двойная точка.</remarks>
    public string Summary
    {
        get
        {
            var reason = Reason.TrimEnd('.', ' ', '\r', '\n');

            return string.IsNullOrWhiteSpace(CrashFile)
                ? $"Прошлый запуск завершился сбоем {Moment:yyyy-MM-dd HH:mm}: {reason}"
                : $"Прошлый запуск завершился сбоем {Moment:yyyy-MM-dd HH:mm}: {reason}. Лог для отправки: {CrashFile}";
        }
    }

    /// <summary>Собирает текст файла-признака.</summary>
    /// <returns>Текст с парами «ключ=значение».</returns>
    public string ToText()
    {
        var text = new StringBuilder();
        text.Append("date=").Append(Moment.ToString("O", CultureInfo.InvariantCulture)).AppendLine();
        text.Append("version=").AppendLine(Version);
        text.Append("crash=").AppendLine(CrashFile);
        text.Append("reason=").AppendLine(Reason);

        return text.ToString();
    }

    /// <summary>Читает файл-признак сбоя.</summary>
    /// <param name="path">Путь к файлу-признаку.</param>
    /// <returns>Запись о сбое или <c>null</c>, если файла нет или он не разобран.</returns>
    /// <remarks>Разбор нарочно терпимый: испорченный признак не должен мешать запуску приложения.</remarks>
    public static CrashReport? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            DateTimeOffset? moment = null;
            var version = string.Empty;
            var crashFile = string.Empty;
            var reason = string.Empty;

            foreach (var line in File.ReadAllLines(path))
            {
                var separator = line.IndexOf('=');

                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator];
                var value = line[(separator + 1)..];

                switch (key)
                {
                    case "date" when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                        moment = parsed;
                        break;
                    case "version":
                        version = value;
                        break;
                    case "crash":
                        crashFile = value;
                        break;
                    case "reason":
                        reason = value;
                        break;
                }
            }

            return moment is { } time ? new CrashReport(time, version, crashFile, reason) : null;
        }
        catch
        {
            // Признак сбоя — вспомогательный файл: его отсутствие или порча не должны мешать запуску.
            return null;
        }
    }

    /// <summary>Записывает файл-признак сбоя.</summary>
    /// <param name="path">Путь к файлу-признаку.</param>
    /// <remarks>Отказ записи молча пропускается: это часть логирования, а не работа игры.</remarks>
    public void Write(string path)
    {
        try
        {
            File.WriteAllText(path, ToText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // Причина осознанная: логирование не имеет права уронить приложение даже при сбое.
        }
    }

    /// <summary>Удаляет файл-признак сбоя.</summary>
    /// <param name="path">Путь к файлу-признаку.</param>
    public static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Уборка признака — не повод мешать запуску: покажется ещё раз, это не ошибка игры.
        }
    }
}
