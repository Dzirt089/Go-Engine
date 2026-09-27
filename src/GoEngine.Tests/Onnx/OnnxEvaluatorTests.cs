using GoEngine.AI.Onnx;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты подключения ONNX Runtime — T-031.</summary>
/// <remarks>
/// Модель KataGo в индекс git не попадает: 72 МБ, скачивается отдельно (`models/README.md`,
/// `DECISIONS.md`, D-033). Здесь проверяется то, что проверяемо без неё: рантайм доступен,
/// отсутствующий и повреждённый файл дают понятный отказ, а не исключение. Тесты настоящей
/// модели — в <see cref="OnnxModelTests"/>.
/// </remarks>
public sealed class OnnxEvaluatorTests
{
    [Fact]
    public void Оннкс_Рантайм_Доступен()
    {
        Assert.True(OnnxEvaluator.RuntimeVersion().IsSuccess);
    }

    [Fact]
    public void Оннкс_Отсутствующий_Файл_Отказ()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-model-{Guid.NewGuid():N}.onnx");

        Assert.False(OnnxEvaluator.Load(path).IsSuccess);
    }

    [Fact]
    public void Оннкс_Пустой_Путь_Бросает()
    {
        Assert.Throws<ArgumentException>(() => OnnxEvaluator.Load("  "));
    }

    [Fact]
    public void Оннкс_Повреждённый_Файл_Отказ_С_Причиной()
    {
        var path = Path.Combine(Path.GetTempPath(), $"go-engine-broken-{Guid.NewGuid():N}.onnx");

        try
        {
            // Рантайм установлен и работает: он сам отвергает файл, который не является моделью.
            File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);

            var loaded = OnnxEvaluator.Load(path);

            Assert.False(loaded.IsSuccess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Оннкс_Описание_Модели_Содержит_Размерности_Входов()
    {
        // Описание строится из сессии; форму типа проверяем на примере-заглушке.
        var info = new OnnxModelInfo(
            "model.onnx",
            [new OnnxTensorInfo("Input", [1, 22, 19, 19])],
            [new OnnxTensorInfo("Policy", [1, 362]), new OnnxTensorInfo("Value", [1, 3])]);

        Assert.Equal("Input [1×22×19×19]", info.Inputs[0].ToString());
    }

    [Fact]
    public void Кодировщик_И_Оценщик_Согласованы_По_Размеру_Входа()
    {
        // Вход модели KataGo — 22 плоскости 19×19 и 19 глобальных признаков (см. D-033).
        var board = new Board(BoardSize.Size19);

        var spatial = KataGoFeatures.EncodeSpatial(board, StoneColor.Black, []);
        var global = KataGoFeatures.EncodeGlobal(board, Komi.For19x19, StoneColor.Black, []);

        Assert.Equal(22 * 19 * 19, spatial.Length);
        Assert.Equal(19, global.Length);
    }
}
