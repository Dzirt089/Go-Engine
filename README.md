# Go Engine

Офлайн-приложение для игры в го и обучения: партия против ИИ, партия двух людей по локальной
Wi-Fi, задачи, уроки, партии-разборы, словарь терминов. Windows, Linux, macOS, Android.

**Стек:** C# 12, .NET 8, Avalonia UI 11, SkiaSharp, xUnit. ИИ — MCTS внутри процесса, нейросетевая
оценка по моделям ONNX (v2).

## Быстрый старт

```powershell
dotnet build src/GoEngine.sln
dotnet run --project src/GoEngine.App.Desktop
./check.ps1          # полная проверка: сборка, тесты, режимы
```

## Документация

- [Материалы для разработчиков](docs/README.md) — с чего начать и что где искать.
- [Вводная презентация](docs/GoEngine-onboarding.pptx) — 12 слайдов для кик-оффа.
- [Онбординг чек-лист](docs/onboarding-checklist.md) — первая неделя.
- [PROJECT.md](PROJECT.md) — манифест проекта.
- [AGENTS.md](AGENTS.md) — Style Bible (обязательна для кода).
- [GO_RULES.md](GO_RULES.md) — правила го и их понимание в продукте.
- [DECISIONS.md](DECISIONS.md) — архитектурные решения.

## Выпуск

Push в `main` запускает CI и публикацию релиза (`v0.2.<N>`, плавающий `latest`, `update.json`).
APK — `com.goengine.app-Signed.apk` из релиза. Перед публикацией обязателен смоук на устройстве —
подробно в [docs/06-release-and-quality.md](docs/06-release-and-quality.md).
