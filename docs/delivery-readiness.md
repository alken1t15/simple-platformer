# Ball Adventure — итоговая интеграция и показ

Материалы Team Lead для [задачи #13](https://github.com/alken1t15/simple-platformer/issues/13),
проверка 09.11.2026. Исходная версия игры —
`82403b8efbcc63fd7041cb1af7e3b06bd0c628da`, движок —
`013e3e29251ae020c9ced5fdeb46ef3d10a7b403`.
Этот документ готовит демонстрацию; факт выступления перед преподавателем,
отправка игры и публикация релиза здесь не заявлены.

После этого аудита изменены уровни 1/2/4: [доработка от 10.11.2026](level-refinement.md).
Приведённые здесь результаты на SHA `82403b8` не являются приёмкой новой геометрии и реакции балок.
12.11.2026 каталог расширен до тринадцати локаций:
[пять новых испытаний](five-challenge-levels.md) и [три сложных уровня с новыми элементами](expert-levels.md).

Функции BA-01–13 интегрированы и проверены в объёме перечисленных ниже отчётов.
По открытым issues на дату аудита зарегистрированных блокирующих дефектов нет;
остаётся #13 с подготовкой этих материалов и их приёмкой через PR.

## Что входит в сборку

3D-платформер с пятью локациями, девятью скинами, 66 монетами и максимумом
15 звёзд. Первые четыре локации доступны сразу; Sky Storm требует по три звезды
на каждой из них (12 суммарно). Для финиша отдельного уровня монеты не обязательны.
Работают меню, пауза, настройки звука/музыки/экрана, локальные сохранения прогресса
и настроек. Правила — [требования](requirements.md), [прогресс](progression.md),
[меню](menu.md); запуск и управление — [README](../README.md).

## Требования → реализация → доказательства

Номера PR ниже относятся к [игровому репозиторию](https://github.com/alken1t15/simple-platformer/pulls).
Это принятые изменения, а не будущие назначения работ. Базовая ручная приёмка
BA-01–13 находится в [PR #49](https://github.com/alken1t15/simple-platformer/pull/49)
и [авторском отчёте #11](https://github.com/alken1t15/simple-platformer/issues/11#issuecomment-6723067684).

| Требование | Реализация и принятые PR | Дополнительная проверка |
|---|---|---|
| BA-01: собственный движок, 3D-шар | GameLevels, GameScenePresenter; #25, #28, #40 | Geometry/Presenter tests; [renderer-маршрут](gameplay-integration.md) |
| BA-02: движение/прыжок | BallSimulator, InputRouter, GameplayHost; #26, #31, #36, #48, #57 | BallSimulatorTests, InputRouterTests, MotionSmoothnessTests |
| BA-03: boost и cooldown | BallSimulator/SimulationSettings; #26, #31 | SimulationSettingsTests, BallSimulatorTests, HudModelTests; boost input в native X11 |
| BA-04: R/Restart | LevelFlow/GameSession, очистка transient input; #29, #36, #40 | LevelFlowTests, SessionRegressionTests |
| BA-05: камера от мыши | InputRouter/CameraRig/GameplayHost; #31, #36, #57 | CameraRigTests, MotionSmoothnessTests; native focus/cursor сценарии |
| BA-06: переходы локаций | LevelCatalog/LevelFlow/PlayerProgress; #29, #38, #40, #51 | ProductionRouteTests, PlayerProgressTests; авторский маршрут #60 |
| BA-07: мосты/наклоны/высоты | TrainingTrack/TurningMaze, sphere/OBB, slope gravity; #25, #26, #38, #53, #57 | PhysicsRegressionTests, SlopeGravityTests, ProductionRouteTests |
| BA-08: монеты и reset | GameSession/TriggerQueries/Presenter; #28, #29 | SessionRegressionTests, TriggerQueriesTests, HudModelTests |
| BA-09: вращающаяся опасность | HazardMotion/события GameSession; #30, #33 | HazardRegressionTests, LevelFlowHazardTests; видимое касание — ручной отчёт |
| BA-10: меню/пауза/выбор | MenuController/MenuLayout/WindowSettings; #37, #43–48, #56, #59 | MenuControllerTests, MenuKeyboardTests, SettingsControlsTests; native X11 |
| BA-11: фон/эффекты/громкость | NativeAudioPlayback/AudioCoordinator; #40, #47, #54 | AudioLifecycleIntegrationTests, MusicSelectionTests; слышимость — авторские Linux-отчёты |
| BA-12: скины без смены физики | GameSkins/SkinTextures/PlayerProgress; #28, #51 | SkinCatalogTests, PlayerProgressTests; выбор виден в авторском показе |
| BA-13: чистый Linux-запуск | README/Play.sh/desktop entry; #50, #55, #60 | [Чистый запуск и репетиция](end2end-demo-protocol.md), результаты ниже |

## Проверки и их границы

| Источник | Что подтверждено | Чего результат не подтверждает |
|---|---|---|
| Авторский отчёт Артёма: `dde53a6`, повтор `cb3c258` | Две исходные локации, BA-01–13; Fedora 45 arm64/Wayland/Apple M2 и дополнительный macOS-прогон; реальные мышь/звук | Все последующие изменения; закрытое без исправления замечание macOS не становится исправленным |
| Авторский [PR #60](https://github.com/alken1t15/simple-platformer/pull/60), `23233dd3` | Новый клон, Fedora Asahi 45/aarch64/KDE Wayland/Apple M2, SDK 10.0.113; маршрут пяти уровней и реальный звук по отчёту автора | Отдельный новый PASS уточнений Enter/Escape, временного профиля и негативного WAV-сценария, добавленных при ревью |
| Git-сверка `23233dd3` → `82403b8` | Нет изменений в `src`, `assets`, `external/engine`, `global.json`, solution и Directory.Build.props | Не является новым ручным прохождением; исходные SHA отчётов сохраняются |
| CI `82403b8`, [run 42622406695](https://github.com/alken1t15/simple-platformer/actions/runs/42622406695) | Linux/Windows compile и CPU tests, documents, graphics-smoke, итоговый build — SUCCESS | Физический монитор, слышимость звука, FPS |
| Чистый локальный клон `82403b8`, Ubuntu 22.04/WSL2 x86_64, SDK 10.0.401 | Restore, Release build: 0 warnings/errors; 368/368 CPU tests; docs/diff PASS | Отдельная загрузка исходников из GitHub: этот контрольный клон создан из локального Git object store, без старых bin/obj |
| Native X11/Xvfb/Mesa в том же чистом клоне | PASS: input/focus/rearm, меню/курсор, автопрыжок, вращение в прыжке, fullscreen, метрики текста/значок и три процесса сохранения настроек | Реальное устройство ввода/аудиовыход или performance benchmark |

Для независимого повторения использовать [протокол чистого клона](end2end-demo-protocol.md).
Локальный контроль использовал свежий клон и точный engine SHA, а не рабочий output
демонстрационной игры. .NET 10.0.401 выбран установленным SDK согласно
`global.json` (`rollForward: latestFeature`); target остаётся `net10.0`.

Автоматическая негативная проверка WAV также выполнена отдельно в WSL Ubuntu:
копия готового output, другой cwd и временный профиль, OpenAL null driver,
удалён `Assets/Audio/jump.wav` → код 1 и `Platformer: Could not find file ... jump.wav`.
После восстановления содержимое WAV совпало с исходным SHA256. Ошибка возникает
до создания окна; этот результат не заявляет ручное прослушивание.

Старые renderer-протоколы X11/Wayland и buffer scale=2 описаны в
[gameplay-integration.md](gameplay-integration.md) и
[engine-integration.md](engine-integration.md). Они не подменяют проверку на физическом HiDPI-мониторе.

## Короткий маршрут показа

1. Запустить `bash scripts/Play.sh --visual` из заранее собранного клона.
   Показать главное меню и пояснить: игра использует собственный движок.
2. Открыть Skins, выбрать один из шести доступных без наград скинов.
   В Settings выбрать разрешение/режим и Apply; открыть Controls, показать управление.
3. В Levels выбрать Training Track. Показать WASD относительно камеры, мышь,
   прыжок/удержание Space, Shift и cooldown, наклон, сбор монет и HUD.
4. Во время Playing поставить паузу → Settings, изменить громкость/музыку,
   вернуться и Continue; услышать изменение после продолжения. Во время паузы
   фон приостановлен, поэтому его слышимость не проверять в неподвижном меню.
5. Пройти Training Track, на результате показать монеты/звёзды и Next → Turning Maze.
   Показать развилку, вращающуюся балку; намеренно проиграть, затем R/Retry.
   Дойти до финиша и показать результат. Next после второго уровня запускает Jump Circuit.
6. При необходимости завершить попытку через Pause → MainMenu → Leave to main menu.
   Перед подтверждением показать Cancel по умолчанию: отмена сохраняет попытку.
   Через Levels показать остальные локации и условие открытия Sky Storm.
7. Выйти через Exit; повторным запуском показать сохранённые скин, настройки
   и лучший рейтинг. Не удалять личный профиль ради показа.

Короткий показ двух обязательных локаций не требует открытия Sky Storm.
Для полного маршрута пяти уровней нужны все монеты на первых четырёх; условия
и временный профиль — в [полном протоколе](end2end-demo-protocol.md).
Продолжительность выступления преподавателем не задана; здесь нет обещания времени прохождения.

## Ответы по архитектуре

| Вопрос | Что показать и ответить |
|---|---|
| Что своё, а что движок? | Игра: правила, уровни, физика сферы, меню, procedural meshes/skins, аудиособытия. Движок: окно, input/cursor, OpenGL renderer, overlay, audio service. [Архитектура](architecture.md) |
| Почему разделены Core и App? | Core — стандартная .NET без окна/GPU; App переводит snapshots в публичные вызовы Engine и управляет lifecycle. Это позволяет проверять правила CPU-тестами |
| Почему движение плавное? | Физика 120 Hz, лимит catch-up; render интерполирует между ticks, камера смотрит на ту же визуальную позицию. Физическое состояние/вращение не берутся из сглаженного transform |
| Как устроены наклоны/опасности? | Sphere против OBB, опора по normal; гравитация проецируется вдоль склона. Опасная балка имеет синхронизированную видимую/CPU-позу; это управляемая сфера, не универсальная rigid-body система |
| Как работают пауза и фокус? | Step останавливается, transient input очищается, cursor освобождается; после активации удерживаемые клавиши требуют release/new press. Музыка/эффекты следуют AudioCoordinator lifecycle |
| Как добавляли API движка? | Отдельный upstream PR → merge → точный game gitlink → проверки → игровая интеграция. В submodule нет локальных патчей и прямых GL-вызовов из App |
| Как сохраняются результаты? | ProgressStore хранит лучший рейтинг; SettingsStore — скин/звук/музыку/подтверждённый режим и оконный размер. JSON пишется атомарно в пользовательский профиль; личные файлы не входят в Git |

## Движок и лицензии

Точный gitlink `013e3e2` — merge
[engine PR #58](https://github.com/alken1t15/simple-3d-engine/pull/58), подключён
[game PR #58](https://github.com/alken1t15/simple-platformer/pull/58), используется в App
через [game PR #59](https://github.com/alken1t15/simple-platformer/pull/59).
История возможностей: input/cursor — engine #52, audio — #51, overlay — #53,
ClientSize — #54, limits/resize API — #55, fixed border — #56, fullscreen — #57,
icon/text ink metrics — #58. Полные ссылки — [engine-integration.md](engine-integration.md).

Проверены [THIRD_PARTY.md](../THIRD_PARTY.md), MIT-notices собственных ресурсов,
[лицензия шрифта](../external/engine/src/Engine3D.OpenGL/Assets/FONT-LICENSE.txt),
CC0 и [notice Exploration Theme](../src/Platformer.App/Assets/Audio/exploration-theme-NOTICE.txt).
SHA256 внешнего WAV совпадает с notice:
`F88993692B978D9BF3D376B1AE45111C1D5F0EA27B4E550E987F580E1384A150`.
Пять исходных WAV — собственные процедурные звуки под MIT; второй трек —
Exploration Theme Cleyton Kauffman под CC0. Значок — сгенерированная иллюстрация
проекта; [происхождение](../assets/branding/README.md). PNG/ICO не являются скриншотами.
OpenTK/GLFW/шрифт/OpenAL сохраняют свои лицензии; системные библиотеки и Windows
OpenAL Soft binary не объявляются собственными ассетами и не включены в репозиторий.
Отдельный релиз/дистрибутив с runtime этой задачей не публикуется.

## Фактический вклад команды

Роли и владельцы — [team.md](team.md). Ниже — примеры принятых результатов по
авторству GitHub PR, не оценка часов или долей. Исправления Codex/Team Lead
не приписываются исходному автору PR.

| Участник | Принятые результаты |
|---|---|
| Алексей Зарубин / alken1t15, Team Lead с Codex | Архитектура/контракты/макет и настройка процесса (#14, #22–24), pin/integration движка (#32, #35, #39–40, #58), исправления окна/меню/звука/движения и оформление (#42–48, #52–59); финальные документальные исправления #49/#60 |
| Владимир Хмелев / VladimirKhmelev | Физика сферы (#26), сеанс/монеты/переходы (#29), подключение опасностей (#33), дополнительные локации/оформление/рейтинг (#51); audio service — engine #51 |
| Николай Яковлев / Nickovlev | CPU-ввод/fixed-step/камера (#31), native input (#36), меню/HUD (#37); расширения input/overlay — engine #52/#53 |
| Роман Кормильцын / ibmpowerpc | Training Track (#25), 3D-представление/скины (#28), опасности (#30), Turning Maze (#38/#46) |
| Константин Кривоносов / fUS1ONd | Независимые регрессии (#27/#41), интеграционные проверки/CLI (#50) |
| Артём Балышев / prettysorrow | Linux manual QA и повтор дефектов (#49), чистый Linux-запуск и репетиция (#60); последующие уточнения документов внесены Team Lead |

## Перед реальным выступлением

Проверить слышимость на выбранном физическом аудиовыходе, фокус/мышь и режим экрана
именно на машине показа. Сохранить работоспособную сборку и известный маршрут;
новый дефект записать с шагами/ожиданием/фактом, отдельно от прежних PASS.
FPS не измерен и не обещан. Работа macOS не является целевой приёмкой Linux.
Время/машина выступления и ограничения преподавателя на доставку не заданы;
назначать их этот документ не пытается.
