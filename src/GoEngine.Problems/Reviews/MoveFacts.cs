namespace GoEngine.Problems;

using System.Globalization;
using GoEngine.Core;

/// <summary>Что произошло ходом: цвет, координата и то, что видно по правилам.</summary>
/// <param name="MoveNumber">Номер хода партии (с единицы).</param>
/// <param name="Color">Чей ход.</param>
/// <param name="Point">Точка хода или <c>null</c> для паса.</param>
/// <param name="Captured">Сколько камней снято ходом.</param>
/// <param name="Atari">Группы соперника, попавшие в атари: цвет и число камней.</param>
/// <param name="Line">Линия от края: 1 — самая крайняя, 4 — четвёртая.</param>
/// <param name="Region">Где сделан ход: угол, сторона или центр.</param>
public readonly record struct MoveFacts(
    int MoveNumber,
    StoneColor Color,
    Point? Point,
    int Captured,
    IReadOnlyList<(StoneColor Color, int Stones)> Atari,
    int Line,
    string Region);
