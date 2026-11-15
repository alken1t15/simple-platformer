# Ball Adventure — контракты реализации v1

Результат проектирования в задаче #3. Это контракт реализации;
`OrientedBox`, `SurfaceDefinition`, `CoinDefinition`, `HazardDefinition` и
`LevelDefinition` реализованы в #4 вместе с данными [Training Track](training-track.md).
В #5 реализованы `GameCommand`, `BallState`, `SimulationSettings`, `BallSimulator`,
`BallStepResult` и `MotionSegment` — [CPU-физика шара](ball-physics.md).
В #7 добавлены общие `SessionSnapshot`/`SessionPhase`/`HazardPose` и
[GameScenePresenter/скины](scene-presenter.md), без логики GameSession из #8.
В #8 реализованы GameSession/GameEvent/LevelCatalog и App/LevelFlow/UserSettings —
[сеанс и переходы](game-session.md). Опасности #20 подключены к Step/reset и Presenter
через snapshot; ввод и оконный host подключены в `--play` (#6); меню/HUD подключены через [MenuController и overlay adapter](menu.md);
master gain связан с AudioDevice; игровые WAV/AudioCoordinator подключены по [контракту звука](game-audio.md).
Остальные типы остаются спецификацией.
Прежняя техническая сцена сохранена;
3D-представление проверяется отдельным preview.

- [Core: типы, сигнатуры, численные пределы, порядок шага и ошибки](core-contract-v1.md).
- [App и движок: ввод, камера, меню, overlay, звук и lifecycle](app-engine-contract-v1.md).
- [Сценарии приёмки и аналитические примеры](acceptance.md).
- [Архитектура и владельцы модулей](architecture.md).

Эти два контракта заменяют предварительный v0.2. Core работает в X/Z, Y вверх,
использует только стандартную .NET и не знает окна, меню, аудио или реального времени.
Core SessionPhase содержит Running/Failed/LevelComplete. GameComplete относится
к App/LevelFlow, который определяет следующий уровень по каталогу.

Все игровые события имеют ключ SessionId/Sequence. Restart создаёт новый SessionId;
старые snapshot и выданные события не меняются. Step при ошибке не оставляет частично
обновлённых монет, времени или очереди событий. Проверка траектории обязательна для
тонких поверхностей, монет, финиша и вращающихся опасностей.

Шаг симуляции один: SimulationSettings.FixedStepSeconds. App ограничивает frame delta
до 0.1 секунды и выполняет максимум 8 ticks за кадр, сохраняя дробный остаток.
Пауза/потеря фокуса/смена уровня очищают аккумулятор и отложенные команды.

Изменения публичного стыка согласуются с Team Lead и затронутыми исполнителями до merge.
Начальные физические параметры — проектные решения, а не требования преподавателя;
балансировка допускается вместе с проверкой достижимости обоих уровней.

## Зимние бонусы и лёд (расширение 15.11.2026)

`SurfaceDefinition(..., traction:1)` сохраняет прежнюю физику; `0.12` на новых
зимних ледяных опорах уменьшает grounded-разгон и торможение. Допустимо0.05..1.
`LevelDefinition(..., powerups:null)` обратно совместим; список `PowerupDefinition`
содержит ID/Center/Kind(Jump|Speed)/DurationSeconds(поумолчанию12,1..30).
ID общие с остальными объектами уровня, дубликаты запрещены.
`BallState.JumpPowerSeconds/SpeedPowerSeconds` и `SessionSnapshot.CollectedPowerupIds`
содержат временное состояние сеанса; старые конструкторы имеют пустые/нулевые defaults.
Принятый Path собирает жетоны, эффект действует со следующегоtick, повторное взятие
того же типа обновляет таймер. Оба множителя1.5, таймеры убывают только в Step.
Restart обнуляет, Pause не вызывает Step, ProfileStore/SettingsStore не сохраняют.
Presenter скрывает/возвращает жетоны по ID, HUD читает толькоsnapshot.

### Сплошные опоры механизмов

`TrapStructures.Attach` в Core формирует неподвижные `SurfaceDefinition(IsBarrier:true)`
по прежней видимой геометрии. `GameLevels.CreateCatalog` применяет его к каждой
поставляемой карте ровно один раз. Прессы имеют base/head/guide, поперечные поршни
первой главы — rail/stop, вращающиеся балки — axle; стойки/перекладины маятников уже
были Core-барьерами. Возвращается новый LevelDefinition с теми же ID, спавном,
монетами, финишем, опасностями и бонусами. Отдельно создаваемая авторская карта
без Attach не получает автоматических опор; это явный этап сборки production-каталога.

Presenter рисует эти же OBB через SurfaceObjects/SetBounds. Прежние статические
декоративные дубликаты удалены. Положение/размер/ориентация совпадают точно и не
зависят от --visual. Шар упирается в опоры, основание и верхнюю раму без поражения;
движущаяся плита сохраняет свой прежний контракт. Подвижные высокие штоки и канаты
остаются декорацией. Боковые основания теперь тоже твёрдые: прежнее замечание о
декоративных невзаимодействующих основаниях выше заменено этим контрактом.
`TrapStructureTests` обходит опоры всех20карт, сверяет bounds Presenter и проверяет
настоящим BallSimulator, что движение в бок детали прекращается и шар не проходит.
