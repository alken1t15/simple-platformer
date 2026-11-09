# Протокол чистого Linux-запуска и репетиции демонстрации Ball Adventure (issue #12)

## 1. Фиксация окружения

| Параметр | Значение | Как посмотреть | Пример |
| --- | --- | --- | --- |
| Дата прогона | |`date` | `Sun Nov  8 20:58:38 MSK 2026` |
| Дистрибутив и версия | | `cat /etc/os-release` | `PRETTY_NAME="Fedora Linux 45"` |
| Архитектура | | `uname -m` | `aarch64` |
| Версия ядра | | `uname -r` | `6.17.0-0.fc45.aarch64` |
| Графическая оболочка | | `echo $XDG_CURRENT_DESKTOP` | `KDE` |
| Графический сервер | | `echo $XDG_SESSION_TYPE` | `wayland` |
| GPU | | `glxinfo -B \| grep "Device"` | `Apple M2 (G14G B0)` |
| Версия .NET SDK | | `dotnet --version` | `10.0.100` |

## 2. Чистый клон с закреплённым сабмодулем

```sh
cd "$(mktemp -d)"
git clone --recurse-submodules https://github.com/alken1t15/simple-platformer.git
cd simple-platformer
git submodule update --init --recursive
```

Опционально, проверка результата:
- [ ] `git rev-parse HEAD` печатает полный SHA текущего коммита main
    - Пример: `3385f487b8f670a0024c30561a5f22f1ca619173`
- [ ] `git submodule status` печатает ` <SHA> external/engine <version>`
    - Пример: ` 04cd6fd441e7974060d5a47b69e49c3598bb047d external/engine (v1.0.0-9-g04cd6fd)`
- [ ] `git -C external/engine status --porcelain` пуст: движок не изменён локально


## 3. Сборка по README без личных артефактов

Опционально, убедиться в отсутствии личных артефактов:
- [ ] `find . -name bin -not -path "./external/*"` — пусто
- [ ] `find . -name obj -not -path "./external/*"` — пусто

Сборка, запуск тестов и других проверок:
- [ ] `dotnet --version` — не ниже `10.0.112`
- [ ] `dotnet restore Platformer.sln` — без ошибок
- [ ] `dotnet build Platformer.sln -c Release --no-restore` — без ошибок и предупреждений
- [ ] `dotnet test Platformer.sln -c Release --no-build` — все тесты пройдены (`N passed / 0 failed`)
- [ ] `python3 scripts/check_docs.py` — PASS
- [ ] `git diff --check` — чисто

## 4. Ресурсы из другого cwd

Из корня репо запомнить путь к игре и запустить её из другого рабочего каталога:
```sh
SIMPLE_PLATFORMER_ROOT="$PWD" # корень репо simple-platformer
cd "$(mktemp -d)"             # изменение cwd, чтобы он не совпадал с каталогом проекта
dotnet run --project "$SIMPLE_PLATFORMER_ROOT/src/Platformer.App" -c Release --visual
```

Smoke-проверка без игрового ввода:
```sh
dotnet run --project "$SIMPLE_PLATFORMER_ROOT/src/Platformer.App" -c Release --no-build -- --smoke
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
- Все ресурсы читаются относительно каталога с игрой, поэтому смена рабочей директории запуска не должна приводить к ошибкам.

## 5. Репетиция демонстрационного маршрута

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
        - [ ] Прохождение пятого уровня вызывает показ финального сообщения и возврат в Main menu
        - [ ] Уровни проходятся штатно: WASD, Space, Shift, сбор монет работают
        - [ ] Счетчик монет и подсчет звезд работают
        - [ ] В процессе игры слышна фоновая мелодия
        - [ ] Прыжок, сбор монет, завершение уровня сопровождаются звуковыми эффектами
3. Дополнительные пункты Main menu (Settings, Skins, Levels)
    - Шаги:
        - Убавить звук через Settings -> Master Volume
        - Поменять музыку через Settings -> Music
        - Поменять разрешение экрана через Settings -> Resolution -> Apply Resolution
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
