using System.Text.Json;
using System.Text.Json.Serialization;
using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Настройки партии, сохраняемые в JSON.</summary>
/// <remarks>
/// Запись (<c>record</c>) даёт сравнение по значениям и <c>with</c> для точечных правок.
/// В файле лежат простые значения (число, имя, ранг), а не доменные типы: так файл настроек
/// не сломается при изменении внутреннего представления <c>Core</c> и <c>AI</c>.
/// Настройки по умолчанию — доска 9×9, уровень 20 кю, игрок чёрными, коми 5.5.
/// </remarks>
public sealed record AppSettings
{
    /// <summary>Параметры записи: отступы, чтобы файл настроек читался человеком.</summary>
    /// <remarks>Экземпляр создаётся один раз: CA1869 и лишние аллокации при каждом сохранении.</remarks>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Сторона доски: 9, 13 или 19.</summary>
    [JsonPropertyName("boardSize")]
    public byte BoardSize { get; set; } = 9;

    /// <summary>Ранг уровня AI в кю.</summary>
    [JsonPropertyName("aiRankKyu")]
    public int AiRankKyu { get; set; } = 20;

    /// <summary>Цвет игрока: <c>Black</c> или <c>White</c>.</summary>
    [JsonPropertyName("playerColor")]
    public string PlayerColor { get; set; } = nameof(StoneColor.Black);

    /// <summary>Коми партии.</summary>
    [JsonPropertyName("komi")]
    public double Komi { get; set; } = 5.5;

    /// <summary>Возвращает размер доски из настроек.</summary>
    /// <returns>Размер доски; при недопустимом значении — 9×9.</returns>
    public BoardSize ToBoardSize() => BoardSize switch
    {
        var value when value == Core.BoardSize.Size13.Value => Core.BoardSize.Size13,
        var value when value == Core.BoardSize.Size19.Value => Core.BoardSize.Size19,
        _ => Core.BoardSize.Size9
    };

    /// <summary>Возвращает коми из настроек.</summary>
    /// <returns>Коми; при недопустимом значении — стандартное для размера доски.</returns>
    public Komi ToKomi() =>
        Komi >= 0 && !double.IsNaN(Komi) ? new Core.Komi(Komi) : Core.Komi.For(ToBoardSize());

    /// <summary>Возвращает цвет игрока из настроек.</summary>
    /// <returns>Цвет игрока; по умолчанию чёрные.</returns>
    public StoneColor ToPlayerColor() => PlayerColor == nameof(StoneColor.White) ? StoneColor.White : StoneColor.Black;

    /// <summary>Возвращает уровень AI из настроек.</summary>
    /// <returns>Уровень сложности; при неизвестном ранге — 20 кю.</returns>
    public DifficultyLevel ToDifficultyLevel() =>
        Enumeration.TryFromId<DifficultyLevel>(AiRankKyu) ?? DifficultyLevel.Kyu20;

    /// <summary>Настройки по умолчанию.</summary>
    public static AppSettings Default => new();

    /// <summary>Создаёт настройки из доменных значений.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="level">Уровень AI.</param>
    /// <param name="playerColor">Цвет игрока.</param>
    /// <param name="komi">Коми.</param>
    /// <returns>Настройки для сохранения.</returns>
    public static AppSettings From(BoardSize size, DifficultyLevel level, StoneColor playerColor, Komi komi) => new()
    {
        BoardSize = size.Value,
        AiRankKyu = level.RankKyu,
        PlayerColor = playerColor.Name,
        Komi = komi.Value
    };

    /// <summary>Разбирает настройки из JSON.</summary>
    /// <param name="json">Содержимое файла настроек.</param>
    /// <returns>Настройки или <c>null</c>, если JSON пуст или не разбирается.</returns>
    public static AppSettings? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch (JsonException)
        {
            // Битый файл настроек не должен мешать играть: вызывающий код возьмёт значения по умолчанию.
            return null;
        }
    }

    /// <summary>Превращает настройки в JSON.</summary>
    /// <returns>Текст файла настроек.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, WriteOptions);
}