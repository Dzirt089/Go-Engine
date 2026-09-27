using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Русские подписи цветов камней: одно место на список выбора и панель партии.</summary>
/// <remarks>
/// Имена <see cref="StoneColor"/> — английские (<c>Black</c>/<c>White</c>), а игроку показываются
/// русские слова. Раньше они были выписаны отдельно в списке настроек и в модели представления,
/// поэтому подписи могли разойтись.
/// </remarks>
public static class StoneColorLabels
{
    /// <summary>Подпись чёрных.</summary>
    private const string BlackLabel = "Чёрные";

    /// <summary>Подпись белых.</summary>
    private const string WhiteLabel = "Белые";

    /// <summary>Подписи цветов в порядке списка выбора: чёрные, белые.</summary>
    public static IReadOnlyList<string> All { get; } = [BlackLabel, WhiteLabel];

    /// <summary>Подпись цвета.</summary>
    /// <param name="color">Цвет камней.</param>
    /// <returns>«Чёрные» или «Белые».</returns>
    public static string Label(StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(color);

        return color == StoneColor.White ? WhiteLabel : BlackLabel;
    }

    /// <summary>Индекс цвета в списке выбора.</summary>
    /// <param name="color">Цвет камней.</param>
    /// <returns><c>0</c> для чёрных, <c>1</c> для белых.</returns>
    public static int IndexOf(StoneColor color) => Label(color) == BlackLabel ? 0 : 1;

    /// <summary>Цвет по индексу списка выбора.</summary>
    /// <param name="index">Индекс в списке.</param>
    /// <returns>Белые при индексе 1, иначе чёрные.</returns>
    public static StoneColor At(int index) => index == 1 ? StoneColor.White : StoneColor.Black;
}
