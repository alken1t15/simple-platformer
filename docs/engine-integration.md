# Движок: возможности и план расширений для Ball Adventure

Закреплённый SHA: **`b20e9edab6e437aeeb4d48c8908b945e0f9f7d2f`**, обновление 03.11.2026.
Он включает принятые [input/cursor PR #52](https://github.com/alken1t15/simple-3d-engine/pull/52)
и [audio PR #51](https://github.com/alken1t15/simple-3d-engine/pull/51).
Ниже различаются доступное API зависимости и ещё не подключённая игровая реализация.

| Возможность | Что есть | Что требуется / граница |
|---|---|---|
| 3D-сцена, depth, текстуры, свет, перспективная камера | Core/OpenGL public API | Переиспользовать; игровая камера в App |
| Геометрия шара | MeshData принимает произвольные вершины; PrimitiveFactory только cube/plane | Процедурная UV-сфера в App без GL; не требуется новый тип renderer или импорт моделей |
| Клавиши | A/D/W/S, оба Shift, Space/R/Escape, Enter, стрелки, Tab; down/pressed/released/focus | Подключить native adapter к InputRouter/host (#6); после активации удержания требуют release/new press |
| Мышь/захват курсора | Immutable PointerSnapshot: logical Position/Delta/buttons/edges; CursorMode.Normal/Captured | Подключить adapter на owner thread; focus loss освобождает capture, gain не восстанавливает его автоматически |
| UI / текст / overlay | Публичного API нет | Минимальный overlay с прямоугольниками, текстом/атласом и картинками; экранные координаты и resize |
| Звук | Отдельный Engine3D.Audio: PCM16 WAV mono/stereo 8–48kHz, voices/loop/pause/volume/dispose, silent fallback | Игровые WAV/AudioCoordinator в #21; device создаёт App явно, preview пока не воспроизводит звук |
| Физика | Не входила в движок | BallSimulation и коллизии реализуются в Platformer.Core, а не в renderer |

## Изменение зависимости

Исходную рабочую копию движка и её workflow не изменять. Для расширения — изолированный
checkout, отдельный upstream PR, проверки, review и merge только при поручении; затем отдельное
изменение gitlink игры на точный принятый SHA. В этом обновлении выбран squash commit PR #52;
его предок `29d2028df53915a1fad52509ee792040637e12b9` — принятый audio PR #51.
Готовность контракта и готовность реализации отмечаются отдельно; не импортировать внутренний NativeWindow.

### Что подключать участникам

- #6: один раз читать `engine.Input` в onUpdate, перенести snapshot в AppInputSnapshot,
  связать native cursor с CaptureRequested и LevelFlow. Доступность API не заменяет реализацию adapter.
- #21: использовать публичный Engine3D.Audio; App уже имеет ProjectReference, устройство/ресурсы
  создаёт и освобождает будущий AppStartup. Игровых WAV/event mapping этим обновлением не добавлено.
- #17/#19: overlay пока отсутствует; нужны отдельные upstream PR и новое закрепление версии.

Команды upstream-проверок в выбранной зависимости:

```sh
dotnet build external/engine/samples/InputSample -c Release
LIBGL_ALWAYS_SOFTWARE=true XDG_SESSION_TYPE=x11 xvfb-run -a python3 external/engine/scripts/check_input_x11.py
dotnet build external/engine/samples/AudioSample -c Release
ALSOFT_DRIVERS=unavailable dotnet run --project external/engine/samples/AudioSample -c Release --no-build -- --expect-silent
```

X11-прогон требует системные Xvfb/libX11/libXtst; воспроизведение аудио — OpenAL Soft.
На Debian/Ubuntu/Mint это `libopenal1`; для no-device и null-driver результаты отделяются
от реальной слышимости. Runtime binaries в игру не копируются, лицензии — [THIRD_PARTY](../THIRD_PARTY.md).

<a id="engine-first"></a>

## Если найден дефект или не хватает API движка

Этот порядок относится ко всем игровым задачам, а не только #16–18.
Исправление движка делается **до интеграции зависимой функции игры**:

1. В текущей задаче игры записать минимальное воспроизведение, ожидаемое/фактическое поведение,
   точный engine SHA, окружение и влияние на функцию. Сначала отделить дефект движка от ошибки Core/App.
   Для известных расширений использовать задачи [#16: ввод](https://github.com/alken1t15/simple-platformer/issues/16),
   [#17: overlay](https://github.com/alken1t15/simple-platformer/issues/17),
   [#18: audio](https://github.com/alken1t15/simple-platformer/issues/18), не дублировать их.
2. Работать в отдельной чистой копии [репозитория движка](https://github.com/alken1t15/simple-3d-engine).
   Например, из корня игры клонировать в игнорируемый `.local/engine-work`; если копия уже есть,
   сначала проверить её status/diff/fetch и создать новую ветку от актуального upstream main.
   Старую копию `E:\comp_graph` и `external/engine` для правок не использовать.
3. Прочитать актуальные [README движка](https://github.com/alken1t15/simple-3d-engine/blob/main/README.md),
   [CONTRIBUTING](https://github.com/alken1t15/simple-3d-engine/blob/main/CONTRIBUTING.md),
   [AGENTS](https://github.com/alken1t15/simple-3d-engine/blob/main/AGENTS.md),
   [API](https://github.com/alken1t15/simple-3d-engine/blob/main/docs/api.md).
   Подтвердить дефект воспроизводящим тестом/сценарием, исправить и выполнить проверки движка.
   Согласованные расширения Ball Adventure дополнительно следуют [App/engine v1](app-engine-contract-v1.md).
4. Открыть PR **в simple-3d-engine**, со ссылкой на полную URL игровой задачи, изменённым API,
   тестами/диагностикой/ограничениями и необходимыми обновлениями документации/лицензий.
   В игровой задаче указать полный URL этого upstream PR. Номер `#16` в движке не означает `#16` игры.
5. Team Lead проверяет CI и требуемые правилами движка одобрения и принимает исправление
   без лишней задержки, как поручил пользователь. «Сразу merge» означает после проверок/ревью,
   не обход обязательного CI или человеческих одобрений. Автор не выдаёт себе Approve.
   До merge движка зависимая игровая интеграция остаётся заблокированной.
6. После merge Team Lead берёт **SHA принятого commit из upstream main** (после squash это не SHA
   ветки автора) и создаёт отдельный PR обновления зависимости **в simple-platformer**.
   В чистой игровой ветке получает commit через `git -C external/engine fetch origin`,
   выбирает его `git -C external/engine checkout --detach <принятый-SHA>` и добавляет gitlink.
   `git submodule update --remote` без фиксированного SHA не использовать.
   Если изменился API, минимальную адаптацию App/ProjectReference включает этот игровой PR.
7. Для нового gitlink выполнить проверки игры из [README](../README.md): restore/build/CPU tests,
   документы/diff и отдельный графический `--smoke`; затронутый ввод/UI/audio проверить на Linux.
   В PR зависимости указать upstream PR, принятый SHA и результаты. Принять игровой PR по его правилам.
8. Только после принятого game gitlink и проверок участник обновляет свою ветку от main,
   выполняет `git submodule update --init --recursive` и продолжает зависимую реализацию.
   Если ветка имеет локальные правки, сначала сохранить работу обычным commit, не делать разрушительный reset.
   В задаче остаются две ссылки: upstream PR и игровой PR обновления зависимости.

Исправление/расширение и авторские тесты пишет назначенный владелец соответствующего модуля;
изменения архитектурного стыка, приёмку и интеграцию версии ведёт Team Lead.
Задачи #16–18 закрываются только после upstream merge **и** принятого проверенного game gitlink.
Остальная независимая работа, подготовка данных и тестов на контрактных двойниках могут продолжаться;
вся игра не останавливается из-за одного изолированного дефекта. Непринятый API нельзя объявлять готовым.

Базовые команды в отдельной копии движка (SDK по его global.json):

```sh
dotnet restore Engine3D.sln
dotnet build Engine3D.sln -c Release --no-restore
dotnet test Engine3D.sln -c Release --no-build
```

Затронутые графические/нативные сценарии — по
[инструкции движка для Linux/WSL](https://github.com/alken1t15/simple-3d-engine/blob/main/docs/spikes.md).
CPU или silent CI не доказывают работу мыши, внешний вид или слышимость.

## Контракты расширений (задачи реализации)

- InputSnapshot остаётся неизменяемым, читается в onUpdate на owner thread. Relative mouse delta,
  pointer position в client logical coordinates, button edges; high DPI conversion для UI определена явно.
  Cursor mode задаётся до/в update, focus loss принудительно освобождает, gain не захватывает молча.
  Поворот камеры не зависит от FPS; `--smoke` не перехватывает мышь.
- Overlay рисуется после 3D в том же окне, собственные orthographic pixels/alpha/depth состояния
  восстанавливаются и не меняют соглашения 3D-матриц. Шрифт/атлас имеет источник и лицензию,
  русский текст меню либо обеспечивается glyph atlas, либо явно выбирается английская локализация.
  API получает обычные данные текста/прямоугольников/изображений, не GL handles и не MenuScreen игры.
- Audio service реализуется отдельным проектом Engine3D.Audio: аудиоконтекст и lifecycle
  независимы от renderer/Scene; клиент использует [контракт v1](app-engine-contract-v1.md).
  Конкретный backend (кандидат OpenAL), способ инициализации и лицензии проверяются техническим spike.
  NuGet OpenTK не означает автоматически готовый audio API или устройство Linux.
  Первичный формат PCM WAV сокращает декодеры; несколько эффектов одновременно и loop фоновой дорожки.
  Нет audio device: понятная диагностика и работоспособный silent mode, без аварии;
  silent CI не является успешной ручной проверкой звука. Слышимость — на реальном Linux.

Контракты проектирует Team Lead; участники реализуют согласованные задачи.
UI/input/audio могут идти параллельно после фиксации lifecycle и сигнатур; изменения общего Engine.cs
интегрируются последовательно, без трёх несовместимых методов Run/Dispose.
Новых пакетов/ассетов в этой переработке не добавлено; соответствующие записи THIRD_PARTY
обязательны в реализационных PR, а не откладываются к сдаче.
