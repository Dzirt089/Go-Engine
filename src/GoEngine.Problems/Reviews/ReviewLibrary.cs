namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Библиотека обучающих партий: встроенный набор и внешние файлы.</summary>
/// <remarks>
/// Устроена как <see cref="LessonLibrary"/>: партии лежат ресурсами сборки (<c>Games/*.json</c>)
/// и потому доступны на всех платформах, включая Android, без правок упаковки. Свой набор можно
/// положить рядом с приложением: <see cref="LoadFrom"/> читает каталог, переданный вызывающим.
/// </remarks>
public static class ReviewLibrary
{
    private static IReadOnlyList<ReviewGame>? _all;

    /// <summary>Все встроенные партии в порядке идентификаторов.</summary>
    /// <exception cref="DomainException">Встроенный файл партии не разбирается.</exception>
    public static IReadOnlyList<ReviewGame> All => _all ??= LoadEmbedded();

    /// <summary>Число встроенных партий.</summary>
    public static int Count => All.Count;

    /// <summary>Общее число ходов во встроенных партиях.</summary>
    public static int MoveCount => All.Sum(static game => game.MoveCount);

    /// <summary>Общее число вопросов во встроенных партиях.</summary>
    public static int QuizCount => All.Sum(static game => game.QuizCount);

    /// <summary>Находит встроенную партию по идентификатору.</summary>
    /// <param name="id">Идентификатор партии.</param>
    /// <returns>Партия или <c>null</c>, если такой нет.</returns>
    public static ReviewGame? ById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        foreach (var game in All)
        {
            if (string.Equals(game.Id, id, StringComparison.Ordinal))
            {
                return game;
            }
        }

        return null;
    }

    /// <summary>Читает партии из каталога с файлами <c>*.json</c>.</summary>
    /// <param name="directory">Каталог с файлами партий.</param>
    /// <returns>Партии или причина отказа на русском языке.</returns>
    /// <remarks>
    /// Отказ возвращается, если каталога нет или хотя бы один файл не разбирается: молча
    /// пропускать битую партию нельзя — игрок решит, что она просто исчезла.
    /// </remarks>
    public static Result<IReadOnlyList<ReviewGame>> LoadFrom(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return Result<IReadOnlyList<ReviewGame>>.Fail($"Каталог партий не найден: {directory}");
        }

        List<ReviewGame> games = [];
        List<string> issues = [];

        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            var parsed = ReviewJson.Parse(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file));

            if (parsed.IsSuccess)
            {
                games.Add(parsed.Value!);
            }
            else
            {
                issues.Add($"{Path.GetFileName(file)}: {parsed.Error}");
            }
        }

        return issues.Count == 0
            ? Result<IReadOnlyList<ReviewGame>>.Ok(games.AsReadOnly())
            : Result<IReadOnlyList<ReviewGame>>.Fail(string.Join("; ", issues));
    }

    private static IReadOnlyList<ReviewGame> LoadEmbedded()
    {
        var assembly = typeof(ReviewLibrary).Assembly;
        List<ReviewGame> games = [];

        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Games.", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new DomainException($"Встроенный ресурс партии не найден: {resource}");
            using var reader = new StreamReader(stream);
            var parsed = ReviewJson.Parse(reader.ReadToEnd(), IdOf(resource));

            if (!parsed.IsSuccess)
            {
                throw new DomainException($"Встроенная партия {resource}: {parsed.Error}");
            }

            games.Add(parsed.Value!);
        }

        if (games.Count == 0)
        {
            throw new DomainException("В сборке нет встроенных партий: файлы Games/*.json не попали в ресурсы.");
        }

        return games.AsReadOnly();
    }

    /// <summary>Возвращает идентификатор партии по имени встроенного ресурса.</summary>
    private static string IdOf(string resource)
    {
        var parts = resource.Split('.');

        return parts.Length >= 2 ? parts[^2] : resource;
    }
}
