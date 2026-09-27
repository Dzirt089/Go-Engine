using GoEngine.AI;
using GoEngine.Core;

using static GoEngine.Tests.TestPositions;

namespace GoEngine.Tests;

/// <summary>Тесты правил конца партии: движок не доедает свою территорию и вовремя пасует.</summary>
/// <remarks>
/// Жалоба пользователя: «противник доел мои камни, а потом начал ходить и стал поедать свои же очки
/// не останавливаясь». Правило живёт в <see cref="EndgamePolicy"/> и проверяется здесь без поиска:
/// позиции собираются прямо на доске, поэтому тесты быстрые и детерминированные.
/// </remarks>
public sealed class EndgamePolicyTests
{
    /// <summary>Строит позицию «стена чёрных»: колонка 0 — территория чёрных, остальное — нейтрально.</summary>
    /// <returns>Доска, где ход в (0,4) заполняет свою территорию, а ход в (4,4) — нейтральная зона.</returns>
    /// <remarks>
    /// Колонка 1 занята чёрными камнями сверху донизу, поэтому пустая колонка 0 граничит только
    /// с чёрными (и с краем доски) — это её территория. Белый камень в (8,8) делает остальную
    /// пустую область нейтральной: она граничит с обоими цветами.
    /// </remarks>
    private static Board WallPosition()
    {
        var board = new Board(BoardSize.Size9);

        for (var y = 0; y <= 8; y++)
        {
            board = board.ApplyMove(Move.Play(new Point(1, (byte)y), StoneColor.Black));
        }

        return board.ApplyMove(Move.Play(new Point(8, 8), StoneColor.White));
    }

    /// <summary>Ход чёрных в (0,4): заполняет свою территорию и ничего не даёт.</summary>
    private static Move OwnTerritoryMove() => Move.Play(new Point(0, 4), StoneColor.Black);

    /// <summary>Ход чёрных в (4,4): нейтральная зона, очко приносит.</summary>
    private static Move NeutralMove() => Move.Play(new Point(4, 4), StoneColor.Black);

    [Fact]
    public void Правило_Отсекает_Заполнение_Своей_Территории()
    {
        var allowed = EndgamePolicy.Instance.Apply(WallPosition(), StoneColor.Black, [OwnTerritoryMove()], opponentPassed: false);

        Assert.Empty(allowed.Moves);
    }

    [Fact]
    public void Отсечение_Всех_Ходов_Означает_Пас()
    {
        // Пустой список при EverythingAllowed = false — это требование пасовать, а не «играй что хочешь».
        var allowed = EndgamePolicy.Instance.Apply(WallPosition(), StoneColor.Black, [OwnTerritoryMove()], opponentPassed: false);

        Assert.False(allowed.EverythingAllowed);
    }

    [Fact]
    public void Правило_Оставляет_Ход_В_Нейтральной_Зоне()
    {
        var allowed = EndgamePolicy.Instance.Apply(WallPosition(), StoneColor.Black, [OwnTerritoryMove(), NeutralMove()], opponentPassed: false);

        Assert.Equal([NeutralMove()], allowed.Moves);
    }

    [Fact]
    public void После_Паса_Соперника_Правило_Оставляет_Растущий_Ход()
    {
        var allowed = EndgamePolicy.Instance.Apply(WallPosition(), StoneColor.Black, [OwnTerritoryMove(), NeutralMove()], opponentPassed: true);

        Assert.Equal([NeutralMove()], allowed.Moves);
    }

    [Fact]
    public void Правило_Не_Отсекает_Спасение_Своей_Группы()
    {
        // Спасение стоит внутри своей территории, но отказ от него стоил бы группы.
        var rescue = Move.Play(new Point(5, 4), StoneColor.Black);

        var allowed = EndgamePolicy.Instance.Apply(RescuePosition(), StoneColor.Black, [rescue], opponentPassed: true);

        Assert.Equal([rescue], allowed.Moves);
    }

    [Fact]
    public void Правило_Не_Отсекает_Захват()
    {
        var capture = Move.Play(new Point(1, 1), StoneColor.White);

        var allowed = EndgamePolicy.Instance.Apply(CapturePosition(), StoneColor.White, [capture], opponentPassed: true);

        Assert.Equal([capture], allowed.Moves);
    }

    [Fact]
    public void Правило_Возвращает_Тот_Же_Список_Когда_Отсекать_Нечего()
    {
        // Тот же экземпляр списка нужен селектору: по нему он понимает, что сужать кандидатов не надо.
        Move[] candidates = [NeutralMove()];

        var allowed = EndgamePolicy.Instance.Apply(WallPosition(), StoneColor.Black, candidates, opponentPassed: false);

        Assert.Same(candidates, allowed.Moves);
    }

    [Fact]
    public void Правило_Разрешает_Пустой_Список()
    {
        var allowed = EndgamePolicy.Instance.Apply(WallPosition(), StoneColor.Black, [], opponentPassed: false);

        Assert.True(allowed.EverythingAllowed);
    }

    [Fact]
    public void История_Без_Ходов_Не_Считается_Пасом_Соперника()
    {
        Assert.False(EndgamePolicy.OpponentPassed(null));
        Assert.False(EndgamePolicy.OpponentPassed([]));
    }

    [Fact]
    public void Ход_В_Истории_Не_Считается_Пасом_Соперника()
    {
        Assert.False(EndgamePolicy.OpponentPassed([Move.Play(new Point(4, 4), StoneColor.Black)]));
    }

    [Fact]
    public void Пас_В_Конце_Истории_Считается_Пасом_Соперника()
    {
        Assert.True(EndgamePolicy.OpponentPassed([Move.Play(new Point(4, 4), StoneColor.Black), Move.Pass(StoneColor.White)]));
    }

    [Fact]
    public void Правило_Требует_Позицию()
    {
        Assert.Throws<ArgumentNullException>(
            () => EndgamePolicy.Instance.Apply(null!, StoneColor.Black, [NeutralMove()], opponentPassed: false));
    }

    [Fact]
    public void Правило_Требует_Цвет()
    {
        Assert.Throws<ArgumentNullException>(
            () => EndgamePolicy.Instance.Apply(WallPosition(), null!, [NeutralMove()], opponentPassed: false));
    }

    [Fact]
    public void Правило_Требует_Список_Ходов()
    {
        Assert.Throws<ArgumentNullException>(
            () => EndgamePolicy.Instance.Apply(WallPosition(), StoneColor.Black, null!, opponentPassed: false));
    }
}
