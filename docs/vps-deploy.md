# Деплой MagicalBall на VPS

Как в TrueTaro и в проекте `71`: **ручной** деплой (WinSCP + bash), без авто-ssh.

Специфика MagicalBall (отличия от TrueTaro):

- Postgres **не** нужен. Смыслы вшиты в `proxy/meaning_bank.py`, кэш — JSON-файл.
- Ollama на VPS **не** нужен.
- На сервере обязателен ключ **GigaChat** (`GIGACHAT_CREDENTIALS`). Без него API живой, но ответы пойдут в синтез/туман.
- Тот же VPS, что TrueTaro (`147.45.173.26`). TrueTaro занимает **17878**. MagicalBall снаружи — **17879**.

**Публичный адрес — IP + порт из `.env` (`API_PORT`).** Отдельный домен не нужен.

Шпаргалка: [`deploy/DEPLOY.txt`](../deploy/DEPLOY.txt).

Скрипты из таблиц ниже — контракт (имена, флаги, что трогают). Реализация 1:1 по этому документу, после согласования.

---

## Порты (важно)

На VPS уже заняты `80`, `443`, `8000`, стек `71` — `18087` / `18088`, TrueTaro — **`17878`**.  
MagicalBall **не** берёт `8000` и **не** берёт `17878`.

| Где | Порт | Кто задаёт |
|-----|------|------------|
| Внутри контейнера `proxy` | всегда **8000** | Dockerfile / gunicorn |
| На хосте VPS (снаружи) | **`API_PORT`** (default **17879**) | `.env` в `/opt/magicalball` |
| JSON-кэш | том Docker `magicalball_cache` | не публикуется |

Godot бьёт в **хостовый** порт:

```text
http://<IP_VPS>:<API_PORT>
```

Пример при default: `http://147.45.173.26:17879`.

Перед стартом на VPS:

```bash
ss -tlnp | grep -E ':17879|:17878|:8000|:18087|:18088' || true
# если 17879 занят — в .env: API_PORT=19448
# firewall / security group: открыть TCP именно этого API_PORT
```

После смены `API_PORT` — тот же номер в Godot `config/api.json` (поле `android_base_url`).  
Редактор на ПК по-прежнему ходит на `http://127.0.0.1:17879` (локальный uvicorn / compose).

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

Godot (Android) → `http://IP:API_PORT/api/v1/oracle`.

Каталоги на одном хосте рядом, не смешивать:

| Стек | Каталог | Хостовый порт |
|------|---------|----------------|
| TrueTaro | `/opt/truetaro` | `17878` |
| MagicalBall | `/opt/magicalball` | `17879` |

Не заливать MagicalBall в `/opt/truetaro` и не делать `reload-proxy` TrueTaro для этого API.

---

## Что уезжает на VPS / что остаётся на ПК

| На VPS | Не на VPS |
|--------|-----------|
| `docker-compose.yml`, `.dockerignore`, `proxy/Dockerfile` | Godot-проект, APK, `.godot`, `.mono` |
| `proxy/` (код, `requirements.txt`, `ce/*.crt`) | `proxy/.env`, `proxy/tests`, `proxy/data/*.json` |
| `deploy/*.sh`, `deploy/env.example`, `DEPLOY.txt` | `deploy/prepare-winscp.ps1` можно класть, но на VPS не запускается |
| корневой `.env` (создаётся на сервере, **не** из WinSCP повторно) | ключи в git |

---

## Скрипты (контракт)

| Где | Файл | Назначение |
|-----|------|------------|
| ПК | `deploy/prepare-winscp.ps1` | Собрать `deploy\winscp-upload\` (без `.env`, без тестов, без кэша) |
| VPS | `deploy/install-on-vps.sh` | Первый `docker compose --env-file .env up -d --build` + wait `/health` |
| VPS | `deploy/reload-proxy-on-vps.sh` | Обновить **только** контейнер `proxy`: `--no-deps --force-recreate`. Том кэша не трогать |
| VPS | `deploy/backup-cache-on-vps.sh` | Скопировать `semantic_cache.json` из тома в `deploy/backups/` |
| VPS | `deploy/check-env.sh` | Проверить, что `GIGACHAT_CREDENTIALS` не пустой и `API_PORT` задан |

Postgres-скрипты TrueTaro (`dump-db-local.ps1`, `restore-db-on-vps.sh`, `generate-secrets.sh` для пароля БД) **не** копируем: БД нет.

Проверка секрета: руками вписать `GIGACHAT_CREDENTIALS` в `/opt/magicalball/.env` (тот же ключ, что в TrueTaro `.env` / локальном `proxy/.env`). `install-on-vps.sh` должен **выйти с ошибкой**, если поле пустое.

---

## A. Первый деплой

### A1. На ПК — пакет для WinSCP

```powershell
cd C:\epm-games\MagicalBall
.\deploy\prepare-winscp.ps1
```

Результат: `deploy\winscp-upload\` — **это** дерево заливать.

Состав пакета:

```text
winscp-upload/
  docker-compose.yml
  .dockerignore
  proxy/                 # код, Dockerfile, requirements, ce/
  deploy/
    DEPLOY.txt
    env.example
    install-on-vps.sh
    reload-proxy-on-vps.sh
    backup-cache-on-vps.sh
    check-env.sh
```

`prepare-winscp.ps1` не копирует: `.env`, `proxy/.env`, `__pycache__`, `tests`, `semantic_cache.json`, `.godot`, `android/`.

### A2. WinSCP → `/opt/magicalball/`

На VPS один раз: `mkdir -p /opt/magicalball`.

Залей **содержимое** `deploy\winscp-upload\` в `/opt/magicalball/`  
(должен появиться `/opt/magicalball/docker-compose.yml`, не вложенная папка `winscp-upload`).

**Не перезаписывай** рабочий `.env` на сервере целиком после первого запуска.

### A3. На VPS

```bash
mkdir -p /opt/magicalball
cd /opt/magicalball
cp deploy/env.example .env
nano .env
# вставь GIGACHAT_CREDENTIALS=...  (из TrueTaro / proxy/.env ПК)
# при необходимости: API_PORT=17879
chmod +x deploy/*.sh
bash deploy/check-env.sh

ss -tlnp | grep -E ':17879|:17878|:8000' || true
# firewall: allow TCP $API_PORT  (default 17879)

bash deploy/install-on-vps.sh
```

Проверка (подставь `API_PORT` из `.env`):

```bash
source <(grep -E '^API_PORT=' .env | sed 's/\r$//')
curl -s http://127.0.0.1:${API_PORT}/health
# {"status":"ok","service":"magicalball"}

curl -s http://<IP_VPS>:${API_PORT}/health
```

С телефона / с ПК вне VPS — тот же хостовый порт. Внутренний `:8000` в URL Godot **не** писать.

Без `GIGACHAT_CREDENTIALS` `/health` зелёный, но гадание не от LLM (клиент покажет туман).

### A4. Клиент Godot

Домен не нужен. Порт в URL = **`API_PORT` с VPS**.

[`config/api.json`](../config/api.json) (и `config/api.prod.example.json`):

```json
{
  "use_proxy": true,
  "proxy_base_url": "http://127.0.0.1:17879",
  "android_base_url": "http://147.45.173.26:17879"
}
```

- Редактор / Windows: `proxy_base_url` → локальный прокси.
- APK: `android_base_url` → VPS.

Если в `.env` на VPS `API_PORT=19448` — в `android_base_url` тоже `:19448`.

Android HTTP (cleartext) должен быть разрешён в экспорте (как в TrueTaro `network_security_config.xml`). Если ещё нет — отдельный шаг перед прод-APK, не часть этого гайда.

Сейчас в `api.json` Android смотрит на `:17878` (TrueTaro). После первого деплоя MagicalBall сменить на **`:17879`**.

---

## B. Последующие апдейты прода (только API)

Код прокси, промпт, санитайзер, очередь — без пересоздания тома и без нового `.env`.

```text
ПК:     .\deploy\prepare-winscp.ps1
WinSCP: залить свежий proxy/ (+ docker-compose.yml / Dockerfile, если менялись)
        не трогать /opt/magicalball/.env
VPS:    cd /opt/magicalball && bash deploy/reload-proxy-on-vps.sh
        curl -s http://127.0.0.1:${API_PORT}/health
```

`reload-proxy-on-vps.sh` эквивалент:

```bash
docker compose --env-file .env up -d --build --no-deps --force-recreate proxy
```

**Не** делать `docker compose down -v`. Том `magicalball_cache` должен пережить recreate контейнера.

Если менялся только Godot (UI, шар, реклама) — VPS не нужен, достаточно новой сборки APK.

---

## C. Бэкап кэша на VPS

Не колода, а JSON семантического кэша:

```bash
bash deploy/backup-cache-on-vps.sh
# → deploy/backups/cache-YYYYMMDD-HHMM.json
```

Восстановление: копировать файл обратно в том (скрипт restore — по необходимости, не в первом релизе). Потеря кэша не ломает прод: ответы снова идут в GigaChat.

---

## Compose на VPS (контракт)

Корневой `docker-compose.yml` для прода:

- Сервис **только** `proxy` (без postgres).
- `env_file` / `--env-file .env` из **корня** `/opt/magicalball/.env`, не `proxy/.env`.
- Проброс `"${API_PORT:-17879}:8000"`.
- `container_name: magicalball-proxy`, `image: magicalball-proxy:latest`.
- Volume: `magicalball_cache:/app/proxy/data` и в settings кэш = `/app/proxy/data/semantic_cache.json`.
- `restart: unless-stopped` — контейнер сам встаёт после `systemctl restart docker` и после ребута VPS, если его не останавливали через `compose stop`.
- Volume: `magicalball_cache:/app/proxy/data` и в settings кэш = `/app/proxy/data/semantic_cache.json`.
- `init: true` (tini) + entrypoint чинит владельца тома, чтобы запись кэша не падала после recreate.
- healthcheck: `curl -fsS http://127.0.0.1:8000/health`.

Внутри контейнера `API_PORT=8000` / bind `0.0.0.0:8000` (как сейчас в Dockerfile). Хостовый `API_PORT` из `.env` только в секции `ports:`.

---

## Переменные (`.env` на VPS)

Источник-шаблон: `deploy/env.example` (копия смыслов из `proxy/env.example` + комментарии про хостовый порт).

| Переменная | Смысл |
|------------|--------|
| `API_PORT` | Порт **на хосте** VPS (Godot). Default `17879`. Смени, если занят |
| `GIGACHAT_CREDENTIALS` | Authorization key Sber. Обязателен для LLM |
| `GIGACHAT_SCOPE` | `GIGACHAT_API_PERS` |
| `GIGACHAT_MODELS` | Ротация моделей |
| `GIGACHAT_VERIFY_SSL` / `GIGACHAT_CA_BUNDLE` | Сертификат Минцифры уже в `proxy/ce/` |
| `AI_QUEUE_TIMEOUT` | Секунды FIFO, default `5` |
| `SEMANTIC_THRESHOLD` | Порог кэша, default `0.82` |

Коммитить `.env` нельзя.

---

## Запрещено

- `docker compose down -v` / `docker volume prune` на `/opt/magicalball`
- Публиковать контейнерный `:8000` как URL в Godot, если `API_PORT` другой
- Сажать MagicalBall на порт TrueTaro `17878`
- Заливать Godot/APK/`android/` на VPS
- Класть ключ GigaChat в APK / `config/api.json`
- Авто-ssh / CI-деплой (как в TrueTaro — только WinSCP + bash)

---

## Частые казусы

| Симптом | Что проверить |
|---------|----------------|
| `bind: address already in use` | Занят `API_PORT` (часто путают с 17878 TrueTaro) — другой порт в `.env` и в `android_base_url` |
| С телефона нет связи | Firewall TCP `API_PORT`; в api.json IP и тот же порт |
| `/health` ok, шар в тумане | Пустой `GIGACHAT_CREDENTIALS` или очередь/GigaChat недоступны |
| После апдейта пропал кэш | Случайно `down -v`; том должен остаться |
| Android ходит в TrueTaro | `android_base_url` ещё `:17878` — сменить на `:17879` |

Локальная отладка: `proxy_base_url` → `http://127.0.0.1:17879`, `uvicorn` или `docker compose` на ПК. VPS для редактора не обязателен.

---

## Чеклист первого раза

1. Свободен ли на VPS TCP **17879** (TrueTaro на 17878 не трогать).
2. `prepare-winscp.ps1` → WinSCP в `/opt/magicalball/` содержимым пакета.
3. `.env` из `env.example` + вписан `GIGACHAT_CREDENTIALS`.
4. `install-on-vps.sh`, curl `/health` с localhost и снаружи.
5. `android_base_url` = `http://147.45.173.26:17879` (или актуальный IP/порт).
6. Сборка APK **после** правки `api.json`.

## Чеклист апдейта API

1. `prepare-winscp.ps1`.
2. WinSCP: `proxy/` (+ Dockerfile/compose, если менялись). `.env` не затирать.
3. `reload-proxy-on-vps.sh`.
4. curl `/health`.
5. Одно гадание с Android: LLM → реклама → солнце, не туман.
