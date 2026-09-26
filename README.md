<p align="center">
  <img src="Assets/icon-readme.png" alt="Aeroway" width="128">
</p>

# Aeroway

Aeroway раньше назывался Zapret UI. Имя сменили, потому что проектов с названием Zapret UI слишком много. Уже установленная программа обновляется сама и переезжает в папку Aeroway.

Современный интерфейс для запуска конфигов [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) на Windows — Discord, YouTube и другие сервисы.

| | |
|---|---|
| **Скачать** | [Aeroway-Setup.exe](https://github.com/RaccoonLaptop/Aeroway/releases/latest) |
| **GitHub** | https://github.com/RaccoonLaptop/Aeroway |
| **Telegram** | https://t.me/AerowayUI |
| **Донат** | https://raccoonlaptop.github.io/Aeroway/donate.html |
| **Руководство** | https://raccoonlaptop.github.io/Aeroway/ |

## Быстрый старт

1. Скачайте и установите **Aeroway-Setup.exe**
2. При первом запуске дождитесь загрузки компонентов Flowseal
3. Добавьте папку программы в исключения антивируса и разрешите `Aeroway.exe` и `winws.exe` в брандмауэре — [инструкция ниже](#антивирус-и-брандмауэр)
4. Выберите стратегию на главной и нажмите **ЗАПУСТИТЬ**

## Антивирус и брандмауэр

Исключения в антивирус и правила брандмауэра добавляются вручную. Без них Windows Defender может удалить `WinDivert` и `winws.exe`, а брандмауэр — заблокировать запуск.

После установки программа лежит в папке:

`%LOCALAPPDATA%\Aeroway`

Обычно это `C:\Users\<имя>\AppData\Local\Aeroway`. Внутри неё находится `zapret\bin\winws.exe`.

### Windows Defender

1. Откройте **Безопасность Windows**.
2. **Защита от вирусов и угроз** → **Управление настройками**.
3. **Исключения** → **Добавление или удаление исключений** → **Добавить исключение** → **Папка**.
4. Укажите `%LOCALAPPDATA%\Aeroway`.

Если установлен другой антивирус, добавьте ту же папку в его исключения. Срабатывание с именем `WinDivert` или `Not-a-virus:RiskTool.Multi.WinDivert` относится к драйверу zapret: файл нужно восстановить из карантина и оставить папку в исключениях.

### Брандмауэр Windows

1. **Безопасность Windows** → **Брандмауэр и безопасность сети** → **Разрешить работу с приложением через брандмауэр**.
2. Нажмите **Изменить параметры** (нужны права администратора).
3. **Разрешить другое приложение** → **Обзор** и добавьте оба файла:
   - `%LOCALAPPDATA%\Aeroway\Aeroway.exe`
   - `%LOCALAPPDATA%\Aeroway\zapret\bin\winws.exe`
4. Для каждого файла отметьте частную и публичную сеть.

## Возможности

- Русский и English
- Запуск/остановка одной кнопкой и из системного трея
- Редактор стратегий, списки, обновления
- Game Filter, IPSet

## Требования

- Windows 10/11 (x64)
- Права администратора

## Благодарности

- [bol-van/zapret](https://github.com/bol-van/zapret)
- [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube)
