# ⛰️ PEAK-Chaos-Mods

Моды и инструменты для **PEAK** (кооперативное восхождение, Unity) для игры с друзьями. Все моды — плагины **BepInEx 5** на C#, собираются без установленной игры (типы PEAK берутся рефлексией).

> Неофициальные моды, к разработчикам PEAK отношения не имеют. Только для приватных лобби и **с согласия всех игроков**. Моды писались без запуска игры — проверены компиляцией и по коду, в игре могут быть ошибки. Об ошибках пиши в Issues.

## Что внутри

| Мод | Версия | Для чего | Папка | Скачать |
|---|---|---|---|---|
| **PeakFun** | 1.32.0 | Главный мод: способности героев, баскетбол, мини-игры, пранки, режим **BACKROOMS: PEAK** (17 уровней, монстры, ловушки), темы, бот-спутник | [`mods/PeakFun`](mods/PeakFun) | [`downloads/PeakFun-1.32.0.zip`](downloads/PeakFun-1.32.0.zip) |
| **PeakMods** | 1.0.0 | Прохождение с модификаторами (правила легче/сложнее/веселее). Без читов | [`mods/PeakMods`](mods/PeakMods) | [`downloads/PeakMods-1.0.0.zip`](downloads/PeakMods-1.0.0.zip) |
| **PeakJujutsu** | 1.0.1 | «Магическая битва» для одиночной игры: 6 персонажей, техники, расширения территорий, охота на проклятия с боссами | [`mods/PeakJujutsu`](mods/PeakJujutsu) | [`downloads/PeakJujutsu-1.0.1.zip`](downloads/PeakJujutsu-1.0.1.zip) |
| **Хаос-менеджер** | — | Веб-приложение: рулетка челленджей, роли, таймер, счётчик позора, бинго | [`web/ChaosManager`](web/ChaosManager) | открыть `index.html` в браузере |

## Установка

1. Скачай нужный `.zip` из папки `downloads/` и **распакуй** его.
2. Следуй `INSTALL_RU.txt` внутри (обычно: запусти `INSTALL.bat` или скопируй содержимое папки `1_COPY_INTO_GAME_FOLDER` в папку игры).
3. Моды можно ставить вместе, но у **всех друзей** должен стоять один и тот же набор (PeakFun синхронизируется по сети).

Откат: `UNINSTALL.bat` (или удалить файлы мода из `BepInEx/plugins`).

## Структура репозитория

```
PEAK-Chaos-Mods/
├── README.md
├── LICENSE
├── downloads/            готовые сборки (.zip) со всем нужным для установки
├── mods/
│   ├── PeakFun/          исходники (src/) + описание
│   ├── PeakMods/
│   └── PeakJujutsu/
└── web/
    └── ChaosManager/     веб-приложение (index.html)
```

## Сборка из исходников

В каждой папке мода: положи `BepInEx.dll` и `0Harmony.dll` (из BepInEx 5) в `libs/`, затем `cd src && dotnet build -c Release`. Нужен .NET SDK; DLL игры не требуются.

В архивы `downloads/` входит BepInEx (LGPL-2.1, [github.com/BepInEx/BepInEx](https://github.com/BepInEx/BepInEx)).

## Лицензия

MIT — см. [LICENSE](LICENSE).
