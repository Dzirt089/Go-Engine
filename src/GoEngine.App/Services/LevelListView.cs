using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Состав и подписи списка уровней: одно место на панель партии и экран настроек.</summary>
/// <remarks>
/// Состав зависит от размера доски и наличия модели (D-038, D-039). Подпись всегда называет
/// фактический движок и точный бюджет для этой доски (D-048, D-054): и панель партии, и экран
/// настроек знают выбранный размер, поэтому короткая подпись без доски здесь не нужна.
/// </remarks>
public sealed class LevelListView
{
    private readonly BoardSize _size;
    private readonly bool _modelAvailable;
    private readonly IReadOnlyList<DifficultyLevel> _levels;
    private readonly IReadOnlyList<string> _labels;

    private LevelListView(BoardSize size, bool modelAvailable)
    {
        _size = size;
        _modelAvailable = modelAvailable;
        _levels = LevelChooser.Available(size, modelAvailable);
        _labels = [.. _levels.Select(level => LevelChooser.Describe(level, size, modelAvailable))];
    }

    /// <summary>Список для панели партии: подпись называет движок, размер доски и бюджет.</summary>
    /// <param name="size">Размер доски партии.</param>
    /// <param name="modelAvailable">Нашлась ли модель для этого размера.</param>
    /// <returns>Список уровней с подписями.</returns>
    public static LevelListView ForGame(BoardSize size, bool modelAvailable) =>
        new(size, modelAvailable);

    /// <summary>Список для экрана настроек: тот же состав и те же подписи, что и в партии.</summary>
    /// <param name="size">Размер доски, выбранный сейчас.</param>
    /// <param name="modelAvailable">Нашлась ли модель для этого размера.</param>
    /// <returns>Список уровней с подписями для выбранной доски.</returns>
    public static LevelListView ForSettings(BoardSize size, bool modelAvailable) =>
        new(size, modelAvailable);

    /// <summary>Уровни в порядке списка.</summary>
    public IReadOnlyList<DifficultyLevel> Levels => _levels;

    /// <summary>Подписи уровней: тот же экземпляр, пока список не пересобран.</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Ищет уровень в списке.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Индекс уровня или <c>-1</c>, если уровня в списке нет.</returns>
    public int IndexOf(DifficultyLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        for (var index = 0; index < _levels.Count; index++)
        {
            if (_levels[index] == level)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Возвращает индекс доступного уровня.</summary>
    /// <param name="preferred">Желаемый уровень.</param>
    /// <returns>Индекс уровня в списке: недоступный заменяется уровнем сброса.</returns>
    public int IndexOfAvailable(DifficultyLevel preferred) =>
        IndexOf(LevelChooser.Resolve(preferred, _size, _modelAvailable));

    /// <summary>Возвращает уровень по индексу списка.</summary>
    /// <param name="index">Индекс в списке.</param>
    /// <returns>Уровень или уровень сброса, если индекс вне списка.</returns>
    public DifficultyLevel At(int index) =>
        index >= 0 && index < _levels.Count ? _levels[index] : LevelChooser.Fallback;
}
