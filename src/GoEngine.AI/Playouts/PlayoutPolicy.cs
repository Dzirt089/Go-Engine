namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Политика игры в playout'ах MCTS: эвристики атари, фигуры и веса окрестностей 3×3.</summary>
/// <remarks>
/// Вся логика выбора хода живёт в <see cref="PatternPolicy"/>; этот класс оставлен точкой входа
/// для MCTS и хранит настройки. MCTS внутри playout'а не вызывается (<c>DECISIONS.md</c>, D-003),
/// своего <see cref="Random"/> политика не создаёт (<c>AGENTS_GO.md</c>, п. 7).
/// </remarks>
public sealed class PlayoutPolicy
{
    private readonly PatternPolicy _policy;

    /// <summary>Создаёт политику с настройками по умолчанию.</summary>
    public PlayoutPolicy() : this(PlayoutConfig.Default)
    {
    }

    /// <summary>Создаёт политику с заданными настройками.</summary>
    /// <param name="config">Вероятности эвристик и защита глаз.</param>
    /// <exception cref="ArgumentOutOfRangeException">Вероятность вне диапазона 0…1.</exception>
    public PlayoutPolicy(PlayoutConfig config)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(config.AtariProbability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(config.AtariProbability, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(config.NeighborProbability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(config.NeighborProbability, 1);

        Config = config;
        _policy = new PatternPolicy(config);
    }

    /// <summary>Настройки политики.</summary>
    public PlayoutConfig Config { get; }

    /// <summary>Выбирает ход playout'а.</summary>
    /// <param name="state">Партия, в которой идёт playout.</param>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <returns>Легальный ход или пас, если ходить некуда.</returns>
    public Move SelectMove(GameState state, Random random)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _policy.SelectMove(state, random);
    }

    /// <summary>Выбирает ход playout'а по позиции и цвету, минуя партию.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Легальный ход или пас, если ходить некуда.</returns>
    public Move SelectMove(Board board, StoneColor color, Random random)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        return _policy.SelectMove(board, color, random);
    }
}