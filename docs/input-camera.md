# Ввод, фиксированный шаг и камера (#6)

Реализованы CPU-модули App и игровой host (обычный запуск / `--play`): нативные WASD/Space/Shift/R,
мышь, захват курсора, GameSession/LevelFlow, Presenter и публичная камера движка.
Меню/HUD #19 подключены в том же окне через публичный overlay. Вторая локация,
игровые WAV и AudioCoordinator ещё требуют #21; ручное прохождение не объявляется
проверенным. `--legacy` и `--presenter` сохраняют технические preview.

## Публичный ввод и порядок кадра

Движок закреплён на **`936b71a7393ef52b7d0d3b277af6b4c287be92c6`**:
[upstream overlay PR #53](https://github.com/alken1t15/simple-3d-engine/pull/53),
[game gitlink PR #35](https://github.com/alken1t15/simple-platformer/pull/35).
Ввод принят ранее в [upstream PR #52](https://github.com/alken1t15/simple-3d-engine/pull/52).
Исходники движка и gitlink в этой реализации #6 не меняются.

`Program` читает `engine.Input` один раз внутри onUpdate на потоке Create.
`NativeInputAdapter.Map` явно сопоставляет все публичные InputKey, Down/Pressed/Released,
фокус и левую кнопку/позицию/дельту указателя с независимым `AppInputSnapshot`.
Неактивный snapshot становится default. Числовые cast, OpenTK/GL, reflection и второе окно
в игровом коде отсутствуют. `InputRouter.Map` принимает как CPU-, так и native snapshot.

`GameplayHost` связывает принятый LevelFlow с `GameplayFrameRunner`:

1. Потеря фокуса вызывает LoseFocus; фокус/переход очищает очередь и время.
2. Router проверяет R/Escape до камеры и до Step. Перезапуск выполняется один раз,
   ticks этого кадра отсутствуют. Pause не накапливает время.
3. Mouse delta применяется один раз за кадр без умножения на deltaSeconds.
   Команда строится с новым yaw того же кадра; pitch не меняет движение X/Z.
4. FixedStepRunner вызывает LevelFlow.Step с фиксированным шагом settings.
   LevelFlow обновляет Core и Presenter. Terminal tick останавливает catch-up.
5. MenuController обрабатывает переходы единственного LevelFlow; host согласует AppScreen,
   переносит CameraPose в публичную Camera и объекты Presenter — в постоянную Scene.
   MenuView после переходов/ticks задаёт единственную композицию overlay/HUD.
6. Program задаёт CursorMode по CaptureRequested: Normal в меню/паузе/без фокуса,
   Captured только после явного Start/Continue/Retry. Движок рисует тот же кадр.

`AppScreen` объявлен только в LevelFlow. Host не дублирует физику, монеты,
переходы или владение ресурсами движка. Один Engine.Run обслуживает весь процесс;
смена Presenter/уровня не создаёт другого окна. Удаление монеты из Presenter
отражается в Scene; Restart восстанавливает её. Настройки принадлежат LevelFlow.
CameraPose использует позицию шара на начало кадра, как в принятом App v1.

## Runner и переходы

`FixedStepRunner` хранит последние удержания и OR фронтов Jump/Boost до первого tick.
Первый tick потребляет фронты; остальные catch-up ticks получают только движение.
Шаг берётся из `SimulationSettings.FixedStepSeconds`, frame clamp — 0.1 с,
бюджет — 8 ticks. Полные лишние ticks отбрасываются, дробный остаток сохраняется;
`DroppedSeconds` учитывает frame clamp и превышение бюджета.

`Clear` очищает очередь/время/fault вне Step; диагностика DroppedSeconds накопительная.
`RequestClear` нужен callback `LevelFlow.resetTransientState`: LevelFlow вызывает его
и при terminal phase прямо внутри Step. В этом случае сброс откладывается до окончания
текущего tick, catch-up немедленно прекращается, остаток/команды очищаются.
Host вызывает SetScreen после возврата Advance, когда рекурсивного Clear уже нет.
Вне Step RequestClear действует как Clear. Это предотвращает повторные no-op ticks
завершённого сеанса и исключение Clear внутри Advance.

Start/Restart/Next сбрасывают камеру; Continue сохраняет yaw/pitch, но первый mouse delta
после перехода игнорируется. При смене экрана/фокуса удерживаемые клавиши требуют
отпускания и нового нажатия. Возврат фокуса сам не продолжает игру.
Невалидное время отклоняется до изменения runner. Исключение Step прекращает кадр,
runner становится faulted, GameplayFrameRunner снимает capture; Program завершает Run,
движок освобождает курсор в finally. Повреждённый сеанс автоматически не продолжается.

## Камера

Target — `ball.Center + (0, 0.3, 0)`. Defaults: distance 6, pitch 0.4,
clamp pitch [-0.35, 1.2], yaw -= delta.X * 0.003, pitch += delta.Y * 0.003 рад/логический пиксель.
Вертикаль без инверсии: мышь вверх поднимает взгляд, мышь вниз опускает.
Yaw приводится к эквивалентному периоду для точности sin/cos.
При yaw0 W идёт к −Z, D к +X; диагональ нормализуется.

Sphere cast радиуса 0.2 против статических OBB использует точное расстояние
и conservative advancement, зазор перед контактом — 0.05. Сокращение немедленное,
восстановление экспоненциальное по времени кадра (8 с⁻¹). Это параметры реализации,
не дополнительные требования преподавателя. Вращающиеся hazards не входят в static obstruction.

`TargetObstructed` диагностирует target внутри объёма/защитного радиуса: Distance=0,
Position у target, LookAt остаётся ненулевым. Fallback предотвращает NaN,
но не делает неправильный spawn свободным от стены. `CastBudgetLimited` означает
консервативную остановку после 128 итераций почти касательного cast;
она может сократить расстояние сильнее нужного, но не пропускает камеру через стену.

## Игровой запуск и проверки

Команда: `dotnet run --project src/Platformer.App -c Release --no-build -- --play`.
Обычный запуск открывает тот же графический Main Menu. Стрелки/Tab и мышь выбирают
пункт, Enter/Space или клик активирует его. Escape ставит паузу; из Paused Escape
или Continue продолжает игру; Back возвращает из подменю. Exit закрывает Main Menu.
Production-каталог GameLevels содержит Training Track и Turning Maze. Сквозной тест проходит обе локации через ввод/runner/сеанс/Presenter; это не ручная QA.

MenuController заменяет прежние временные переходы стенда #6. После освобождения capture
первая Normal позиция указателя задаёт базу выбора, исключая случайный переход с Continue
на Settings из-за смены координат. Клавиатура и указатель используют один UI selection model.

`--help` работает без окна. Любой `--smoke` ограничен тремя кадрами;
игровой smoke показывает Main Menu/Training Track без ввода, Step, AudioDevice и capture.
`--trace-input` выводит JSON-диагностику снимка/кадра/UI для X11-драйвера;
по умолчанию поток отключён. [Меню и audio volume](menu.md).

Авторские CPU-тесты: InputRouterTests, FixedStepRunnerTests, CameraRigTests,
GameplayFrameRunnerTests, GameplayHostTests и GameplayMenuIntegrationTests. Последние проверяют реальный сеанс,
Scene/Camera, terminal catch-up, переходы, pause/focus/rearm, reset и монеты.
Отдельные аналитические тесты камеры проверяют стены/наклоны/рёбра/embedded target;
тесты с BallSimulator сравнивают результат при 1/2/5 ticks за render frame.

Команды X11 и границы ручной приёмки — [input-camera-testing.md](input-camera-testing.md).
CPU-тесты и автоматический native-прогон не заменяют физическую мышь, настоящий Alt-Tab,
HiDPI и прохождение обоих игровых уровней. Issue #6 не закрывается автоматически.
Новых runtime-пакетов/ассетов нет; test-only XTest указан в THIRD_PARTY.
