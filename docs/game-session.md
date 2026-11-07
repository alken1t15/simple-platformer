# Сеанс, монеты и переходы уровней (#8)

Реализована CPU-часть [задачи #8](https://github.com/alken1t15/simple-platformer/issues/8):
GameSession, события, статические триггеры, LevelCatalog и App/LevelFlow.
Program открывает графическое меню и игровой host #6/#19 с вводом/runner/камерой.
Техническая сцена и Presenter сохранены отдельными preview; Turning Maze и AudioCoordinator
из #21 ещё требуют интеграции. [Меню и HUD](menu.md). [Проверки host](input-camera-testing.md).
Опасности (#20) подключены к CPU-сеансу и Presenter через snapshot.
Engine и закреплённый gitlink не изменены.

## Core

GameSession использует BallSimulator.CreateInitialState/Step и готовый SessionSnapshot.
Running переходит в Failed/LevelComplete. Команда проверяется даже в terminal phase;
валидный Step после завершения — no-op. Время равно Tick * FixedStepSeconds.

Порядок tick: физика и полный Path → принятые BoostStarted/Jumped → проверка опасностей
и KillY → несобранные монеты в ordinal-порядке ID → Finish.
Если Center.Y + Radius < KillY или есть контакт с опасностью, создаётся одно Failed,
монеты/финиш tick пропускаются, прежние собранные монеты сохраняются. Несколько контактов
не создают дополнительные Failed. Монеты не являются условием финиша.
Snapshot с позами опасностей, LastPath, sequence и очередь заменяются только после
подготовки всего результата; ошибка физики/бюджета/позы/переполнения не оставляет частичного
обновления. Непрочитанные события предыдущих ticks сохраняются при ошибке.

Событие имеет ключ SessionId/Sequence; Sequence начинается с 1. DrainEvents возвращает
отделённую read-only коллекцию и опустошает очередь. Restart создаёт новый SessionId,
Tick=0, SimulationSeconds=0, исходный шар/опору/boost и фазы опасностей, пустые монеты/события.
Предыдущие snapshot и выданные события остаются неизменными.

LastPath хранит полный read-only путь последнего успешного tick и публикуется вместе
со snapshot/очередью. Ошибка Step не публикует новый путь; terminal Step и DrainEvents
не меняют старый. До первого Step и после Restart LastPath пуст, ранее выданный путь
сохраняется. LevelFlow передаёт LastPath в трёхаргументный Presenter.Apply на каждом tick,
чтобы качение при контактах учитывало последовательность сегментов, а не хорду.

Монеты проверяются по минимальному расстоянию до каждого сегмента Path с порогом
BallRadius + CoinRadius. Для finish минимизируется расстояние сегмента до локального
AABB ориентированного box: между пересечениями плоскостей это квадратичная функция.
Учитываются неподвижный путь, начальное перекрытие и касания грани/ребра/угла.
Расширенный AABB и прямая хорда между endpoints не подменяют точную геометрию/Path.

Опасности проверяются принятым `HazardCollision.IntersectsPath` из #20 на исходном
BallStepResult.Path с tickStartSeconds = временем предыдущего snapshot и тем же settings.
Порядок проверки — ordinal по ID, первая подтверждённая опасность завершает поиск контакта.
Вращение учитывается и на неподвижных участках Path. В Snapshot.Hazards включены все позы
из `HazardMotion.Evaluate` на новом SimulationSeconds, а в исходном snapshot и после
Restart — на времени 0. Поэтому время физики и изображения балки согласовано.
Начальное пересечение с hazard приводит к Failed при первом Step.

Конструктор отклоняет несовместимый бюджет через ArgumentException с ID опасности:
ceil((максимальный путь шара + путь наиболее удалённой точки балки за tick) / (BallRadius/4))
не должен превышать MaxCollisionSubsteps. Эта предварительная оценка не гарантирует
разрешение любого касания: фактический сложный Path или почти касательный контакт могут
исчерпать бюджет IntersectsPath; исключение передаётся клиенту с сохранением прежнего состояния.

## Каталог и App

LevelCatalog копирует непустой список уровней с уникальными ordinal ID. Порядок списка
задаёт Next; последнему возвращается null. Неизвестный ID — KeyNotFoundException,
пустой — ArgumentException, null — ArgumentNullException. Один уровень допустим для
теста; поставляемая игра требует минимум две локации.

LevelFlow получает каталог, SimulationSettings и существующий UserSettings.
Snapshot/Presenter равны null до Start и после MainMenu/GameComplete.
CompletedLevels — отделённый read-only список результатов, сохранённых через Next;
TotalCoinCount включает их и монеты текущей попытки. UserSettings содержит
MasterVolume (finite [0,1]), Muted и SkinId из GameSkins; сохраняется тот же экземпляр.

| Вызов / условие | Результат |
|---|---|
| Start(id), любой экран | Новый маршрут/SessionId/Presenter/Scene, прежние результаты очищены, Playing |
| Step(command), Playing | Tick Core и Apply snapshot к Presenter; Failed/LevelResult при завершении |
| Step, другой экран | No-op: время/boost не продвигаются |
| Pause / LoseFocus, Playing | Paused, очистка input/runner, pause audio; повторный Pause безопасен |
| Continue, Paused | Playing после очистки input/runner и resume audio |
| Restart, Playing/Paused/Failed/LevelResult | Новая попытка того же уровня; Presenter/Scene переиспользуются, прошлые завершённые уровни сохранены |
| Next, LevelResult | Текущий результат сохранён, следующий уровень с новым SessionId/Presenter/Scene |
| Next после последнего | GameComplete, текущий сеанс/Presenter сняты, один App event GameCompleted |
| MainMenu | Сеанс/результаты сняты, настройки сохранены |

Недопустимый переход — InvalidOperationException. Start/Next готовят новый сеанс
до изменения прежнего маршрута и вызова callbacks. Ошибка ID, spawn, позы или бюджета
hazard сохраняет прежнее состояние. Повторный Next из GameComplete отклоняется.
Start сразу level2 не приписывает монеты level1; Retry заменяет текущую попытку,
сохраняя результаты пройденных локаций. Старые события не переносятся в новый сеанс.

## Подключение адаптеров

Конструктор LevelFlow принимает необязательные callbacks:

- `resetTransientState`: очистить input/фронты и вызвать FixedStepRunner.Clear;
- `stopGameplayAudio`: остановить источники/очереди прежней попытки;
- `setAudioPaused(bool)`: приостановить/продолжить аудио.

На Start/Restart/Next/MainMenu и при завершении уровня вызываются resetTransientState,
stopGameplayAudio, setAudioPaused(false) по порядку. Pause/Continue очищают input/runner
и устанавливают audio pause. Focus gain сам игру не продолжает.
Callbacks подготовки перехода вызываются до публикации нового сеанса. Их внешние
побочные действия не откатываются при исключении; host должен прекратить текущий кадр.
При завершении Step Core уже согласованно завершён; ошибка адаптера не откатывает tick.

Не подключённые в CPU-тестах callbacks не означают выполненную интеграцию ввода/звука.
Host #6/#19 берёт Scene у Presenter, маршрутизирует R/Escape до tick, при переходе
очищает аккумулятор и прекращает catch-up шаги старого сеанса. Окно/курсор остаются у host.
AudioCoordinator #21 получает DrainEvents по SessionId/Sequence; render не повторяет звуки.
UserSettings — общая модель процесса, UI настроек и аудиоресурсы здесь не реализованы.

## Проверки

Авторские GameSessionTests/TriggerQueriesTests/LevelCatalogTests/LevelFlowTests проверяют
Path против хорды/endpoint, thin/rotated finish, касания, KillY, разовые ID/events,
rollback, reset, pause, маршрут и настройки. Внутренний simulate-стык служит для
аналитического Path и воспроизводимого сбоя; публичный GameSession всегда использует BallSimulator.
GameSessionHazardTests/LevelFlowHazardTests проверяют приоритет поражения над монетой/финишем,
контакт между крайними позами на втором tick, Path вместо хорды, одноразовые события,
реальные ошибки бюджета, неизменность snapshot/очереди, позы Presenter, паузу и Restart.
Маршрут Start→Failed→Retry→Finish→Next→GameComplete проверяется с настоящей физикой
и Presenter на двух контролируемых тестовых уровнях, которые не заменяют Turning Maze.
Независимое QA #9 остаётся отдельным от авторских тестов.
Графический smoke не подтверждает ручное управление или прохождение двух локаций.
GameplayHostTests проверяют стык LevelFlow/runner/Presenter: завершение останавливает catch-up
после одного terminal tick через RequestClear, без рекурсивного Clear и fault runner.

В интеграции #19 LevelFlow также предоставляет OpenLevelSelect/OpenSettings/OpenSkins/Back.
Подменю возвращается на исходный экран; Settings из Paused сохраняет паузу, Escape в
Paused вызывает Continue. Escape в MainMenu не закрывает приложение — используется Exit.
Эти переходы очищают ввод через тот же callback host, не создавая другого сеанса.


## Production host и аудио

GameplayHost соединяет LevelFlow с InputRouter/runner, Presenter и AudioCoordinator; GameLevels содержит обе локации. Stop/pause callbacks вызываются при переходах, события доставляются один раз после кадра. Проверки маршрута/Retry/прямого level2 — [протокол](gameplay-integration.md), lifecycle звука — [game-audio.md](game-audio.md). Ранее приведённые CPU-проверки не подменяются ручной QA.
