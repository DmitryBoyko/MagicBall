# Прокси «Волшебный шар»

Ключ GigaChat живёт только на сервере. Клиент шлёт JSON в `POST /api/v1/oracle`.

## Локально

```powershell
cd C:\epm-games\MagicalBall
copy proxy\env.example proxy\.env
# вставьте GIGACHAT_CREDENTIALS из TrueTaro .env
pip install -r proxy\requirements.txt
$env:PYTHONPATH="C:\epm-games\MagicalBall"
uvicorn proxy.main:app --host 127.0.0.1 --port 17879
```

Godot (редактор): `config/api.json` → `http://127.0.0.1:17879`

## VPS (общий с TrueTaro)

На том же хосте `147.45.173.26:17878` добавлен маршрут `/api/v1/oracle`.
После заливки кода TrueTaro:

```bash
bash deploy/reload-proxy-on-vps.sh
curl http://127.0.0.1:17878/api/v1/oracle
```

Android: `android_base_url` = `http://147.45.173.26:17878`
