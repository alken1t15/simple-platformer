# Запуск на Windows и через WSLg

Целевая платформа и основа приёмки проекта — Linux. На Windows можно запускать
ту же portable .NET-сборку напрямую: это полезно, если захват мыши через WSLg
даёт слишком быстрые или странные повороты камеры.

## Что проверено

На одном Windows-компьютере, с той же сборкой и исходной чувствительностью:

- WSLg 1.0.65 / X11 передавал большие смещения захваченной мыши: до ~699 logical px
  за кадр, что соответствовало ~120° поворота при 0.003 рад/px.
- При прямом запуске Windows пользователь подтвердил нормальное управление камерой.
- С локальной OpenAL Soft 1.25.2 / WASAPI пользователь подтвердил одновременно
  нормальную камеру и слышимые музыку/эффекты.

Это сравнение конкретного окружения, не доказательство ошибки на всех Windows/WSL
или обычных Linux-системах. В [трекере WSLg](https://github.com/microsoft/wslg/issues/240)
описан похожий симптом усиленного mouse input. Глобальная чувствительность игры
по результатам этого сравнения не менялась.

## Прямой запуск Windows

### 1. Подготовить сборку

Нужны Windows x64, драйвер OpenGL 3.3 и **Windows .NET 10 Runtime либо SDK 10**.
Проверка в PowerShell:

```powershell
dotnet --list-runtimes
```

В списке должен быть `Microsoft.NETCore.App 10.0.*`. SDK 8/9 не заменяет Runtime 10.
Установка — [официальная страница .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).
Для `dotnet run` и сборки нужен именно SDK 10 по [global.json](../global.json); для запуска готовой DLL
достаточно Runtime 10.

Соберите игру по [README](../README.md#подготовка-и-проверки), используя .NET 10 SDK.
Собирать можно в Windows или в WSL на Windows-диске, доступном обеим системам.
После обычного portable build DLL находится здесь:

```text
src/Platformer.App/bin/Release/net10.0/Platformer.App.dll
```

Если собирали в WSL, запускайте в Windows через `dotnet ...dll`: созданный Linux
apphost `Platformer.App` не является Windows EXE. Сохраните весь output, включая
`runtimes`, Assets, deps.json и runtimeconfig.json; одной DLL недостаточно.

### 2. Подключить OpenAL Soft для Windows x64

Старая системная Creative OpenAL в проверенном окружении открывала устройство,
но пользователь не слышал игру. Помогла отдельная современная OpenAL Soft рядом
со сборкой. Системные DLL заменять не требуется.

**Полностью закройте игру**, затем выполните из корня репозитория в PowerShell:

```powershell
$ErrorActionPreference = 'Stop'
$gameDirectory = Join-Path (Get-Location) 'src/Platformer.App/bin/Release/net10.0'
if (-not (Test-Path (Join-Path $gameDirectory 'Platformer.App.dll'))) {
    throw 'Сначала соберите Platformer.sln в Release.'
}
$downloadDirectory = Join-Path (Get-Location) 'artifacts/openal-soft-1.25.2'
New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
$archive = Join-Path $downloadDirectory 'openal-soft-1.25.2-bin.zip'
Invoke-WebRequest -Uri 'https://openal-soft.org/openal-binaries/openal-soft-1.25.2-bin.zip' `
    -OutFile $archive -TimeoutSec 120
$expectedHash = '67a0c4b800bd860c93c04f38caf8cbe4875f9c84700ac430efc451f70e265434'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'SHA256 архива OpenAL Soft не совпал; DLL не устанавливается.'
}
Expand-Archive -LiteralPath $archive -DestinationPath $downloadDirectory -Force
$packageDirectory = Join-Path $downloadDirectory 'openal-soft-1.25.2-bin'
Copy-Item -LiteralPath (Join-Path $packageDirectory 'bin/Win64/soft_oal.dll') `
    -Destination (Join-Path $gameDirectory 'OpenAL32.dll')
Copy-Item -LiteralPath (Join-Path $packageDirectory 'COPYING') `
    -Destination (Join-Path $gameDirectory 'OpenAL-Soft-COPYING.txt')
```

Версия и SHA256 закреплены по [официальному release 1.25.2](https://github.com/kcat/openal-soft/releases/tag/1.25.2).
Используется именно **Win64**, под именем `OpenAL32.dll`. Текст LGPL из `COPYING`
сохраняется рядом. Исходники доступны в указанном release/tag. DLL, архив и output
игнорируются Git; личную установку не нужно добавлять в репозиторий.

### 3. Открыть игру

В PowerShell из корня репозитория:

```powershell
$gameDll = Join-Path (Get-Location) 'src/Platformer.App/bin/Release/net10.0/Platformer.App.dll'
$previousAudioDriver = $env:ALSOFT_DRIVERS
try {
    $env:ALSOFT_DRIVERS = 'wasapi'
    dotnet $gameDll
} finally {
    $env:ALSOFT_DRIVERS = $previousAudioDriver
}
```

Этот выбор аудиобэкенда действует только в текущем процессе PowerShell и его игре;
предыдущее значение восстанавливается после выхода. После Play/Start включается
музыка уровня; Main Menu не имеет музыкального фона. Проверьте Space, монету и
настройки громкости/mute. Ошибки устройства и сообщение `Audio: silent mode`
видны в терминале: 100% в меню не устраняет отсутствие устройства вывода.

## Если нужен запуск через WSLg

Для native audio в Ubuntu/Mint нужны `libopenal1` и `libpulse0`:

```sh
sudo apt install libopenal1 libpulse0
printenv PULSE_SERVER
ALSOFT_DRIVERS=pulse dotnet run --project src/Platformer.App -c Release --no-build
```

WSLg обычно задаёт `PULSE_SERVER=unix:/mnt/wslg/PulseServer`.
Если устройство недоступно при запуске, текущий AudioDevice переходит в silent fallback
и сам не переключается на появившийся звук. Восстановите WSLg audio и перезапустите
игру; не используйте `ALSOFT_DRIVERS=null` для обычной игры — это технический backend
без слышимого вывода.

При усиленном вращении камеры сравните с прямым Windows-запуском выше.
Проверка на целевом обычном Linux остаётся отдельной: работоспособность Windows
или автоматический Xvfb-прогон не заменяют её.

## Частые проблемы

| Симптом | Действие |
|---|---|
| Требуется .NET 10 | Установить Windows Runtime 10 для запуска DLL; SDK 10 нужен для build/run |
| Камера слишком быстрая только в WSLg | Проверить ту же сборку напрямую в Windows, не менять сразу общий camera gain |
| В Windows нет звука, ошибок нет | Проверить локальную Win64 OpenAL Soft, активный Windows-вывод и громкость приложения |
| В логе `silent mode` / `NoDevice` | Восстановить устройство/бэкенд и перезапустить приложение |
| Build/Copy-Item сообщает Access denied | Закрыть native Windows-игру перед сборкой или заменой загруженных DLL |
| Нет Assets/шрифта/звуков | Перенести весь build output; восстановить его обычной сборкой по README |
