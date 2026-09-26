using GoEngine.AI;
using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Пространства имён: у движка один namespace на проект, папки его не дробят.</summary>
/// <remarks>
/// Папки делят код по областям (`Mcts`, `Sgf`, `Playouts`), но пространство имён у `Core`
/// и `AI` одно — иначе тип приходится искать с `using` и он «прячется» от читателя.
/// У `App` правило другое: там пространства имён повторяют папки (`Views`, `ViewModels`),
/// потому что на них ссылается XAML (`clr-namespace`). Тесты тоже живут одним namespace.
/// </remarks>
public sealed class NamespaceConventionTests
{
    [Fact]
    public void Core_Использует_Одно_Пространство_Имён()
    {
        var spaces = Namespaces(typeof(Board).Assembly);

        Assert.Equal(["GoEngine.Core"], spaces);
    }

    [Fact]
    public void AI_Использует_Одно_Пространство_Имён()
    {
        var spaces = Namespaces(typeof(MctsMoveSelector).Assembly);

        Assert.Equal(["GoEngine.AI"], spaces);
    }

    [Fact]
    public void GoEngine_Onnx_Использует_Одно_Пространство_Имён()
    {
        var spaces = Namespaces(typeof(OnnxEvaluator).Assembly);

        Assert.Equal(["GoEngine.AI.Onnx"], spaces);
    }

    /// <summary>Собирает пространства имён публичных типов сборки — по одному на проект.</summary>
    private static string[] Namespaces(System.Reflection.Assembly assembly) =>
        assembly.GetExportedTypes()
            .Select(static type => type.Namespace ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
