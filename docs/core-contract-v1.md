# Ball Adventure: контракт реализации Core v1

Контракт задачи #3 принят в PR #22 (02.11.2026); реализация остаётся задачами команды.
В #4 реализованы OrientedBox, SurfaceDefinition, CoinDefinition, HazardDefinition,
LevelDefinition и данные [Training Track](training-track.md).
В #5 реализованы GameCommand, BallState, SimulationSettings, BallSimulator,
BallStepResult и MotionSegment — [описание физики](ball-physics.md).
В #7 реализованы общие SessionSnapshot/SessionPhase/HazardPose для
[Presenter](scene-presenter.md) и сеанса. В #8 реализованы GameSession, GameEvent/Kind
и LevelCatalog — [CPU-сеанс](game-session.md). HazardMotion/HazardCollision ожидаются
из #20: временно GameSession явно отклоняет уровни с hazards через NotSupportedException.
Остальные правила ниже остаются целевым v1.
Имена и формы ниже — общий контракт для задач #4, #5, #8, #9, #20, #21.
Изменение публичного стыка согласуется с Team Lead и затронутыми исполнителями до merge.
Все типы — namespace `Platformer.Core`, только стандартная .NET10/System.Numerics.

<a id="data"></a>

## Данные и создание

| Тип | Публичная форма / смысл |
|---|---|
| `OrientedBox` | `OrientedBox(Vector3 center, Vector3 halfExtents, Quaternion orientation)`; getters `Center`, `HalfExtents`, `Orientation`; `Vector3 ToLocal(Vector3 point)`, `Vector3 ToWorld(Vector3 point)`, `Vector3 ClosestPoint(Vector3 point)` |
| `SurfaceDefinition` | `SurfaceDefinition(string id, OrientedBox bounds)`; getters `Id`, `Bounds` |
| `CoinDefinition` | `CoinDefinition(string id, Vector3 center, float radius)`; getters `Id`, `Center`, `Radius` |
| `HazardDefinition` | `HazardDefinition(string id, Vector3 pivot, Vector3 axis, Vector3 centerOffset, Vector3 halfExtents, Quaternion initialOrientation, float angularSpeed, float initialAngle)`; однотипная вращающаяся опасная балка |
| `LevelDefinition` | `LevelDefinition(string id, Vector3 spawn, float killY, OrientedBox finish, IReadOnlyList<SurfaceDefinition> surfaces, IReadOnlyList<CoinDefinition> coins, IReadOnlyList<HazardDefinition> hazards)`; getters соответствуют параметрам, коллекции immutable |
| `LevelCatalog` | `LevelCatalog(IReadOnlyList<LevelDefinition> levels)`; `Levels`, `LevelDefinition Get(string id)`, `string? GetNextId(string id)`; порядок списка задаёт прохождение, минимум две записи для поставляемой игры |
| `GameCommand` | readonly value `(Vector2 MoveWorldXZ, bool JumpPressed, bool BoostPressed)`; `Vector2.X` — world X, `Vector2.Y` — world Z; длина ≤1 |
| `BallState` | readonly value: `Center`, `Velocity` (`Vector3`), `Radius` (`float`), `Grounded` (`bool`), `GroundNormal` (`Vector3`), `BoostRemainingSeconds`, `BoostCooldownSeconds` (`float`) |
| `HazardPose` | readonly value `(string Id, OrientedBox Bounds)` — физика и presenter используют одну позу |
| `SessionPhase` | enum `Running`, `Failed`, `LevelComplete`; нет меню, паузы и GameComplete |
| `SessionSnapshot` | immutable class: `Guid SessionId`, `string LevelId`, `long Tick`, `double SimulationSeconds`, `SessionPhase Phase`, `BallState Ball`, `IReadOnlyList<string> CollectedCoinIds`, `IReadOnlyList<HazardPose> Hazards`; `CoinCount` = число собранных ID |
| `GameEventKind` | enum `Jumped`, `BoostStarted`, `CoinCollected`, `Failed`, `LevelCompleted` |
| `GameEvent` | readonly value `(Guid SessionId, long Sequence, long Tick, GameEventKind Kind, string? EntityId)` |

Общие правила: все позиции конечны, ID непустые и сравниваются ordinal/case-sensitive;
ID поверхностей/монет/опасностей уникальны в рамках уровня. HalfExtents/радиусы >0.
Axis конечная ненулевая и нормализуется; Orientation конечная ненулевая и нормализуется
при создании. Для quaternion допустимо отклонение от unit length: нормализация обязательна,
не передавать ненормированный quaternion в вычисления. Нулевые/непредставимые нормы отклоняются.
Координаты/размеры по модулю ≤10000 единиц, размеры ≥0.001 единицы; эти пределы ограничивают
область float-контракта и уменьшают неоднозначность допусков.
Отрицательный scale не используется как физический half extent.

Входные списки копируются; возвращаемые списки нельзя привести к изменяемому внутреннему
массиву/списку. Snapshot сохраняет прежние значения после следующего Step/Restart.
`CollectedCoinIds` упорядочены ordinal для воспроизводимости; `Hazards` — по ID.
`LevelCatalog.Get/GetNextId` для неизвестного непустого ID — `KeyNotFoundException`.
`GetNextId` возвращает следующий ID в порядке Levels, для последнего — null.
Для поставляемого каталога это level1 → level2 → null; пустой каталог отклоняется.
Каталог с одной записью допустим для изолированного теста: GetNextId её ID возвращает null;
поставляемая игра всё равно содержит минимум две локации.
Finish/поверхности могут пересекаться (зона финиша над площадкой); пересечение finish и hazard
не запрещает уровень автоматически, поскольку опасность движется. Безопасность маршрута — проверка контента.

## Настройки симуляции

`SimulationSettings` — неизменяемый класс с constructor по всем перечисленным значениям
и `static SimulationSettings Default`. App и Core используют один экземпляр или одинаковый
валидированный набор; второго независимого значения шага у App нет.

| Property | Default | Допустимые значения |
|---|---|---|
| `FixedStepSeconds` | 1/120f | [1/240, 1/30] с |
| `BallRadius` | 0.5f | [0.1, 2] |
| `MoveSpeed` | 4.5f | (0, 20] |
| `MoveAcceleration` | 20f | (0, 100] |
| `BrakingAcceleration` | 24f | (0, 100] |
| `AirControlFactor` | 0.25f | [0, 1] |
| `Gravity` | 18f | (0, 100] |
| `JumpSpeed` | 6f | (0, 20] |
| `BoostMultiplier` | 1.8f | [1, 3] |
| `BoostDurationSeconds` | 1f | (0, 5] |
| `BoostCooldownSeconds` | 3f | [0, 10] |
| `MaxSlopeRadians` | 50° в радианах | (0, 80°] |
| `SkinWidth` | 0.001f | [1e-4, 0.01] и <Radius/10 |
| `PositionEpsilon` | 1e-5f | [1e-6, 1e-4], <SkinWidth |
| `PenetrationSlop` | 1e-4f | [PositionEpsilon, SkinWidth] |
| `MaxCollisionSubsteps` | 32 | [1, 128] |
| `MaxContactIterations` | 8 | [1, 32] |
| `MaxFallSpeed` | 30f | [JumpSpeed, 100] |

Все float конечны; неверные поля/комбинации — `ArgumentOutOfRangeException` с именем параметра.
Поддерживаемый путь на один подшаг для шара и конца балки ≤Radius/4.
`MaxFallSpeed`, горизонтальный boost и вращение балки проверяются в комбинации с dt и бюджетом.
Данные, требующие больше подшагов, отклоняются до начала сеанса (`ArgumentException` с ID опасности
или именем настройки); больший бюджет не разрешает молча пропускать проверку траектории.

## Публичные границы реализации

Справочник сигнатур (не готовый файл C#):

```csharp
public sealed class BallSimulator
{
    public BallSimulator(LevelDefinition level, SimulationSettings settings);
    public BallState CreateInitialState();
    public BallStepResult Step(BallState before, GameCommand command);
}
public sealed class BallStepResult
{
    public BallState Ball { get; }
    public IReadOnlyList<MotionSegment> Path { get; }
}
public readonly record struct MotionSegment(Vector3 Start, Vector3 End, float StartFraction, float EndFraction);
public static class HazardMotion
{
    public static HazardPose Evaluate(HazardDefinition hazard, double simulationSeconds);
}
public static class HazardCollision
{
    public static bool IntersectsPath(HazardDefinition hazard,
        IReadOnlyList<MotionSegment> path, double tickStartSeconds, SimulationSettings settings);
}
public sealed class GameSession
{
    public GameSession(LevelDefinition level, SimulationSettings settings);
    public SessionSnapshot Snapshot { get; }
    public void Step(GameCommand command);
    public IReadOnlyList<GameEvent> DrainEvents();
    public void Restart();
}
```

`BallSimulator.CreateInitialState` принадлежит #5: создаёт шар из level.Spawn/settings,
нулевую скорость/boost/cooldown, вычисляет опору и разрешает начальное проникновение
не глубже SkinWidth; при более глубоком — ArgumentException с surface ID.
GameSession (#8) вызывает эту функцию при создании и Restart и не дублирует решатель #5.
LevelDefinition (#4) проверяет форму данных/конечность spawn; проверка опоры с конкретными
settings выполняется этим методом при создании сеанса.

`BallSimulator.Step` — чистое преобразование состояния шара со статическими поверхностями;
оно не зависит от GameSession/счётчика монет/препятствий/окна. `Path` — фактически пройденная
ломаная после разрешения контактов, с непересекающимися возрастающими долями [0,1].
Это позволяет GameSession проверять триггеры/опасности по реальному движению, не телепортировать
шар по прямой через стену. Путь охватывает весь tick, в том числе неподвижные интервалы;
следовательно, вращение опасности может задеть неподвижную сферу.
`HazardMotion.Evaluate` возвращает позу без изменения определения; большой угол редуцируется
перед float-преобразованием. Последовательность сложения вращений согласована с row-vector convention:
local orientation → вращение вокруг world axis → translation pivot.
Нормализованная axis в мировых координатах; центр = pivot + Rotate(centerOffset, axis, angle),
orientation = исходная ориентация с тем же world rotation. Радиус вращения включает длину centerOffset
и максимальное расстояние вершины балки от её центра.

`HazardCollision.IntersectsPath` реализуется в #20, вызывается GameSession из #8.
Это чистая проверка опасности против сферы радиуса settings.BallRadius на полном Path.
Path содержит прямолинейные отрезки движения **шара**, не дуги движения опасности.
IntersectsPath сама строит консервативную проверку вращения hazard на этих интервалах;
Evaluate только в endpoints её не заменяет. GameSession передаёт исходный BallStepResult.Path.
Для каждой доли f время опасности = tickStartSeconds + f*settings.FixedStepSeconds;
вызовы Evaluate и проверка дуги относятся к тем же интервалам, что и путь шара.
TickStartSeconds конечен и ≥0; Path не пуст, копировать/менять его не требуется.
Проверка валидирует конечные координаты, непрерывность соседних End/Start в пределах
PositionEpsilon и покрытие долями всего [0,1] без зазоров/перекрытий; неподвижный отрезок допустим.
Неверный Path — ArgumentException; бюджет из settings, исчерпание — InvalidOperationException
с hazard ID. Функция не меняет hazard/path/очередь событий; false означает проверенное
отсутствие контакта, а не «не хватило бюджета». Session коммитит tick только после всех проверок.
BallSimulator отклоняет BallState.Radius, не совпадающий с settings.BallRadius в пределах
PositionEpsilon: у физики, Path и hazard query один радиус.

### Step и приоритеты

Конструктор GameSession создаёт устойчивую опору по contact probe в spawn и проверяет
отсутствие глубокого начального проникновения. Неглубокое пересечение ≤SkinWidth разрешается
проекцией; более глубокое в spawn — `ArgumentException` с ID поверхности.
Сразу после создания персонаж может прыгнуть, если probe подтвердил опору.

1. Проверить команду и состояние до мутации. MoveWorldXZ с длиной >1+1e-6 отклоняется;
   в пределах округления нормализуется. NaN/Infinity — ошибка. Terminal phase: команда всё равно
   валидируется, затем Step no-op без времени/событий.
2. BoostPressed активирует усиление, если и длительность, и cooldown равны0.
   Повторное нажатие во время boost/cooldown игнорируется. На этом tick новое ускорение уже действует.
   Сначала вычисляется доступность по состоянию начала tick, потом уменьшаются таймеры;
   cooldown начинается с завершения boost, избыток dt переносится в cooldown, без потерянного времени.
3. Grounded проверяется contact probe; JumpPressed снимает опору и задаёт Velocity.Y=JumpSpeed.
   Удержание не создаёт повторного прыжка. В воздухе работает AirControlFactor; нет автопрыжка при посадке.
4. Полунеявный Euler: скорость, затем перемещение. Контакты: ближайшая точка OBB, исходящий normal;
   убрать только входящую нормальную составляющую скорости. На допустимом наклоне движение
   проецируется на касательную без ускорения длины вектора. При отсутствии команды стабильная опора
   удерживает шар на допустимом наклоне; на слишком крутом нормаль не даёт Grounded.
5. Static contacts используют ограниченный решатель. Равенство выбирается ordinal Surface.Id,
   внутри OBB — ближайшая грань, затем порядок осей X/Y/Z и положительная сторона при полном равенстве.
   Начальное касание блокирует только входящее движение; касательное/удаляющееся не приклеивается.
6. Опасности проверяются на всём временном Path шара против вращающейся геометрии, включая неподвижный Path.
   Нужен консервативный sweep/разбиение с гарантированной границей дуги, а не только sampling OBB endpoints.
   При фактическом исчерпании бюджета сохранить предыдущее согласованное состояние и выдать
   `InvalidOperationException` с surface/hazard ID; никакого частичного Tick/coin/event update.
7. Если Center.Y + Radius < LevelDefinition.KillY или есть контакт hazard — Failed, выдаётся одно Failed;
   монеты/финиш этого tick не обрабатываются. Затем монеты по ID, затем Finish.
   Монеты — сферы: расстояние пути ≤BallRadius+CoinRadius; finish — sphere/OBB trajectory query.
8. Tick и SimulationSeconds продвигаются один раз после успешного tick, даже если он завершил сеанс.
   `LevelCompleted` создаёт только GameSession; `GameCompleted` — событие App/LevelFlow, не Core enum.

`DrainEvents()` возвращает отделённую read-only коллекцию, затем пуста до новых событий.
Sequence начинается1, монотонен в рамках SessionId; overflow отклоняется до мутации.
`Restart()` создаёт новый SessionId, Tick=0, пустой счёт/очередь, phase=Running, восстановлены
hazard time/boost и ball initial state. Поэтому ключ (SessionId, Sequence) не повторяется после Restart.
App обязан очистить отложенные commands/frame time и звуковые источники прежнего сеанса.

## Поведение ошибок и владение

- Null обязательного объекта/коллекции — ArgumentNullException.
- Пустой ID, дубликаты, null-элемент, противоречивый уровень — ArgumentException с ID/параметром.
- Число вне диапазона/непредставимое значение — ArgumentOutOfRangeException.
- Невозможное состояние исполнения/бюджет/переполнение — InvalidOperationException без частичной мутации.
- Отсутствующий ID каталога — KeyNotFoundException.
- Core не IDisposable, не владеет engine resources и не читает файлы/реальные часы.

Наблюдаемые примеры и аналитические ориентиры — [приёмочные сценарии](acceptance.md).
