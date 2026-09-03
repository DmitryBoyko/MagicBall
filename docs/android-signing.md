# Android: подпись APK

По шаблону `MagicCubes`: один ключ на **debug и release**.

## Keystore

| Параметр | Значение |
|----------|----------|
| Файл | `magicalball-release.keystore` (корень проекта) |
| Alias | `magicalball` |
| Store / key password | `123456` |
| Алгоритм | RSA 2048 |
| Срок | 10000 дней |
| DN | `CN=Magical Ball, OU=Games, O=EPM Games, L=Unknown, ST=Unknown, C=RU` |

Credentials Godot (локально, `.godot/export_credentials.cfg`, не в git):

```
keystore/debug="c:/epm-games/MagicalBall/magicalball-release.keystore"
keystore/debug_user="magicalball"
keystore/debug_password="123456"
keystore/release="c:/epm-games/MagicalBall/magicalball-release.keystore"
keystore/release_user="magicalball"
keystore/release_password="123456"
```

Быстрая установка:

```powershell
.\tools\setup_export_credentials.ps1
```

В редакторе: **Project → Export → Android → Keystore**: Debug и Release указывают на один файл. `package/signed=true`.

- Debug-сборка (`--export-debug`) подписывается debug-слотом.
- Release-сборка (`--export-release`) — release-слотом.

## Создать keystore заново

```powershell
& "C:\Program Files\Eclipse Adoptium\jdk-25.0.3.9-hotspot\bin\keytool.exe" -genkeypair -v `
  -keystore "c:\epm-games\MagicalBall\magicalball-release.keystore" `
  -alias magicalball `
  -keyalg RSA -keysize 2048 -validity 10000 `
  -storepass 123456 -keypass 123456 `
  -dname "CN=Magical Ball, OU=Games, O=EPM Games, L=Unknown, ST=Unknown, C=RU"
```

## Иконки

```powershell
python tools/generate_app_icon.py
```

Создаёт `icon.png` (512×512) и `assets/icons/*` из `original-icon.png` (1024×1024) в корне проекта.
