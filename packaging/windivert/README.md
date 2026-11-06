# Наборы WinDivert для Aeroway

Aeroway держит **два** комплекта файлов и подставляет нужный в `zapret/bin` тумблером «Скрыть WinDivert от античита». По умолчанию активен обычный комплект. Ручная правка имени службы в hex (в том числе на `aaaaaaaaa`) не используется.

| Каталог | Что внутри | Куда попадает при выборе |
|---|---|---|
| `stock/` | Обычные `WinDivert.dll` и `WinDivert64.sys` из `packaging/zapret/bin` | `zapret/bin`, служба `WinDivert` |
| `hide/` | Пропатченные `WinDivert.dll` и `Monkey64.sys` из [bol-van/zapret-win-bundle `windivert-hide`](https://github.com/bol-van/zapret-win-bundle/tree/master/windivert-hide) | `zapret/bin`, служба `Monkey` |

Рядом с `winws.exe` всегда лежит один активный комплект. Второй остаётся в этом каталоге и не затирается переключением.

## Откуда взяты файлы

- Скрытие: коммит [`bb6a20af35ed74778eb669f5413990eb8209c711`](https://github.com/bol-van/zapret-win-bundle/commit/bb6a20af35ed74778eb669f5413990eb8209c711) ветки `master` (2026-11-05). Текст upstream — в [`upstream/README.txt`](upstream/README.txt), удаление службы — в [`upstream/monkey_delete.cmd`](upstream/monkey_delete.cmd) (`sc stop monkey` / `sc delete monkey`).
- Рекомендация Flowseal для античита: раздел «Античит ругается на WinDivert» в [README zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube/blob/main/README.md), он же указывает на пакет bol-van.
- Обычный комплект — те же `WinDivert.dll` и `WinDivert64.sys`, что уже лежат в `packaging/zapret/bin` этого репозитория (Flowseal / bol-van).

`Monkey64.sys` в пакете bol-van — побайтовая копия `WinDivert64.sys`. Меняются имя файла и сборка `WinDivert.dll`: она открывает `\Monkey64.sys` и регистрирует службу `Monkey`. Имя устройства `\\.\WinDivert` остаётся прежним — для этого драйвер пришлось бы пересобрать и подписать заново.

### SHA-256

| Файл | SHA-256 |
|---|---|
| `stock/WinDivert.dll` | `c1e060ee19444a259b2162f8af0f3fe8c4428a1c6f694dce20de194ac8d7d9a2` |
| `stock/WinDivert64.sys` | `8da085332782708d8767bcace5327a6ec7283c17cfb85e40b03cd2323a90ddc2` |
| `hide/WinDivert.dll` | `944368b7a6eb0a97701e152a9171267db66377cc31bde415ab5da22c7a09cc43` |
| `hide/Monkey64.sys` | `8da085332782708d8767bcace5327a6ec7283c17cfb85e40b03cd2323a90ddc2` |

## Лицензии

WinDivert (basil00) распространяется на выбор по **LGPLv3** или **GPLv2**: <https://github.com/basil00/WinDivert>. Исходники WinDivert в этот репозиторий не входят.

Пакет zapret-win-bundle — MIT, copyright bol-van. Заметка о лицензиях движка, который уже лежит в Aeroway, есть в `packaging/zapret/LICENSE.txt`.

Скрытый `WinDivert.dll` — не исходная сборка basil00 и не ручная hex-правка Aeroway. Это пересобранная DLL из `windivert-hide` с небольшим патчем bol-van: загрузка `Monkey64.sys` и служба `Monkey`.

## Ограничение

Скрытие помогает, только если проверка ищет службу `WinDivert` или файл `WinDivert64.sys`. Проверка имени устройства по-прежнему видит WinDivert. Некоторые античиты всё равно могут выдать бан.
