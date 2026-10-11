using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Панель разбора объясняет каждый ход: пустоты быть не должно.</summary>
/// <remarks>
/// Жалоба пользователя 2026-10-11 (скриншот): на третьем ходу панель пуста, видно только счётчик
/// и подпись списка. Здесь проверяется, что модель отдаёт строку о ходе, а вид её показывает.
/// </remarks>
public sealed class ReviewMoveInfoTests
{
    [Fact]
    public void Панель_Объясняет_Третий_Ход()
    {
        var model = new ReviewViewModel();

        Advance(model, 3);

        Assert.False(string.IsNullOrWhiteSpace(model.MoveInfo), $"пустая строка о ходе: {model.MoveCounter}");
        Assert.True(model.HasMoveInfo);
    }

    [Fact]
    public void Строка_О_Ходе_Говорит_За_Игрока()
    {
        var model = new ReviewViewModel();

        Advance(model, 1);

        // Строка говорит за игрока: «Чёрные: D6 — занимаю угол…».
        Assert.Contains(":", model.MoveInfo, StringComparison.Ordinal);
        Assert.True(
            model.MoveInfo.StartsWith("Чёрные", StringComparison.Ordinal) || model.MoveInfo.StartsWith("Белые", StringComparison.Ordinal),
            $"строка не называет сторону: {model.MoveInfo}");
    }

    [Fact]
    public void Вид_Показывает_Строку_О_Ходе()
    {
        // Проверка вида, а не только модели: пустая панель на телефоне — это невидимый текст.
        var model = new ReviewViewModel();
        var view = new ReviewView(model);
        Advance(model, 3);

        view.Measure(new Size(380, 780));
        view.Arrange(new Rect(0, 0, 380, 780));

        if (view.Content is Control content)
        {
            content.Measure(new Size(380, 780));
            content.Arrange(new Rect(0, 0, 380, 780));
        }

        var text = view.FindControl<TextBlock>("MoveInfoText");

        Assert.NotNull(text);
        Assert.True(text!.IsVisible, "строка о ходе скрыта");
        Assert.False(string.IsNullOrWhiteSpace(text.Text), "строка о ходе пуста");

        // Размеров здесь не проверяем: без окна содержимое прокрутки не раскладывается.
        // Что строка действительно видна игроку, проверяет живой стенд на телефоне (verify-games.ps1).
    }

    [Fact]
    public void Каждый_Ход_Первой_Партии_Имеет_Объяснение()
    {
        var model = new ReviewViewModel();
        var game = ReviewLibrary.All[0];

        for (var index = 1; index <= game.Moves.Count; index++)
        {
            Advance(model, 1);

            Assert.False(string.IsNullOrWhiteSpace(model.MoveInfo), $"ход {index}: пустая строка");
        }
    }

    /// <summary>Делает указанное число ходов: отвечает на вопросы разбора, как игрок.</summary>
    /// <param name="model">Модель разбора.</param>
    /// <param name="steps">Сколько ходов пройти.</param>
    private static void Advance(ReviewViewModel model, int steps)
    {
        var game = ReviewLibrary.All[model.SelectedIndex];
        var guard = 0;

        for (var index = 0; index < steps && guard++ < steps * 3 + 5; index++)
        {
            if (model.IsQuiz)
            {
                model.Play(game.Moves[model.MoveNumber].Point);
                continue;
            }

            model.Next();
        }
    }
}
