# 01. Архитектура

## Контекст

Приложение офлайновое: игра с ИИ, задачи и уроки работают без сети. Сеть нужна для проверки
обновлений и для необязательной партии двух людей по локальной Wi-Fi (D-118): одна партия —
один хост, без интернет-серверов и аккаунтов. ИИ работает внутри процесса.

## Проекты и зависимости (проверено по `.csproj`)

```
Core  ←  AI  ←  AI.Onnx
  ↑       ↑
  ├──── Problems
  └──── Multiplayer  (TLS, UDP-обнаружение, протокол, таймер хода)
            ↑
          App  ←  App.Desktop (голова, + AI.Onnx)
            ↑
          App.Android (голова, + AI.Onnx)

Tests     → Core, AI, AI.Onnx, Problems, Multiplayer
App.Tests → App
```

| Проект | Назначение | Ссылается на | Ключевой пакет |
|---|---|---|---|
| `GoEngine.Core` | правила, доска, счёт, SGF | — | только BCL |
| `GoEngine.AI` | MCTS, эвристики, уровни | Core | — |
| `GoEngine.AI.Onnx` | нейросетевая оценка (v2) | Core, AI | Microsoft.ML.OnnxRuntime |
| `GoEngine.Problems` | задачи, уроки, разборы, словарь | Core, AI | — |
| `GoEngine.Multiplayer` | локальная сеть: TLS-транспорт, UDP-обнаружение комнат, протокол ходов, таймер 90 с, reconnect | Core | только BCL |
| `GoEngine.App` | виды, модели, сервисы, рендер, лобби | Core, AI, Problems, Multiplayer | Avalonia, SkiaSharp |
| `GoEngine.App.Desktop` | точка входа настольных ОС | App, AI.Onnx | Avalonia.Desktop |
| `GoEngine.App.Android` | точка входа Android | App, AI.Onnx | Avalonia.Android |
| `GoEngine.Tests` | доменные тесты (~1261) | Core, AI, AI.Onnx, Problems, Multiplayer | xUnit |
| `GoEngine.App.Tests` | тесты вида и модели (~967) | App | xUnit, SkiaSharp |
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

### Multiplayer

- `MultiplayerOptions` — таймаут хода 90 секунд, переподключение 60 секунд, ожидание решения
  хозяина 2 минуты, интервалы heartbeat и обнаружения, порты, предел авто-пасов для предохранителя.
- `RoomWatcher`/`RoomAnnouncer` — UDP-объявление комнаты и список комнат; хозяин шлёт
  с эфемерного порта, гость слушает 47812.
- `LanTlsConnection`, `EphemeralTlsCertificate`, `TlsCertificateValidator` — TCP+TLS на BCL;
  на Windows при запрете импорта PFX сертификат однократно создаётся в пользовательском хранилище.
- `LanGameHost`/`LanGameClient` — host-authoritative сессия: гость присылает `move_request`,
  хозяин применяет и рассылает `move_applied`, при обрыве ждёт возвращения и шлёт полное
  состояние. `TurnTimer` — таймер хода; правило авто-паса живёт в Core.

### App

- `Views/` — `ShellView` (оболочка), `BoardView` (партия), `NetworkLobbyView` (сетевое лобби),
  `ProblemView`, `LessonView`, `ReviewView`,
  `SettingsView`, `GlossaryView`, `AboutView`, `LoadingView`, баннеры и раскладка `BoardPanelLayout`.
- `ViewModels/` — `ShellViewModel`, `MainViewModel` (в том числе сетевой режим),
  `NetworkLobbyViewModel`, `ProblemViewModel`, `LessonViewModel`, `ReviewViewModel`,
  `GlossaryViewModel`, `UpdateViewModel`.
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

### 1a. Сетевая партия двух людей

```
Лобби (NetworkLobbyViewModel) → RoomWatcher: список комнат
  → тап по карточке → LanGameClient.JoinAsync (TCP+TLS) → LanGameHost принимает гостя
  → MainViewModel.StartNetworkHost/Guest → GameState
  → гость: move_request → хост: проверка, GameState.Play, move_applied → гость: GameState.Play
  → таймер 90 с у хоста → GameState.PlayAutoPass → move_applied(autoPass)
```

Хозяин — единственный арбитр: только он применяет ходы и делает вынужденный пас. Гость
не применяет свой ход до подтверждения. Авто-пас не завершает партию (правило в Core),
а серия из трёх авто-пасов прерывает зависшую партию. При обрыве таймер стоит на паузе,
после возврата хозяин шлёт полное состояние.

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
7. Сеть не меняет правила `Core`: авто-пас — это `PlayAutoPass`, а не отдельная игра; ходы
   в сетевом режиме проходят через `GameState`, а не через параллельную доску.

## Как добавлять новое

| Хочу | Куда | Обязательно |
|---|---|---|
| правило/структуру домена | `Core` | тесты в `GoEngine.Tests`, XML-доки |
| сетевой протокол/таймер | `Multiplayer` | тесты в `GoEngine.Tests`, без Avalonia |
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
