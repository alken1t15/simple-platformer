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
через snapshot; ввод, меню и audio host ещё не подключены. Остальные типы остаются спецификацией.
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
