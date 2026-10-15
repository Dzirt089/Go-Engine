using Avalonia;
using Avalonia.Controls;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Панель разбора показывает нужные элементы на каждом ходу каждой партии.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-15: «Партии, после некоторого хода — нет информации о ходах.
/// Она есть в коде скорее всего — сломалось отображение». Проверка не верит модели: она смотрит
/// на элементы вида и требует, чтобы счётчик хода был видим и заполнен, а рядом с ним стояла либо
/// заметка разбора, либо строка о ходе.
/// </remarks>
public sealed class ReviewPanelContentTests
{
    [Fact]
    public void На_Каждом_Ходу_Видно_Счётчик_И_Объяснение()
    {
        var game = ReviewLibrary.All[0];
        var model = new ReviewViewModel();
        var view = new ReviewView(model);
        List<string> errors = [];
        var guard = 0;

        while (!model.IsCompleted && guard++ < game.Moves.Count + 20)
        {
            Arrange(view);

            var counter = view.FindControl<TextBlock>("MoveCounterText");
            var note = view.FindControl<TextBlock>("NoteTextText");
            var moveInfo = view.FindControl<TextBlock>("MoveInfoText");

            if (counter is null || !counter.IsVisible || string.IsNullOrWhiteSpace(counter.Text))
            {
                errors.Add($"ход {model.MoveNumber}: счётчик не виден или пуст");
            }
            else if (note is null || moveInfo is null)
            {
                errors.Add($"ход {model.MoveNumber}: в разметке нет строк разбора");
            }
            else if (!note.IsVisible && !moveInfo.IsVisible)
            {
                errors.Add($"ход {model.MoveNumber}: ни заметки, ни строки о ходе");
            }

            if (model.IsQuiz)
            {
                model.Play(game.Moves[model.MoveNumber].Point);
                continue;
            }

            if (!model.CanGoNext)
            {
                break;
            }

            model.Next();
        }

        Assert.True(errors.Count == 0, string.Join(" | ", errors.Take(4)));
    }

    [Fact]
    public void В_Разметке_Есть_Все_Элементы_Панели()
    {
        // Список обязательных элементов панели: если элемент потеряется при правке разметки,
        // проверка упадёт — именно так пропала строка о ходе в прошлый раз.
        var view = new ReviewView(new ReviewViewModel());
        string[] required =
        [
            "MoveCounterText", "NoteTitleText", "NoteTextText", "MoveInfoText",
            "GameTitleText", "MessageText", "ShapeList", "PanelActions", "Board"
        ];

        List<string> missing = [];

        foreach (var name in required)
        {
            if (view.FindControl<Control>(name) is null)
            {
                missing.Add(name);
            }
        }

        Assert.True(missing.Count == 0, $"нет элементов: {string.Join(", ", missing)}");
    }

    /// <summary>Укладывает вид в размер телефона: раскладка нужна для видимости элементов.</summary>
    /// <param name="view">Вид разбора.</param>
    private static void Arrange(ReviewView view)
    {
        const double width = 380;
        const double height = 780;

        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));

        if (view.Content is Control content)
        {
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
        }
    }
}
