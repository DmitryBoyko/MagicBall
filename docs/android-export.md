# Android: экспорт APK

## Быстрый старт

```powershell
cd C:\epm-games\MagicalBall

# 1) Шаблон Android (один раз в Godot)
#    Project -> Install Android Build Template...

# 2) Подготовка экспорта
.\tools\setup_android_export.ps1
.\tools\setup_export_credentials.ps1

# 3) Godot
#    Editor Settings -> Export -> Android:
#      - Java SDK Path = JDK 17 (скрипт покажет путь)
#      - Android SDK Path = %LOCALAPPDATA%\Android\Sdk
#    Project -> Export -> Android -> Export Project
```

APK: `../Builds/MagicalBall.apk`  
API для Android: `http://147.45.173.26:18437` (см. `config/api.json`).

---

## Что чинит `setup_android_export.ps1`

| Проблема | Решение |
|----------|---------|
| `no solution file was found` | `MagicBall.sln` в корне (уже есть) |
| `MSB4126 ... Release\|Any CPU` | В `MagicBall.sln` должен быть `Release` → `ExportRelease` (алиас) |
| Нет Yandex AAR | Скачивает `addons/GodotAndroidYandexAds/bin/*.aar` |
| `SDK location not found` | Создаёт `android/build/local.properties` |
| `compileSdk = 36` warning | `android.suppressUnsupportedCompileSdk=36` |
| Gradle lint падает на Java 25 | `org.gradle.java.home` → JDK 17 |
| HTTP на VPS не работает | `usesCleartextTraffic` в export plugin |

Подпись keystore: [`android-signing.md`](android-signing.md).

---

## Обязательные настройки Godot

**Editor Settings → Export → Android:**

| Поле | Значение |
|------|----------|
| Java SDK Path | `C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot` |
| Android SDK Path | `C:\Users\<you>\AppData\Local\Android\Sdk` |

JDK **17** обязателен. JDK 25 ломает Android Gradle (ошибка lint `25.0.3`).

Если JDK 17 нет:

```powershell
winget install EclipseAdoptium.Temurin.17.JDK
```

**Project → Export → Android:**

- Use Gradle Build: включено
- Min SDK / Target SDK: по умолчанию (24 / 36)
- Keystore: см. `android-signing.md`

---

## Файлы проекта

| Файл | Назначение |
|------|------------|
| `MagicBall.sln` | Решение .NET для экспорта |
| `MagicBall.csproj` | C# проект |
| `export_presets.cfg` | Пресет Android |
| `addons/GodotAndroidYandexAds/bin/*/*.aar` | Нативный плагин рекламы |
| `android/build/` | Шаблон Gradle (из Godot, в `.gitignore`) |

После **Install Android Build Template** снова запусти `.\tools\setup_android_export.ps1` — он допишет `gradle.properties` и `local.properties`.

---

## Частые ошибки

### `Failed to build project` (.NET)

```powershell
dotnet build MagicBall.sln -c ExportRelease
```

Должно собраться без ошибок.

### `GIGACHAT` / proxy — не про экспорт

Сервер: [`vps-deploy.md`](vps-deploy.md).

### Gradle: `compileSdk = 36`

Предупреждение. Скрипт добавляет suppress. Если вручную — в `android/build/gradle.properties`:

```properties
android.suppressUnsupportedCompileSdk=36
```

### Gradle: lint `25.0.3`

Java 25 не поддерживается AGP 8.6.1. Поставь JDK 17 и укажи в Godot + `gradle.properties`:

```properties
org.gradle.java.home=C:/Program Files/Eclipse Adoptium/jdk-17.0.20.101-hotspot
```

### С телефона API не отвечает

Firewall VPS + `android_base_url` в `config/api.json`. Cleartext HTTP разрешён через export plugin.

---

## Проверка Gradle вручную

```powershell
cd android\build
.\gradlew.bat assembleStandardRelease
```

`BUILD SUCCESSFUL` — Gradle ок, можно экспортировать из Godot.
