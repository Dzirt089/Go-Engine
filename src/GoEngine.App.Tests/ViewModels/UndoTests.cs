using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Отмена и возврат хода: партия обязана оставаться играбельной.</summary>
/// <remarks>
/// Регрессия захода 1: пользователь пасовал несколько раз, соперник продолжал ходить, после нажатия
/// «Отменить» игра ломалась. Тесты держат три требования: отмена не оставляет партию без хода
/// (когда ходить должен соперник, а он не ходит), отмена завершённой партии возвращает её в игру,
/// а «Вернуть» даёт ровно прежнее состояние.
/// </remarks>
public sealed class UndoTests
{
    /// <summary>Зерно проверок: партия должна повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    /// <summary>Сколько ходов делает партия в проверках: больше лимита даже для 19×19.</summary>
    private const int MoveLimit = 400;

    [Fact]
    public void Отмена_Первого_Хода_Соперника_Не_Ломает_Партию()
    {
        // Игрок белый: первый ход делает соперник. Отменять ход игрока ещё нечего, и отмена
        // не имеет права оставить партию в состоянии «ходит соперник, но он не ходит».
        var model = Create(StoneColor.White);

        Assert.Equal("Белые", model.ToMove);
        Assert.Equal("1", model.MoveNumber);

        Assert.False(model.Undo());
        Assert.Equal("Белые", model.ToMove);
        Assert.Equal("1", model.MoveNumber);
    }

    [Fact]
    public void Отмена_Во_Время_Отсутствия_Ходов_Ничего_Не_Меняет()
    {
        var model = Create(StoneColor.Black);

        Assert.False(model.Undo());
        Assert.Equal("Чёрные", model.ToMove);
        Assert.Equal("0", model.MoveNumber);
    }

    [Fact]
    public void Отмена_После_Завершения_Партии_Возвращает_Партию_В_Игру()
    {
        var model = Create();
        PlayUntilFinished(model);

        Assert.NotEqual("Идёт", model.Status);
        Assert.True(model.HasOutcome);

        Assert.True(model.Undo());

        Assert.Equal("Идёт", model.Status);
        Assert.False(model.HasOutcome);
        Assert.Equal("Чёрные", model.ToMove);
    }

    [Fact]
    public void После_Отмены_Партия_Доигрывается_До_Конца()
    {
        var model = Create();
        PlayUntilFinished(model);

        Assert.True(model.Undo());
        Assert.True(model.Pass());

        PlayUntilFinished(model);

        Assert.NotEqual("Идёт", model.Status);
    }

    [Fact]
    public void Возврат_После_Отмены_Даёт_Прежнее_Состояние()
    {
        var model = Create();
        _ = model.PlayMove(new Point(4, 4));
        _ = model.PlayMove(new Point(2, 2));

        var before = Fingerprint(model);

        Assert.True(model.Undo());
        Assert.True(model.Redo());

        Assert.Equal(before, Fingerprint(model));
    }

    [Fact]
    public void Отмена_И_Возврат_Сохраняют_Историю_Суперко()
    {
        // Ход, повторяющий позицию партии, запрещён и после отмены с возвратом: история позиций
        // восстанавливается вместе с доской (иначе суперко переставал бы действовать).
        var model = Create();
        _ = model.PlayMove(new Point(4, 4));
        var moveNumber = model.MoveNumber;

        Assert.True(model.Undo());
        Assert.True(model.Redo());

        Assert.Equal(moveNumber, model.MoveNumber);
        Assert.True(model.PlayMove(new Point(3, 3)));
        Assert.Equal("Чёрные", model.ToMove);
    }

    /// <summary>Играет пасами и ответами AI, пока партия не завершится.</summary>
    /// <param name="model">Модель представления.</param>
    private static void PlayUntilFinished(MainViewModel model)
    {
        var moves = 0;

        while (model.Status == "Идёт" && moves < MoveLimit)
        {
            _ = model.Pass();
            moves++;
        }
    }

    /// <summary>Слепок состояния партии: позиция, ходы, очередь и статус.</summary>
    /// <param name="model">Модель представления.</param>
    /// <returns>Строка, по которой сравниваются состояния.</returns>
    private static string Fingerprint(MainViewModel model) =>
        string.Join('|', model.Board, model.MoveNumber, model.ToMove, model.Status, model.Score, model.Captures);

    /// <summary>Создаёт модель представления с фиксированным зерном.</summary>
    /// <param name="color">Цвет игрока.</param>
    /// <returns>Модель представления партии 9×9 со случайным соперником: партия идёт быстро.</returns>
    private static MainViewModel Create(StoneColor? color = null)
    {
        var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu30, color ?? StoneColor.Black, Komi.For9x9);

        return new MainViewModel(settings, new Random(Seed));
    }
}
