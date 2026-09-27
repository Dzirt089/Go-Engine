# T-035: Подключение специализированных моделей для 9×9 и 13×13

## Роль

Ты — senior .NET разработчик в проекте Go Engine.
Стиль: `AGENTS.md`, `AGENTS_GO.md`. Правила: `GO_RULES.md`. Границы: `PROJECT.md`.

## Цель

Реализовать профили моделей: для каждого размера доски (9×9, 13×13, 19×19) должна использоваться своя ONNX-модель. Если специализированная модель для размера не найдена — нейросеть не используется, уровень откатывается до MCTS.

## Аудит перед началом

1. Прочитать `DECISIONS.md` — D-034, D-036, D-037, D-038 и новое **D-039**.
2. Изучить `OnnxEvaluator` (слой `AI.Onnx`) и интерфейс `IPositionEvaluator` (слой `AI`).
3. Учесть, что `AiFactory` находится в слое `AI` и не зависит от `AI.Onnx`.

## Что нужно сделать

### 1. Расширить `IPositionEvaluator`

Добавить метод:

```csharp
/// <summary>
/// Загружает модель, подходящую для указанного размера доски.
/// </summary>
/// <param name="boardSize">Размер доски (9, 13 или 19).</param>
/// <param name="modelsDirectory">Каталог, в котором лежат файлы моделей.</param>
/// <returns>true, если модель успешно загружена; false — если файл не найден.</returns>
bool LoadModelForBoardSize(int boardSize, string modelsDirectory);
2. Реализовать профили моделей в OnnxEvaluator

Создать вспомогательный класс ModelProfile (слой AI.Onnx):
csharp

public sealed record ModelProfile(
    string FileName,
    int MinBoardSize,
    int MaxBoardSize,
    string Description
);

Создать статический класс ModelProfiles:
csharp

public static class ModelProfiles
{
    public static readonly ModelProfile Main19x19 = new(
        "kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx",
        13, 19,
        "Основная модель KataGo (b28c512nbt). Рекомендуется для 19×19 и 13×13."
    );

    public static readonly ModelProfile Finetuned9x9 = new(
        "kata9x9-finetuned.uint8.onnx",
        7, 11,
        "Специализированная модель KataGo Finetuned 9x9. Только для 9×9."
    );

    public static IReadOnlyList<ModelProfile> All => new[] { Main19x19, Finetuned9x9 };

    public static ModelProfile? FindForBoardSize(int boardSize)
        => All.FirstOrDefault(p => boardSize >= p.MinBoardSize && boardSize <= p.MaxBoardSize);
}

3. Реализовать LoadModelForBoardSize в OnnxEvaluator
csharp

public bool LoadModelForBoardSize(int boardSize, string modelsDirectory)
{
    var profile = ModelProfiles.FindForBoardSize(boardSize);
    if (profile is null)
        return false;

    var path = Path.Combine(modelsDirectory, profile.FileName);
    if (!File.Exists(path))
        return false;

    LoadModel(path);
    return true;
}

4. Обновить AiFactory

Метод Create должен принимать дополнительный параметр string? modelsDirectory = null.

При запросе уровня с нейросетью (Dan5):

    Если evaluator == null → InvalidOperationException("Нейросеть не подключена.").

    Если modelsDirectory == null → InvalidOperationException("Не указан каталог моделей.").

    Вызвать evaluator.LoadModelForBoardSize(boardSize, modelsDirectory).

    Если возвращено false → InvalidOperationException($"Нет специализированной модели для доски {boardSize}×{boardSize}.").

5. Обновить SettingsViewModel

    Добавить свойство SelectedBoardSize (9, 13, 19).

    При изменении SelectedBoardSize проверять, доступен ли выбранный уровень.

    Если выбран Dan5, а для нового размера нет модели — автоматически переключить на Kyu10.

    Добавить свойство AvailableDifficultyLevels — отфильтрованный список уровней в зависимости от BoardSize и наличия файла модели.

6. Обновить Program.cs (Desktop)

При старте:

    Определить modelsDirectory = Path.Combine(AppContext.BaseDirectory, "models").

    Создать OnnxEvaluator (если файл основной модели существует).

    Передать evaluator и modelsDirectory в MainViewModel / App.

7. Написать тесты

    GoEngine.Tests: AiFactory выбрасывает исключение при отсутствии модели для размера.

    GoEngine.Tests: AiFactory создаёт Dan5 для 9×9, если специализированная модель найдена (mock IPositionEvaluator).

    GoEngine.App.Tests: SettingsViewModel понижает уровень до Kyu10 при смене размера на 9×9, если выбран Dan5.

Что НЕ нужно делать

    Не переносить ссылку на ONNX Runtime в слой Core или библиотеку App.

    Не хардкодить пути к файлам моделей вне ModelProfiles.

    Не пытаться «настроить» основную модель для 9×9.

Definition of Done

    □

    IPositionEvaluator расширен методом LoadModelForBoardSize.
    □

    OnnxEvaluator реализует выбор модели по размеру доски.
    □

    AiFactory корректно обрабатывает отсутствие модели для размера.
    □

    SettingsViewModel фильтрует уровни и понижает выбор при смене доски.
    □

    Program.cs передаёт modelsDirectory в App.
    □

    Написаны тесты в GoEngine.Tests и GoEngine.App.Tests.
    □

    dotnet test — зелёный, ./check.ps1 — зелёный.
    □

    В ответе перечислены применённые правила.

Формат ответа

    Слой, папка, имя файла (для каждого измененного/созданного файла).

    Полный код файла.

    Полный код тестов.

    Обновлённый STATE.md (полностью).

text


---

## 3. Обновление `STATE.md` (Раздел «Текущая задача»)

```markdown
## Текущая задача

**T-035. Подключение специализированных моделей для 9×9 и 13×13** — `in_progress`:
Реализовать профили моделей: для каждого размера доски используется своя ONNX-модель. Для 9×9 — специализированная KataGo Finetuned 9x9, для 19×19 и 13×13 — основная b28c512nbt. Если модель для размера не найдена — нейросеть не используется, уровень откатывается до MCTS.

4. Ключевой код: ModelProfiles.cs

Слой: AI.Onnx
Папка: GoEngine.AI.Onnx/Models/
Файл: ModelProfiles.cs
csharp

using System.Collections.Generic;
using System.Linq;

namespace GoEngine.AI.Onnx.Models;

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

5. Ключевой код: обновление IPositionEvaluator

Слой: AI
Папка: GoEngine.AI/Evaluators/
Файл: IPositionEvaluator.cs (добавить метод)
csharp

namespace GoEngine.AI.Evaluators;

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

6. Ключевой код: AiFactory (изменённый)

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
                throw new InvalidOperationException(
                    "Нейросеть не подключена. Убедитесь, что модель загружена при старте приложения.");

            if (modelsDirectory is null)
                throw new InvalidOperationException(
                    "Не указан каталог моделей. Передайте modelsDirectory в AiFactory.Create().");

            if (!_evaluator.LoadModelForBoardSize(boardSize, modelsDirectory))
                throw new InvalidOperationException(
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

7. Ключевой код: SettingsViewModel (фрагмент)

Слой: App
Папка: GoEngine.App/ViewModels/
Файл: SettingsViewModel.cs (ключевые изменения)
csharp

private int _selectedBoardSize = 19;

public int SelectedBoardSize
{
    get => _selectedBoardSize;
    set
    {
        if (_selectedBoardSize == value) return;
        _selectedBoardSize = value;
        OnPropertyChanged();
        RefreshAvailableLevels();
        EnsureSelectedLevelIsValid();
    }
}

public ObservableCollection<DifficultyLevel> AvailableDifficultyLevels { get; } = new();

private void RefreshAvailableLevels()
{
    AvailableDifficultyLevels.Clear();

    var levels = DifficultyLevel.All;

    foreach (var level in levels)
    {
        if (level.Kind == PlayerKind.NeuralNetwork)
        {
            // Нейросеть доступна, только если для этого размера есть модель.
            if (!IsModelAvailableForBoardSize(SelectedBoardSize))
                continue;
        }

        AvailableDifficultyLevels.Add(level);
    }
}

private void EnsureSelectedLevelIsValid()
{
    if (SelectedDifficultyLevel is null)
        return;

    if (!AvailableDifficultyLevels.Contains(SelectedDifficultyLevel))
    {
        // Понижаем до Kyu10 — безопасный MCTS-уровень.
        SelectedDifficultyLevel = DifficultyLevel.Kyu10;
    }
}

private bool IsModelAvailableForBoardSize(int boardSize)
{
    // Проверяет наличие файла модели в каталоге моделей.
    // Реализация зависит от того, как App получает путь к modelsDirectory.
    var profile = ModelProfiles.FindForBoardSize(boardSize);
    if (profile is null) return false;

    var path = Path.Combine(_modelsDirectory, profile.FileName);
    return File.Exists(path);
}

8. Ключевой код: Program.cs (Desktop)

Слой: App.Desktop
Файл: Program.cs (фрагмент инициализации)
csharp

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
        // Логируем, но не падаем — приложение должно работать без сети.
        Console.Error.WriteLine($"Не удалось загрузить основную модель: {ex.Message}");
    }
}

var app = new App(evaluator, modelsDirectory);
app.Run();

9. Ключевые тесты

Файл: GoEngine.Tests/AiFactoryTests.cs
csharp

[Fact]
public void Create_Dan5_WithoutEvaluator_ThrowsInvalidOperation()
{
    var factory = new AiFactory(evaluator: null);
    var level = DifficultyLevel.Dan5;
    var ex = Assert.Throws<InvalidOperationException>(
        () => factory.Create(level, boardSize: 19, modelsDirectory: "/tmp/models"));
    Assert.Contains("Нейросеть не подключена", ex.Message);
}

[Fact]
public void Create_Dan5_On9x9_WithoutSpecializedModel_ThrowsInvalidOperation()
{
    var evaluator = new FakeEvaluator(loadSuccess: false);
    var factory = new AiFactory(evaluator);
    var level = DifficultyLevel.Dan5;

    var ex = Assert.Throws<InvalidOperationException>(
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

Файл: GoEngine.App.Tests/SettingsViewModelTests.cs
csharp

[Fact]
public void ChangingBoardSizeTo9x9_WhenDan5Selected_FallsBackToKyu10()
{
    var vm = new SettingsViewModel(modelsDirectory: "/tmp/models");
    vm.SelectedDifficultyLevel = DifficultyLevel.Dan5;
    vm.SelectedBoardSize = 19;

    vm.SelectedBoardSize = 9;

    Assert.Equal(DifficultyLevel.Kyu10, vm.SelectedDifficultyLevel);
}

10. Инструкция по получению специализированной модели 9×9

Модель Finetuned 9x9 доступна на официальном сайте KataGo:

    Откройте: https://katagotraining.org/extra_networks/

    Найдите раздел «KataGo Finetuned 9x9 Net» (October 2023).

    Скачайте файл kata9x9-finetuned.bin.gz (или аналогичное имя).

    Конвертируйте в ONNX одним из способов:

Способ A — встроенная команда KataGo:
bash

./katago dumponnx -model kata9x9-finetuned.bin.gz -out kata9x9-finetuned.onnx

Способ B — скрипт Kaya:
bash

pixi run katago-onnx convert ./kata9x9-finetuned.bin.gz

    Положите результат в models/kata9x9-finetuned.uint8.onnx (или переименуйте в соответствии с ModelProfiles.Finetuned9x9.FileName).

Если специализированная модель для 13×13 найдена не будет — используется основная модель b28c512nbt, которая на 13×13 показывает удовлетворительное качество (ход K10, оценка 62%).