using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты чтения и записи партии — файлом и потоком.</summary>
/// <remarks>
/// Потоковые перегрузки появились ради мобильных систем: там у выбранного файла может не быть
/// локального пути. Круговорот «записать в поток → прочитать из потока» проверяется здесь,
/// потому что на телефоне этот путь — единственный.
/// </remarks>
public sealed class SgfStoreTests
{
    [Fact]
    public void Партия_Переживает_Круговорот_Через_Поток()
    {
        var game = SampleGame();

        using var stream = new MemoryStream();

        Assert.True(SgfStore.Save(game, stream).IsSuccess);

        stream.Position = 0;

        var loaded = SgfStore.Load(stream);

        Assert.True(loaded.IsSuccess);
        Assert.Equal(game.Moves.Count, loaded.Value.Moves.Count);
    }

    [Fact]
    public void Поток_Сохраняет_Размер_Доски_И_Коми()
    {
        var game = SampleGame();

        using var stream = new MemoryStream();
        _ = SgfStore.Save(game, stream);
        stream.Position = 0;

        var loaded = SgfStore.Load(stream);

        Assert.Equal(game.Size, loaded.Value.Size);
        Assert.Equal(game.Komi, loaded.Value.Komi);
    }

    [Fact]
    public void Поток_Не_Закрывается_После_Записи()
    {
        using var stream = new MemoryStream();

        _ = SgfStore.Save(SampleGame(), stream);

        // Поток остаётся живым: им распоряжается вызывающий код (файл из диалога выбора).
        Assert.True(stream.CanWrite);
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void Пустой_Поток_Даёт_Отказ_С_Причиной()
    {
        using var stream = new MemoryStream();

        var loaded = SgfStore.Load(stream);

        Assert.False(loaded.IsSuccess);
    }

    [Fact]
    public void Запись_В_Поток_Только_Для_Чтения_Даёт_Отказ()
    {
        using var stream = new MemoryStream(new byte[16], writable: false);

        var saved = SgfStore.Save(SampleGame(), stream);

        Assert.False(saved.IsSuccess);
    }

    /// <summary>Собирает небольшую партию для круговорота.</summary>
    /// <returns>Партия 9×9 с двумя ходами.</returns>
    private static SgfGame SampleGame()
    {
        List<Move> moves =
        [
            Move.Play(new Point(2, 2), StoneColor.Black),
            Move.Play(new Point(6, 6), StoneColor.White)
        ];

        return new SgfGame(BoardSize.Size9, Komi.For9x9, moves, "Партия для проверки круговорота");
    }
}
