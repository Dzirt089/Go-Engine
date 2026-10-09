namespace GoEngine.App.Services;

using System.Text.Json.Serialization;

/// <summary>Прогресс одного урока или одной обучающей партии.</summary>
/// <remarks>
/// Для урока <see cref="Step"/> — номер текущего шага (с единицы), для партии — сколько ходов уже
/// сыграно (с нуля): в обоих случаях это «где игрок остановился», и по нему разбор продолжается
/// с того же места.
/// </remarks>
public sealed record StudyItemProgress
{
    /// <summary>Где игрок остановился: шаг урока или сыгранные ходы партии.</summary>
    [JsonPropertyName("step")]
    public int Step { get; init; }

    /// <summary>Материал пройден до конца.</summary>
    [JsonPropertyName("completed")]
    public bool Completed { get; init; }
}
