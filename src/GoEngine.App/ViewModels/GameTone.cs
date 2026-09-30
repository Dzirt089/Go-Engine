namespace GoEngine.App.ViewModels;

/// <summary>Итог партии глазами игрока: он выиграл, проиграл или ничья.</summary>
/// <remarks>
/// Нужен виду, чтобы покрасить баннер итога. Цвет победителя для этого не годится: игроку
/// важно не «победили чёрные», а выиграл он сам или нет.
/// </remarks>
public enum GameTone
{
    /// <summary>Партия идёт: итога ещё нет.</summary>
    None,

    /// <summary>Игрок выиграл.</summary>
    Win,

    /// <summary>Игрок проиграл.</summary>
    Loss,

    /// <summary>Ничья.</summary>
    Draw
}
