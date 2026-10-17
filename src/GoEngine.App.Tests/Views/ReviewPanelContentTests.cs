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
    public void Панель_Показывает_Ход_С_Начала()
    {
        // Жалоба пользователя 2026-10-15: «На 8-м ходу и далее место урезанное, ничего не видно» —
        // прокрутка панели сохраняла смещение с прошлого хода, и заголовок заметки обрезался сверху.
        var game = ReviewLibrary.All[0];
        var model = new ReviewViewModel();
        var view = new ReviewView(model);
        var scroll = view.FindControl<ScrollViewer>("NoteScroll");
        List<string> errors = [];
        var guard = 0;

        while (!model.IsCompleted && guard++ < game.Moves.Count + 20)
        {
            Arrange(view);

            if (scroll is not null && scroll.Offset.Y > 0.5)
            {
                errors.Add($"ход {model.MoveNumber}: панель прокручена на {scroll.Offset.Y:F0} точек");
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
    public void Сообщение_Внутри_Прокрутки_И_С_Цветом_Из_Темы()
    {
        // Замечание пользователя 2026-10-17: «сообщение „Верно!..“ не прокручивается» — строка
        // стояла в отдельной полосе панели и обрезалась. Теперь она внутри прокрутки и берёт цвет
        // из темы классами; старая ошибка с FindResource без темы давала Foreground = null,
        // и Skia не рисовал ни одной буквы.
        var model = new ReviewViewModel();
        var view = new ReviewView(model);

        Arrange(view);

        var text = view.FindControl<TextBlock>("MessageText");
        var scroll = view.FindControl<ScrollViewer>("NoteScroll");

        Assert.NotNull(text);
        Assert.NotNull(scroll);

        // Сообщение прокручивается вместе с текстом: оно внутри области прокрутки.
        var inside = false;
        for (var parent = text!.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, scroll))
            {
                inside = true;
                break;
            }
        }

        Assert.True(inside, "сообщение стоит вне прокрутки: длинную строку не дочитать");

        // Цвет задаётся классами сообщения, а не кистью, найденной без темы.
        Assert.True(
            text.Classes.Contains("message-good") || text.Classes.Contains("message-bad"),
            "у сообщения нет класса цвета: " + string.Join(",", text.Classes));
    }

    [Fact]
    public void Текст_Хода_Не_Дублируется_В_Сообщении()
    {
        // Регрессия: Message получал текст заметки целиком, и панель резервировала вторую копию
        // абзаца. Сообщение говорит только об исходе хода.
        var game = ReviewLibrary.All[0];
        var model = new ReviewViewModel();
        var guard = 0;

        while (!model.IsCompleted && guard++ < 12)
        {
            if (model.HasNote && model.HasMessage)
            {
                Assert.NotEqual(model.NoteText, model.Message);
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
