# Прокси «Волшебный шар»

Ключ GigaChat живёт только на сервере. Клиент шлёт JSON в `POST /api/v1/oracle`.

Прод на VPS (первый деплой и апдейты): **[`docs/vps-deploy.md`](vps-deploy.md)**, шпаргалка [`deploy/DEPLOY.txt`](../deploy/DEPLOY.txt).

## Локально

```powershell
cd C:\epm-games\MagicalBall
copy proxy\env.example proxy\.env
# вставьте GIGACHAT_CREDENTIALS
pip install -r proxy\requirements.txt
$env:PYTHONPATH="C:\epm-games\MagicalBall"
uvicorn proxy.main:app --host 127.0.0.1 --port 17879
```

Godot (редактор): `config/api.json` → `proxy_base_url` = `http://127.0.0.1:17879`

## VPS (свой стек, рядом с TrueTaro)

Не маршрут внутри TrueTaro. Отдельный compose в `/opt/magicalball`, хостовый порт **17879**.

Android: `android_base_url` = `http://147.45.173.26:17879` (пример: `config/api.prod.example.json`).
