# 01. Архитектура

## Контекст

Приложение полностью офлайновое: один процесс, без серверов и без сетевых зависимостей во время игры.
Сеть нужна только для проверки обновлений. ИИ работает внутри процесса.

## Проекты и зависимости (проверено по `.csproj`)

```
Core  ←  AI  ←  AI.Onnx
  ↑       ↑
  └──── Problems
            ↑
          App  ←  App.Desktop (голова, + AI.Onnx)
            ↑
          App.Android (голова, + AI.Onnx)

Tests     → Core, AI, AI.Onnx, Problems
App.Tests → App
```

| Проект | Назначение | Ссылается на | Ключевой пакет |
|---|---|---|---|
| `GoEngine.Core` | правила, доска, счёт, SGF | — | только BCL |
| `GoEngine.AI` | MCTS, эвристики, уровни | Core | — |
| `GoEngine.AI.Onnx` | нейросетевая оценка (v2) | Core, AI | Microsoft.ML.OnnxRuntime |
| `GoEngine.Problems` | задачи, уроки, разборы, словарь | Core, AI | — |
| `GoEngine.App` | виды, модели, сервисы, рендер | Core, AI, Problems | Avalonia, SkiaSharp |
| `GoEngine.App.Desktop` | точка входа настольных ОС | App, AI.Onnx | Avalonia.Desktop |
| `GoEngine.App.Android` | точка входа Android | App, AI.Onnx | Avalonia.Android |
| `GoEngine.Tests` | доменные тесты (~1200) | Core, AI, AI.Onnx, Problems | xUnit |
| `GoEngine.App.Tests` | тесты вида и модели (~900) | App | xUnit, SkiaSharp |
| `GoEngine.Tools.TestRunner` | запасной прогон тестов без vstest | — | — |

Правило: **слой не ссылается на слой выше себя**. Нарушение — критический дефект архитектуры.

## Что где лежит

### Core

- `Board/` — `Board`, `Point`, `Move`, `Group`, `GroupTracker`, `CaptureResolver`, `MoveLegality`;
  `BoardCoordinates` (подписи точек), `BoardSize`, `StoneColor`, `MoveType`.
- `Rules/` — `KoRule`, `PositionHash`, `PositionHistory`.
- `Scoring/` — `Scorer`, `FinalScore`, `ScoringRule`, `Komi`, `Score`, `DeadGroupSearch`, `Endgame`,
  `AreaBreakdown`.
- `Game/` — `GameState`, `GameStatus`, `GameResult`.
- `Common/` — `Enumeration`, `Result`/`Result<T>`, `MoveResult`, `DomainException`.
- `Sgf/` — `SgfReader`, `SgfWriter`, `SgfGame`.

### AI

- `Selectors/` — `IMoveSelector`, `AiFactory`, `DifficultyLevel`, тактические фильтры, политики конца партии.
- `Mcts/` — `MctsMoveSelector`, `MctsSearch`, `MctsTree`, `MctsNode`, `MctsConfig`, оценка позиции.
- `Playouts/`, `Patterns/`, `Evaluation/`, `SelfPlay/` — playout'ы, паттерны 3×3, оценка, самосоперничество.

### Problems

- задачи: `Problem*` (SGF-разбор, сессия, проверка цели, жизнь, формы, тексты);
- уроки: `Lessons/` (JSON, сессия, шаги, словарь);
- разборы: `Reviews/` (`ReviewGame`, `ReviewNote`, `ReviewQuiz`, `ReviewSession`, `MoveDescriber`).

### App

- `Views/` — `ShellView` (оболочка), `BoardView` (партия), `ProblemView`, `LessonView`, `ReviewView`,
  `SettingsView`, `GlossaryView`, `AboutView`, `LoadingView`, баннеры и раскладка `BoardPanelLayout`.
- `ViewModels/` — `ShellViewModel`, `MainViewModel`, `ProblemViewModel`, `LessonViewModel`,
  `ReviewViewModel`, `GlossaryViewModel`, `UpdateViewModel`.
- `Services/` — настройки и их файл, прогресс обучения, `ScoringState`, `GameClock`, размеры моделей,
  установка моделей, пути данных, метрики платформы, `BoardLayoutRules`.
- `Rendering/` — `BoardRenderer` (Skia), геометрия, анимация камней, звук, значки форм.
- `Controls/` — собственные элементы управления.

## Ключевые потоки

### 1. Ход игрока

```
BoardView (щелчок) → BoardGeometry.HitTest → MainViewModel.Play(point)
  → GameState.TryPlay(Move) → MoveLegality → Board.ApplyMove (иммутабельно)
  → CaptureResolver → GameState (смена хода, проверка конца)
  → MainViewModel.NotifyAll → вид перерисовывается
```

AI ходит через `IMoveSelector` в фоновой задаче; отмена — токен; ответ применяется в UI-потоке.

### 2. Завершение партии и счёт

```
два паса / сдача / согласие сторон → GameStatus = FinishedBy...
  → ScoringState.Sync(board, status) → DeadGroupSearch (кого считать мёртвым)
  → FinalScore(board, dead, ScoringRule) → Territory/AreaMargin/Winner
  → баннер итога + строки счёта (`Score`, `ScoreDetail`)
```

Правило: счёт всегда считается по системе из настроек партии (японская — по умолчанию).
Если сторона не поставила ни одного камня, позиция считается **недоигранной**: территория не
начисляется, победитель не называется, предлагается «Продолжить партию» (D-115).

### 3. Запуск приложения

```
Program.Main → AppLog + CrashReporter → App.OnFrameworkInitializationCompleted
  → LoadingView (первый кадр сразу) → фоновая подготовка (модели, установка, настройки)
  → ShellView (единственная на процесс) → MainViewModel → MainWindow/BoardView
```

Логи и отчёты о падениях пишутся в каталог данных приложения; при следующем запуске показывается
признак прошлого сбоя.

## Инварианты, которые нельзя ломать

1. Публичный API `Board` иммутабелен, внутреннее представление не отдаётся наружу.
2. `GroupTracker` и доска не расходятся: ход всегда идёт через `GameState`/`Board.ApplyMove`.
3. Ожидаемые ошибки — `Result`, а не исключения. `Core`/`AI` не бросают `InvalidOperationException`.
4. `Core`/`AI` не логируют и не знают про время и случайность напрямую (`TimeProvider`, инжектированный `Random`).
5. Содержание не хардкодится в UI: UI читает `Problems` (библиотеки задач/уроков/разборов/словаря).
6. Порядок слоёв соблюдается в обе стороны: `Problems` не знает про Avalonia.

## Как добавлять новое

| Хочу | Куда | Обязательно |
|---|---|---|
| правило/структуру домена | `Core` | тесты в `GoEngine.Tests`, XML-доки |
| эвристику ИИ | `AI` | детерминированный тест с seed |
| задачу/урок/разбор | `Problems` + `04-content.md` | тест содержания |
| экран | `App/Views` + `ViewModels` | тест вида/модели, проверка раскладки |
| проверку без окна | `App.Desktop/Diagnostics` | регистрация в `ModeArguments` + `check.ps1`/CI |
| решение | `DECISIONS.md` | D-номер, причина, последствия |

## Где искать ответы

- правила игры и границы — `GO_RULES.md`;
- почему так — `DECISIONS.md` (свежие решения в конце, ищите `D-0xx`);
- текущее состояние и незакрытые работы — `docs/08-known-issues.md`;
- как проверять — `docs/05-development.md` и `docs/06-release-and-quality.md`.
