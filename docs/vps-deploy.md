# Деплой MagicalBall на VPS

Как в TrueTaro и в проекте `71`: **ручной** деплой (WinSCP + bash), без авто-ssh.

Специфика MagicalBall (отличия от TrueTaro):

- Postgres **не** нужен. Смыслы вшиты в `proxy/meaning_bank.py`, кэш — JSON-файл.
- Ollama на VPS **не** нужен.
- На сервере обязателен ключ **GigaChat** (`GIGACHAT_CREDENTIALS`). Без него API живой, но ответы пойдут в синтез/туман.
- Тот же VPS, что TrueTaro (`147.45.173.26`). TrueTaro занимает **17878**. MagicalBall снаружи — **18437** (нестандартный порт).

**Публичный адрес — IP + порт из `.env` (`VPS_IP`, `API_PORT`).** Отдельный домен не нужен.

Шпаргалка: [`deploy/DEPLOY.txt`](../deploy/DEPLOY.txt).

---

## Порты (важно)

На VPS уже заняты `80`, `443`, `8000`, стек `71` — `18087` / `18088`, TrueTaro — **`17878`**.  
MagicalBall **не** берёт `8000` и **не** берёт `17878`.

| Где | Порт | Кто задаёт |
|-----|------|------------|
| Внутри контейнера `proxy` | всегда **8000** | Dockerfile / gunicorn |
| На хосте VPS (снаружи) | **`API_PORT`** (default **18437**) | `.env` в `/opt/magicalball` |
| JSON-кэш | том Docker `magicalball_cache` | не публикуется |

Godot бьёт в **хостовый** порт:

```text
http://<VPS_IP>:<API_PORT>
```

Default: `http://147.45.173.26:18437`.

Перед стартом на VPS:

```bash
ss -tlnp | grep -E ':18437|:17878|:8000|:18087|:18088' || true
# если 18437 занят — в .env: API_PORT=<другой свободный>
# firewall / security group: открыть TCP именно этого API_PORT
```

После смены `API_PORT` — тот же номер в Godot `config/api.json` (поле `android_base_url`).  
Редактор на ПК по-прежнему ходит на `http://127.0.0.1:18437` (локальный uvicorn / compose).

---

## Схема

```text
ПК:  .\deploy\prepare-winscp.ps1
        │
        ▼ WinSCP (содержимое deploy\winscp-upload\)
VPS: /opt/magicalball/
        │
        ├─ docker compose (только proxy)
        ├─ volume magicalball_cache  →  /app/proxy/data
        └─ host :API_PORT  →  container :8000
```

Godot (Android) → `http://147.45.173.26:18437/api/v1/oracle`.

Каталоги на одном хосте рядом, не смешивать:

| Стек | Каталог | Хостовый порт |
|------|---------|----------------|
| TrueTaro | `/opt/truetaro` | `17878` |
| MagicalBall | `/opt/magicalball` | `18437` |

---

## Скрипты

| Где | Файл | Назначение |
|-----|------|------------|
| ПК | `deploy/prepare-winscp.ps1` | Собрать `deploy\winscp-upload\` (без `.env`, без тестов, без кэша) |
| VPS | `deploy/install-on-vps.sh` | Первый `docker compose --env-file .env up -d --build` + wait `/health` |
| VPS | `deploy/reload-proxy-on-vps.sh` | Обновить **только** контейнер `proxy`: `--no-deps --force-recreate` |
| VPS | `deploy/backup-cache-on-vps.sh` | Скопировать `semantic_cache.json` из тома в `deploy/backups/` |
| VPS | `deploy/check-env.sh` | Проверить `GIGACHAT_CREDENTIALS`, `VPS_IP`, `API_PORT` |

---

## A. Первый деплой

### A1. На ПК

```powershell
cd C:\epm-games\MagicalBall
.\deploy\prepare-winscp.ps1
```

### A2. WinSCP → `/opt/magicalball/`

Залей **содержимое** `deploy\winscp-upload\` в `/opt/magicalball/`.

### A3. На VPS

```bash
mkdir -p /opt/magicalball
cd /opt/magicalball
cp deploy/env.example .env
nano .env
# GIGACHAT_CREDENTIALS=...
# VPS_IP=147.45.173.26
# API_PORT=18437
chmod +x deploy/*.sh
bash deploy/check-env.sh
bash deploy/install-on-vps.sh
```

Проверка:

```bash
curl -s http://127.0.0.1:18437/health
curl -s http://147.45.173.26:18437/health
```

### A4. Клиент Godot

[`config/api.json`](../config/api.json):

```json
{
  "use_proxy": true,
  "proxy_base_url": "http://127.0.0.1:18437",
  "android_base_url": "http://147.45.173.26:18437"
}
```

---

## B. Апдейт API

```text
ПК:     .\deploy\prepare-winscp.ps1
WinSCP: proxy/  (не трогать .env)
VPS:    bash deploy/reload-proxy-on-vps.sh
```

**Не** делать `docker compose down -v`.

---

## Переменные (`.env` на VPS)

| Переменная | Смысл |
|------------|--------|
| `VPS_IP` | Публичный IP VPS. Default `147.45.173.26` |
| `API_PORT` | Порт **на хосте** VPS (Godot). Default `18437` |
| `GIGACHAT_CREDENTIALS` | Authorization key Sber. Обязателен для LLM |

---

## Чеклист первого раза

1. Firewall: TCP **18437** открыт.
2. `prepare-winscp.ps1` → WinSCP в `/opt/magicalball/`.
3. `.env` + `GIGACHAT_CREDENTIALS`.
4. `install-on-vps.sh`, curl `/health`.
5. `android_base_url` = `http://147.45.173.26:18437`.
6. Сборка APK.
