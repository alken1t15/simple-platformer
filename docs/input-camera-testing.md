# Проверка управления, меню и камеры #6/#19

Порядок кадра — [input-camera.md](input-camera.md), UI/переходы — [menu.md](menu.md).
Требования ручной приёмки остаются в [общих сценариях](acceptance.md) и карточках
[#6](https://github.com/alken1t15/simple-platformer/issues/6) /
[#19](https://github.com/alken1t15/simple-platformer/issues/19).

## CPU и ограниченный запуск

После restore/build по README:

```sh
dotnet test Platformer.sln -c Release --no-build
dotnet run --project src/Platformer.App -c Release --no-build -- --help
dotnet run --project src/Platformer.App -c Release --no-build -- --smoke
dotnet run --project src/Platformer.App -c Release --no-build -- --play --smoke
dotnet run --project src/Platformer.App -c Release --no-build -- --legacy --smoke
```

Любой smoke заканчивается после трёх кадров, не выполняет игровые ticks/ввод,
не создаёт AudioDevice и не захватывает мышь. Default/`--play` показывают меню,
legacy и Presenter — соответствующие preview. Команды двух скинов Presenter — в README.

CPU проверяет реальные сеансы, монеты, terminal catch-up, смену Presenter в одной Scene,
фронты и release/new press, направление взгляда, все состояния UI, выбор мышью/клавиатурой,
геометрию hit-test/overlay, настройки и callback начальной громкости, скин в Presenter,
возврат Settings→Paused и отсутствие ticks до явного Continue.
Тест с Captured→Normal воспроизводил непреднамеренный выбор Settings вместо Continue;
первый Normal snapshot теперь задаёт базовую позицию, а не движение выбора.

## Автоматический native X11

Нужны Linux, .NET 10, X11-дисплей с OpenGL 3.3, системные libX11/libXtst и Python3.
Python-пакеты не нужны; ImageMagick при наличии сохраняет снимки.
Запускать на отдельном тестовом дисплее: драйвер синтезирует клавиши/мышь,
переключает фокус и меняет размер своего игрового окна. В finally отпускает свои
клавиши/кнопки, завершает процесс и восстанавливает фокус/указатель.

```sh
XDG_SESSION_TYPE=x11 python3 scripts/check_gameplay_x11.py
```

Для software-прогона в окружении с Xvfb:

```sh
LIBGL_ALWAYS_SOFTWARE=true XDG_SESSION_TYPE=x11 xvfb-run -a -s '-screen 0 1280x800x24' python3 scripts/check_gameplay_x11.py
```

Драйвер проверяет:

- все сопоставленные клавиши, левую кнопку/позицию указателя, меню без ticks/capture;
- MainMenu/LevelSelect/Settings/Skins, клик после resize по обновлённым logical bounds;
- master volume/mute через настоящий AudioDevice, сохранение выбранного скина;
- Play/capture/HUD, реальное движение, mouse up/down без инверсии, yaw-relative WASD;
- pause/Settings/Back/Continue, R, Space и оба Shift без повторных фронтов;
- focus loss/gain, отсутствие auto-resume, удержание W дольше autorepeat и release/new press;
- возврат Main Menu, Escape без закрытия и штатный выход кнопкой Exit.

Драйвер задаёт `ALSOFT_DRIVERS=null`: проверяется native audio context/gain, а не слышимость.
Поток `--trace-input` содержит кадр, snapshot, UI-геометрию и настройки.
Логи/снимки сохраняются в игнорируемый `artifacts/input-camera`; другой каталог — `--output`.
Это синтетический ввод и прямое переключение X11-фокуса, не настоящий Alt-Tab через WM.

## Оставшаяся ручная приёмка

Запустить обычное приложение либо `--play` и проверить:

- читаемость Main Menu, Levels, Settings и Skins; клавиатуру, hover и клик, адаптацию к resize;
- Play, WASD/Space/оба Shift/R, mouse up→взгляд вверх, mouse down→вниз; камеру на мосту,
  наклоне и у стен, сокращение дистанции и плавное восстановление;
- Escape→Paused, Settings→Back→Paused, Continue/Escape→Playing; отсутствие скачка мыши
  и срабатывания удержанных при переходе клавиш до release/new press;
- настоящий Alt-Tab с удержаниями: пауза/Normal, возврат без auto-resume, затем явный Continue;
- сворачивание/восстановление и доступный HiDPI-стенд; отсутствующий масштаб отмечать непроверенным;
- падение/Failed/Retry, финиш/LevelResult/Next/GameComplete, сохранение скина и настроек;
- слышимый звук и немедленный эффект громкости после поставки WAV/AudioCoordinator из #21.

Каталог пока содержит только настоящую Training Track. Полное прохождение двух локаций
требует #21. Null/silent backend и изменение gain не заменяют звуковую приёмку.

## Проверенный локальный этап — 06.11.2026

SDK 10.0.114: restore, Release без ошибок/предупреждений, **233/233 CPU**, без пропусков.
Help, документы, Python syntax и diff прошли проверки.
На отдельном Xephyr/X11/software GL прошли полный native-драйвер и пять трёхкадровых smoke:
Main Menu, `--play`, legacy, Presenter classic-blue и orange/overview.
JSON игрового smoke подтвердил Normal, отсутствие Step и AudioDevice.

В настоящем renderer дополнительно сняты все девять UI-состояний на контролируемых
тестовых сеансах. Обзор и отдельные кадры визуально просмотрены; это подтверждает
растровый UI, а не прохождение Training Track/Turning Maze. Тестовая LevelSelect
с двумя уровнями не является production-каталогом.
Снимки/логи находятся в локальном `artifacts/menu-integration`; генератор fixtures личный
и в продукт не включается. Сквозной native-драйвер сохраняет снимки настоящего приложения.

Реальная физическая мышь, WM Alt-Tab, native HiDPI, слышимость, FPS и полное прохождение
не принимаются этим результатом. Удалённые проверки выполняет CI PR.

После сообщения об упоре у jump-landing добавлена отдельная CPU-регрессия физики:
исходный MaxContactIterations воспроизведён до исправления. Совместные контакты теперь
не зацикливаются; проверены скольжение, непрерывность Path, отсутствие проникновения,
опора и выход прыжком. PR #19 добавляет проверку 150 кадров через игровой host;
остальные исправления физики и финиша включены в зависимый PR #6.
Повторный bounded App smoke завершился за три кадра с Normal; ручной прогон этой щели
физической клавиатурой/мышью пока не заявляется выполненным.

Ориентация финиша Training Track также проверена: проём поперёк конечной прямой,
а завершение сеанса происходит у ворот, не на входе на площадку. Два regression-теста
воспроизвели прежнюю ориентацию и преждевременное завершение до исправления.
Полный набор оконной интеграции меню — 233 PASS.
