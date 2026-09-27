namespace GoEngine.Problems;

using System.Reflection;
using GoEngine.Core;

/// <summary>Библиотека задач проекта: встроенный набор и внешние файлы.</summary>
/// <remarks>
/// <para>
/// Встроенный набор лежит ресурсами сборки (файлы <c>Problems/*.sgf</c>), поэтому доступен на всех
/// платформах, включая Android, без правок упаковки. Задачи читает <see cref="ProblemSgf"/>:
/// игровой <c>SgfReader</c> варианты не поддерживает и слоем задач не используется.
/// </para>
/// <para>
/// Свои задачи можно положить рядом с приложением: <see cref="LoadFrom"/> читает каталог, переданный
/// вызывающим, — магических путей в слое нет.
/// </para>
/// </remarks>
public static class ProblemLibrary
{
    private static IReadOnlyList<Problem>? _all;

    /// <summary>Все встроенные задачи библиотеки, упорядоченные по идентификатору.</summary>
    /// <exception cref="DomainException">Встроенный файл задачи не разбирается.</exception>
    public static IReadOnlyList<Problem> All => _all ??= LoadEmbedded();

    /// <summary>Число встроенных задач.</summary>
    public static int Count => All.Count;

    /// <summary>Находит встроенную задачу по идентификатору.</summary>
    /// <param name="id">Идентификатор задачи.</param>
    /// <returns>Задача или <c>null</c>, если такой нет.</returns>
    public static Problem? ById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        foreach (var problem in All)
        {
            if (string.Equals(problem.Id, id, StringComparison.Ordinal))
            {
                return problem;
            }
        }

        return null;
    }

    /// <summary>Читает задачи из каталога с файлами <c>*.sgf</c>.</summary>
    /// <param name="directory">Каталог с файлами задач.</param>
    /// <returns>Задачи или причина отказа на русском языке.</returns>
    /// <remarks>
    /// Отказ возвращается, если каталога нет или хотя бы один файл не разбирается: молча пропускать
    /// битый файл нельзя — игрок решит, что задача просто исчезла.
    /// </remarks>
    public static Result<IReadOnlyList<Problem>> LoadFrom(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return Result<IReadOnlyList<Problem>>.Fail($"Каталог задач не найден: {directory}");
        }

        List<Problem> problems = [];
        List<string> issues = [];

        foreach (var file in Directory.EnumerateFiles(directory, "*.sgf").OrderBy(path => path, StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var parsed = ProblemSgf.Parse(File.ReadAllText(file), id);

            if (parsed.IsSuccess)
            {
                problems.Add(parsed.Value!);
            }
            else
            {
                issues.Add($"{Path.GetFileName(file)}: {parsed.Error}");
            }
        }

        return issues.Count == 0
            ? Result<IReadOnlyList<Problem>>.Ok(problems.AsReadOnly())
            : Result<IReadOnlyList<Problem>>.Fail(string.Join("; ", issues));
    }

    private static IReadOnlyList<Problem> LoadEmbedded()
    {
        var assembly = typeof(ProblemLibrary).Assembly;
        List<Problem> problems = [];

        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sgf", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new DomainException($"Встроенный ресурс задачи не найден: {resource}");
            using var reader = new StreamReader(stream);
            var parsed = ProblemSgf.Parse(reader.ReadToEnd(), IdOf(resource));

            if (!parsed.IsSuccess)
            {
                throw new DomainException($"Встроенная задача {resource}: {parsed.Error}");
            }

            problems.Add(parsed.Value!);
        }

        if (problems.Count == 0)
        {
            throw new DomainException("В сборке нет встроенных задач: файлы Problems/*.sgf не попали в ресурсы.");
        }

        return problems.AsReadOnly();
    }

    /// <summary>Возвращает идентификатор задачи по имени встроенного ресурса.</summary>
    private static string IdOf(string resource)
    {
        var parts = resource.Split('.');

        return parts.Length >= 2 ? parts[^2] : resource;
    }
}
