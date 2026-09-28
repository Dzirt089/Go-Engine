using System.Text;

namespace GoEngine.App.Services.Logging;

/// <summary>Текст исключения для лога: цепочка вложенных исключений и стек.</summary>
/// <remarks>
/// Падение на телефоне или у игрока разбирают по файлу, поэтому в лог попадает вся цепочка
/// (<c>InnerException</c> до самого глубокого) и стек каждого уровня: по одному внешнему типу
/// причину не найти (пример — <c>TypeInitializationException</c> без внутреннего сообщения).
/// </remarks>
public static class ExceptionText
{
    /// <summary>Сколько вложенных исключений попадает в текст.</summary>
    /// <remarks>Ограничение защищает от патологической вложенности и от бесконечного цикла.</remarks>
    private const int MaxDepth = 8;

    /// <summary>Собирает текст исключения: тип, сообщение, цепочка вложенных и стеки.</summary>
    /// <param name="exception">Исключение; <c>null</c> — пустая строка.</param>
    /// <returns>Текст исключения.</returns>
    public static string Describe(Exception? exception)
    {
        if (exception is null)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        var current = exception;
        var depth = 0;

        while (current is not null && depth < MaxDepth)
        {
            if (depth > 0)
            {
                text.Append("  вложено: ");
            }

            text.Append(current.GetType().FullName).Append(": ").Append(current.Message);

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                text.AppendLine().Append(current.StackTrace);
            }

            current = current.InnerException;
            depth++;

            if (current is not null)
            {
                text.AppendLine();
            }
        }

        if (current is not null)
        {
            text.AppendLine().Append("  вложенность обрезана: глубже ").Append(MaxDepth).Append(" уровней");
        }

        return text.ToString();
    }

    /// <summary>Краткая причина для файла о сбое: тип и первое сообщение.</summary>
    /// <param name="exception">Исключение.</param>
    /// <returns>Одна строка с причиной.</returns>
    public static string Summarize(Exception? exception)
    {
        if (exception is null)
        {
            return "причина неизвестна";
        }

        var inner = exception;

        while (inner.InnerException is not null)
        {
            inner = inner.InnerException;
        }

        var message = inner.Message.ReplaceLineEndings(" ").Trim();

        return message.Length == 0
            ? inner.GetType().Name
            : inner.GetType().Name + ": " + message;
    }
}
