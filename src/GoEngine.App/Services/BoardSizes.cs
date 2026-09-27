using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Стороны доски, которые предлагает приложение: список, подписи и правило коми.</summary>
/// <remarks>
/// Одно место на панель партии и экран настроек. Раньше список размеров и подписи были
/// продублированы в модели представления и в виде настроек, и смена доски не тянула за собой
/// правило коми: в файл настроек уезжала пара «9×9 + 7.5» от 19×19 (D-051).
/// </remarks>
public static class BoardSizes
{
    /// <summary>Размеры доски в порядке списка выбора — по возрастанию.</summary>
    private static readonly BoardSize[] AllSizes = [BoardSize.Size9, BoardSize.Size13, BoardSize.Size19];

    /// <summary>Подписи размеров: собираются один раз, чтобы список выбора не пересоздавался.</summary>
    private static readonly string[] AllLabels = [.. AllSizes.Select(Label)];

    /// <summary>Размеры доски в порядке списка выбора.</summary>
    public static IReadOnlyList<BoardSize> All => AllSizes;

    /// <summary>Подписи размеров доски: тот же экземпляр, пока размеры не изменились.</summary>
    public static IReadOnlyList<string> Labels => AllLabels;

    /// <summary>Размер сброса: 9×9.</summary>
    public static BoardSize Fallback => BoardSize.Size9;

    /// <summary>Ищет размер в списке.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>Индекс в списке или <c>-1</c>, если размера в списке нет.</returns>
    public static int IndexOf(BoardSize size)
    {
        for (var index = 0; index < AllSizes.Length; index++)
        {
            if (AllSizes[index] == size)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Возвращает размер по индексу списка.</summary>
    /// <param name="index">Индекс в списке.</param>
    /// <returns>Размер доски или <see cref="Fallback"/>, если индекс вне списка.</returns>
    public static BoardSize At(int index) =>
        index >= 0 && index < AllSizes.Length ? AllSizes[index] : Fallback;

    /// <summary>Подпись стороны доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>Например, «9×9».</returns>
    public static string Label(BoardSize size) => size.ToString();

    /// <summary>Коми по правилам для размера доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>7.5 для 19×19, 5.5 для 13×13 и 9×9 (<c>GO_RULES.md</c>, п. 10; D-049).</returns>
    public static Komi KomiFor(BoardSize size) => Komi.For(size);
}
