# [DRAFT] Протокол чистого Linux-запуска и репетиции демонстрации Ball Adventure (issue #12)

## 1. Фиксация окружения

| Параметр | Значение | Как посмотреть | Пример |
| --- | --- | --- | --- |
| Дата прогона | |`date` | `Sun Nov  8 20:58:38 MSK 2026` |
| Дистрибутив и версия | | `cat /etc/os-release` | `PRETTY_NAME="Fedora Linux 45"` |
| Архитектура | | `uname -m` | `aarch64` |
| Версия ядра | | `uname -r` | `6.17.0-0.fc45.aarch64` |
| Графическая оболочка | | `echo $XDG_CURRENT_DESKTOP` | `GNOME` |
| Графический сервер | | `echo $XDG_SESSION_TYPE` | `wayland` |
| CPU | | `lscpu \| grep "Model name"` | `Apple M2` |
| GPU и драйвер | | `lspci -k \| grep -A3 -i vga` | `Mesa, драйвер asahi` |
| Аудиоустройство | | `pactl info \| grep -E "Sink\|Driver"` | `PipeWire, built-in speakers` |
| Версия .NET SDK | | `dotnet --version` | `10.0.100` |

## 2. Чистый клон с закреплённым сабмодулем

```sh
cd "$(mktemp -d)"
git clone --recurse-submodules https://github.com/alken1t15/simple-platformer.git
cd simple-platformer
git submodule update --init --recursive
git rev-parse HEAD                      
git submodule status                    
git -C external/engine status --porcelain
```

TODO ожидаемое поведение

## 3. Сборка по README без личных артефактов

- [ ] Убедиться, что клон новый: нет `bin/`/`obj` до restore —`find . -name bin -not -path "./external/*"` → пусто
- [ ] `dotnet restore Platformer.sln` — без ошибок
- [ ] `dotnet build Platformer.sln -c Release --no-restore` — без ошибок и предупреждений
- [ ] `dotnet test Platformer.sln -c Release --no-build` — все тесты пройдены (`N passed / 0 failed`)
- [ ] `python3 scripts/check_docs.py` — PASS
- [ ] `git diff --check` — чисто

## 4. Ресурсы из другого cwd

TODO команды

Проверка отсутствия зависимости от локальных путей
- Запуск из `mktemp -d` (cwd ≠ каталог приложения) — ресурсы резолвятся относительно пути приложения, а не cwd.
- Пять звуковых WAV в output присутствуют; атлас/шрифт поставляются движком.

## 5. Репетиция демонстрационного маршрута

- [ ] Main Menu: Play / Levels / Settings / Skins / Exit; курсор свободен
- [ ] Settings: смена master volume слайдером, mute, Apply; значения сохраняются при повторном входе
- [ ] Skins: применить скин карточкой
- [ ] Level 1 (Training Track): движение WASD, прыжок, boost, монеты, финиш → Next
- [ ] Level 2 (Turning Maze): прохождение до результата; звук фона и эффекты слышимы
- [ ] Финальный результат после уровня 2
- [ ] Levels → direct запуск level 2 без прохождения level 1
- [ ] Поражение (балкой или падением) → Retry; повторный запуск приложения
- [ ] Exit/закрытие окна: процесс завершён (`pgrep -f Platformer.App` пусто), курсор освобождён, звук не висит, второго окна нет; `Ctrl+C` в терминале игру не убивает (Play.sh через `setsid`)

## 6. Просмотр логов

TODO команды + ожидаемое поведение

Прогон по `artifacts/run/*.log`
