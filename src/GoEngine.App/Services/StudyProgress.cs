namespace GoEngine.App.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Прогресс обучения: что уже пройдено и где игрок остановился.</summary>
/// <remarks>
/// <para>
/// Обучение — не одноразовый экран: в курсе десятки шагов и целые партии, и игрок возвращается
/// к ним по частям. Прогресс хранится отдельным файлом рядом с настройками (как оформление):
/// это свойство обучения, а не партии.
/// </para>
/// <para>
/// Записи — по идентификаторам материала (<c>go-01</c>, <c>game-03</c>), а не по номерам в списке:
/// добавление урока в середину курса не должно сдвигать чужой прогресс.
/// </para>
/// </remarks>
public sealed class StudyProgress
{
    /// <summary>Параметры записи: отступы, чтобы файл читался человеком.</summary>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Прогресс по урокам: идентификатор урока — шаг и признак прохождения.</summary>
    [JsonPropertyName("lessons")]
    public Dictionary<string, StudyItemProgress> Lessons { get; init; } = [];

    /// <summary>Прогресс по обучающим партиям: идентификатор партии — ход и признак разбора.</summary>
    [JsonPropertyName("games")]
    public Dictionary<string, StudyItemProgress> Games { get; init; } = [];

    /// <summary>Последний открытый урок: к нему возвращается раздел «Обучение».</summary>
    [JsonPropertyName("lastLesson")]
    public string? LastLesson { get; set; }

    /// <summary>Последняя открытая партия.</summary>
    [JsonPropertyName("lastGame")]
    public string? LastGame { get; set; }

    /// <summary>Сколько уроков пройдено до конца.</summary>
    [JsonIgnore]
    public int CompletedLessons => Lessons.Values.Count(static item => item.Completed);

    /// <summary>Сколько партий разобрано до конца.</summary>
    [JsonIgnore]
    public int CompletedGames => Games.Values.Count(static item => item.Completed);

    /// <summary>Пустой прогресс: ничего не начато.</summary>
    public static StudyProgress Empty => new();

    /// <summary>Читает прогресс из JSON.</summary>
    /// <param name="json">Содержимое файла; <c>null</c> или мусор — пустой прогресс.</param>
    /// <returns>Прогресс; испорченный файл не мешает учиться.</returns>
    /// <remarks>
    /// Порча файла — ожидаемый исход, а не нарушение инварианта (<c>AGENTS.md</c>, п. 4):
    /// игрок теряет отметки, но приложение работает.
    /// </remarks>
    public static StudyProgress Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new StudyProgress();
        }

        try
        {
            return JsonSerializer.Deserialize<StudyProgress>(json) ?? new StudyProgress();
        }
        catch (JsonException)
        {
            return new StudyProgress();
        }
    }

    /// <summary>Записывает прогресс в JSON.</summary>
    /// <returns>Текст файла прогресса.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, WriteOptions);

    /// <summary>Возвращает прогресс урока.</summary>
    /// <param name="lessonId">Идентификатор урока.</param>
    /// <returns>Запись прогресса или <c>null</c>, если урок ещё не открывали.</returns>
    public StudyItemProgress? ForLesson(string lessonId) => Find(Lessons, lessonId);

    /// <summary>Возвращает прогресс партии.</summary>
    /// <param name="gameId">Идентификатор партии.</param>
    /// <returns>Запись прогресса или <c>null</c>, если партию ещё не открывали.</returns>
    public StudyItemProgress? ForGame(string gameId) => Find(Games, gameId);

    /// <summary>Запоминает, где игрок остановился в уроке.</summary>
    /// <param name="lessonId">Идентификатор урока.</param>
    /// <param name="step">Номер текущего шага (с единицы).</param>
    /// <param name="completed">Урок пройден до конца.</param>
    public void SaveLesson(string lessonId, int step, bool completed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lessonId);

        Lessons[lessonId] = new StudyItemProgress { Step = Math.Max(step, 1), Completed = completed };
        LastLesson = lessonId;
    }

    /// <summary>Запоминает, где игрок остановился в разборе партии.</summary>
    /// <param name="gameId">Идентификатор партии.</param>
    /// <param name="move">Сколько ходов партии сыграно.</param>
    /// <param name="completed">Партия разобрана до конца.</param>
    public void SaveGame(string gameId, int move, bool completed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        Games[gameId] = new StudyItemProgress { Step = Math.Max(move, 0), Completed = completed };
        LastGame = gameId;
    }

    /// <summary>Ищет запись прогресса по идентификатору.</summary>
    private static StudyItemProgress? Find(IReadOnlyDictionary<string, StudyItemProgress> items, string id) =>
        !string.IsNullOrWhiteSpace(id) && items.TryGetValue(id, out var value) ? value : null;
}
