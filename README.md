# Ball Adventure

Проект 3D-платформера на C#/.NET 10 и [Simple 3D Engine](https://github.com/alken1t15/simple-3d-engine):
шарик проходит две локации с мостами, наклонами, монетами и вращающимися препятствиями.
Целевая версия включает WASD/прыжок/ускорение, камеру мышью, звук и меню уровней/громкости/скинов.
Цель — Linux, три недели с 02.11.2026, готовность не позднее 23.11.2026.

**Сейчас это каркас:** есть старая техническая сцена, отдельный 3D-preview, CPU-физика, тесты и CI.
Полный игровой цикл Ball Adventure, управление, две локации, мышь, меню и звук ещё не реализованы.
Ниже команды preview, а не обещание готовой игры.

В Core подготовлены неизменяемые 3D-данные первой локации
[Training Track](docs/training-track.md): `TrainingTrack.Create()`, OBB дорожек,
наклон, мост, прыжковый зазор и 10 монет. Данные подключены к 3D-preview;
интеграция игрового управления и полное прохождение требуют следующих задач.

В Core реализована [физика шара](docs/ball-physics.md): фиксированный шаг,
движение/прыжок/boost и непрерывные столкновения сферы со статическими OBB.
Есть CPU-проверки наклонов, тонких поверхностей и участков Training Track.
Физика пока не подключена к окну; игровое управление и полное прохождение ещё требуют интеграции.

В App реализован [3D Presenter](docs/scene-presenter.md): шар, скины classic-blue/orange,
дорожки, монеты, рамка финиша и отображение поз опасностей. Отдельный preview показывает
их на сценарных snapshot; управление/камера/сеанс ещё требуют интеграции.

В #8 реализована [CPU-часть сеанса и переходов](docs/game-session.md): монеты/финиш
по Path, KillY, события, Restart, LevelCatalog и App/LevelFlow с сохранением настроек.
Snapshot применяется к Presenter; оконный host ещё не подключён. Уровни с hazards
явно отклоняются до интеграции #20. Полное закрытие #8 и настоящий маршрут двух локаций
ещё требуют зависимостей; текущие команды запуска остаются preview.

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
`c69f190042cea9649f551c5196780059e892e0be`: база `v1.0.0` с публичным клавиатурным вводом,
принятым в [PR движка №50](https://github.com/alken1t15/simple-3d-engine/pull/50).
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
