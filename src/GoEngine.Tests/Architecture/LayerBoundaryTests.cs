using System.Reflection;
using GoEngine.AI;
using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Границы слоёв: слой не ссылается на слой выше себя (AGENTS.md, раздел 1).</summary>
/// <remarks>
/// Правило `Core` ← `AI` ← `App` держалось на дисциплине и ревью. Этот тест читает ссылки
/// сборок и подписи публичных членов: случайная зависимость вверх по слоям падает на сборке,
/// а не находится глазами через месяц.
/// Тест видит ссылки и сигнатуры, но не тела методов: вызов файлового API внутри метода
/// он не поймает. Границу «нет ввода-вывода» держат сигнатуры — этого достаточно, потому что
/// файловые операции в проекте вынесены в `GoEngine.App` (`DECISIONS.md`, D-024).
/// </remarks>
public sealed class LayerBoundaryTests
{
    /// <summary>Библиотеки интерфейса: в Core и AI их быть не должно.</summary>
    private static readonly string[] UiAssemblies = ["Avalonia", "SkiaSharp"];

    /// <summary>Проверяет, что сборка не ссылается на другие проекты Go Engine, кроме названных.</summary>
    [Fact]
    public void Core_Ссылается_Только_На_Базовую_Библиотеку()
    {
        var engine = Referenced(typeof(Board).Assembly)
            .Where(static name => name.StartsWith("GoEngine.", StringComparison.Ordinal));

        Assert.Empty(engine);
    }

    [Fact]
    public void Core_Не_Ссылается_На_Библиотеки_Интерфейса()
    {
        var ui = Referenced(typeof(Board).Assembly).Where(IsUiAssembly);

        Assert.Empty(ui);
    }

    [Fact]
    public void AI_Ссылается_Только_На_Core()
    {
        var engine = Referenced(typeof(MctsMoveSelector).Assembly)
            .Where(static name => name.StartsWith("GoEngine.", StringComparison.Ordinal))
            .Where(static name => name != "GoEngine.Core");

        Assert.Empty(engine);
    }

    [Fact]
    public void AI_Не_Ссылается_На_Библиотеки_Интерфейса()
    {
        var ui = Referenced(typeof(MctsMoveSelector).Assembly).Where(IsUiAssembly);

        Assert.Empty(ui);
    }

    [Fact]
    public void AI_Onnx_Ссылается_Только_На_Core_И_AI()
    {
        var engine = Referenced(typeof(OnnxEvaluator).Assembly)
            .Where(static name => name.StartsWith("GoEngine.", StringComparison.Ordinal))
            .Where(static name => name is not ("GoEngine.Core" or "GoEngine.AI"));

        Assert.Empty(engine);
    }

    [Fact]
    public void Core_Не_Работает_С_Файлами()
    {
        var files = FileTypesInPublicApi(typeof(Board).Assembly);

        Assert.Empty(files);
    }

    [Fact]
    public void AI_Не_Работает_С_Файлами()
    {
        var files = FileTypesInPublicApi(typeof(MctsMoveSelector).Assembly);

        Assert.Empty(files);
    }

    private static bool IsUiAssembly(string name) =>
        UiAssemblies.Contains(name, StringComparer.Ordinal);

    private static string[] Referenced(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .Where(static name => name.Length > 0)
            .ToArray();

    /// <summary>Собирает типы ввода-вывода, которые видны в публичных членах сборки.</summary>
    private static string[] FileTypesInPublicApi(Assembly assembly) =>
        assembly.GetExportedTypes()
            .SelectMany(SignatureTypes)
            .Where(static type => type.Namespace?.StartsWith("System.IO", StringComparison.Ordinal) == true)
            .Select(static type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>Перечисляет типы, встречающиеся в подписях публичных членов типа.</summary>
    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var member in type.GetMembers(flags))
        {
            switch (member)
            {
                case MethodInfo method:
                    yield return method.ReturnType;

                    foreach (var parameter in method.GetParameters())
                    {
                        yield return parameter.ParameterType;
                    }

                    break;

                case ConstructorInfo constructor:
                    foreach (var parameter in constructor.GetParameters())
                    {
                        yield return parameter.ParameterType;
                    }

                    break;

                case PropertyInfo property:
                    yield return property.PropertyType;
                    break;

                case FieldInfo field:
                    yield return field.FieldType;
                    break;
            }
        }
    }
}
