# Протокол чистого Linux-запуска и репетиции демонстрации Ball Adventure (issue #12)

Документ описывает воспроизводимую установку на Linux: чистый клон с закреплённым
сабмодулем, зависимости системы, сборка, запуск из другого каталога, диагностика
отсутствующих ресурсов и маршрут репетиции показа. Он не заменяет ручную приёмку
содержания игры из [#11](https://github.com/alken1t15/simple-platformer/issues/11).

## 1. Фиксация окружения

Заполняется на машине прогона. Ниже — значения подтверждённого прогона 09.11.2026.

| Параметр | Значение прогона | Как посмотреть |
| --- | --- | --- |
| Дата прогона | `Mon Nov  9 13:20 MSK 2026` | `date` |
| Дистрибутив | `Fedora Linux Asahi Remix 45 (KDE Plasma Desktop Edition Prerelease)` | `cat /etc/os-release` |
| Архитектура | `aarch64` | `uname -m` |
| Версия ядра | `7.2.12-402.asahi.fc45.aarch64+16k` | `uname -r` |
| Графическая оболочка | `KDE` | `echo $XDG_CURRENT_DESKTOP` |
| Графический сервер | `wayland` | `echo $XDG_SESSION_TYPE` |
| GPU и драйвер | `Apple M2 (G14G B0)`, `Mesa 26.2.6`, `OpenGL 4.6 Compatibility` | `glxinfo -B \| grep -iE "Device\|OpenGL"` |
| Версия .NET SDK | `10.0.113` | `dotnet --version` |

## 2. Системные зависимости

- Нужны графический Linux-сеанс, .NET 10 SDK по `global.json` и OpenGL 3.3.
- На **x86_64** этого достаточно: нативная GLFW приходит в output из NuGet, поэтому обычный запуск и CI работают без системных пакетов.
- На **arm64** может понадобиться системный GLFW 3: `sudo dnf install glfw-devel`.
- Для работы звука нужен `libopenal.so.1`: без него игра печатает `Audio: silent mode. ...` и работает без звука.


## 3. Чистый клон с закреплённым сабмодулем

```sh
cd "$(mktemp -d)"
git clone --recurse-submodules https://github.com/alken1t15/simple-platformer.git
cd simple-platformer
git submodule update --init --recursive
```

Проверка результата:

- [ ] `git rev-parse HEAD` печатает SHA клонированного `main`
    - Пример: `1edd7d6e646360eddc16daf26d99394921ba79e4`
- [ ] `git submodule status` печатает ` <SHA> external/engine <version>`
    - Пример: ` 013e3e29251ae020c9ced5fdeb46ef3d10a7b403 external/engine (v1.0.0-10-g013e3e2)`
- [ ] `git -C external/engine status --porcelain` пуст: движок не изменён локально

## 4. Сборка по README без личных артефактов

Убедиться в отсутствии личных build outputs:
- [ ] `find . -name bin -not -path "./external/*"` — пусто
- [ ] `find . -name obj -not -path "./external/*"` — пусто

Сборка и проверки:
- [ ] `dotnet --version` — не ниже `10.0.112`
- [ ] `dotnet restore Platformer.sln` — без ошибок
- [ ] `dotnet build Platformer.sln -c Release --no-restore` — 0 предупреждений, 0 ошибок
- [ ] `dotnet test Platformer.sln -c Release --no-build` — `Failed: 0`
- [ ] `python3 scripts/check_docs.py` — PASS
- [ ] `git diff --check` — чисто

## 5. Ресурсы, запуск из другого cwd и диагностика

- Ресурсы читаются от каталога сборки (`AppContext.BaseDirectory`), а не от текущего каталога, поэтому запуск из чужого cwd не должен приводить к ошибкам.
- Если файл ресурса отсутствует, игра печатает `Platformer: Could not find file '...'` и завершается с кодом 1, поэтому пропажа ресурсов проверяется обычным запуском.

Запомнить путь к игре и запустить из другого каталога:

```sh
SIMPLE_PLATFORMER_ROOT="$PWD" # корень репо simple-platformer
cd "$(mktemp -d)"             # cwd больше не совпадает с проектом
dotnet run --project "$SIMPLE_PLATFORMER_ROOT/src/Platformer.App" -c Release --no-build -- --visual
```

Опционально, проверить вручную, что все ресурсы имеются (из корня репо):
```sh
find src/Platformer.App/bin/Release/net10.0/Assets -type f | sort
```

Ожидаемый вывод команд (актуальный каталог main на `1edd7d6e646360eddc16daf26d99394921ba79e4`):
```text
src/Platformer.App/bin/Release/net10.0/Assets/Audio/CC0-1.0.txt
src/Platformer.App/bin/Release/net10.0/Assets/Audio/coin.wav
src/Platformer.App/bin/Release/net10.0/Assets/Audio/exploration-theme-NOTICE.txt
src/Platformer.App/bin/Release/net10.0/Assets/Audio/exploration-theme.wav
src/Platformer.App/bin/Release/net10.0/Assets/Audio/fail.wav
src/Platformer.App/bin/Release/net10.0/Assets/Audio/finish.wav
src/Platformer.App/bin/Release/net10.0/Assets/Audio/jump.wav
src/Platformer.App/bin/Release/net10.0/Assets/Audio/LICENSE.txt
src/Platformer.App/bin/Release/net10.0/Assets/Audio/music.wav
src/Platformer.App/bin/Release/net10.0/Assets/Audio/README.md
src/Platformer.App/bin/Release/net10.0/Assets/Branding/ball-adventure-window.png
src/Platformer.App/bin/Release/net10.0/Assets/FONT-LICENSE.txt
```

- Звуки — шесть WAV: пять собственных (монета, поражение, финиш, прыжок, основной фон) и второй музыкальный трек `exploration-theme.wav`.
- `Assets/Branding/ball-adventure-window.png` — иконка окна игры.
- Текстовые файлы — лицензии и описание источников.
- Своих файлов текстур у игры нет: геометрия и текстуры скинов генерируются процедурно.  

## 6. Репетиция демонстрационного маршрута

Этапы и проверки демонстрации:
1. Главное меню
    - Шаги:
        - Запустить игру
    - Проверки:
        - [ ] Видны и доступны для выбора Play / Levels / Settings / Skins / Exit
        - [ ] Курсор отображается и его позиция не захвачена окном
2. Прохождение игры
    - Шаги:
        - Нажать на Play
        - Пройти пять уровней
    - Проверки:
        - [ ] Выбор Play вызывает переход на первый уровень
        - [ ] Прохождение очередного уровня и нажатие Next вызывает переход на следующий уровень
        - [ ] Уровни проходятся штатно: WASD, Space, Shift, сбор монет работают
        - [ ] Счетчик монет и подсчет звезд работают
        - [ ] В процессе игры слышна фоновая мелодия
        - [ ] Прыжок, сбор монет, завершение уровня сопровождаются звуковыми эффектами
3. Дополнительные пункты Main menu (Settings, Skins, Levels)
    - Шаги:
        - Убавить звук через Settings -> Master volume
        - Поменять музыку через Settings -> Music
        - Поменять разрешение экрана через Settings -> Resolution -> Apply resolution
        - Перейти в Skins и поменять скин
        - Перейти сразу на второй уровень через Levels
    - Проверки:
        - [ ] Второй уровень загрузился без необходимости проходить первый
        - [ ] Звук фоновой мелодии и звуковых эффектов убавился
        - [ ] Мелодия на фоне изменилась
        - [ ] Разрешение экрана изменилось
        - [ ] Скин шара изменился
4. Возможность поражения и рестарта
    - Шаги:
        - Перейти на второй уровень (уже выполнено после третьего этапа)
    - Проверки:
        - [ ] После падения в пропасть появляется экран поражения
        - [ ] После прикосновения к балке появляется экран поражения
        - [ ] Нажатие R вызывает рестарт уровня
5. Выход из игры
    - Шаги:
        - Нажать Exit в Main menu
    - Проверки:
        - Окно с игрой закрылось, курсор освобожден
        - Процесс завершён (`pgrep -f Platformer.App` пусто), звук не висит, второго окна нет
        - Код возврата 0 (`echo $?`)
6. Сохранение состояния при перезапуске
    - Шаги:
        - Повторно запустить игру
    - Проверки:
        - [ ] Выбранный скин шара сохранился (см. в Skins)
        - [ ] Настройки звука сохранились (см. в Settings)
        - [ ] Разрешение экрана сохранилось (см. в Settings)
        - [ ] Количество заработанных звезд сохранилось (см. в Levels)
