# Ввод, фиксированный шаг и камера: независимая часть #6

Реализованы CPU-модули App и авторские тесты. Это **не завершение #6** и не
игровое управление в окне: engine input #16 и сеанс/LevelFlow #8 ещё не подключены.
CPU GameSession/LevelFlow из #8 уже приняты в main через PR #29 (`40d3b2a`);
ветка #6 согласована с ними. Используется единственное общее объявление AppScreen
в `LevelFlow.cs`; InputFrame больше не объявляет этот enum. Оконного host по-прежнему нет.
Обычный запуск и `--presenter` сохраняют прежний preview; `--help` и ограниченный
трёхкадровый `--smoke` сохраняются, smoke не захватывает мышь.

## Модули и порядок кадра

- `InputFrame`, `MenuInput`, `AppScreen` соответствуют [App v1](app-engine-contract-v1.md#app).
  `InputRouter` маршрутизирует WASD относительно yaw, Space/Shift как фронты,
  R/Escape как переходы, клавиатуру и указатель меню. Pitch не участвует в движении.
  После смены экрана/фокуса удерживаемые кнопки требуют release/new press.
- `FixedStepRunner` использует `SimulationSettings.FixedStepSeconds`. При нуле ticks
  сохраняет OR фронтов, обновляет удержания; первый tick потребляет Jump/Boost,
  остальные получают только движение. Frame ограничен 0.1 с, бюджет — 8 ticks,
  лишние полные ticks отбрасываются, дробный остаток сохраняется.
  Накопительный `DroppedSeconds` учитывает и frame clamp, и превышение бюджета;
  `Clear` очищает очередь/время/fault, но сохраняет этот счётчик диагностики.
  Невалидное время отклоняется до изменения состояния. Ошибка callback блокирует
  дальнейший Advance до явного восстановления сеанса и Clear; рекурсивный вызов запрещён.
- `CameraRig.Update` вызывается один раз на render frame. Target передаёт вызывающий:
  `ball.Center + (0, 0.3, 0)`. Defaults: distance 6, pitch 0.4; pitch clamp [-0.35, 1.2].
  Mouse delta применяется без умножения на время: yaw/pitch -= delta * 0.003.
  Yaw свободно вращается; эквивалентный угол приводится к периоду для точности sin/cos.
  Sphere cast радиуса 0.2 использует точное расстояние до статических OBB и
  conservative advancement; отступ от первого контакта — 0.05. Сокращение немедленное,
  восстановление экспоненциальное по времени кадра (коэффициент 8 с⁻¹).
  Эти численные параметры реализации не добавляют требований к игре.
- `GameplayFrameRunner` связывает CPU-модули: сначала focus и R/Escape, затем мышь,
  команда с новым yaw, затем ticks. В меню ticks отсутствуют. Потеря фокуса переводит
  Playing в Paused; возврат фокуса не продолжает игру. `SetScreen(Playing)` — явный
  вызов будущего Start/Continue; захват выражен только свойством `CaptureRequested`.
  Первый mouse delta после перехода игнорируется, чтобы исключить скачок.
  В smoke запрос capture всегда false. Ошибка Step переводит координатор в Failed
  и снимает capture; состояние Core восстанавливает владелец сеанса.

При R результат содержит `RestartPressed`, ticks этого кадра равны нулю, очередь
очищена. Вызывающий обязан выполнить Restart сеанса до следующего Advance, затем
`SetScreen(Playing, resetCamera: true)`. При Next/Start нового уровня также вызвать
SetScreen даже при неизменном экране. LevelFlow, каталог, монеты, UI и физика здесь
не дублируются. Callback Step получает команду; в тестах он вызывает настоящий BallSimulator.
Камера использует позицию шара на начало кадра, как в принятом порядке App v1.

## Граница с закреплённой версией движка

Прежний блокер отсутствия W/S/Shift/Pointer/CursorMode на `c69f190` устранён upstream
и обновлением зависимости игры до **`b20e9edab6e437aeeb4d48c8908b945e0f9f7d2f`**.
Расширение принято в [PR движка #52](https://github.com/alken1t15/simple-3d-engine/pull/52);
используется его merge SHA, не HEAD автора. Порядок следующих обновлений — [engine-first](engine-integration.md#engine-first).

Текущий CPU-вход остаётся неизменяемым `AppInputSnapshot` с `AppButtons`, Down,
Pressed/Released, фокусом и указателем. Это явно обозначенная контрактная граница App,
а не подмена namespace движка или готовый native adapter. `InputRouter.Map` пока
принимает этот тип; overload с доступным `Engine3D.OpenGL.InputSnapshot` из App v1 ещё нужно реализовать в #6.
Нельзя применять числовые cast неподдерживаемых InputKey или читать окно через reflection.

После принятия обновления gitlink остаётся игровая интеграция:

1. В `onUpdate` один раз прочитать `engine.Input`; публичные IsDown/WasPressed/WasReleased,
   IsFocused и Pointer перенести в AppInputSnapshot. Не читать ввод из CPU ticks.
2. Подключить overload InputRouter для принятого InputSnapshot и адаптер cursor mode:
   Normal в меню/focus loss/smoke, Captured только после явного Start/Continue.
   `CaptureRequested` само по себе не меняет native cursor.
3. Связать готовые CPU GameSession/LevelFlow #8 с host: передать Step, выполнять Restart/Next до следующего tick,
   менять экраны по фазе сеанса; передавать CameraPose в публичную Camera и snapshot в Presenter.
4. Выполнить проверки ниже. Upstream PR указан выше; связь с игровым PR обновления
   зависимости фиксируется в карточках #16/#18 при его публикации/приёмке.

## Проверки и ограничения

CPU-тесты: `InputRouterTests`, `FixedStepRunnerTests`, `CameraRigTests`,
`GameplayFrameRunnerTests`. Покрывают 0/1/несколько ticks, фронты и последние удержания,
clamp/budget/остаток/ошибки callback, R/Escape до Step, pause/restart/next/focus,
release/new press, меню/указатель, yaw0 и поворот в том же кадре, pitch clamp,
тонкую/повёрнутую стену, наклон, круглое ребро и embedded target. Сценарий с BallSimulator
на Training Track сравнивает итоговое состояние при 1/2/5 ticks на render frame.

`TargetObstructed` диагностирует target внутри препятствия или ближе radius к нему:
дистанция становится нулевой, Position остаётся у target, направление взгляда ненулевое.
Если сам target внутри стены, свободная от пересечений позиция не гарантируется;
fallback предотвращает NaN и перескок через стену, но не исправляет некорректный spawn/target.
`CastBudgetLimited` означает консервативную остановку после 128 итераций почти касательного
cast. Она может сократить дистанцию сильнее необходимого, но не разрешает проход через стену.
Вращающиеся опасности не входят в static obstruction по контракту.

После подключения зависимостей нужен отдельный ручной Linux-прогон: Start/Continue и
свободный курсор меню, WASD/Space/оба Shift/R, мышь без зависимости от FPS,
Alt-Tab с удержанными кнопками и без автоматического Continue, resize/DPI,
камера на мосту/наклоне/у стен лабиринта. Эти проверки сейчас **не выполнены**;
CPU-тесты и графический smoke не заменяют их. Issue остаётся открытой до интеграции.

Новых пакетов, ассетов и лицензируемых ресурсов не добавлено; THIRD_PARTY не меняется.
