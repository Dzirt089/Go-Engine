using System.Text.Json;
using System.Text.Json.Serialization;
using GoEngine.AI;
using GoEngine.Core;
using ScoringRule = GoEngine.Core.ScoringRule;

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
    /// <remarks>
    /// Размер коми определяет регламент встречи, а не правила: в турнирах РФ на 19×19 типично 6.5,
    /// в <c>GO_RULES.md</c> закреплены 7.5 для 19×19 и 5.5 для 13×13 и 9×9. Значение по умолчанию —
    /// из правил проекта, игрок меняет его в настройках.
    /// </remarks>
    [JsonPropertyName("komi")]
    public double Komi { get; set; } = 5.5;

    /// <summary>Система подсчёта: <c>Japanese</c> или <c>Chinese</c>.</summary>
    /// <remarks>
    /// Хранится именем, а не числом: файл настроек читается человеком, и перестановка элементов
    /// перечисления не изменит смысл сохранённого значения. Неизвестное имя — не ошибка:
    /// берётся система по умолчанию.
    /// </remarks>
    [JsonPropertyName("scoringRule")]
    public string ScoringRule { get; set; } = nameof(Core.ScoringRule.Japanese);

    /// <summary>Играть ли звук ходов.</summary>
    /// <remarks>
    /// Звук — украшение, и игрок вправе его выключить: в турнирном зале, в транспорте или просто
    /// потому, что не нужен. Ключ читается терпимо: в старом файле настроек его нет, и тогда звук
    /// включён — так же, как в свежей установке. Хранится здесь, а не в голове платформы: выбор
    /// игрока один на все системы, а настройки уже переживают перезапуск.
    /// </remarks>
    [JsonPropertyName("soundEnabled")]
    public bool SoundEnabled { get; set; } = true;

    /// <summary>Возвращает размер доски из настроек.</summary>
    /// <returns>Размер доски; при недопустимом значении — 9×9.</returns>
    public BoardSize ToBoardSize() => BoardSize switch
    {
        var value when value == Core.BoardSize.Size13.Value => Core.BoardSize.Size13,
        var value when value == Core.BoardSize.Size19.Value => Core.BoardSize.Size19,
        _ => Core.BoardSize.Size9
    };

    /// <summary>Исправляет пару «доска + коми», оставшуюся от прежней версии приложения.</summary>
    /// <remarks>
    /// До D-051 экран настроек не пересчитывал коми при смене размера доски, и в файл уезжала
    /// пара «9×9 + 7.5» — значение, верное для 19×19. Для досок 9×9 и 13×13 коми 7.5 не является
    /// значением правил (<c>GO_RULES.md</c>, п. 10; D-049), поэтому такая пара распознаётся
    /// однозначно и заменяется правилом. Обратный случай (19×19 с коми 5.5) не трогаем: 5.5
    /// на большой доске — допустимый выбор игрока, и от наследия дефекта его не отличить.
    /// </remarks>
    public void RepairKomi()
    {
        var size = ToBoardSize();

        if (size.Value != Core.BoardSize.Size19.Value && Komi == Core.Komi.For19x19.Value)
        {
            Komi = Core.Komi.For(size).Value;
        }
    }

    /// <summary>Возвращает систему подсчёта из настроек.</summary>
    /// <returns>
    /// Система подсчёта; при неизвестном имени — японская, как в большинстве турниров РФ.
    /// </returns>
    /// <remarks>
    /// Японская система — основная в приложении: очки считаются как территория плюс пленные плюс
    /// снятые мёртвыми камни, плюс коми белым. Китайская (площадь) считается рядом и не прячется.
    /// </remarks>
    public Core.ScoringRule ToScoringRule() =>
        Enumeration.TryFromName<Core.ScoringRule>(ScoringRule) ?? Core.ScoringRule.Japanese;

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
    /// <param name="rule">Система подсчёта; <c>null</c> — японская.</param>
    /// <param name="soundEnabled">Играть ли звук ходов.</param>
    /// <returns>Настройки для сохранения.</returns>
    /// <remarks>
    /// Система подсчёта и звук переживают смену доски, уровня и загрузку партии: это не свойства
    /// партии, а выбор игрока, и терять его при каждой правке настроек нельзя.
    /// </remarks>
    public static AppSettings From(
        BoardSize size,
        DifficultyLevel level,
        StoneColor playerColor,
        Komi komi,
        ScoringRule? rule = null,
        bool soundEnabled = true) => new()
    {
        BoardSize = size.Value,
        AiRankKyu = level.RankKyu,
        PlayerColor = playerColor.Name,
        Komi = komi.Value,
        ScoringRule = (rule ?? Core.ScoringRule.Japanese).Name,
        SoundEnabled = soundEnabled
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
