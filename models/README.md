# Модели нейросети

Файлы моделей в репозиторий не коммитятся: они весят десятки мегабайт и распространяются
отдельно (`.gitignore`, `DECISIONS.md` D-033). Здесь лежит только описание того, что скачать.

## KataGo (v2, MCTS с нейросетевой оценкой)

| Что | Значение |
|---|---|
| Файл | `kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx` |
| Источник | https://huggingface.co/kaya-go/kaya |
| Размер | 75 176 902 байта (≈72 МБ) |
| SHA-256 | `D9EA9DB19C59F3934EF566ABB8D296B3E94A22916A88352810499D02C88C5828` |
| Лицензия | MIT (веса KataGo и конвертация kaya-go) |
| Сеть | 28 блоков, 512 каналов; динамическая квантизация uint8 |

Скачать (Windows PowerShell, macOS, Linux):

```bash
mkdir -p models
curl -L -o models/kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx \
  https://huggingface.co/kaya-go/kaya/resolve/main/kata1-b28c512nbt-adam-s11165M-d5387M/kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx
```

Проверить, что скачалось то, что нужно:

```powershell
(Get-FileHash models/kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx -Algorithm SHA256).Hash
```

Без файла тесты модели и T-033 помечаются пропущенными — сборка и `check.ps1` остаются
зелёными, поэтому модель нужна только тем, кто работает над v2.

## Контракт модели

| Имя | Направление | Форма | Смысл |
|---|---|---|---|
| `bin_input` | вход | `[1, 22, 19, 19]` | 22 плоскости позиции (раскладка KataGo V7) |
| `global_input` | вход | `[1, 19]` | 19 глобальных признаков |
| `policy` | выход | `[1, 6, 362]` | логиты ходов; первый канал — политика, 362-й индекс — пас |
| `value` | выход | `[1, 3]` | логиты исхода: победа, поражение, ничья с точки зрения ходящего |
| `ownership`, `scoring`, `futurepos`, `seki`, `scorebelief` | выход | — | вспомогательные головы, пока не используются |

Признаки готовит `KataGoFeatures`, вывод — `OnnxEvaluator.Evaluate`.
