namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Разбор позиции: что происходит в партии и на что смотреть.</summary>
/// <remarks>
/// Заметка привязана к номеру хода: она объясняет позицию <b>перед</b> ходом с этим номером
/// (0 — начало партии). Вопрос внутри заметки превращает разбор в практику.
/// </remarks>
public sealed class ReviewNote
{
    /// <summary>Создаёт заметку разбора.</summary>
    /// <param name="before">Номер хода партии, перед которым стоит заметка (0 — начало).</param>
    /// <param name="title">Короткий заголовок.</param>
    /// <param name="text">Текст разбора.</param>
    /// <param name="quiz">Вопрос к позиции; <c>null</c> — просто разбор.</param>
    /// <exception cref="DomainException">Заметка пуста или ссылается на отрицательный ход.</exception>
    public ReviewNote(int before, string title, string text, ReviewQuiz? quiz = null)
    {
        if (before < 0)
        {
            throw new DomainException($"Заметка разбора ссылается на ход {before}: номера ходов начинаются с нуля.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Before = before;
        Title = title;
        Text = text;
        Quiz = quiz;
    }

    /// <summary>Номер хода партии, перед которым стоит заметка.</summary>
    public int Before { get; }

    /// <summary>Короткий заголовок заметки.</summary>
    public string Title { get; }

    /// <summary>Текст разбора.</summary>
    public string Text { get; }

    /// <summary>Вопрос к позиции или <c>null</c>, если ход искать не нужно.</summary>
    public ReviewQuiz? Quiz { get; }
}
