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
| 2026-11-11 | docs | Уточнён D-133: directed broadcast помогает при Wi-Fi default route, но VPN/мобильные данные/Wi-Fi без интернета и нестандартные маски — ограничение; матрица P2P подчищена по факту PASS v0.2.128/131 | reviewer замечания закрыты (docs); живая phone↔phone — за tester | lead |
| 2026-11-11 | multicast/lock | BTK-W09 объявляет комнату, но не принимает чужие broadcast; добавлен WifiManager.MulticastLock + CHANGE_WIFI_MULTICAST_STATE, пересчёт broadcast каждый тик, дедупликация /24 и /16 | LanNetwork-тесты 3/3; релиз v0.2.133, ждёт живой матрицы | lead, tester |
| 2026-11-11 | v0.2.133 | R-012 PASS: независимое ревью hotfixes, живая матрица на CTR/MIS/BTK + Desktop: R-001 3/3, Back, темы 5.83/5.05 и 8.91/7.63, phone↔phone discovery+никiri, обрыв/терминал, protocol mismatch, cold start/Recents. PC-наблюдатель — Windows Firewall (окружение). | tester accept-p1p2.md; R-001…R-012 закрыты (R-008 deferred) | tester, reviewer, lead |
| 2026-11-11 | R-013 | Случайный зверь по умолчанию в лобби (Random.Shared, новый зверь на каждое открытие, ручной выбор сохраняется) | App 1092/1092 (+2), ревью R-012 чисто; релиз v0.2.134, живая проверка на 3 Android | coder, reviewer, lead |
| 2026-11-11 | v0.2.134 | R-013 PASS: зверь по умолчанию случаен (CTR: Лиса/Тигр/Заяц…; BTK: Лиса/Тигр/Сова/Панда/Лягушка), ручной выбор сохраняется, аватар уходит в комнату/заявку/шапку, discovery/join/нигири без регресса | tester accept-p1p2.md; обновлять все устройства | tester |
| 2026-11-11 | R-014 | Нигири: буферизация color_draw_offer до подписки (фикс «неверное раскрытие» на ПК↔Android) + хозяин видит свою горсть (число/чётность), понятные тексты | App 1093/1093, LanGameClient 2/2; релиз v0.2.135, живая проверка ПК↔Android | lead, coder |
| 2026-11-11 | v0.2.136 | Нигири: согласование «1 камень / 2 камня / 5 камней» в текстах хозяина и раскрытия | ColorDraw 11/11, App 10/10; релиз v0.2.136, быстрый живой recheck | lead |
| 2026-11-11 | v0.2.135/136 | R-014 PASS: ПК-хост→Android-гость и Android-хост→ПК-гость — без «неверного раскрытия», хост видит число/чётность, гость понятный текст, цвета верные; phone↔phone регресс PASS; «1 камень/2 камня/5 камней» | accept/v135; APK 0.2.136 | tester |
