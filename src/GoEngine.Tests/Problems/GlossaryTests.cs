using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Тесты словаря терминов: данные, ссылки на уроки и язык объяснений.</summary>
/// <remarks>
/// Словарь — это данные, поэтому тесты проверяют сам материал: термины должны ссылаться
/// на существующие уроки, объяснения — быть на русском, а каждый урок обязан иметь хотя бы один
/// разобранный в нём термин (замечание пользователя 2026-10-07).
/// </remarks>
public sealed class GlossaryTests
{
    [Fact]
    public void Словарь_Загружается()
    {
        Assert.True(GlossaryLibrary.Count >= 10, $"терминов: {GlossaryLibrary.Count}");
    }

    [Fact]
    public void Названия_Терминов_Уникальны()
    {
        var names = GlossaryLibrary.All.Select(term => term.Name).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Каждый_Термин_Ссылается_На_Существующий_Урок()
    {
        foreach (var term in GlossaryLibrary.All)
        {
            foreach (var id in term.Lessons)
            {
                Assert.True(
                    LessonLibrary.ById(id) is not null,
                    $"термин «{term.Name}» ссылается на несуществующий урок {id}");
            }
        }
    }

    [Fact]
    public void У_Каждого_Урока_Есть_Термины()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            Assert.True(
                GlossaryLibrary.ForLesson(lesson.Id).Count > 0,
                $"у урока {lesson.Id} нет ни одного термина в словаре");
        }
    }

    [Fact]
    public void Объяснения_Терминов_На_Русском()
    {
        Assert.All(
            GlossaryLibrary.All,
            term => Assert.True(
                term.Text.Any(ch => ch >= 'А' && ch <= 'я'),
                $"термин «{term.Name}»: объяснение без кириллицы"));
    }

    [Fact]
    public void У_Терминов_Есть_Оригинал()
    {
        // Оригинальное название — справка для игрока: термины Го звучат по-японски,
        // и без оригинала слово из партии не связать со словарём.
        Assert.All(GlossaryLibrary.All, term => Assert.False(string.IsNullOrWhiteSpace(term.Original)));
    }

    [Fact]
    public void Пустой_Словарь_Отклоняется()
    {
        var parsed = GlossaryLibrary.Parse("""{"terms":[]}""");

        Assert.False(parsed.IsSuccess);
    }

    [Fact]
    public void Текст_Уроков_На_Русском()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            Assert.True(
                lesson.Steps.All(step => step.Text.Any(ch => ch >= 'А' && ch <= 'я')),
                $"{lesson.Id}: есть шаг без кириллицы");
        }
    }
}
