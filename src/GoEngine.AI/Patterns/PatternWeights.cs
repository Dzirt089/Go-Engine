namespace GoEngine.AI.Patterns;

/// <summary>Веса окрестностей 3×3 для выбора хода в playout'е.</summary>
/// <remarks>
/// Веса подобраны вручную по смыслу фигур (hane, nobi, cut, atari, блокировка глаза)
/// и приведены к каноническому коду: повороты и отражения дают один и тот же вес.
/// Точные таблицы GNU Go сюда не переносятся: там другая структура паттернов
/// (окрестности с учётом края и последовательностей ходов), поэтому взяты консервативные
/// значения, которые можно подкручивать по замерам серии T-019.
/// Обозначения: <c>X</c> — свой камень, <c>O</c> — чужой, <c>.</c> — пусто, <c>#</c> — край.
/// </remarks>
public static class PatternWeights
{
    /// <summary>Вес окрестности, которой нет в таблице.</summary>
    public const double DefaultWeight = 0.5;

    /// <summary>Наименьший возможный вес: ход без веса всё равно должен быть возможен.</summary>
    public const double MinimalWeight = 0.05;

    private static readonly IReadOnlyDictionary<ushort, double> Weights = Create();

    /// <summary>Возвращает вес окрестности.</summary>
    /// <param name="pattern">Окрестность вокруг точки хода.</param>
    /// <returns>Вес фигуры; для неизвестных окрестностей — <see cref="DefaultWeight"/>.</returns>
    public static double WeightFor(Pattern3x3 pattern) =>
        Weights.TryGetValue(pattern.Canonical().Code, out var weight) ? weight : DefaultWeight;

    /// <summary>Строит таблицу весов.</summary>
    /// <returns>Веса по каноническим кодам.</returns>
    private static Dictionary<ushort, double> Create()
    {
        Dictionary<ushort, double> weights = [];

        // Контактная игра: свой камень рядом с чужим — обычное продолжение борьбы.
        Add(weights, 0.9, "...", ".XO", "...");
        Add(weights, 0.8, "...", ".X.", "..O");

        // Ханэ: подрезаем чужое расширение по краю.
        Add(weights, 0.95, "#O#", "OX.", "...");
        Add(weights, 0.9, "...", "OXO", "...");

        // Ноби: тянемся вдоль своих камней.
        Add(weights, 0.85, "...", ".XX", "...");
        Add(weights, 0.8, "...", "XX.", "...");

        // Соединение своих групп.
        Add(weights, 0.9, "X.X", ".X.", "...");

        // Разрезание чужих камней.
        Add(weights, 0.95, ".O.", ".X.", ".O.");
        Add(weights, 0.85, ".O.", "OX.", ".O.");

        // Атари на чужой камень: ход рядом с чужим камнем, у которого мало дамэ.
        Add(weights, 1.0, ".O.", "OX.", "...");

        // Спасение своего камня из атари.
        Add(weights, 1.0, "...", "OXO", "...");
        Add(weights, 0.9, "...", ".X.", ".O.");

        // Блокировка чужого глаза: ходим внутрь чужого глазного пространства.
        Add(weights, 0.95, "O.O", ".X.", "...");
        Add(weights, 0.9, ".O.", "OXO", ".O.");

        // Первая линия: обычно плохо, если только это не крайняя нужда.
        Add(weights, 0.2, "###", "#X.", "...");
        Add(weights, 0.25, "##.", "#X.", "...");

        // Ход в пустоту вдали от камней: почти всегда потеря темпа.
        Add(weights, 0.1, "...", ".X.", "...");
        Add(weights, 0.15, "...", ".X.", "..O");
        Add(weights, 0.2, "..O", ".X.", "...");

        return weights;
    }

    /// <summary>Добавляет вес фигуры по её схеме.</summary>
    /// <param name="weights">Таблица весов.</param>
    /// <param name="weight">Вес фигуры.</param>
    /// <param name="top">Верхняя строка схемы.</param>
    /// <param name="middle">Средняя строка схемы, в центре — свой камень.</param>
    /// <param name="bottom">Нижняя строка схемы.</param>
    private static void Add(Dictionary<ushort, double> weights, double weight, string top, string middle, string bottom)
    {
        var code = 0;

        for (var index = 0; index < Pattern3x3.Cells; index++)
        {
            var row = index / 3;
            var column = index % 3;
            var symbol = row switch
            {
                0 => top[column],
                1 => middle[column],
                _ => bottom[column]
            };

            code |= SymbolValue(symbol) << (2 * index);
        }

        weights[new Pattern3x3((ushort)code).Canonical().Code] = weight;
    }

    /// <summary>Переводит символ схемы в код клетки.</summary>
    /// <param name="symbol">Символ схемы.</param>
    /// <returns>Код клетки: 0 — пусто, 1 — свой, 2 — чужой, 3 — край.</returns>
    private static int SymbolValue(char symbol) => symbol switch
    {
        'X' => 1,
        'O' => 2,
        '#' => 3,
        _ => 0
    };
}
