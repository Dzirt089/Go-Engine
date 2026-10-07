namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Библиотека уроков проекта: встроенный набор и внешние файлы.</summary>
/// <remarks>
/// Устроена как <see cref="ProblemLibrary"/>: уроки лежат ресурсами сборки
/// (<c>Lessons/*.json</c>) и потому доступны на всех платформах, включая Android, без правок
/// упаковки. Свой набор можно положить рядом с приложением: <see cref="LoadFrom"/> читает
/// каталог, переданный вызывающим, — магических путей в слое нет.
/// </remarks>
public static class LessonLibrary
{
    private static IReadOnlyList<Lesson>? _all;

    /// <summary>Все встроенные уроки в порядке идентификаторов.</summary>
    /// <exception cref="DomainException">Встроенный файл урока не разбирается.</exception>
    public static IReadOnlyList<Lesson> All => _all ??= LoadEmbedded();

    /// <summary>Число встроенных уроков.</summary>
    public static int Count => All.Count;

    /// <summary>Общее число шагов во встроенных уроках.</summary>
    public static int StepCount => All.Sum(static lesson => lesson.StepCount);

    /// <summary>Находит встроенный урок по идентификатору.</summary>
    /// <param name="id">Идентификатор урока.</param>
    /// <returns>Урок или <c>null</c>, если такого нет.</returns>
    public static Lesson? ById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        foreach (var lesson in All)
        {
            if (string.Equals(lesson.Id, id, StringComparison.Ordinal))
            {
                return lesson;
            }
        }

        return null;
    }

    /// <summary>Читает уроки из каталога с файлами <c>*.json</c>.</summary>
    /// <param name="directory">Каталог с файлами уроков.</param>
    /// <returns>Уроки или причина отказа на русском языке.</returns>
    /// <remarks>
    /// Отказ возвращается, если каталога нет или хотя бы один файл не разбирается: молча
    /// пропускать битый урок нельзя — игрок решит, что урок просто исчез.
    /// </remarks>
    public static Result<IReadOnlyList<Lesson>> LoadFrom(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return Result<IReadOnlyList<Lesson>>.Fail($"Каталог уроков не найден: {directory}");
        }

        List<Lesson> lessons = [];
        List<string> issues = [];

        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            var parsed = LessonJson.Parse(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file));

            if (parsed.IsSuccess)
            {
                lessons.Add(parsed.Value!);
            }
            else
            {
                issues.Add($"{Path.GetFileName(file)}: {parsed.Error}");
            }
        }

        return issues.Count == 0
            ? Result<IReadOnlyList<Lesson>>.Ok(lessons.AsReadOnly())
            : Result<IReadOnlyList<Lesson>>.Fail(string.Join("; ", issues));
    }

    private static IReadOnlyList<Lesson> LoadEmbedded()
    {
        var assembly = typeof(LessonLibrary).Assembly;
        List<Lesson> lessons = [];

        // Только уроки: словарь терминов лежит таким же ресурсом, но разбирается своим слоем.
        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Lessons.", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new DomainException($"Встроенный ресурс урока не найден: {resource}");
            using var reader = new StreamReader(stream);
            var parsed = LessonJson.Parse(reader.ReadToEnd(), IdOf(resource));

            if (!parsed.IsSuccess)
            {
                throw new DomainException($"Встроенный урок {resource}: {parsed.Error}");
            }

            lessons.Add(parsed.Value!);
        }

        if (lessons.Count == 0)
        {
            throw new DomainException("В сборке нет встроенных уроков: файлы Lessons/*.json не попали в ресурсы.");
        }

        return lessons.AsReadOnly();
    }

    /// <summary>Возвращает идентификатор урока по имени встроенного ресурса.</summary>
    private static string IdOf(string resource)
    {
        var parts = resource.Split('.');

        return parts.Length >= 2 ? parts[^2] : resource;
    }
}
