# Ball Adventure — контракт модулей v0.2

Проект API для реализации командой. Типы ниже не реализованы. Team Lead фиксирует контракт
в задаче #3, согласует изменения и численные пределы до независимой реализации.
Не переносить API устаревшего Bounds2/GameInput.MoveAxis в 3D.

## Единицы и данные

Мировые длины — условные единицы, время — секунды, скорость — единицы/с, углы — радианы.
System.Numerics, правая система, Y вверх. Данные уровня неизменяемы и копируют входные коллекции.

| Тип | Контракт |
|---|---|
| OrientedBox | Center/PositiveHalfExtents/NormalizedOrientation; WorldToLocal/ClosestPoint; устойчивое пересечение |
| LevelDefinition | Id, spawn, KillY, поверхности, монеты, вращающиеся опасности, область финиша; уникальные ID |
| BallState | Center(Vector3), Velocity(Vector3), Radius>0, Grounded, GroundNormal; изменение только симуляцией |
| GameCommand | MoveWorldXZ(Vector2, длина≤1), JumpPressed, BoostPressed; без ссылок на Engine.Input |
| SimulationSettings | FixedStepSeconds, MaxFrameDelta, MaxCatchUpSteps, скорости/гравитация/прыжок, BoostDuration/Cooldown, MaxSlopeAngle и CollisionBudget |
| SessionSnapshot | LevelId, phase, ball, collectible IDs, время/фазы опасностей, boost state, счёт монет; read-only view |
| GameEvent | Sequence, Tick, Kind(Jumped/CoinCollected/Failed/LevelCompleted/GameCompleted), EntityId |
| UserSettings | MasterVolume[0,1], Muted, SkinId; хранится App, а не физикой |

Предлагаемая форма (не код реализации):

```csharp
public sealed class GameSession
{
    public GameSession(LevelDefinition level, SimulationSettings settings);
    public SessionSnapshot Snapshot { get; }
    public void Step(GameCommand command); // использует единственный settings.FixedStepSeconds
    public IReadOnlyList<GameEvent> DrainEvents(); // выдать однократно и очистить очередь
    public void Restart();
}
```

Переход между уровнями выполняет App/LevelFlow: создаёт новую GameSession из каталога,
а не переносит ball/coins старого уровня. MainMenu/LevelSelect/Settings/SkinSelect — состояния App.
Core phase: Running/Failed/LevelComplete/GameComplete; пауза реализована остановкой Step в App.
Окончание последнего уровня определяется LevelFlow по каталогу, не зашитым числом2 в физике.
Для этого GameCompleted формирует LevelFlow, остальные события — GameSession; единый
event dispatcher не повторяет их и не смешивает sequence разных сеансов (SessionId + Sequence).

## Порядок Step

1. Проверить команду и неизменяемые settings до мутации; неконечные/недопустимые аргументы — ArgumentOutOfRangeException,
   null level/settings — ArgumentNullException; противоречивый level — ArgumentException с EntityId.
2. Обновить boost/cooldown и намерение; принять Jump только при опоре, фронт не хранить в Core между Step.
3. Синхронно переместить опасности/сферу с ограничением расстояния за подшаг, разрешить static contacts.
4. При KillY/контакте hazard — Failed, сбор/финиш в этом tick не выполняются. Повторный Step terminal phase — no-op.
5. Собрать пересечённые монеты (каждый ID один раз), затем проверить финиш и выдать события.

Триггеры монет/финиша проверяются по траектории шара, а не только конечной точке при большой скорости.
Падение/опасность имеют приоритет над финишем; собранная в более раннем tick монета не исчезает из счёта
до Restart. Область финиша валидируется отдельно от опасных объёмов при построении уровня.
Restart восстанавливает initial snapshot и очищает pending events; App дополнительно очищает commands/accumulator.

## App input / камера / UI / звук

InputRouter направляет снимок только активному экрану. MouseLook получает relative delta один раз за frame,
при смене фокуса/режима курсора первый delta игнорируется. GameCommand уже содержит worldXZ относительно yaw.
MaxCatchUp и отбрасывание excess time задаются settings; при pause/focus/смене уровня accumulator=0.
В pause не копится boost cooldown, sim time или pending jump. Held Shift после возврата не создаёт BoostPressed.

Presenter читает SessionSnapshot, не меняет ball. VisualRotation вычисляется по пройденному пути и радиусу,
не участвует в контактах. CameraRig использует тот же LevelDefinition static geometry, но отдельный radius/safety margin.
UIScreens формируют универсальный overlay через публичный API движка; игровые меню не помещаются в Engine.
AudioCoordinator принимает события с ключом SessionId/Sequence, воспроизводит один раз,
настройки применяет немедленно, на выходе освобождает источники/буферы/контекст на их owner thread.

## Численные ориентиры контракта

Это начальные проектные значения Team Lead для проверки, а не требования преподавателя.
До закрытия #3 они проверяются на простых геометрических примерах; дальнейшая балансировка
не меняет единицы, ограничения и смысл API. Изменение записывается вместе с тестами.

| Параметр | Начальная величина / правило |
|---|---|
| Radius / FixedStepSeconds | 0.5 единицы / 1/120 секунды |
| Скорость / ускорение движения | 4.5 единицы/с / 20 единиц/с²; при отсутствии команды торможение 24 единицы/с² |
| Gravity / JumpSpeed | 18 единиц/с² вниз / 6 единиц/с вверх |
| Воздушное управление | 25% ускорения на опоре; вертикальная скорость прыжка не усиливается удержанием Space |
| Boost | Множитель максимальной скорости 1.8; длительность1с, cooldown3с после завершения; новое нажатие необходимо |
| Опора | Normal.Y ≥ cos(50°); контакт в пределах skin width 0.001 единицы, прыжок требует Grounded |
| Контакты и float | epsilon длины 1e-5, penetration slop 1e-4 единицы; orientation norm отклонение ≤1e-4, нормализуется при создании |
| Бюджеты | 8 sim ticks/кадр, до32 collision substeps/tick; конечные положительные настройки, суммы счетчиков без переполнения |
| Время | Boost заканчивается при elapsed ≥ duration; cooldown готов при remaining ≤0, остаток ограничен снизу0 |
| KillY | Failed, когда Center.Y + Radius < KillY: вся сфера ниже границы; значение берётся из уровня |

Интегратор: полунеявный Euler — сначала скорость от ускорений, затем перемещение.
Максимальный путь на подшаг для шара и наиболее удалённой точки вращающейся балки — Radius/4.
Это ограничение дополняет консервативный sweep, а не доказывает отсутствие проскока само по себе.
Невозможная комбинация размеров/скорости/шага/CollisionBudget отклоняется при валидации settings/level;
при неожиданном исчерпании безопасное положение сохраняется и выдаётся диагностируемая ошибка,
а не пропускается путь сквозь геометрию. Hazard pose и ball используют одинаковые временные границы.

После mouse look текущего render frame вычисляется yaw, затем из того же yaw — MoveWorldXZ
для последующих ticks кадра. В безтиковом кадре обновляется последнее удерживаемое направление,
но накопленные фронты сохраняются до tick. Mouse delta не хранится как команда симуляции.
Pitch камеры: диапазон[-0.35,1.2]рад, расстояние6 единиц, safety radius0.2 единицы;
изменение этих параметров тестируется на стене, наклоне и старте внутри препятствия.

## Обязательная проверка контракта

Sphere/OBB edge/corner/inside, уклоны и сход с края, high-speed/boost thin geometry,
rotating hazard sweep, достижимость уровня, coin/finish sweep, приоритет Failed,
full reset/transition, одноразовые события, 0/1/multiple ticks, pause/focus/cursor,
обе громкости0/1 и отсутствие audio device. Допуски задаются в #3 вместе с объяснением;
не обещать битовую идентичность float между платформами. Внешние OpenTK/GL-типы не входят в Core/API.
