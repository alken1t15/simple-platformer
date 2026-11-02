# Ball Adventure

Проект 3D-платформера на C#/.NET 10 и [Simple 3D Engine](https://github.com/alken1t15/simple-3d-engine):
шарик проходит две локации с мостами, наклонами, монетами и вращающимися препятствиями.
Целевая версия включает WASD/прыжок/ускорение, камеру мышью, звук и меню уровней/громкости/скинов.
Цель — Linux, три недели с 02.11.2026, готовность не позднее 23.11.2026.

**Сейчас это каркас:** запускается старая техническая сцена трёх платформ сбоку, есть тесты и CI.
Ball Adventure, игровой шар, управление, две локации, мышь, меню и звук ещё не реализованы.
Ниже команды текущего preview, а не обещание готовой игры.

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
```

Обычный запуск показывает неподвижный макет уровня сбоку; нажмите Escape в активном окне или закройте окно для выхода.
`--smoke` показывает три кадра и завершает процесс. Это проверка интеграции, не игровая приёмка и не FPS-тест.
Linux/WSL: [графический запуск](docs/development-process.md#linux-и-wsl).

## Документы

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
