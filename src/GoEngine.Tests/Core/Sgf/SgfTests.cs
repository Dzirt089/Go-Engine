using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты формата SGF — <c>AGENTS.md</c>, п. 10.</summary>
public sealed class SgfTests
{
    [Fact]
    public void Sgf_Круговорот_Сохраняет_Позицию()
    {
        var game = SgfGameOf(
            Move.Play(new Point(4, 4), StoneColor.Black),
            Move.Play(new Point(2, 2), StoneColor.White),
            Move.Play(new Point(6, 6), StoneColor.Black));

        var restored = SgfReader.Read(SgfWriter.Write(game));

        Assert.Equal(game.Moves, restored.Value.Moves);
    }

    [Fact]
    public void Sgf_Размер_Доски_И_Коми_Сохраняются()
    {
        var game = new SgfGame(BoardSize.Size13, Komi.For13x13, [], null);

        var restored = SgfReader.Read(SgfWriter.Write(game)).Value;

        Assert.Equal(BoardSize.Size13, restored.Size);
    }

    [Fact]
    public void Sgf_Коми_Читается_Из_Свойства()
    {
        var restored = SgfReader.Read("(;GM[1]FF[4]SZ[9]KM[6.5])").Value;

        Assert.Equal(6.5, restored.Komi.Value);
    }

    [Fact]
    public void Sgf_Без_Коми_Берётся_Стандартное()
    {
        var restored = SgfReader.Read("(;GM[1]FF[4]SZ[9];B[ee])").Value;

        Assert.Equal(Komi.For9x9, restored.Komi);
    }

    [Fact]
    public void Sgf_Пас_Записывается_И_Читается()
    {
        var game = SgfGameOf(Move.Pass(StoneColor.Black), Move.Pass(StoneColor.White));

        var restored = SgfReader.Read(SgfWriter.Write(game)).Value;

        Assert.All(restored.Moves, move => Assert.Equal(MoveType.Pass, move.Type));
    }

    [Fact]
    public void Sgf_Комментарий_Сохраняется()
    {
        var game = new SgfGame(BoardSize.Size9, Komi.For9x9, [], "Партия из теста");

        var restored = SgfReader.Read(SgfWriter.Write(game)).Value;

        Assert.Equal("Партия из теста", restored.Comment);
    }

    [Fact]
    public void Sgf_Скобки_В_Комментарии_Экранируются()
    {
        var game = new SgfGame(BoardSize.Size9, Komi.For9x9, [], "партия [1] из теста");

        var restored = SgfReader.Read(SgfWriter.Write(game)).Value;

        Assert.Equal("партия [1] из теста", restored.Comment);
    }

    [Fact]
    public void Sgf_Неизвестные_Свойства_Пропускаются()
    {
        var restored = SgfReader.Read("(;GM[1]FF[4]SZ[9]PB[Игрок]PW[AI]RU[Chinese];B[ee])").Value;

        Assert.Single(restored.Moves);
    }

    [Fact]
    public void Sgf_Варианты_В_Скобках_Не_Ломают_Чтение()
    {
        var restored = SgfReader.Read("(;GM[1]SZ[9];B[ee](;W[cc])(;W[gg]))").Value;

        Assert.Single(restored.Moves);
    }

    [Fact]
    public void Sgf_Партия_Восстанавливается_В_GameState()
    {
        var game = SgfGameOf(
            Move.Play(new Point(4, 4), StoneColor.Black),
            Move.Play(new Point(2, 2), StoneColor.White));

        var restored = SgfReader.Read(SgfWriter.Write(game)).Value.ToGameState();

        Assert.Equal(2, restored.Value!.MoveNumber);
    }

    [Fact]
    public void Sgf_Без_SZ_Отказ()
    {
        Assert.False(SgfReader.Read("(;GM[1]FF[4];B[ee])").IsSuccess);
    }

    [Fact]
    public void Sgf_Недопустимый_Размер_Отказ()
    {
        Assert.False(SgfReader.Read("(;GM[1]FF[4]SZ[10])").IsSuccess);
    }

    [Fact]
    public void Sgf_Битый_Текст_Отказ()
    {
        Assert.False(SgfReader.Read("это не SGF").IsSuccess);
    }

    [Fact]
    public void Sgf_Пустой_Текст_Бросает()
    {
        Assert.Throws<ArgumentException>(() => SgfReader.Read("   "));
    }

    [Fact]
    public void Sgf_Ход_Вне_Доски_Не_Восстанавливается()
    {
        var restored = SgfReader.Read("(;GM[1]SZ[9];B[kk])").Value.ToGameState();

        Assert.False(restored.IsSuccess);
    }

    [Fact]
    public void Sgf_Запись_Начинается_С_Корневого_Узла()
    {
        var text = SgfWriter.Write(SgfGameOf(Move.Play(new Point(0, 0), StoneColor.Black)));

        Assert.StartsWith("(;GM[1]FF[4]", text, StringComparison.Ordinal);
    }

    /// <summary>Создаёт партию SGF с ходами на доске 9×9.</summary>
    /// <param name="moves">Ходы партии.</param>
    /// <returns>Партия для записи.</returns>
    private static SgfGame SgfGameOf(params Move[] moves) =>
        new(BoardSize.Size9, Komi.For9x9, moves, null);
}