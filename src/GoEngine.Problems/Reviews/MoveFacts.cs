namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Что произошло ходом: цвет, координата и то, что видно по правилам и позиции.</summary>
/// <param name="MoveNumber">Номер хода партии (с единицы).</param>
/// <param name="Color">Чей ход.</param>
/// <param name="Point">Точка хода или <c>null</c> для паса.</param>
/// <param name="Captured">Сколько камней снято ходом.</param>
/// <param name="Atari">Группы соперника, попавшие в атари: цвет и число камней.</param>
/// <param name="Line">Линия от края: 1 — самая крайняя, 4 — четвёртая.</param>
/// <param name="Region">Где сделан ход: угол, сторона или центр.</param>
/// <param name="Connects">Ход примыкает к своим камням.</param>
/// <param name="Cuts">Ход примыкает к двум разным группам соперника: разрезает их.</param>
/// <param name="OwnGroups">Сколько своих групп стоит рядом: две и больше — ход их соединяет.</param>
/// <remarks>
/// Факты считаются по двум позициям: <c>before</c> нужна для соединения и разрезания (что было
/// до хода), <c>after</c> — для снятий и атари. Ничего не выдумывается: за игрока говорится только
/// то, что видно по правилам.
/// </remarks>
public readonly record struct MoveFacts(
    int MoveNumber,
    StoneColor Color,
    Point? Point,
    int Captured,
    IReadOnlyList<(StoneColor Color, int Stones)> Atari,
    int Line,
    string Region,
    bool Connects,
    bool Cuts,
    int OwnGroups);
