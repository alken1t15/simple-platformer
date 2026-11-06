# Ball Adventure

Проект 3D-платформера на C#/.NET 10 и [Simple 3D Engine](https://github.com/alken1t15/simple-3d-engine):
шарик проходит две локации с мостами, наклонами, монетами и вращающимися препятствиями.
Целевая версия включает WASD/прыжок/ускорение, камеру мышью, звук и меню уровней/громкости/скинов.
Цель — Linux, три недели с 02.11.2026, готовность не позднее 23.11.2026.

**Сейчас это частичная реализация:** есть технические preview и стенд `--play` с управлением
Training Track, мышью, камерой, физикой и сеансом. Полные меню/HUD, вторая локация и
игровой звук ещё не подключены. Это не готовая Ball Adventure.

В Core подготовлены неизменяемые 3D-данные первой локации
[Training Track](docs/training-track.md): `TrainingTrack.Create()`, OBB дорожек,
наклон, мост, прыжковый зазор и 10 монет. Данные подключены к 3D-preview;
управление доступно в `--play`, ручная приёмка полного прохождения ещё требуется.

В Core реализована [физика шара](docs/ball-physics.md): фиксированный шаг,
движение/прыжок/boost и непрерывные столкновения сферы со статическими OBB.
Есть CPU-проверки наклонов, тонких поверхностей и участков Training Track.
Физика подключена к оконному стенду `--play`; полное ручное прохождение ещё не подтверждено.

В App подключены [ввод, фиксированный шаг и камера](docs/input-camera.md) из #6:
публичный snapshot движка читается один раз в onUpdate, LevelFlow получает команды,
камера поворачивается мышью, фокус ставит паузу. В `--play` Enter/Space запускает или
продолжает Training Track; полноценное графическое меню остаётся задачей #19.

В App реализован [3D Presenter](docs/scene-presenter.md): шар, скины classic-blue/orange,
дорожки, монеты, рамка финиша и отображение поз опасностей. Отдельный preview показывает
их на сценарных snapshot; `--play` использует Presenter настоящего сеанса.

В #8 реализована [CPU-часть сеанса и переходов](docs/game-session.md): монеты/финиш
по Path, KillY, события, Restart, LevelCatalog и App/LevelFlow с сохранением настроек.
Опасности из #20 проверяются по полному Path и времени шага до монет/финиша;
контакт завершает попытку поражением. Snapshot передаёт позы балок в Presenter,
Restart возвращает начальную фазу. Оконный host подключён в `--play`;
настоящий маршрут двух локаций требует данных #21 и полноценных меню #19.

## Подготовка

Нужны Git и .NET 10 **SDK** по [global.json](global.json). SDK 8/9 и один Runtime не подходят.
Первый restore требует NuGet; графический запуск требует дисплей и OpenGL 3.3.

```sh
git clone --recurse-submodules https://github.com/alken1t15/simple-platformer.git
cd simple-platformer
```

Для существующего клона:

```sh
git submodule update --init --recursive
dotnet --version
```

Движок закреплён submodule `external/engine` на commit
`7d097269004ad59fc014cdeb707ccd9911c63841`: публичные клавиши/мышь/CursorMode из
[PR движка №52](https://github.com/alken1t15/simple-3d-engine/pull/52) и аудиосервис из
[PR №51](https://github.com/alken1t15/simple-3d-engine/pull/51), overlay из
[PR №53](https://github.com/alken1t15/simple-3d-engine/pull/53). Используется принятый squash SHA,
а не HEAD ветки автора. [Возможности и ограничения](docs/engine-integration.md).

App ссылается на `Engine3D.Audio`, поэтому обычная сборка проверяет доступность аудио API.
Устройство автоматически не создаётся; в preview звука пока нет. Для будущего воспроизведения
на Debian/Ubuntu/Mint нужна системная `libopenal1`; без устройства доступен диагностируемый
silent fallback. Silent/null-driver проверки не подтверждают слышимость.
OverlayFrame/SetOverlay доступны для меню и HUD задачи #19; игровые экраны ещё не реализованы.
Встроенный ASCII-атлас и его лицензия поставляются движком; меню проектируется на английском.
Реальный HiDPI с framebuffer≠client ещё не проверен; synthetic GPU scale=2 этого не подтверждает.
Native input/cursor подключён в #6; игровой audio adapter остаётся задачей #21.
Изменения движка вносятся отдельным PR; рабочая копия зависимости в игре не редактируется.
Проверка, сборка и тесты одной командой (PowerShell 7):

```powershell
pwsh -NoProfile -File scripts/Verify.ps1
```

Эквивалент вручную:

```sh
dotnet restore Platformer.sln
dotnet build Platformer.sln -c Release --no-restore
dotnet test Platformer.sln -c Release --no-build
python scripts/check_docs.py
```

## Запуск

```sh
dotnet run --project src/Platformer.App -c Release --no-build -- --help
dotnet run --project src/Platformer.App -c Release --no-build -- --play
dotnet run --project src/Platformer.App -c Release --no-build -- --play --smoke
dotnet run --project src/Platformer.App -c Release --no-build
dotnet run --project src/Platformer.App -c Release --no-build -- --smoke
dotnet run --project src/Platformer.App -c Release --no-build -- --presenter
dotnet run --project src/Platformer.App -c Release --no-build -- --presenter --skin orange --overview --smoke
```

Обычный запуск показывает неподвижный макет уровня сбоку; нажмите Escape в активном окне или закройте окно для выхода.
`--smoke` показывает три кадра и завершает процесс. Это проверка интеграции, не игровая приёмка и не FPS-тест.
Linux/WSL: [графический запуск](docs/development-process.md#linux-и-wsl).
`--presenter` включает отдельный 3D-preview; Space меняет скин, R сбрасывает сценарий,
`--still` останавливает сценарное движение для снимка. Это не игровой ввод из #6.

`--play` — отдельный стенд #6: Enter/Space — старт/продолжение/повтор/далее,
WASD — движение относительно камеры, Space — прыжок, любой Shift — boost,
R — перезапуск, мышь — камера (вверх — поднять взгляд, вниз — опустить).
Escape ставит паузу, затем возвращает на старт;
на стартовом экране Escape закрывает стенд. Alt-Tab ставит паузу; возврат требует
явного Enter. Удержанные при переходе клавиши требуют отпускания и нового нажатия.
Подсказки и состояния выводятся в терминал; это временный стенд без меню/HUD #19.
`--play --smoke` показывает три кадра Training Track без ввода, Step и захвата мыши.
[Проверки управления и оставшаяся ручная приёмка](docs/input-camera-testing.md).

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
