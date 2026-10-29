# Simple Platformer

Учебный платформер на C#/.NET 10 и [Simple 3D Engine](https://github.com/alken1t15/simple-3d-engine).
Сейчас подготовлены структура, техническая сцена из трёх платформ, тесты и CI.
Управление, персонаж, физика, коллизии и условия победы ещё не реализованы.

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

Движок закреплён submodule `external/engine` на релизе `v1.0.0`,
commit `8a71343ba1289cb20911a871d84210b8c6f5d6a1`. Его исходный репозиторий не изменяется.
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

Обычный запуск показывает неподвижный макет уровня; закройте окно для выхода.
`--smoke` показывает три кадра и завершает процесс. Это проверка интеграции, не игровая приёмка и не FPS-тест.
Linux/WSL: [графический запуск](docs/development-process.md#linux-и-wsl).

## Документы

- [Инструкции агентам](AGENTS.md)
- [Участие и команды](CONTRIBUTING.md)
- [Архитектура и границы](docs/architecture.md)
- [Объём подготовки и следующие решения](docs/project-scope.md)
- [Разработка, ревью и защита main](docs/development-process.md)
- [Сторонние компоненты](THIRD_PARTY.md), [лицензия](LICENSE)
