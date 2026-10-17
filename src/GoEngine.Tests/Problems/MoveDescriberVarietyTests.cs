using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests.Problems;

/// <summary>Комментарии разбора рождаются из позиции, а не повторяются шаблоном.</summary>
/// <remarks>
/// Замечание пользователя 2026-10-17: «партии приписывают однотипные сообщения… идея была показать
/// ход мысли игрока на основе текущего положения камней, а не копипастить шаблоны из хода в ход».
/// </remarks>
public sealed class MoveDescriberVarietyTests
{
    /// <summary>Строки разбора первой партии: образец для глаза и материал для проверок.</summary>
    /// <returns>Строки по ходам.</returns>
    public static IReadOnlyList<string> SampleGameLines()
    {
        var game = ReviewLibrary.All[0];
        List<string> lines = [];
        var before = game.StartPosition();
        var after = before;

        for (var i = 0; i < Math.Min(24, game.Moves.Count); i++)
        {
            var move = game.Moves[i];
            after = after.ApplyMove(move);
            lines.Add(MoveDescriber.Describe(MoveDescriber.FactsOf(before, after, move, i + 1), game.Size));
            before = after;
        }

        return lines;
    }

    [Fact]
    public void Соседние_Ходы_Не_Повторяют_Фразу()
    {
        var lines = SampleGameLines();

        for (var i = 1; i < lines.Count; i++)
        {
            Assert.NotEqual(lines[i - 1], lines[i]);
        }
    }

    [Fact]
    public void В_Партии_Есть_Разные_Замыслы()
    {
        var lines = SampleGameLines();

        // Замысел — последний фрагмент строки: он и раньше был шаблоном, теперь считается по позиции.
        var intents = lines
            .Select(line => line.TrimEnd('.').Split("; ").Last())
            .ToList();

        Assert.True(intents.Distinct(StringComparer.Ordinal).Count() >= 8, string.Join(" | ", intents.Take(8)));
    }

    [Fact]
    public void Строка_Называет_Точку_И_Дыхания()
    {
        var lines = SampleGameLines();

        Assert.Contains("дыхани", lines[0], StringComparison.Ordinal);
        Assert.Matches(@"^Чёрные: [A-J]\d+ — ", lines[0]);
        Assert.Matches(@"^Белые: [A-J]\d+ — ", lines[1]);
    }

    [Fact]
    public void Образец_Для_Чтения_Лежит_В_Артефактах()
    {
        // Диагностический вывод: удобно глазами проверить, что тексты разные и по делу.
        var directory = Path.Combine(Path.GetTempPath(), "goengine-describer");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "sample.txt"), SampleGameLines());
        Assert.True(File.Exists(Path.Combine(directory, "sample.txt")));
    }
}
