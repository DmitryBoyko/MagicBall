# Архитектура «Волшебный шар: гадание»

Один экран Godot 4.7 Mono (C# / .NET 8), портрет 720×1600. Клиент собирает контекст, во время симуляции рекламы гоняет MobileNetV2, затем вызывает GigaChat по тому же контракту, что TrueTaro.

## Слои

```
Ui (MainScene)
 ├─ ProfileModal / InterpretationSheet / AdOverlay / VortexField
 ├─ ContextManager          ← имя, зодиак, батарея, якоря, фото-тег
 ├─ Vision                  ← ImagePreprocessor → PhotoSampler → OnnxInferenceEngine → InferenceWorker
 └─ AiGateway               ← FIFO 5с → GigaChatClient → extract_summary
                                 ↓ сбой / очередь полна
                            SemanticCache (порог 0.82) → MeaningBank.synthesize (156)
```

| Класс | Роль |
|---|---|
| `GameRoot` | Autoload: прогрев ONNX, профиль, настройки, музыка |
| `ProfileStore` | `user://user_profile.json` |
| `AstroCalculator` | Знак, стихия, планета, число судьбы, тотем, возраст |
| `ContextManager` | Склейка детерминированных + динамических + хаос-модификаторов |
| `ImagePreprocessor` | Последнее фото / тест → 224×224 RGB8 ImageNet NCHW (150528 float) |
| `OnnxInferenceEngine` | Синглтон `InferenceSession`, NNAPI→CPU, защита от `DllNotFoundException` |
| `PhotoSampler` | Последние 3–10 фото по одному → сводка архетипа/палитры/света |
| `MysticTagConverter` | ImageNet → русский архетип |
| `OracleProxyClient` | `POST /api/v1/oracle` — ключ Sber только на сервере |
| `AiGateway` | прокси → локальный synthesize, `osiris_present` |
| `SummaryExtractor` | Тело → `interpretation`, хвост после `[[ИТОГ]]` → `summary` |
| `VortexField` | 2600 частиц, 3 центра, 5 рукавов, фазы 0.08 / 0.22 / ≥2.4 / 0.95 с |

## Поток гадания

1. Тап «Спросить Оракула» блокирует кнопку.
2. Полноэкранный вихрь. Параллельно: последние N фото по одному (декод на главном потоке, MobileNet в фоне) → одна сводка в контекст.
3. Шар входит в вихрь. HTTP к GigaChat ставится в FIFO.
4. Удержание вихря не короче 2.4 с, даже если ответ уже пришёл.
5. Спад 0.95 с → кнопка «Открыть толкование».

## Данные

- Профиль и настройки — JSON в `user://`.
- Секреты GigaChat — `res://config/api.json` (как TrueTaro `.env`: Basic-ключ, Client ID, scope).
- Модель — `res://models/mobilenetv2-7.onnx`, при старте копируется в `user://models/` (реальный путь для native ORT).
- Якоря — `res://data/semantic_anchors.json` (60 позиций).
