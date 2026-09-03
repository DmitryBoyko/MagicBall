# Деплой MagicalBall на VPS

Ручной деплой (WinSCP + bash), без авто-ssh — как TrueTaro и проект `71`.

**Публичный адрес:** `http://147.45.173.26:18437`  
**Каталог на VPS:** `/opt/magicalball/`  
**Шпаргалка:** [`deploy/DEPLOY.txt`](../deploy/DEPLOY.txt)

---

## Что на сервере

| Компонент | Нужен? |
|-----------|--------|
| Docker + compose | да |
| Postgres | нет |
| Ollama | нет |
| GigaChat ключ (`GIGACHAT_CREDENTIALS`) | да — без него `/health` живой, но гадания уйдут в «туман» |

TrueTaro на том же VPS: `/opt/truetaro`, порт **17878**. MagicalBall — **18437** (нестандартный, не путать с TrueTaro и не брать 80/443/8000).

---

## Порты

| Где | Порт | Кто задаёт |
|-----|------|------------|
| Внутри контейнера | **8000** | Dockerfile / gunicorn |
| Снаружи (Godot, firewall) | **18437** | `API_PORT` в `/opt/magicalball/.env` |
| Кэш | том `magicalball_cache` | не публикуется |

В Godot **никогда** не писать `:8000` — только хостовый `API_PORT`.

Проверка занятости перед стартом:

```bash
ss -tlnp | grep -E ':18437|:17878|:8000|:18087|:18088' || true
```

Если `18437` занят — сменить `API_PORT` в `.env` **и** в `config/api.json` → `android_base_url`.

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
        └─ host :18437  →  container :8000
```

---

## Скрипты

| Где | Файл | Назначение |
|-----|------|------------|
| ПК | `deploy/prepare-winscp.ps1` | Собрать `deploy\winscp-upload\` |
| VPS | **`deploy/setup-vps.sh`** | **Всё в одном:** `.env`, ключ, firewall, docker, health |
| VPS | `deploy/check-env.sh` | Проверить `GIGACHAT_CREDENTIALS`, `VPS_IP`, `API_PORT` |
| VPS | `deploy/install-on-vps.sh` | Только docker compose + wait `/health` |
| VPS | `deploy/reload-proxy-on-vps.sh` | Обновить контейнер `proxy` (том кэша не трогать) |
| VPS | `deploy/backup-cache-on-vps.sh` | Бэкап `semantic_cache.json` |

---

## A. Первый деплой

### A1. На ПК

```powershell
cd C:\epm-games\MagicalBall
.\deploy\prepare-winscp.ps1
```

Результат: `deploy\winscp-upload\` — заливать **содержимое** этой папки.

### A2. WinSCP → VPS

```text
Локально:  deploy\winscp-upload\*
На VPS:    /opt/magicalball/
```

Должен появиться `/opt/magicalball/docker-compose.yml` (не вложенная папка `winscp-upload`).

**Не перезаписывать** рабочий `.env` на сервере при последующих заливках.

### A3. На VPS — одна команда (рекомендуется)

```bash
mkdir -p /opt/magicalball
cd /opt/magicalball
chmod +x deploy/*.sh
bash deploy/setup-vps.sh
```

`setup-vps.sh` делает сам:

1. Создаёт `.env` из `deploy/env.example`, если его нет
2. Проставляет `VPS_IP=147.45.173.26`, `API_PORT=18437`
3. **Копирует `GIGACHAT_CREDENTIALS` из `/opt/truetaro/.env`**, если в magicalball ключ пустой
4. Открывает `ufw allow 18437/tcp`, если ufw активен
5. Запускает `install-on-vps.sh` (build + health)

### A3-alt. Ручной блок (если `setup-vps.sh` ещё не залит)

Вставить **целиком** на VPS:

```bash
cd /opt/magicalball

cp -n deploy/env.example .env 2>/dev/null || true
grep -q '^VPS_IP=' .env || echo 'VPS_IP=147.45.173.26' >> .env
grep -q '^API_PORT=' .env || echo 'API_PORT=18437' >> .env
sed -i 's/^VPS_IP=.*/VPS_IP=147.45.173.26/' .env
sed -i 's/^API_PORT=.*/API_PORT=18437/' .env

if grep -q '^GIGACHAT_CREDENTIALS=.\+' /opt/truetaro/.env 2>/dev/null; then
  CRED=$(grep '^GIGACHAT_CREDENTIALS=' /opt/truetaro/.env | cut -d= -f2- | tr -d '\r')
  sed -i "s|^GIGACHAT_CREDENTIALS=.*|GIGACHAT_CREDENTIALS=${CRED}|" .env
  echo "OK: ключ взят из /opt/truetaro/.env"
else
  echo "Нет ключа в /opt/truetaro/.env"
  echo "Вставь вручную: nano /opt/magicalball/.env"
  echo "Строка: GIGACHAT_CREDENTIALS=твой_ключ"
  exit 1
fi

chmod +x deploy/*.sh
command -v ufw >/dev/null && ufw status 2>/dev/null | grep -q 'Status: active' && ufw allow 18437/tcp
bash deploy/install-on-vps.sh
curl -s http://127.0.0.1:18437/health
curl -s http://147.45.173.26:18437/health
```

Если TrueTaro на другом пути — ключ вписать вручную:

```bash
nano /opt/magicalball/.env
# GIGACHAT_CREDENTIALS=ваш_base64_ключ_без_кавычек
```

Ключ также есть на ПК: `proxy\.env` или личный кабинет GigaChat (Authorization key).

### A4. Ожидаемый результат

Docker:

```text
Container magicalball-proxy  Started
0.0.0.0:18437->8000/tcp
```

Health (локально и снаружи):

```json
{"status":"ok","service":"magicalball"}
```

Проверка порта:

```bash
ss -tlnp | grep ':18437'
docker compose ps
docker compose logs proxy --tail 50
```

### A5. Firewall

Открыть TCP **18437** в двух местах:

1. **На VPS** (если ufw):

```bash
sudo ufw allow 18437/tcp
sudo ufw status
```

2. **В панели хостера** (security group / firewall VPS) — иначе с телефона не достучаться, даже если `curl` с самого сервера ок.

### A6. Клиент Godot

[`config/api.json`](../config/api.json):

```json
{
  "use_proxy": true,
  "proxy_base_url": "http://127.0.0.1:18437",
  "android_base_url": "http://147.45.173.26:18437"
}
```

| Среда | Поле | URL |
|-------|------|-----|
| Редактор / Windows | `proxy_base_url` | `http://127.0.0.1:18437` |
| APK (Android) | `android_base_url` | `http://147.45.173.26:18437` |

После смены `api.json` — **пересобрать APK**.

### A7. Проверка с телефона

1. Установить свежий APK
2. Сделать одно гадание
3. Должен прийти ответ от LLM (не «туман»)
4. Если `/health` с ПК ок, а с телефона нет — смотреть firewall хостера

---

## B. Обновление API (код прокси)

```text
ПК:     .\deploy\prepare-winscp.ps1
WinSCP: залить proxy/ (+ docker-compose.yml, если менялся)
        НЕ трогать /opt/magicalball/.env
VPS:    cd /opt/magicalball && bash deploy/reload-proxy-on-vps.sh
        curl -s http://127.0.0.1:18437/health
```

Только UI Godot → новая APK, VPS не нужен.

**Запрещено:** `docker compose down -v`, `docker volume prune` — убьёт кэш.

---

## C. Бэкап кэша

```bash
cd /opt/magicalball
bash deploy/backup-cache-on-vps.sh
# → deploy/backups/cache-YYYYMMDD-HHMM.json
```

Потеря кэша не ломает прод — ответы снова пойдут в GigaChat.

---

## Переменные `.env`

Шаблон: [`deploy/env.example`](../deploy/env.example)

| Переменная | Default | Смысл |
|------------|---------|--------|
| `VPS_IP` | `147.45.173.26` | Публичный IP |
| `API_PORT` | `18437` | Порт на хосте (Godot + firewall) |
| `GIGACHAT_CREDENTIALS` | — | **Обязателен.** Authorization key Sber |
| `GIGACHAT_SCOPE` | `GIGACHAT_API_PERS` | |
| `GIGACHAT_MODELS` | ротация GigaChat-2… | |
| `SEMANTIC_THRESHOLD` | `0.82` | Порог семантического кэша |

`.env` **не коммитить**.

---

## Частые проблемы

| Симптом | Решение |
|---------|---------|
| `ERROR: GIGACHAT_CREDENTIALS is empty` | `cp deploy/env.example .env` создаёт пустой ключ. Вписать ключ или взять из `/opt/truetaro/.env` (блок A3-alt) |
| `ss -tlnp \| grep 18437` пусто | `install-on-vps.sh` не дошёл до конца — смотреть `docker compose logs proxy` |
| `/health` ok на VPS, с телефона нет | Firewall хостера: открыть TCP 18437 |
| `bind: address already in use` | Порт занят — другой `API_PORT` в `.env` и в `android_base_url` |
| `/health` ok, шар в тумане | Пустой или неверный `GIGACHAT_CREDENTIALS` |
| Android ходит в TrueTaro | В `api.json` ещё `:17878` — сменить на `:18437` |
| После апдейта пропал кэш | Случайно сделали `down -v` |

---

## Запрещено

- `docker compose down -v` / `docker volume prune`
- Публиковать контейнерный `:8000` в Godot
- Сажать MagicalBall на порт TrueTaro `17878`
- Ключ GigaChat в APK / git
- Заливать Godot-проект / APK на VPS

---

## Чеклист первого раза

- [ ] `prepare-winscp.ps1` → WinSCP в `/opt/magicalball/`
- [ ] `bash deploy/setup-vps.sh` (или блок A3-alt)
- [ ] `curl` `/health` локально и с `147.45.173.26`
- [ ] Firewall: ufw + панель хостера, TCP 18437
- [ ] `android_base_url` = `http://147.45.173.26:18437`
- [ ] Пересборка APK
- [ ] Одно гадание с телефона — ответ LLM, не туман

## Чеклист апдейта API

- [ ] `prepare-winscp.ps1`
- [ ] WinSCP: `proxy/` (`.env` не трогать)
- [ ] `reload-proxy-on-vps.sh`
- [ ] `curl /health`
- [ ] Тест гадания с Android
