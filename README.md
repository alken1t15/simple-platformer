# Simple Platformer

Учебный 2D-платформер на C#/.NET 10 и [Simple 3D Engine](https://github.com/alken1t15/simple-3d-engine).
Сейчас подготовлены структура, техническая сцена из трёх платформ, тесты и CI.
Управление персонажем, физика, коллизии и условия победы ещё не реализованы.

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
- [Команда и роли](docs/team.md)
- [Доска и распределённые issues](docs/github-project.md)
- [Проект игрового API](docs/game-api.md)
- [Требования и открытые решения](docs/requirements.md)
- [Объём подготовки и следующие решения](docs/project-scope.md)
- [Разработка, ревью и защита main](docs/development-process.md)
- [Сторонние компоненты](THIRD_PARTY.md), [лицензия](LICENSE)
