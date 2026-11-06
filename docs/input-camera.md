# Ввод, фиксированный шаг и камера (#6)

Реализованы CPU-модули App и оконный стенд `--play`: нативные WASD/Space/Shift/R,
мышь, захват курсора, GameSession/LevelFlow, Presenter и публичная камера движка.
Это частичная интеграция Ball Adventure. Полноценные меню/HUD (#19), звук и
вторая локация (#21) ещё не подключены; физическая мышь и ручное прохождение
не объявляются проверенными. Обычный запуск и `--presenter` сохраняют preview.

## Публичный ввод и порядок кадра

Движок закреплён на **`7d097269004ad59fc014cdeb707ccd9911c63841`**:
[input PR #52](https://github.com/alken1t15/simple-3d-engine/pull/52),
[overlay PR #53](https://github.com/alken1t15/simple-3d-engine/pull/53),
[game gitlink PR #35](https://github.com/alken1t15/simple-platformer/pull/35).
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
5. Host согласует AppScreen, переносит CameraPose в публичную Camera,
   актуальные объекты Presenter — в постоянную Scene, переданную Engine.Run.
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

## Стенд и проверки

Команда: `dotnet run --project src/Platformer.App -c Release --no-build -- --play`.
Enter/Space запускает Training Track, продолжает паузу, повторяет Failed или
вызывает Next после финиша; после единственной локации получается GameComplete.
Escape — пауза/возврат на старт/выход со старта. Это клавиатурный стенд с подсказками
в терминале, не реализация меню #19. Production-каталог содержит только готовую
Training Track: тестовые вторые уровни не подменяют Turning Maze.

`--help` работает без окна. Любой `--smoke` ограничен тремя кадрами;
`--play --smoke` показывает Training Track без ввода, Step и capture.
`--trace-input` вместе с `--play` выводит JSON-диагностику снимка и игрового кадра
для X11-драйвера; по умолчанию поток диагностики отключён.

Авторские CPU-тесты: InputRouterTests, FixedStepRunnerTests, CameraRigTests,
GameplayFrameRunnerTests и GameplayHostTests. Последние проверяют реальный сеанс,
Scene/Camera, terminal catch-up, переходы, pause/focus/rearm, reset и монеты.
Отдельные аналитические тесты камеры проверяют стены/наклоны/рёбра/embedded target;
тесты с BallSimulator сравнивают результат при 1/2/5 ticks за render frame.

Команды X11 и границы ручной приёмки — [input-camera-testing.md](input-camera-testing.md).
CPU-тесты и автоматический native-прогон не заменяют физическую мышь, настоящий Alt-Tab,
HiDPI и прохождение обоих игровых уровней. Issue #6 не закрывается автоматически.
Новых runtime-пакетов/ассетов нет; test-only XTest указан в THIRD_PARTY.
