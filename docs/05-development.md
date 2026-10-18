# 05. Разработка: окружение, сборка, тесты, проверки

## Что нужно

| Инструмент | Версия | Зачем |
|---|---|---|
| .NET SDK | 8.0.x (см. `global.json`) | сборка и тесты |
| PowerShell 7 (`pwsh`) | любая | `check.ps1`, вспомогательные скрипты (Windows) |
| Android SDK + workload `android` | для APK | **APK локально не собирается** в этой среде (SDK 10 vs `net8.0-android`); APK берётся из CI |
| Эмулятор Android | AVD `Android_Emulator_35` | живые проверки |

Остальное — пакеты NuGet, версии централизованы в `Directory.Packages.props`.

## Быстрый старт

```powershell
# сборка
dotnet build src/GoEngine.sln

# тесты (основной путь)
dotnet test src/GoEngine.sln

# приложение настольное
dotnet run --project src/GoEngine.App.Desktop

# полная проверка проекта (definition of done)
./check.ps1
./check.sh            # Linux/macOS
```

Если `dotnet test` в среде падает до запуска тестов (Win32Exception (5) в vstest), используйте
запасной прогон через `src/GoEngine.Tools.TestRunner`:

```powershell
$env:TEMP='artifacts/test-temp'; $env:TMP=$env:TEMP
dotnet build src/GoEngine.sln
dotnet run --project src/GoEngine.Tools.TestRunner --no-build -- src/GoEngine.Tests/bin/Debug/net8.0/GoEngine.Tests.dll
dotnet run --project src/GoEngine.Tools.TestRunner --no-build -- src/GoEngine.App.Tests/bin/Debug/net8.0/GoEngine.App.Tests.dll
```

## Что делает `check.ps1`

1. Собирает решение без предупреждений (warnings-as-errors).
2. Гоняет **оба** набора тестов (домен ~1226, приложение ~922).
3. Прогоняет проверочные режимы приложения без окна (16 режимов, см. ниже).
4. Опционально `--stress` — много партий подряд.

Код возврата ≠ 0 — проверка не пройдена. CI использует те же команды (`ci.yml`).

## Проверочные режимы без окна

Реализация — `src/GoEngine.App.Desktop/Diagnostics` (`ModeArguments`, `CheckModes`).
Запуск: `dotnet run --project src/GoEngine.App.Desktop --no-build -- --mode`.

| Режим | Что проверяет |
|---|---|
| `--smoke` | запуск без окна: сборка и точка входа целы |
| `--check` | попадание щелчка в точку доски |
| `--state` | данные панели статуса |
| `--settings` | запись/чтение настроек |
| `--sgf` | запись/чтение партии в SGF |
| `--animation` | анимация камней |
| `--e2e` | полный сценарий партии |
| `--stress` | нагрузка: много партий подряд |
| `--render`, `--render-anim`, `--render-territory` | рендер PNG тем же рендерером, что на экране |
| `--result` | баннер итога: победа/поражение/ничья |
| `--problems` | задачи: подписи, линия решения, отказ постороннему ходу |
| `--territory` | подсчёт: территория, пленные, мёртвые |
| `--clock` | часы партии: старт, пауза, возобновление, отмена |
| `--update` | обновление на подставном ответе сервера |
| `--crash-test` | проверка лога падений (в `check` не входит: падает намеренно) |

Новый режим: константа в `ModeArguments`, ветка в `CheckModes`, запуск в `check.ps1` и в `ci.yml`.

## Как писать код: краткая выжимка Style Bible

Полностью — `AGENTS.md` (обязателен). Самое важное:

- слои `Core ← AI ← App`, `Problems` между ними; нарушение — критический дефект;
- `sealed`, file-scoped namespace, `_camelCase`, `[]` для коллекций, `IReadOnlyList<T>` наружу;
- доменные типы — `Enumeration`, не `enum`;
- ожидаемые исходы — `Result`, `DomainException` только на инварианты;
- `TimeProvider` вместо `DateTime.Now`; инжектированный `Random`;
- логи только в `App` и через `[LoggerMessage]`; в `Core`/`AI` — никаких;
- XML-доки и комментарии на русском, объясняют **почему**;
- тесты: xUnit, имена на русском, один тест — одно утверждение, регрессии подписываются.

Формат ответа при генерации кода: слой → папка → имя файла → **полный файл** → тесты →
«какие правила Style Bible применены». Если правило противоречит запросу — спросить.

## Git и задачи

- `GIT.md` — правила веток и коммитов; сообщение — по сути изменения, ссылка на `D-xxx`/`T-xxx`.
- Задачи — `TASKS/T-0NN-*.md` по шаблону `TASK_TEMPLATE.md`; отчёты — `REPORTS/`.
- Решение оформляется как `## D-0xx. Название` в `DECISIONS.md` (причина, последствия, альтернативы).
- `artifacts/`, `tools/`, `TASKS/`, `REPORTS/` в git не входят.
- Перед коммитом: `./check.ps1`, `git add src <документы>`, осмысленное сообщение.

## Общие ресурсы (важно)

1. **Сборка и тесты — один ресурс.** Параллельный `dotnet build`/`dotnet test` в одной рабочей копии
   валит копирование dll (MSB3027). Запускать по очереди.
2. **Живой интерфейс — один на машину.** Блокировка `%TEMP%\goengine-ui.lock`: занял → сделал →
   освободил. Не убивать чужие процессы по имени — только свой по PID.
3. **Эмулятор — один на агента.** Каждому прогону свой AVD или последовательная работа;
   переключение задач на чужом экране делает прогон невалидным.

## Типовые проблемы

| Симптом | Причина | Что делать |
|---|---|---|
| MSB3027/MSB3021 «файл занят» | параллельная сборка/зависший TestRunner | закрыть зависший процесс по PID, пересобрать |
| `dotnet test` падает с Win32Exception (5) | хост vstest не стартует в среде | `TestRunner` (см. выше) |
| XAML-ошибки AVLN2000 | неверное свойство у контрола (например, `ItemSpacing` у `WrapPanel`) | читать сообщение, заменить на поддерживаемое |
| `CS0246`/`CS0103` в Android-коде | имя `Android` перекрыто пространством `GoEngine.App.Android` | писать `global::Android...` |
| промахи тапов на Android | смещение отрисовки/касаний на высоту статус-бара | цели ≥64 dp; см. D-107 |
| «висит на стартовой картинке» | правки в подготовке или создании оболочки | проверить на эмуляторе до выпуска |

## Полезные артефакты

- логи приложения: каталог данных (`AppDataPaths`), на Android — `Android/data/com.goengine.app/files/logs/`;
- отчёты QA и ревизий: `artifacts/qa*/`, `artifacts/fix*/` (не в git);
- скриншоты живых прогонов: рядом с отчётами;
- PNG проверочного рендера: режимы `--render*`.
