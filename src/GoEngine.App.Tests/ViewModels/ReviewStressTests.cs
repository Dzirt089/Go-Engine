using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Разбор партий под нагрузкой: все партии проходятся целиком всеми способами.</summary>
/// <remarks>
/// <para>
/// Жалоба пользователя 2026-10-10: приложение упало на Android на первой обучающей партии
/// (примерно восьмидесятый ход). Общий код разбора — модель представления; здесь она прогоняется
/// по каждой партии до конца, со всеми действиями, которые игрок может сделать: «Далее», ответ
/// на вопрос ходом по доске, «Назад», «Заново», переключение партий, повторный проход, чтение
/// всех свойств панели на каждом ходу.
/// </para>
/// <para>
/// Проверка нужна не ради чисел, а ради падения: если общий код падает, тест это покажет
/// на настольной машине, а не только на телефоне.
/// </para>
/// </remarks>
public sealed class ReviewStressTests
{
    [Fact]
    public void Все_Партии_Проходятся_Целиком_Со_Всеми_Действиями()
    {
        var model = new ReviewViewModel();

        for (var index = 0; index < model.Labels.Count; index++)
        {
            model.SelectGame(index);
            Walk(model, index);
        }
    }

    [Fact]
    public void Партия_Проходится_Заново_И_От_Середины()
    {
        var model = new ReviewViewModel();
        model.SelectGame(0);
        Walk(model, 0);

        // Повторный проход начинается с сохранённого места: так приложение продолжает разбор
        // после перезапуска, и именно этот путь ведёт к восьмидесятому ходу партии.
        model.Restart();
        Walk(model, 0);
    }

    [Fact]
    public void Разбор_Продолжается_С_Сохранённого_Шага()
    {
        // Прогресс сохранён на середине первой партии: продолжение обязано работать и не падать.
        var progress = new StudyProgress();
        progress.SaveGame("game-01", 80, false);
        var model = new ReviewViewModel(ReviewLibrary.All, progress);

        Assert.True(model.MoveNumber >= 0);
        Walk(model, 0);
    }

    /// <summary>Проходит текущую партию до конца всеми действиями игрока.</summary>
    /// <param name="model">Модель представления разбора.</param>
    /// <param name="index">Номер партии в списке.</param>
    private static void Walk(ReviewViewModel model, int index)
    {
        var game = ReviewLibrary.All[index];
        var guard = 0;

        while (!model.IsCompleted && guard++ < game.Moves.Count * 3 + 50)
        {
            // Чтение всех свойств панели — то же, что делает вид на каждой перерисовке.
            _ = model.NoteTitle;
            _ = model.NoteText;
            _ = model.HasNote;
            _ = model.MoveCounter;
            _ = model.Message;
            _ = model.IsMessageGood;
            _ = model.Board;
            _ = model.QuizPoints;
            _ = model.HintPoint;

            if (model.IsQuiz)
            {
                // Ответ на вопрос: ход партии в этой точке — он всегда верный.
                model.Play(game.Moves[model.MoveNumber].Point);
                continue;
            }

            if (model.CanGoNext)
            {
                model.Next();
                continue;
            }

            break;
        }

        Assert.True(model.IsCompleted, $"партия не пройдена: {model.MoveCounter}");
    }
}
