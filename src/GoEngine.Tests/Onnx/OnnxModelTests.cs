using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты настоящей модели KataGo — T-033.</summary>
/// <remarks>
/// Модель лежит в `models/` и в индекс git не попадает: 72 МБ. Без неё тесты помечаются
/// пропущенными (<see cref="ModelFactAttribute"/>), поэтому проверка на агенте остаётся зелёной.
/// Что проверяется: контракт входов и выходов, осмысленность политики и значения. Числа модели
/// не закрепляются: другая модель даст другие числа, но те же свойства.
/// </remarks>
public sealed class OnnxModelTests
{
    /// <summary>Индекс паса в политике: точки доски 19×19, затем пас.</summary>
    private const int PassIndex = 19 * 19;

    [ModelFact]
    public void Модель_Ждёт_Двадцать_Две_Плоскости_И_Девятнадцать_Признаков()
    {
        using var evaluator = Load();

        var spatial = evaluator.Info.Inputs.Single(static input => input.Name == "bin_input");
        var global = evaluator.Info.Inputs.Single(static input => input.Name == "global_input");

        Assert.Equal([KataGoFeatures.SpatialPlanes, KataGoFeatures.GlobalFeatures], new[] { spatial.Dimensions[1], global.Dimensions[1] });
    }

    [ModelFact]
    public void Модель_Даёт_Политику_На_Все_Ходы_И_Пас()
    {
        using var evaluator = Load();

        var evaluation = evaluator.Evaluate(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        // Длина вектора — точки доски и пас: в объявлении модели последняя ось динамическая,
        // поэтому размер виден только по результату вывода.
        Assert.Equal(PassIndex + 1, evaluation.Value.Policy.Count);
    }

    [ModelFact]
    public void Модель_Политика_Это_Вероятности()
    {
        using var evaluator = Load();

        var evaluation = evaluator.Evaluate(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        Assert.Equal(1.0, evaluation.Value.Policy.Sum(static probability => probability), 0.001);
    }

    [ModelFact]
    public void Модель_Кодировщик_Согласован_С_Моделью()
    {
        using var evaluator = Load();

        var spatial = evaluator.Info.Inputs.Single(static input => input.Name == "bin_input").Dimensions[1];
        var global = evaluator.Info.Inputs.Single(static input => input.Name == "global_input").Dimensions[1];

        var board = new Board(BoardSize.Size19);

        Assert.Equal(
            new[] { spatial, global },
            new[] { KataGoFeatures.EncodeSpatial(board, StoneColor.Black, []).Length / (19 * 19), KataGoFeatures.EncodeGlobal(board, Komi.For19x19, StoneColor.Black, []).Length });
    }

    [ModelFact]
    public void Модель_На_Пустой_Доске_Играет_В_Углу()
    {
        using var evaluator = Load();

        var evaluation = evaluator.Evaluate(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        Assert.True(evaluation.IsSuccess, evaluation.Error);
        Assert.True(IsCornerOpening(evaluation.Value.BestMove()), $"лучший ход: {evaluation.Value.BestMove()}");
    }

    [ModelFact]
    public void Модель_Пас_На_Пустой_Доске_Почти_Не_Выбирает()
    {
        using var evaluator = Load();

        var evaluation = evaluator.Evaluate(new Board(BoardSize.Size19), Komi.For19x19, StoneColor.Black, []);

        Assert.True(evaluation.Value.Policy[PassIndex] < 0.01f);
    }

    [ModelFact]
    public void Модель_Считает_Перевес_В_Двадцать_Камней_Победой()
    {
        using var evaluator = Load();

        var evaluation = evaluator.Evaluate(Handicap(StoneColor.Black, 20), Komi.For19x19, StoneColor.Black, []);

        Assert.True(evaluation.Value.WinProbability > 0.9, $"победа: {evaluation.Value.WinProbability:F3}");
    }

    [ModelFact]
    public void Модель_Отдаёт_Перевес_Сопернику()
    {
        using var evaluator = Load();

        var evaluation = evaluator.Evaluate(Handicap(StoneColor.Black, 20), Komi.For19x19, StoneColor.White, []);

        Assert.True(evaluation.Value.WinProbability < 0.1, $"победа: {evaluation.Value.WinProbability:F3}");
    }

    [ModelFact]
    public void Модель_Большое_Коми_Уменьшает_Шансы_Чёрных()
    {
        using var evaluator = Load();

        var board = new Board(BoardSize.Size19);
        var withoutKomi = evaluator.Evaluate(board, new Komi(0), StoneColor.Black, []);
        var withKomi = evaluator.Evaluate(board, new Komi(50), StoneColor.Black, []);

        Assert.True(withKomi.Value.WinProbability < withoutKomi.Value.WinProbability);
    }

    [ModelFact]
    public void Модель_Принимает_Размер_Доски_Динамически()
    {
        using var evaluator = Load();

        // Оси высоты и ширины у модели динамические, поэтому 9×9 считается. Верить этой оценке
        // нельзя: сеть обучена на 19×19, и на 9×9 первый ход у неё F4 при 87 % за чёрных
        // (D-034). Тест держит контракт, а не качество игры на малой доске.
        var evaluation = evaluator.Evaluate(new Board(BoardSize.Size9), Komi.For9x9, StoneColor.Black, []);

        Assert.Equal((9 * 9) + 1, evaluation.Value.Policy.Count);
    }

    /// <summary>Загружает модель или сообщает, почему не вышло.</summary>
    /// <returns>Оценщик модели.</returns>
    private static OnnxEvaluator Load()
    {
        var loaded = OnnxEvaluator.Load(ModelFile.FullPath);

        Assert.True(loaded.IsSuccess, $"модель не загрузилась: {loaded.Error ?? "без причины"}");

        return loaded.Value!;
    }

    /// <summary>Первый ход в углу: третья или четвёртая линия от края.</summary>
    /// <param name="index">Индекс хода в политике.</param>
    /// <returns><c>true</c>, если ход стоит в одном из четырёх углов.</returns>
    private static bool IsCornerOpening(int index)
    {
        if (index is < 0 or >= PassIndex)
        {
            return false;
        }

        var x = index % 19;
        var y = index / 19;
        var near = (int value) => value is 2 or 3;
        var far = (int value) => value is 15 or 16;

        return (near(x) || far(x)) && (near(y) || far(y));
    }

    /// <summary>Ставит камни одного цвета через один в центре доски.</summary>
    /// <param name="color">Цвет камней.</param>
    /// <param name="count">Сколько камней поставить.</param>
    /// <returns>Доска с перевесом этого цвета.</returns>
    private static Board Handicap(StoneColor color, int count)
    {
        var board = new Board(BoardSize.Size19);
        var placed = 0;

        for (byte y = 2; y < 17 && placed < count; y += 2)
        {
            for (byte x = 2; x < 17 && placed < count; x += 2)
            {
                board = board.ApplyMove(Move.Play(new Point(x, y), color));
                placed++;
            }
        }

        return board;
    }
}
