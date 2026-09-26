# Состояние проекта

> Этот файл агент обязан обновлять **после каждой задачи**, до коммита.
> При старте новой сессии агент читает его первым.

## Текущая фаза

**Фаза 1. Core: доска и ходы**

## Текущая задача

**T-001. `Core.Board` + `Core.Move`** — завершена

## Статус

`done` — код написан, `dotnet build` без ошибок и предупреждений, `dotnet test` зелёный.
Git-коммит и push **не выполнены**: в рабочей папке нет work tree (см. «Блокеры»).

## Следующая задача

**T-002. `Core.GroupTracker` + `Core.Capture`** — `next`

## Дата начала фазы

2026-09-26

## Последнее обновление

2026-09-26T18:45:04Z

## Что сделано в текущей фазе

- [x] Создан solution `GoEngine.sln` (классический формат `.sln`, не `.slnx`)
- [x] Создан проект `GoEngine.Core` — class library, net8.0, C# 12
- [x] Создан проект `GoEngine.Tests` — xUnit, net8.0
- [x] `BoardSize` — размер доски, валидация только 9 / 13 / 19, свойство `Area`
- [x] `Point` — точка, `IsOnBoard`, `Neighbors` по стороне, формат координат «D4»
- [x] `StoneColor` — `Enumeration`: `Empty`, `Black`, `White`, метод `Opponent()`
- [x] `MoveType` — `Enumeration`: `Play`, `Pass`, `Resign`
- [x] `Move` — `readonly record struct`, фабрики `Play` / `Pass` / `Resign`, `Move.None`
- [x] `Board` — иммутабельный `ApplyMove`, `Clone`, `MakeMove`, `At`, `IsEmpty`,
      `AllPoints`, `EmptyPoints`, `OccupiedPoints`
- [x] `DomainException` — единственное исключение нарушения инварианта движка
- [x] `Enumeration` — базовый тип перечислений, `FromId<T>` / `FromName<T>`
- [x] 66 юнит-тестов; все имена из Definition of Done задачи присутствуют

## Что осталось в текущей фазе

- [ ] `Core.GroupTracker` + `Core.Capture` — задача T-002
- [ ] `Core.SuicideCheck` — задача T-003
- [ ] `MoveGenerator` (базовый) — в T-001 не входил, отдельной задачи в `PLAN.md` нет

## Блокеры

**Git-инфраструктура нерабочая.** Проверено командами:

- `git rev-parse --is-bare-repository` → `true`: папка `D:\Harness\Work-1` —
  это bare-репозиторий, а не рабочее дерево.
- `git status`, `git add -A`, `git commit` → `fatal: this operation must be run in a work tree`
- `git remote -v` → пусто: remote `origin` не настроен
- `git log` → `your current branch 'master' does not have any commits yet`
- `gh` и `glab` в системе не установлены, учётные данные GitHub/GitLab не заданы

Итог: шаг «commit + push во все remote'ы» из T-001 выполнить невозможно до решения пользователя.

## Открытые вопросы к пользователю

1. **Рабочее дерево.** Где оно должно лежать? Сейчас `D:\Harness\Work-1` — bare-репозиторий,
   и в нём же лежат файлы проекта. Предложение: `D:\Harness\Work-1` сделать обычным рабочим
   репозиторием (ветка `main`), а bare вынести в `D:\Harness\Work-1.git` и связать как `origin`.
2. **GitHub / GitLab.** Имена пользователей и способ аутентификации (`gh` и `glab` не установлены).
   Имя репозитория по умолчанию — `go-engine`, видимость — public.

## Журнал последних изменений

| Дата | Задача | Что сделано |
|---|---|---|
| 2026-09-26 | T-001 | Создан solution, пустой `Board` |
| 2026-09-26 | T-001 | Созданы `GoEngine.sln`, `GoEngine.Core`, `GoEngine.Tests` (net8.0, C# 12) |
| 2026-09-26 | T-001 | Реализованы `BoardSize`, `Point`, `StoneColor`, `MoveType`, `Move`, `Board`, `DomainException`, `Enumeration` |
| 2026-09-26 | T-001 | 66 юнит-тестов; `dotnet build` — 0 предупреждений, `dotnet test` — зелёный |
| 2026-09-26 | T-001 | Задача переведена в `done`; коммит и push заблокированы отсутствием work tree |

## Заметки для следующей сессии

- Читать `GO_RULES.md` перед началом.
- `Board` иммутабелен на публичном API. Снятие групп — T-002, самоубийство — T-003, ко — T-005:
  сейчас `ApplyMove` только ставит камень.
- Файлы задач лежат в папке `TSAKS/`, хотя `PLAN.md` и `GO_RULES.md` (п. 13) ссылаются на `TASKS/`.
  Расхождение зафиксировано; папка не переименована без команды пользователя.
- Папки `samples/`, на которую ссылаются `AGENTS.md` и `AGENTS_GO.md`, в репозитории нет:
  стиль брался только из этих двух файлов.
- .NET 8 SDK в системе не установлен (только SDK 10.0.400 и runtime 8.0.30): сборка `net8.0`
  проходит, targeting pack подтягивается из nuget.org. Для офлайн-сборки поставить .NET 8 SDK.

## Команды для проверки

```bash
dotnet build
dotnet test
```
