# Журнал реализации

> Короткая хронология: дата, ревизия/коммит, что сделано, чем проверено, кто.
> Подробные отчёты — в `artifacts/`, решения — в `DECISIONS.md`, требования — в `REQUIREMENTS.md`.

| Дата | Ревизия | Что сделано | Проверки | Кто |
|---|---|---|---|---|
| 2026-11-10 | 1a7778d | Разведка по запросу владельца: крэш Android, «Назад», темы, розыгрыш цвета, матрица P2P, память проекта. Код не менялся. | Crash-логи разобраны; вариант/план в artifacts/lead/proposal-v124.md | Lead, coder, reviewer, tester |
| 2026-11-10 | 1a7778d | Разведка закрыта: coder/reviewer отчёты; tester — тема (реальный телефон), сеть ПК↔ПК, попытка воспроизвести крэш. | artifacts/coder/recon.md, artifacts/reviewer/recon.md, artifacts/tester/recon.md | coder, reviewer, tester |
| 2026-11-10 | 1a7778d | Найден детерминированный repro R-001 (3/3): am start с NEW_TASK|MULTIPLE_TASK при живом процессе; телефон CTR-LX1 оставлен в ADB. | artifacts/tester/crash-repro/repro-recipe.md + crash-логи 220858/220926/221023 | tester |
| 2026-11-10 | рабочая копия | P1: MainViewHost.Release до base.OnCreate, идемпотентный AppLog, иерархический Back + подтверждение выхода из сети/Home; P2: локальные on-accent значения и токен AppOnAccentMuted в обеих темах. Принято ревью с условиями: SingleTask убираем (маскирует repro), Back-сеть только в игровом разделе, +тесты/доки. | build 0 ошибок; App.Tests 1044/1044; check.ps1 -SkipStress зелёный; engine 1257+5skip; ревью artifacts/reviewer/review-p1p2.md | coder, lead, reviewer |

## Формат записи
- Одна строка на завершённый шаг/коммит.
- Обязательно: что изменено, чем проверено (тесты/живые прогоны/ревью), кто делал.
- Не закрывать пункт без ссылки на REQUIREMENTS-номер и проверку.
