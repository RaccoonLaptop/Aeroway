<p align="center">
  <img src="Assets/icon-readme.png" alt="Aeroway" width="128">
</p>

<h1 align="center">Aeroway</h1>

<p align="center"><strong>Программа, которая одной кнопкой открывает Discord, YouTube и другие сервисы на Windows.</strong></p>

<p align="center">
  <a href="https://github.com/RaccoonLaptop/Aeroway/releases/latest"><img alt="Версия" src="https://img.shields.io/github/v/release/RaccoonLaptop/Aeroway?style=for-the-badge&labelColor=12151c&color=e0b240&display_name=release&label=%D0%B2%D0%B5%D1%80%D1%81%D0%B8%D1%8F"></a>
  <a href="https://github.com/RaccoonLaptop/Aeroway/releases"><img alt="Скачиваний" src="https://img.shields.io/github/downloads/RaccoonLaptop/Aeroway/total?style=for-the-badge&labelColor=12151c&color=34D399&label=%D1%81%D0%BA%D0%B0%D1%87%D0%B8%D0%B2%D0%B0%D0%BD%D0%B8%D0%B9"></a>
  <a href="https://github.com/RaccoonLaptop/Aeroway/stargazers"><img alt="Звёзды" src="https://img.shields.io/github/stars/RaccoonLaptop/Aeroway?style=for-the-badge&labelColor=12151c&color=e0b240&label=%D0%B7%D0%B2%D1%91%D0%B7%D0%B4%D1%8B"></a>
  <a href="https://t.me/AerowayUI"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-AerowayUI-2AABEE?style=for-the-badge&labelColor=12151c&logo=telegram&logoColor=white"></a>
</p>

<p align="center">
  <a href="https://github.com/RaccoonLaptop/Aeroway/releases/latest"><img alt="Скачать последнюю версию" src="https://img.shields.io/badge/%D0%A1%D0%BA%D0%B0%D1%87%D0%B0%D1%82%D1%8C%20%D0%BF%D0%BE%D1%81%D0%BB%D0%B5%D0%B4%D0%BD%D1%8E%D1%8E%20%D0%B2%D0%B5%D1%80%D1%81%D0%B8%D1%8E-e0b240?style=for-the-badge&logo=github&logoColor=050b1b"></a>
  &nbsp;
  <a href="https://raccoonlaptop.github.io/Aeroway/"><img alt="Документация" src="https://img.shields.io/badge/%D0%94%D0%BE%D0%BA%D1%83%D0%BC%D0%B5%D0%BD%D1%82%D0%B0%D1%86%D0%B8%D1%8F-12151c?style=for-the-badge&logo=readthedocs&logoColor=e0b240"></a>
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
| [Руководство](https://raccoonlaptop.github.io/Aeroway/) | Быстрый старт, интерфейс, если не работает, файлы и удаление |
| [Донат](https://raccoonlaptop.github.io/Aeroway/donate.html) | Сумму указываете сами, оплата через ЮKassa |
| [Telegram](https://t.me/AerowayUI) | Канал с новостями и вопросами |

---

## Содержание

- [Что это](#что-это)
- [Что умеет программа](#что-умеет-программа)
- [Быстрый старт](#быстрый-старт)
- [Интерфейс](#интерфейс)
- [Поддержать](#поддержать)
- [Благодарности](#благодарности)

---

## Что это

Aeroway запускает готовые стратегии Flowseal поверх обычного zapret (`winws`). Это не zapret2 и не `winws2`: конфиги от нового движка сюда не подходят.

Интерфейс на русском и английском. Программа открывается от имени администратора: Windows спрашивает разрешение один раз при запуске, а не при каждом включении обхода.

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
4. При запуске программы согласитесь с запросом Windows. Затем на главной выберите стратегию и нажмите **Запустить**.

Зелёная иконка в трее — обход работает. Синяя — остановлен. Правый клик: статус, запуск, остановка, открыть окно, выход.

---

## Интерфейс

| Раздел | Что там |
|---|---|
| **Главная** | Выбор стратегии, кнопка запуска, автозапуск, статус |
| **Стратегии** | Редактор `.bat`, списки, свои конфиги |
| **Сервис** | Game Filter, IPSet, язык, обновления Flowseal и программы |
| **Диагностика** | Проверка `winws.exe` и WinDivert, журнал |
| **Тест стратегий** | Прогон пресетов и выбор лучшего на главную |

Снизу меню — кнопки Telegram, GitHub и Донат. Справа внизу — переключатель анимации фона.

Если обход не запускается или нужно удалить программу, это разобрано в [руководстве](https://raccoonlaptop.github.io/Aeroway/).

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
