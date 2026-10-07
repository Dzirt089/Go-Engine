namespace GoEngine.Problems;

using System.Text.Json;
using GoEngine.Core;

/// <summary>Словарь терминов Го, которые встречаются в обучении.</summary>
/// <remarks>
/// Как и уроки, словарь лежит встроенным ресурсом (<c>Glossary/*.json</c>) и потому доступен
/// на всех платформах, включая Android. Объяснения — на русском: обучение ведётся по-русски,
/// а оригинальные названия приводятся рядом как справка (замечание пользователя 2026-10-07).
/// </remarks>
public static class GlossaryLibrary
{
    private static IReadOnlyList<GoTerm>? _all;

    /// <summary>Все термины словаря, упорядоченные по названию.</summary>
    /// <exception cref="DomainException">Встроенный файл словаря не разбирается.</exception>
    public static IReadOnlyList<GoTerm> All => _all ??= LoadEmbedded();

    /// <summary>Число терминов.</summary>
    public static int Count => All.Count;

    /// <summary>Термины, которые разбираются в уроке.</summary>
    /// <param name="lessonId">Идентификатор урока.</param>
    /// <returns>Термины урока в порядке названий.</returns>
    public static IReadOnlyList<GoTerm> ForLesson(string lessonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lessonId);

        return All.Where(term => term.Lessons.Contains(lessonId, StringComparer.Ordinal)).ToList().AsReadOnly();
    }

    /// <summary>Читает словарь из текста JSON.</summary>
    /// <param name="text">Содержимое файла словаря.</param>
    /// <returns>Термины или причина отказа на русском языке.</returns>
    public static Result<IReadOnlyList<GoTerm>> Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            return Result<IReadOnlyList<GoTerm>>.Fail($"Словарь не разбирается как JSON: {exception.Message}");
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("terms", out var terms)
                || terms.ValueKind != JsonValueKind.Array)
            {
                return Result<IReadOnlyList<GoTerm>>.Fail("В словаре нет списка терминов (terms).");
            }

            List<GoTerm> parsed = [];

            foreach (var element in terms.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return Result<IReadOnlyList<GoTerm>>.Fail("Термин словаря должен быть объектом JSON.");
                }

                var name = Text(element, "name");
                var body = Text(element, "text");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(body))
                {
                    return Result<IReadOnlyList<GoTerm>>.Fail("У термина словаря нужны название (name) и объяснение (text).");
                }

                List<string> lessons = [];

                if (element.TryGetProperty("lessons", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var lesson in list.EnumerateArray())
                    {
                        if (lesson.ValueKind == JsonValueKind.String && lesson.GetString() is { Length: > 0 } id)
                        {
                            lessons.Add(id);
                        }
                    }
                }

                parsed.Add(new GoTerm(name, Text(element, "original") ?? string.Empty, body, lessons.AsReadOnly()));
            }

            if (parsed.Count == 0)
            {
                return Result<IReadOnlyList<GoTerm>>.Fail("Словарь пуст.");
            }

            return Result<IReadOnlyList<GoTerm>>.Ok(
                parsed.OrderBy(term => term.Name, StringComparer.CurrentCulture).ToList().AsReadOnly());
        }
    }

    private static IReadOnlyList<GoTerm> LoadEmbedded()
    {
        var assembly = typeof(GlossaryLibrary).Assembly;
        List<GoTerm> terms = [];

        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Glossary.", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new DomainException($"Встроенный ресурс словаря не найден: {resource}");
            using var reader = new StreamReader(stream);
            var parsed = Parse(reader.ReadToEnd());

            if (!parsed.IsSuccess)
            {
                throw new DomainException($"Встроенный словарь {resource}: {parsed.Error}");
            }

            terms.AddRange(parsed.Value!);
        }

        if (terms.Count == 0)
        {
            throw new DomainException("В сборке нет встроенного словаря: файлы Glossary/*.json не попали в ресурсы.");
        }

        return terms.AsReadOnly();
    }

    /// <summary>Читает строковое свойство объекта JSON.</summary>
    /// <param name="element">Объект JSON.</param>
    /// <param name="name">Имя свойства.</param>
    /// <returns>Строку или <c>null</c>, если свойства нет.</returns>
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
