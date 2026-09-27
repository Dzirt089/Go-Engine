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

Признаки готовит `KataGoFeatures`, вывод — `OnnxEvaluator.Evaluate`. Сеть `IPositionEvaluator`
отдаёт оценку поиску MCTS (`DECISIONS.md`, D-034).

## Где сеть уместна

Модель принимает доски любого размера (оси высоты и ширины динамические), но обучена на 19×19.
На 9×9 её первый ход и оценка неправдоподобны (F4 и 87 % за чёрных при коми 5,5), поэтому
играть с сетью и мерить её силу нужно на 19×19. Прежняя таблица силы на 9×9 (D-021) с сетью
напрямую не сравнима.
## Попытка конвертации модели для 9×9 (2026-09-27)

Что сделано:

- скачана специализированная модель `kata9x9-b18c384nbt-20231025.bin.gz` (93,3 МБ) из релиза
  `v1.13.2-kata9x9` проекта lightvector/KataGo — формат `.bin.gz`, нативный для KataGo;
- скачана сборка KataGo v1.15.3 для Windows (Eigen/CPU, AVX2) в `tools/katago`;
- поставлен пайплайн конвертации `kaya-go/katago-onnx` в отдельное окружение `tools/venv`
  (torch CPU, onnx, onnxruntime).

Почему конвертация не выполнена:

1. **Команды `dumponnx` в KataGo нет.** Проверено запуском `katago.exe` без аргументов: среди
   подкоманд только gtp, benchmark, genconfig, contribute, match, version, analysis, tuner,
   selfplay, gatekeeper, evalsgf, testgpuerror, runtests и отладочные `run*` — экспорта модели
   в ONNX среди них нет. Сборка Eigen(CPU) ONNX-бэкенд не содержит.
2. **Пайплайн `kaya-go/katago-onnx` принимает не `.bin.gz`, а PyTorch-чекпоинт `model.ckpt`**:
   `download_and_extract_model` качает `media.katagotraining.org/uploaded/networks/zips/kata1/<имя>.zip`
   и берёт из него `model.ckpt`, а затем `convert_katago_torch_to_onnx` экспортирует его через
   `torch.onnx.export`. Для 9×9-модели PyTorch-чекпоинт найти не удалось: проверенные адреса на
   `media.katagotraining.org` (в том числе `zips/kata9x9/…`) отвечают отказом.

Что нужно, чтобы довести до конца (любой из путей):

- **А.** PyTorch-чекпоинт 9×9-модели: тогда конвертация идёт готовым пайплайном за минуты.
- **Б.** Читатель формата `.bin.gz` → `state_dict` (или сразу в ONNX): формат описан в исходниках
  KataGo (`cpp/neuralnet/modelversion.cpp`, `neuralnet.cpp`), работа на 1–2 сессии; выход должен
  совпасть по контракту с уже подключённой моделью (`bin_input`, `global_input`, `policy`, `value`).
- **В.** Сборка KataGo с `USE_ONNX_BACKEND=1` (CMake + MSVC + ONNX Runtime) — тогда экспорт делает
  сам движок через `OnnxModelBuilder`.

Пока файла нет, профиль `Finetuned9x9` остаётся без файла: на 9×9 доступны уровни кю, уровень Дан
там не предлагается (`DECISIONS.md`, D-039).
