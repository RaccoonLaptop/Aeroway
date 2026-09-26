<p align="center">
  <img src="Assets/icon-readme.png" alt="Aeroway" width="128">
</p>

<h1 align="center">Aeroway</h1>

<p align="center"><strong>Программа, которая одной кнопкой открывает Discord, YouTube и другие сервисы на Windows.</strong></p>

<p align="center">
  <img src="https://img.shields.io/badge/Discord-5865F2?style=for-the-badge&logo=discord&logoColor=white" alt="Discord">
  <img src="https://img.shields.io/badge/YouTube-FF0000?style=for-the-badge&logo=youtube&logoColor=white" alt="YouTube">
  <img src="https://img.shields.io/badge/Telegram-2AABEE?style=for-the-badge&logo=telegram&logoColor=white" alt="Telegram">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-2.0.0-e0b240" alt="Версия 2.0.0">
  <a href="https://github.com/RaccoonLaptop/Aeroway/stargazers"><img src="https://img.shields.io/github/stars/RaccoonLaptop/Aeroway?style=flat-square&label=stars&color=e0b240" alt="Звёзды"></a>
  <a href="https://t.me/AerowayUI"><img src="https://img.shields.io/badge/Telegram-t.me%2FAerowayUI-2AABEE?style=flat-square&logo=telegram&logoColor=white" alt="Telegram"></a>
</p>

<p align="center">
  <a href="https://github.com/RaccoonLaptop/Aeroway/releases/latest"><img src="https://img.shields.io/badge/%D0%A1%D0%BA%D0%B0%D1%87%D0%B0%D1%82%D1%8C-Aeroway--Setup.exe-e0b240?style=for-the-badge&logo=windows&logoColor=050b1b" alt="Скачать"></a>
  <a href="https://raccoonlaptop.github.io/Aeroway/"><img src="https://img.shields.io/badge/%D0%94%D0%BE%D0%BA%D1%83%D0%BC%D0%B5%D0%BD%D1%82%D0%B0%D1%86%D0%B8%D1%8F-%D1%80%D1%83%D0%BA%D0%BE%D0%B2%D0%BE%D0%B4%D1%81%D1%82%D0%B2%D0%BE-0d2240?style=for-the-badge" alt="Документация"></a>
  <a href="https://raccoonlaptop.github.io/Aeroway/donate.html"><img src="https://img.shields.io/badge/%D0%94%D0%BE%D0%BD%D0%B0%D1%82-%D0%AEKassa-e0b240?style=for-the-badge" alt="Донат"></a>
</p>

Удобная оболочка для конфигов [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) и движка [zapret](https://github.com/bol-van/zapret) (`winws`). Не нужно разбираться в командной строке: выбрали стратегию и нажали **Запустить**.

Раньше программа называлась Zapret UI. Уже установленная версия обновляется сама.

Новости и вопросы — в [Telegram-канале Aeroway](https://t.me/AerowayUI).

<p align="center">
  <img src="docs/screenshot.png" alt="Главный экран Aeroway" width="720">
  <br>
  <em>Главный экран</em>
</p>

> Инструмент для исследовательских и образовательных целей. Используйте по законам своей страны.

---

## Документация

Короткая выжимка — ниже. Подробная установка, антивирус и брандмауэр — на [сайте руководства](https://raccoonlaptop.github.io/Aeroway/). Там же русский и English.

| | |
|---|---|
| [Скачать](https://github.com/RaccoonLaptop/Aeroway/releases/latest) | Установщик `Aeroway-Setup.exe`, .NET ставить не нужно |
| [Руководство](https://raccoonlaptop.github.io/Aeroway/) | Быстрый старт, исключения, разделы программы, трей |
| [Донат](https://raccoonlaptop.github.io/Aeroway/donate.html) | Сумму указываете сами, оплата через ЮKassa |
| [Telegram](https://t.me/AerowayUI) | Канал с новостями и вопросами |

---

## Содержание

- [Что это](#что-это)
- [Что умеет программа](#что-умеет-программа)
- [Быстрый старт](#быстрый-старт)
- [Экран за экраном](#экран-за-экраном)
- [Если не работает](#если-не-работает)
- [Файлы и удаление](#файлы-и-удаление)
- [Поддержать](#поддержать)
- [Благодарности](#благодарности)

---

## Что это

Aeroway запускает готовые стратегии Flowseal поверх обычного zapret (`winws`). Это не zapret2 и не `winws2`: конфиги от нового движка сюда не подходят.

Интерфейс на русском и английском. Окно не требует прав администратора само по себе. Запрос Windows появляется, когда обход загружает драйвер.

---

## Что умеет программа

- **Запуск одной кнопкой** на главном экране и из системного трея.
- **Готовые стратегии Flowseal** и свои `.bat`.
- **Редактор стратегий и списков**, Game Filter и IPSet.
- **Тест стратегий** — лучший пресет можно поставить на главную.
- **Диагностика** файлов обхода перед запуском.
- **Автообновление** программы и компонентов Flowseal. Установка — после подтверждения.
- **Автозапуск** в трее при входе в Windows.
- **Свои файлы сохраняются** при обновлении Flowseal. Есть экспорт и импорт.

---

## Быстрый старт

1. Скачайте и установите [Aeroway-Setup.exe](https://github.com/RaccoonLaptop/Aeroway/releases/latest).
2. При первом запуске дождитесь загрузки компонентов Flowseal.
3. Добавьте папку `%LOCALAPPDATA%\Aeroway` в исключения антивируса и разрешите `Aeroway.exe` и `winws.exe` в брандмауэре. По шагам — в [руководстве](https://raccoonlaptop.github.io/Aeroway/).
4. На главной выберите стратегию и нажмите **Запустить**. Если Windows спросит разрешение — согласитесь: без этого драйвер не загрузится.

Зелёная иконка в трее — обход работает. Синяя — остановлен. Правый клик: статус, запуск, остановка, открыть окно, выход.

---

## Экран за экраном

| Раздел | Что там |
|---|---|
| **Главная** | Выбор стратегии, кнопка запуска, автозапуск, статус |
| **Стратегии** | Редактор `.bat`, списки, свои конфиги |
| **Сервис** | Game Filter, IPSet, язык, обновления Flowseal и программы |
| **Диагностика** | Проверка `winws.exe` и WinDivert, журнал |
| **Тест стратегий** | Прогон пресетов и выбор лучшего на главную |

Снизу меню — кнопки Telegram, GitHub и Донат. Справа внизу — переключатель анимации фона.

---

## Если не работает

1. **Антивирус забрал `WinDivert` или `winws.exe`.** Восстановите файл из карантина и исключите папку `%LOCALAPPDATA%\Aeroway`. Срабатывание `Not-a-virus:RiskTool.Multi.WinDivert` относится к драйверу zapret.
2. **Брандмауэр молча блокирует запуск.** Разрешите `Aeroway.exe` и `zapret\bin\winws.exe` для частной и публичной сети.
3. **Сайты всё ещё не открываются.** Смените стратегию или обновите Flowseal в разделе «Сервис».
4. **Окно не открывается.** Поставьте последнюю версию со [страницы релизов](https://github.com/RaccoonLaptop/Aeroway/releases/latest).

---

## Файлы и удаление

После установки программа лежит в `%LOCALAPPDATA%\Aeroway`. Обычно это `C:\Users\<имя>\AppData\Local\Aeroway`. Движок — в `zapret\bin\winws.exe`.

Удаление — через «Приложения и возможности», имя **Aeroway**. Ярлык и иконка тоже от Aeroway.

Если при удалении папки Windows пишет, что занят `WinDivert64.sys`, драйвер ещё загружен. Остановите обход, затем в PowerShell от администратора:

```
sc.exe stop WinDivert
sc.exe delete WinDivert
```

В PowerShell нужна именно команда `sc.exe`. После этого файл отпускает папку.

---

## Поддержать

Если программа пригодилась:

- [поставьте звезду](https://github.com/RaccoonLaptop/Aeroway) на этой странице;
- [поддержите проект](https://raccoonlaptop.github.io/Aeroway/donate.html) — сумму указываете сами, оплата через ЮKassa;
- напишите в [Telegram](https://t.me/AerowayUI), какая стратегия заработала у вашего провайдера.

---

## Благодарности

- [bol-van/zapret](https://github.com/bol-van/zapret) — движок обхода DPI (`winws`).
- [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) — стратегии и списки.

## Лицензия

MIT, см. [LICENSE](LICENSE). Движок zapret распространяется по своей лицензии.
