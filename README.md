# Ball Adventure

3D-платформер на C#/.NET 10 и [Simple 3D Engine](https://github.com/alken1t15/simple-3d-engine).
Шар перемещается в X/Z, Y вверх; мышь поворачивает камеру от третьего лица.
Цель проекта — Linux, две локации с наклонами/мостами, монетами, вращающимися
опасностями, звуком и меню уровней/громкости/скинов. План — три недели с 02.11.2026,
готовность не позднее 23.11.2026; это план, а не подтверждение полной готовности.

**Подключены управление, камера, меню, HUD и звук для двух локаций.**
Обычный запуск открывает графическое Main Menu в том же окне, что и 3D-сцена.
Работают Play, выбор доступного уровня, настройки master volume/mute, скины,
пауза, повтор и результаты. Каталог содержит Training Track и Turning Maze;
Next ведёт на следующую локацию, после второй — к общему результату.
Автоматический маршрут через настоящий renderer проходит обе локации с 25/25 монетами.
Ручная приёмка на целевом компьютере и слышимость ещё требуют проверки.

В Core реализованы [Training Track](docs/training-track.md),
[физика шара](docs/ball-physics.md), [сеанс/монеты/опасности](docs/game-session.md).
[Presenter](docs/scene-presenter.md) показывает настоящий snapshot сеанса;
[ввод/камера](docs/input-camera.md) и [меню/HUD](docs/menu.md) связаны через один LevelFlow.
Технические preview сохранены отдельными режимами.

## Подготовка и проверки

Нужны Git и .NET 10 **SDK** по [global.json](global.json). SDK 8/9 и один Runtime не подходят.
Графический запуск требует дисплей и OpenGL 3.3. Для первого restore нужен NuGet.

```sh
git clone --recurse-submodules https://github.com/alken1t15/simple-platformer.git
cd simple-platformer
git submodule update --init --recursive
dotnet --version
dotnet restore Platformer.sln
dotnet build Platformer.sln -c Release --no-restore
dotnet test Platformer.sln -c Release --no-build
python scripts/check_docs.py
git diff --check
```

PowerShell 7: `pwsh -NoProfile -File scripts/Verify.ps1` выполняет проверки с остановкой при ошибке.

Движок закреплён на **`936b71a7393ef52b7d0d3b277af6b4c287be92c6`**:
input/cursor [PR #52](https://github.com/alken1t15/simple-3d-engine/pull/52),
audio [PR #51](https://github.com/alken1t15/simple-3d-engine/pull/51) и
ASCII overlay [PR #53](https://github.com/alken1t15/simple-3d-engine/pull/53).
Этот SHA принят в игре через [PR #39](https://github.com/alken1t15/simple-platformer/pull/39):
[engine PR #54](https://github.com/alken1t15/simple-3d-engine/pull/54) добавил ClientSize для Wayland.
Исходники движка и gitlink в интеграции #6/#19 не редактируются.
Атлас и FONT-LICENSE.txt поставляет движок; английский UI соответствует его ASCII-шрифту.
[Сторонние компоненты](THIRD_PARTY.md), [возможности/ограничения движка](docs/engine-integration.md).

App создаёт один AudioDevice при игровом запуске на потоке окна; настройки немедленно
меняют его MasterVolume, mute сохраняет предпочитаемую громкость. На Debian/Ubuntu/Mint
для native audio нужна `libopenal1`; без устройства возможен диагностируемый silent fallback.
AudioCoordinator подключает jump/coin/fail/finish и один музыкальный loop;
пауза приостанавливает voices, Restart/Next останавливают прежние эффекты.
Собственные PCM16 WAV поставляются относительно пути приложения; [источники и lifecycle](docs/game-audio.md).
OpenAL null/silent-проверка не подтверждает слышимость. Smoke не создаёт AudioDevice.

## Запуск

**Windows/WSL:** [пошаговая инструкция](docs/windows-wsl.md) — сборка в WSL,
прямой Windows-запуск и локальная OpenAL Soft для звука. Если через WSLg камера
вращается слишком сильно, сравните с native Windows-запуском той же сборки.

```sh
dotnet run --project src/Platformer.App -c Release --no-build
dotnet run --project src/Platformer.App -c Release --no-build -- --play
dotnet run --project src/Platformer.App -c Release --no-build -- --smoke
dotnet run --project src/Platformer.App -c Release --no-build -- --help
dotnet run --project src/Platformer.App -c Release --no-build -- --legacy --smoke
dotnet run --project src/Platformer.App -c Release --no-build -- --presenter
dotnet run --project src/Platformer.App -c Release --no-build -- --presenter --skin orange --overview --smoke
```

Обычный запуск и `--play` открывают Main Menu. Стрелки/Tab выбирают пункт,
Enter/Space или клик активирует его; мышь и клавиатура используют один выбор.
В игре WASD двигает шар относительно yaw камеры, Space прыгает, любой Shift даёт boost,
R перезапускает уровень. Мышь вверх поднимает взгляд, вниз опускает.
Escape ставит паузу; из Paused Escape/Continue продолжает игру, из подменю — возвращает назад.
Main Menu закрывается кнопкой Exit либо системным закрытием окна.

В меню курсор свободен; Play/Continue захватывает его. Потеря фокуса ставит паузу,
возврат фокуса не продолжает игру. Удержанные при переходе кнопки требуют release/new press.
HUD показывает уровень, Coins collected/total и boost ready/active/cooldown.
Settings, открытые из Paused, сохраняют паузу и возвращаются в неё.

Любой `--smoke` ограничен тремя показанными кадрами: без игрового ввода, ticks и capture.
По умолчанию он проверяет меню; `--legacy` — старую сцену, `--presenter` — сценарный 3D-preview.
В Presenter Space меняет скин, R сбрасывает сценарий, `--still` останавливает его;
это отдельный preview без игрового управления.

`--trace-input` в игровом режиме выводит JSON снимков/кадров/UI для native-драйвера.
[Автоматические проверки и ручная Linux-приёмка](docs/input-camera-testing.md).
Smoke/CPU/X11 не заменяют физическую мышь, настоящий Alt-Tab, HiDPI, FPS или полное прохождение.

## Документы

- [С чего начать свою задачу и куда отправлять PR](docs/task-guide.md)
- [Инструкции агентам](AGENTS.md)
- [Участие и команды](CONTRIBUTING.md)
- [Архитектура и границы](docs/architecture.md)
- [План разработки и зависимости](project_plan.md)
- [Даты всех задач](docs/schedule.md)
- [Сценарии приёмки](docs/acceptance.md)
- [Команда и роли](docs/team.md)
- [Доска и распределённые issues](docs/github-project.md)
- [Проект игрового API](docs/game-api.md)
- [Пробелы и расширения движка](docs/engine-integration.md)
- [Требования и открытые решения](docs/requirements.md)
- [Содержание уровней, меню, моделей и звуков](docs/content-design.md)
- [Объём подготовки и следующие решения](docs/project-scope.md)
- [Разработка, ревью и защита main](docs/development-process.md)
- [Сторонние компоненты](THIRD_PARTY.md), [лицензия](LICENSE)
