# T-036: Конвертация специализированной модели 9×9 в ONNX

## Роль

Ты — senior .NET разработчик в проекте Go Engine.
Стиль: `AGENTS.md`, `AGENTS_GO.md`. Правила: `GO_RULES.md`. Границы: `PROJECT.md`.

## Цель

Получить файл `kata9x9-finetuned.uint8.onnx` из нативной модели `kata9x9-b18c384nbt-20231025.bin.gz` и положить его в `models/`, чтобы профиль `ModelProfiles.Finetuned9x9` заработал без правок кода.

## Аудит перед началом

1. Прочитать `DECISIONS.md` — D-039.
2. Изучить `ModelProfiles` и `OnnxEvaluator`.
3. Проверить, какие инструменты уже установлены: `katago`, `python`, `pixi`, `cmake`.

## Что нужно сделать

### Приоритет 1: Сборка KataGo с ONNX-бэкендом

Команда `dumponnx` доступна **только** в сборках KataGo с бэкендами TensorRT или ONNX. Сборка Eigen (CPU) её не содержит.

1. Проверить наличие MSVC / CMake.
2. Собрать KataGo с `USE_ONNX_BACKEND=ON`.
3. Выполнить:
   ```bash
   ./katago dumponnx -model kata9x9-b18c384nbt-20231025.bin.gz -out kata9x9-finetuned.onnx
4. Ключевой код: ModelProfiles.cs

Слой: AI.Onnx
Папка: GoEngine.AI.Onnx/Models/
Файл: ModelProfiles.cs
csharp

using System.Collections.Generic;
using System.Linq;

namespace GoEngine.AI.Onnx;

/// <summary>
/// Описание профиля нейросетевой модели: какой файл использовать
/// для какого диапазона размеров доски.
/// </summary>
public sealed record ModelProfile(
    string FileName,
    int MinBoardSize,
    int MaxBoardSize,
    string Description
);

/// <summary>
/// Каталог доступных профилей моделей.
/// </summary>
public static class ModelProfiles
{
    /// <summary>
    /// Основная модель KataGo (b28c512nbt).
    /// Обучена преимущественно на 19×19. На 13×13 показывает удовлетворительное качество.
    /// </summary>
    public static readonly ModelProfile Main19x19 = new(
        FileName: "kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx",
        MinBoardSize: 13,
        MaxBoardSize: 19,
        Description: "Основная модель KataGo (b28c512nbt). Рекомендуется для 19×19 и 13×13."
    );

    /// <summary>
    /// Специализированная модель, дообученная для игры на 9×9.
    /// На других размерах качество деградирует.
    /// </summary>
    public static readonly ModelProfile Finetuned9x9 = new(
        FileName: "kata9x9-finetuned.uint8.onnx",
        MinBoardSize: 7,
        MaxBoardSize: 11,
        Description: "Специализированная модель KataGo Finetuned 9x9. Только для 9×9."
    );

    /// <summary>
    /// Все доступные профили.
    /// </summary>
    public static IReadOnlyList<ModelProfile> All { get; } = new[]
    {
        Finetuned9x9,
        Main19x19,
    };

    /// <summary>
    /// Находит профиль, подходящий для указанного размера доски.
    /// Возвращает <c>null</c>, если подходящего профиля нет.
    /// </summary>
    public static ModelProfile? FindForBoardSize(int boardSize)
        => All.FirstOrDefault(p => boardSize >= p.MinBoardSize && boardSize <= p.MaxBoardSize);
}

5. Ключевой код: IPositionEvaluator.cs

Слой: AI
Папка: GoEngine.AI/Evaluators/
Файл: IPositionEvaluator.cs
csharp

namespace GoEngine.AI;

/// <summary>
/// Оценщик позиции на основе нейросети.
/// </summary>
public interface IPositionEvaluator
{
    /// <summary>
    /// Оценивает текущую позицию на доске.
    /// </summary>
    PositionEvaluation Evaluate(BoardState board, StoneColor nextPlayer);

    /// <summary>
    /// Загружает модель, подходящую для указанного размера доски.
    /// </summary>
    /// <param name="boardSize">Размер доски (9, 13 или 19).</param>
    /// <param name="modelsDirectory">Каталог, в котором лежат файлы моделей.</param>
    /// <returns>
    /// <c>true</c>, если модель успешно загружена;
    /// <c>false</c> — если файл для этого размера не найден.
    /// </returns>
    bool LoadModelForBoardSize(int boardSize, string modelsDirectory);
}

6. Ключевой код: AiFactory.cs

Слой: AI
Папка: GoEngine.AI/Selectors/
Файл: AiFactory.cs
csharp

using System;
using GoEngine.AI.Evaluators;
using GoEngine.Core;

namespace GoEngine.AI.Selectors;

public sealed class AiFactory
{
    private readonly IPositionEvaluator? _evaluator;

    public AiFactory(IPositionEvaluator? evaluator = null)
    {
        _evaluator = evaluator;
    }

    /// <summary>
    /// Создаёт AI-игрока для указанного уровня и размера доски.
    /// </summary>
    /// <param name="level">Уровень сложности.</param>
    /// <param name="boardSize">Размер доски (9, 13, 19).</param>
    /// <param name="modelsDirectory">Каталог с ONNX-моделями. Обязателен для уровней с нейросетью.</param>
    public IPlayer Create(DifficultyLevel level, int boardSize, string? modelsDirectory = null)
    {
        if (level.Kind == PlayerKind.NeuralNetwork)
        {
            if (_evaluator is null)
                throw new DomainException(
                    "Нейросеть не подключена. Убедитесь, что модель загружена при старте приложения.");

            if (modelsDirectory is null)
                throw new DomainException(
                    "Не указан каталог моделей. Передайте modelsDirectory в AiFactory.Create().");

            if (!_evaluator.LoadModelForBoardSize(boardSize, modelsDirectory))
                throw new DomainException(
                    $"Нет специализированной модели для доски {boardSize}×{boardSize}. " +
                    $"Проверьте наличие файла в каталоге: {modelsDirectory}");

            return new NeuralNetworkPlayer(level, _evaluator);
        }

        return level.Kind switch
        {
            PlayerKind.Random => new RandomPlayer(),
            PlayerKind.Heuristic => new HeuristicPlayer(level),
            PlayerKind.Mcts => new MctsPlayer(level),
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };
    }
}

7. Ключевой код: LevelChooser.cs (фрагмент)

Слой: App
Папка: GoEngine.App/Services/
Файл: LevelChooser.cs
csharp

using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using GoEngine.AI.Onnx;

namespace GoEngine.App.Services;

public sealed class LevelChooser
{
    private readonly string _modelsDirectory;

    public ObservableCollection<DifficultyLevel> AvailableLevels { get; } = new();

    public LevelChooser(string modelsDirectory)
    {
        _modelsDirectory = modelsDirectory;
        Refresh(19);
    }

    public void Refresh(int boardSize)
    {
        AvailableLevels.Clear();

        foreach (var level in DifficultyLevel.All)
        {
            if (level.Kind == PlayerKind.NeuralNetwork)
            {
                if (!IsModelAvailableForBoardSize(boardSize))
                    continue;
            }

            AvailableLevels.Add(level);
        }
    }

    private bool IsModelAvailableForBoardSize(int boardSize)
    {
        var profile = ModelProfiles.FindForBoardSize(boardSize);
        if (profile is null) return false;

        var path = Path.Combine(_modelsDirectory, profile.FileName);
        return File.Exists(path);
    }
}

8. Ключевой код: Program.cs (Desktop)

Слой: App.Desktop
Файл: Program.cs
csharp

using System;
using System.IO;
using GoEngine.AI;
using GoEngine.AI.Onnx;
using GoEngine.App;

var modelsDirectory = Path.Combine(AppContext.BaseDirectory, "models");

IPositionEvaluator? evaluator = null;
var mainModelPath = Path.Combine(modelsDirectory, ModelProfiles.Main19x19.FileName);

if (File.Exists(mainModelPath))
{
    try
    {
        evaluator = new OnnxEvaluator(mainModelPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Не удалось загрузить основную модель: {ex.Message}");
    }
}

var app = new App(evaluator, modelsDirectory);
app.Run();

9. Тесты

Файл: GoEngine.Tests/AiFactoryTests.cs
csharp

[Fact]
public void Create_Dan5_WithoutEvaluator_ThrowsDomainException()
{
    var factory = new AiFactory(evaluator: null);
    var level = DifficultyLevel.Dan5;
    var ex = Assert.Throws<DomainException>(
        () => factory.Create(level, boardSize: 19, modelsDirectory: "/tmp/models"));
    Assert.Contains("Нейросеть не подключена", ex.Message);
}

[Fact]
public void Create_Dan5_On9x9_WithoutSpecializedModel_ThrowsDomainException()
{
    var evaluator = new FakeEvaluator(loadSuccess: false);
    var factory = new AiFactory(evaluator);
    var level = DifficultyLevel.Dan5;

    var ex = Assert.Throws<DomainException>(
        () => factory.Create(level, boardSize: 9, modelsDirectory: "/tmp/models"));

    Assert.Contains("Нет специализированной модели", ex.Message);
}

[Fact]
public void Create_Dan5_On9x9_WithSpecializedModel_ReturnsNeuralNetworkPlayer()
{
    var evaluator = new FakeEvaluator(loadSuccess: true);
    var factory = new AiFactory(evaluator);
    var level = DifficultyLevel.Dan5;

    var player = factory.Create(level, boardSize: 9, modelsDirectory: "/tmp/models");

    Assert.IsType<NeuralNetworkPlayer>(player);
}

private sealed class FakeEvaluator : IPositionEvaluator
{
    private readonly bool _loadSuccess;

    public FakeEvaluator(bool loadSuccess) => _loadSuccess = loadSuccess;

    public PositionEvaluation Evaluate(BoardState board, StoneColor nextPlayer)
        => new(0.5, Array.Empty<Move>());

    public bool LoadModelForBoardSize(int boardSize, string modelsDirectory)
        => _loadSuccess;
}

Файл: GoEngine.App.Tests/LevelChooserTests.cs
csharp

[Fact]
public void Refresh_9x9_WithoutModel_HidesDanLevels()
{
    var chooser = new LevelChooser("/nonexistent");
    chooser.Refresh(9);

    Assert.DoesNotContain(chooser.AvailableLevels, l => l.Kind == PlayerKind.NeuralNetwork);
}

10. Инструкция по конвертации модели 9×9
Вариант A: Сборка KataGo с ONNX-бэкендом

Требования: Visual Studio 2022 (MSVC), CMake, ONNX Runtime.
bash

git clone https://github.com/lightvector/KataGo.git
cd KataGo/cpp
cmake -B build -DUSE_ONNX_BACKEND=ON -DUSE_EIGEN_BACKEND=OFF
cmake --build build --config Release
./build/Release/katago dumponnx \
  -model kata9x9-b18c384nbt-20231025.bin.gz \
  -out kata9x9-finetuned.onnx

Затем положите файл в models/kata9x9-finetuned.uint8.onnx.
Вариант B: Внешний конвертер isty2e/KataGoONNX
bash

git clone https://github.com/isty2e/KataGoONNX.git
cd KataGoONNX
pip install torch onnx onnxmltools
gunzip kata9x9-b18c384nbt-20231025.bin.gz
python convert.py --model kata9x9-b18c384nbt-20231025.bin \
  --model-config model.config.json \
  --output kata9x9-finetuned.onnx

model.config.json должен содержать:
json

{ "version": 8, "support_japanese_rules": true, "use_fixup": true, "use_scoremean_as_lead": false }

Ограничение: конвертер поддерживает только сети семейства g170 (b10+). Архитектура b18c384nbt может не поддерживаться.
Вариант C: Оставить 9×9 на MCTS

Если конвертация не удастся — профиль Finetuned9x9 остаётся без файла, и LevelChooser автоматически скроет уровни Дан для 9×9. Это поведение уже реализовано в T-035.
11. Что ответить агенту

    T-035 принята. Отклонения обоснованы.

    По модели 9×9: команда dumponnx существует, но только в сборках с ONNX/TensorRT-бэкендом. Твоя сборка Eigen её не содержит — это не ошибка.

    План: заводи T-036 с целью получить kata9x9-finetuned.uint8.onnx. Приоритет 1 — сборка KataGo с USE_ONNX_BACKEND=ON и dumponnx. Приоритет 2 — конвертер isty2e/KataGoONNX. Если оба не сработают — оставляем 9×9 на MCTS (вариант Г), профиль ждёт файл.

    Код уже готов: ModelProfiles, AiFactory, LevelChooser подхватят ONNX-файл без правок.