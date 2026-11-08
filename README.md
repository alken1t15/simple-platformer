# Ball Adventure

3D-платформер на C#/.NET 10 и [Simple 3D Engine](https://github.com/alken1t15/simple-3d-engine).
Шар перемещается в X/Z, Y вверх; мышь поворачивает камеру от третьего лица.
Цель проекта — Linux, две локации с наклонами/мостами, монетами, вращающимися
опасностями, звуком и меню уровней/громкости/скинов. План — три недели с 02.11.2026,
готовность не позднее 23.11.2026; это план, а не подтверждение полной готовности.

**Подключены управление, камера, меню, HUD и звук для пяти локаций.**
Обычный запуск открывает графическое Main Menu в том же окне, что и 3D-сцена.
Работают Play, выбор доступного уровня, настройки master volume/mute, скины,
пауза, повтор и результаты. Каталог содержит Training Track, Turning Maze,
Jump Circuit, Island Circuit и финальный Sky Storm. Next ведёт на следующую локацию, после последней — к общему результату.
В меню показаны девять скинов, включая баскетбольный и футбольный, с изображениями образцов.

В Settings доступны Resolution и Apply resolution: размер окна выбирается мышью
или Left/Right и применяется отдельной кнопкой; Current window показывает результат ОС.
Display: Fullscreen занимает весь монитор в текущем разрешении (1920×1080 на Full HD
при масштабе 100%); Display: Windowed возвращает обычное окно. Apply применяет оба выбора.
Растягивание окна за края и углы отключено — используйте Resolution → Apply resolution.
Обычное окно ограничено минимумом 800×450 и доступной областью экрана с учётом рамки;
на маленьком экране минимум уменьшается. [Подробности и ограничения Wayland](docs/menu.md#window-size).
Исходный автоматический renderer-маршрут двух локаций прошёл с 25/25 монетами.
CPU-маршрут всех пяти локаций проходит через настоящий host/ввод/физику/Presenter/аудиособытия
с 66/66 монетами в обычном режиме и с `--visual`. Полная ручная приёмка остаётся отдельно.
Пользователь подтвердил слышимость музыки, прыжка и монеты на Linux-ноутбуке 08.11.2026.

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

Движок закреплён на **`04cd6fd441e7974060d5a47b69e49c3598bb047d`**:
input/cursor [PR #52](https://github.com/alken1t15/simple-3d-engine/pull/52),
audio [PR #51](https://github.com/alken1t15/simple-3d-engine/pull/51) и
ASCII overlay [PR #53](https://github.com/alken1t15/simple-3d-engine/pull/53).
Текущий SHA — merge [engine PR #57](https://github.com/alken1t15/simple-3d-engine/pull/57):
fullscreen в текущем разрешении монитора. [Engine PR #56](https://github.com/alken1t15/simple-3d-engine/pull/56)
добавил Resizable=false для запрета растягивания за края. [Engine PR #55](https://github.com/alken1t15/simple-3d-engine/pull/55)
добавил ограничения окна и SetClientSize/ClientSizeLimits для выбора разрешения.
Ранее [game PR #39](https://github.com/alken1t15/simple-platformer/pull/39)
подключил ClientSize для Wayland из [engine PR #54](https://github.com/alken1t15/simple-3d-engine/pull/54).
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

После сборки обычный **Linux-запуск отдельно от терминала**:

```sh
bash scripts/Play.sh
```

Лаунчер требует `dotnet` (.NET 10 Runtime), `setsid` из util-linux и графический сеанс.
Игра получает отдельную session/process group: Ctrl+C и закрытие исходного терминала
не завершают её. stdout/stderr сохраняются в игнорируемом `artifacts/run/`, путь выводится
при запуске. Лаунчер не собирает проект; завершайте игру через Exit/закрытие окна.
Обычный выход не печатает счётчик кадров. Для диагностики используйте команды ниже:
`dotnet run` остаётся связанным с терминалом и управляется Ctrl+C;
`Rendered frames` выводится только в smoke/trace/preview-режимах.

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
dotnet run --project src/Platformer.App -c Release --no-build -- --visual --presenter --skin football --still
```

Обычный запуск и `--play` открывают Main Menu. Стрелки/Tab выбирают пункт,
Enter/Space или клик активирует его; мышь и клавиатура используют один выбор.
В игре WASD двигает шар относительно yaw камеры, Space прыгает (удержание повторяет
прыжок после приземления), любой Shift даёт boost,
R перезапускает уровень. Мышь вверх поднимает взгляд, вниз опускает.
Escape ставит паузу; из Paused Escape/Continue продолжает игру, из подменю — возвращает назад.
Main Menu закрывается кнопкой Exit либо системным закрытием окна.
Карточка в Levels сразу запускает уровень, карточка в Skins сразу применяет скин.
Возврат из текущей попытки в Main Menu требует подтверждения; Cancel выбран по умолчанию,
Escape отменяет. После подтверждения попытка завершается, фон остаётся последним кадром
уровня без восстановления собранных монет. Новая игра начинается только по Play/карточке уровня.

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

### Дополнительное оформление и уровни

`--visual` включает альтернативное оформление: шахматную трассу со светлыми краями,
яркий узорный шар с рисованным бликом, золотые монеты с фасками и рельефной звездой,
облака и парящие острова с пальмами, камнями и небольшими домиками.
Окружение дополняют маяк, маленькие островки между дорожками и покачивающиеся воздушные шары.
Декорации используют округлые формы, объёмные листья и сглаженные нормали.
На пересечениях дорога имеет общую сетку плиток; совпадающие плоскости обрезаются,
поэтому участки не мерцают друг сквозь друга.
Под шаром на опоре появляется контактное затемнение; дальние острова имеют более
приглушённую палитру. Это стилизация средствами App, без настоящих теней движка.
Меши и текстуры созданы процедурно в App. Геометрия Core, физика и правила прохождения
те же; обычный запуск сохраняет прежнее оформление. Флаг остаётся обязательным для этого стиля.
Прогресс, девять скинов и пять уровней работают в обоих режимах оформления.

```sh
dotnet run --project src/Platformer.App -c Release -- --visual
dotnet run --project src/Platformer.App -c Release --no-build -- --visual --presenter --overview --still
dotnet run --project src/Platformer.App -c Release --no-build -- --visual --presenter --still
```

Первый режим открывает настоящее меню/игру, два последних позволяют спокойно осмотреть
оформление без захвата мыши; Escape закрывает preview. `--visual --smoke` проверяет
три кадра меню. Эксперимент использует базовый свет, без новых эффектов в движке.

Jump Circuit добавляет три полосатые смертельные стены: их нужно перепрыгнуть,
любой контакт означает поражение. Island Circuit — изогнутые мосты, подъём, вращающаяся
балка и финальный прыжок. Исходные Training Track и Turning Maze сохранены.
Первые четыре уровня открыты в Levels; финал требует 12 звёзд. [состав, маршрут и проверки](docs/extra-levels.md).

Скины: `classic-blue`, `orange`, `emerald`, `violet`, `basketball`, `football`, `neon`, `aurora`, `champion`.
В preview Space циклически переключает все девять. Активация открытой карточки сразу применяет скин; скин сохраняется после Restart и перехода, радиус/физика не меняются.

За финиш даётся 1 звезда, за половину монет — 2, за все — 3. Лучшие результаты
сохраняются между запусками. Наградные скины открываются за 6/9/15 звёзд.
Sky Storm добавляет петлю с путепроводом и рассыпающиеся платформы; открывается
за 12/12 звёзд первых четырёх уровней. [Правила и сохранение](docs/progression.md).

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
