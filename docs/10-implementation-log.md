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
| 2026-11-10 | 36c4def, 168cf2f | P1/P2 выпущены v0.2.124; на Android регресс: вечный экран «Готовлю нейросеть…» (continuation после await не возобновлялся, runtime pack 8.0.34) | cold start на CTR-LX1/AVD15: зависание; Desktop работает | Lead, tester |
| 2026-11-10 | 4cc5cb3 | Диагностика OpenShellViewAsync (try/catch+сообщение на экране), пин SDK 8.0.427; runtime pack остался 8.0.34 | v0.2.125: зависание без [CRT], UI-поток idle | Lead, tester, reviewer |
| 2026-11-10 | 77c7d13, eadc12e | Обход: PrepareAsync ConfigureAwait(false) + Dispatcher.UIThread.Post; MainActivity не трогает Avalonia на cold start; неудачный RuntimeFrameworkVersion убран | **v0.2.127: cold start до Home на CTR-LX1/AVD15**, logcat без FATAL/[CRT] | Lead, tester |
| 2026-11-11 | P3 | Нигири-розыгрыш цвета (протокол v2, commit-reveal, таймауты, выбор цвета, hardening F1–F4, cancel-pending-join) | App 1090/1090, Engine 1261+11 skip; ревью без блокеров | coder, reviewer, lead |
| 2026-11-11 | discovery fix | v0.2.129 не видела комнаты в одной сети: UDP-объявление содержало версию игры (2) вместо версии пакета обнаружения (1) | discovery-тесты 5/5, добавлен регресс; релиз v0.2.130 | lead, coder |
| 2026-11-11 | color fix | Приёмка v0.2.130: инверсия цвета при выигрыше гостя в нигири; фикс раскладки host/guest + тест ориентации | App 1090/1090, ColorDraw 9/9; релиз v0.2.131 | lead, tester |
| 2026-11-11 | v0.2.131 | P3 нигири PASS: обе стороны видят розыгрыш, ориентация цвета в обе стороны, фолбэки 15с, обрыв → терминал «В меню» (проверено точным тапом; прежнее «не нажимается» — ложное, тап был ниже кнопки), мисматч версии, регресс cold start/Recents/Back. Discovery v0.2.130 PASS. | tester accept-p1p2.md; R-001…R-011 закрыты (кроме R-008 deferred); косметика 360dp — minor | tester, lead |
| 2026-11-11 | broadcast fix | Телефон-хост объявляет комнату только глобальным broadcast; добавлены направленные /24 и /16 по адресу маршрута (Android), кнопки терминала 64 dp | тест LanNetworkBroadcastTests 1/1; релиз v0.2.132 | lead |
| 2026-11-11 | roles | R-012: закреплены роли coder/reviewer/tester и обязательный порядок код→ревью→живая матрица→релиз (docs/11-agent-roles.md) | применено к хотфиксам discovery/color/broadcast: reviewer ревьюит диффы, tester гоняет матрицу v0.2.132 | lead |
