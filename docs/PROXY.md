# Прокси «Волшебный шар»

Ключ GigaChat живёт только на сервере. Клиент шлёт JSON в `POST /api/v1/oracle`.

Прод на VPS: **[`docs/vps-deploy.md`](vps-deploy.md)** (полный гайд), шпаргалка [`deploy/DEPLOY.txt`](../deploy/DEPLOY.txt).

Первый деплой на VPS — одна команда после WinSCP:

```bash
cd /opt/magicalball && chmod +x deploy/*.sh && bash deploy/setup-vps.sh
```

## Локально

```powershell
cd C:\epm-games\MagicalBall
copy proxy\env.example proxy\.env
# вставьте GIGACHAT_CREDENTIALS
pip install -r proxy\requirements.txt
$env:PYTHONPATH="C:\epm-games\MagicalBall"
uvicorn proxy.main:app --host 127.0.0.1 --port 18437
```

Godot (редактор): `config/api.json` → `proxy_base_url` = `http://127.0.0.1:18437`

## VPS (свой стек, рядом с TrueTaro)

Отдельный compose в `/opt/magicalball`, хостовый порт **18437**, IP **147.45.173.26**.

Android: `android_base_url` = `http://147.45.173.26:18437` (см. `config/api.prod.example.json`).
