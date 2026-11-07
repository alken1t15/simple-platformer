# Независимые CPU-регрессии — задача #9

Матрица связывает требования BA и наблюдаемые сценарии [приёмки](acceptance.md)
с модулями Core и независимыми тестами в `tests/Platformer.Tests/Regression`.
Эталоны выводятся из геометрии раскладки и контракта [Core v1](core-contract-v1.md),
а не из копии решателя. CPU-тесты не доказывают визуальный вид, ручную проходимость,
FPS и слышимость; для них — QA-чек-лист на Linux (#11).

Первая часть матрицы (PR #27) покрыла модули #4 и #5. После слияния `GameSession` (#8),
`HazardMotion`/`HazardCollision` (#20) и данных Turning Maze (#21, PR #38/#40) прежний
тест-дизайн реализован независимыми регрессиями ниже. Авторские
[проверки GameSession/LevelFlow](game-session.md#проверки) их не заменяют.

## Реализованные проверки

Файлы: [физика](../tests/Platformer.Tests/Regression/PhysicsRegressionTests.cs),
[уровень](../tests/Platformer.Tests/Regression/LevelRegressionTests.cs),
[сеанс](../tests/Platformer.Tests/Regression/SessionRegressionTests.cs),
[опасности](../tests/Platformer.Tests/Regression/HazardRegressionTests.cs),
[вторая локация](../tests/Platformer.Tests/Regression/TurningMazeRegressionTests.cs),
[общие данные и допуски](../tests/Platformer.Tests/Regression/RegressionFixtures.cs).

Допуски и их обоснование:

| Имя | Значение | Обоснование |
|---|---|---|
| Зазор опоры | [r−PenetrationSlop, r+SkinWidth+PositionEpsilon] | Контракт Core v1: защитный радиус r+SkinWidth, допустимое проникновение PenetrationSlop |
| `NormalTolerance` | 1e-5 | Нормаль из нормализованного float-кватерниона: несколько ULP единичного вектора (~1e-7), запас ×100 |
| `AccumulatedTolerance` | 1e-4 | Высота/скорость после ≤1200 ticks при модулях ≤10: накопление float 1e-6..1e-5; много меньше SkinWidth=1e-3 и шага скорости за tick 1/6 |
| `DriftTolerance` | 1e-6 | Покой без команды: торможение к нулю даёт точный ноль, остаётся округление позиции около 1 |
| Непрерывность Path | PositionEpsilon=1e-5 | Контракт Path в Core v1 |

| BA / сценарий | Модуль | Тест | Эталон и допуск |
|---|---|---|---|
| BA-07, A07 | SphereGeometry, BallSimulator | `FlatTopFaceGivesUpNormalForAnyEquivalentBoxOrientation` | Одна плита тремя ориентациями OBB: normal +Y, центр в зазоре опоры, 1 с покоя без дрейфа |
| BA-07, A07 | BallSimulator, Training Track | `TrainingTrackRampNormalAndUphillDirectionFollowTwentyDegreeGeometry` | Рампа из раскладки (2,0)→(5,3·tan20°): normal (−sin20°, cos20°, 0); vy/vx = tan20°; длина скорости ≤ MoveSpeed; расстояние до плоскости в зазоре опоры |
| BA-07, A07 | BallSimulator | `SupportFlipsAroundConfiguredMaxSlope` | MaxSlope 50°: 49° — опора, 51° — нет |
| BA-02, A03 | BallSimulator | `JumpApexMatchesClosedFormSemiImplicitEulerSum` | H = dt·(N·v0 − g·dt·N(N+1)/2), N = v0/(g·dt); эталоны посчитаны вручную: 0.975 (default), 0.54375, 1.975; посадка — индекс шага 2N−2 ±1 (Σv=0 при 2N−1 шагах); повторный фронт Space в воздухе не даёт прыжка |
| BA-03, A04 (часть Core) | BallSimulator | `BoostPressEveryTickActivatesOncePerDurationPlusCooldownAndCapsSpeed` | Фронт BoostPressed каждый tick 10 с: активации на ticks 0, 480, 960; скорость ≤ 8.1, на 119-м tick 8.1, на 479-м 4.5; boost и cooldown не пересекаются. Удержание клавиши → один фронт проверяет App/runner, не Core |
| BA-02/07, A09 (частично) | SphereGeometry sweep | `MaxFallSpeedNeverTunnelsThroughThinPlateAtAnyTickPhase` | Плита 0.002, падение 30: 37 фаз старта, шар нигде не ниже r−PenetrationSlop, посадка в зазоре опоры. Горизонтальный boost и рёбра/углы — авторские `MaximumConfiguredBoostCannotTunnelThroughThinWall` и SphereGeometryTests |
| Контракт Step | BallSimulator | `TrainingTrackRouteReplayIsBitIdenticalForAnySurfaceOrder` | Маршрут с рампой, прыжком, boost: побитовое совпадение Ball и Path при повторе, новом симуляторе и 4 перестановках surfaces. На Training Track нет одновременных контактов, поэтому сортировку проверяет следующий тест |
| Независимость порядка surfaces | BallSimulator | `SimultaneousContactBudgetErrorNamesSameSurfaceForAnyOrder` | Пол и стена симметричны, числа точно представимы (dt 1/64, g 16): одновременный контакт; ошибка бюджета называет `wall` при любом порядке списка |
| Контракт Path (вход #8/#20) | BallSimulator | `RoutePathIsContinuousCoversWholeTickAndNeverEntersSurfaces` | Path непрерывен (PositionEpsilon), доли покрывают [0,1] без зазоров, в 9 точках каждого отрезка расстояние центра до каждого OBB ≥ r−PenetrationSlop |
| Контракт ошибок (прокси для Snapshot #8) | BallSimulator | `FailedTickLeavesNoHiddenStateAndEarlierPathsStayIntact` | Исчерпание MaxContactIterations: InvalidOperationException с ID `wall`; Path прежнего результата не меняется и read-only, повтор после ошибки совпадает. BallState — значение, его неизменность не проверяется |
| BA-08, A10 | LevelDefinition, Training Track | `EveryTrainingTrackCoinIsTouchedByBallRestingBelowIt` | Шар, опущенный под монету по реальной физике, касается её: расстояние ≤ r + CoinRadius. Число монет из данных уровня |
| BA-06, A13 | Training Track | `BallRestingOnFinishPlatformOverlapsFinishVolume` | Шар на финишной площадке пересекает Finish; spawn — нет |
| Копирование коллекций | LevelDefinition | `SimulationIgnoresLaterChangesOfLevelSourceList` | Чёрный ящик: после Clear/Add исходного списка поведение шара за 1 с то же. Структурная проверка — авторский `AllInputListsAreCopiedAndCannotBeMutatedThroughReturnedInterfaces` |
| Неизменяемость уровня | TrainingTrack | `TrainingTrackFactoryReturnsIndependentEquivalentLevels` | Два вызова фабрики — разные эквивалентные экземпляры |
| BA-09, A11 | HazardMotion | `EvaluatePlacesCenterAtPivotPlusRotatedOffsetForAnyTime` | Поворот вокруг +Y: X → (cosθ,0,−sinθ); центр и ось балки при θ=0, π/2 и после 10⁶ периодов; определение не меняется. Допуск `AccumulatedTolerance`: ошибка большого времени ~1e-9 |
| BA-09, A11 | HazardCollision | `BeamSweepingThroughStationaryBallWithinOneTickIsAHit` | Балка (offset 2, half (1,0.1,0.1)) за tick −30°→+30°: в обеих позах зазор 2·sin30°−0.1−r = 0.4 > 0, в середине tick ось проходит через шар — `IntersectsPath` = true |
| BA-09 | HazardCollision | `BeamThatStopsShortOrPassesBeyondReachDoesNotHit` | Поворот −30°→−20° (ось в 0.68 от центра) и полный проход при радиусе шара 3.6 (зазор ≈0.097 до угла 3.0033) — промаха нет |
| BA-09 | HazardCollision | `BrokenPathIsRejectedAndExhaustedBudgetNamesHazard` | Пустой Path, зазор долей, разрыв End/Start, неполное покрытие, обратный порядок — ArgumentException; отрезок 10 при шаге r/4 (80 > 32 подшагов) — InvalidOperationException с ID |
| BA-09, A11, level2 | GameSession, Turning Maze | `TurningMazeArenaBeamReachesBallRestingInArenaWithinGeometricWindow` | Шар покоится в арене на радиусе 1.5 под углом π/2 к начальной оси балки: Failed при угле в [acos((r+0.15√2)/1.5), acos(r/1.5)+dt], одно событие Failed |
| BA-08, A10 | GameSession | `ThinCoinBesideTickChordIsCollectedByPathBetweenEndpoints` | Монета сбоку от середины отрезка tick на R−ε, ε = L²/(32R): обе позы шара дальше R, засчитывается в этом tick по Path; при R+1e-3 — никогда |
| BA-08, A10 | GameSession | `CoinOnPathCountsOnceEvenWhenBallPassesItAgain` | Проход вправо, влево и снова вправо через монету — одно CoinCollected |
| BA-09, A11 | GameSession | `HazardTouchedInSameTickAsFinishAndCoinFailsWithoutCompletingOrCollecting` | Опасность, Finish и монета входят в конец Path tick K на L/2: Failed без LevelCompleted и монеты; контроль без опасности — монета и финиш в том же tick |
| BA-04, A12 | GameSession | `RestartIssuesFreshSessionAndLeavesEarlierSnapshotUntouched` | Новый SessionId, Tick 0, время 0, пустые монеты/очередь/Path, boost сброшен, поза опасности при t=0; прежний Snapshot не изменился; монета снова засчитывается в новом сеансе |
| BA-04 | GameSession | `SequenceStartsAtOneInEverySessionAndEventKeysNeverRepeat` | Три сеанса с промежуточными DrainEvents: Sequence 1..n без пропусков в каждом SessionId, ключи (SessionId, Sequence) уникальны, непрочитанное перед Restart отброшено |
| Контракт ошибок | GameSession | `ContactBudgetFailureLeavesSnapshotPathAndQueuedEventsUnchanged` | Посадка у стены при бюджете 1 контакта: тот же Snapshot и LastPath, повтор падает так же, в очереди только прежняя монета с Sequence 1 |
| Независимость порядка | GameSession, Turning Maze | `PermutedLevelCollectionsProduceIdenticalEventsAndSnapshots` | Данные level2 + вторая опасность, 3 перестановки surfaces/coins/hazards: совпадают события и снимки каждого tick |
| BA-08, A10, level2 | Turning Maze | `EveryTurningMazeCoinIsTouchedByBallLandingBelowIt` | Первая поза с опорой после падения под каждой монетой (включая рампу) — расстояние ≤ r + CoinRadius; число монет из данных |
| BA-06, A13, level2 | Turning Maze | `BallRestingOnTurningMazeFinishPlatformOverlapsFinishButSpawnDoesNot` | Шар на финишной площадке пересекает Finish, spawn — нет |
| BA-07, A07, level2 | Turning Maze | `TurningMazeRampNormalAndUphillDirectionFollowTwentyDegreeGeometry` | Рампа (24,0,8)→(27,3·tan20°,8): normal (−sin20°, cos20°, 0), vy/vx = tan20°, зазор опоры |

## Результаты прогона

Окружение: Linux, .NET SDK 10.0.403, Release, без дисплея и OpenGL.

| Этап | Основа | Регрессии #9 | Весь набор |
|---|---|---|---|
| PR #27 (#4/#5) | main `c8f6032` | 17 PASS | 86 PASS |
| Сеанс, опасности, level2 (#8/#20/#21) | main `dde53a6` | 32 PASS (+15) | 256 PASS, 0 FAIL, 0 SKIP |

SHA итогового commit фиксируется в описании PR.

Дефектов в #4/#5/#8/#20/#21 не выявлено. Дефект, найденный позже, оформляется
отдельной строкой: минимальное воспроизведение, тест, issue/PR исправления.

Чувствительность проверена мутациями в рабочей копии (в commit не входят): правка,
`dotnet test ... --filter "FullyQualifiedName~Regression"`, обратное применение той же правки.

| Файл: мутация (было → стало) | Упавшие регрессии |
|---|---|
| BallSimulator: `if (SphereGeometry.Sweep(` → `if (false && SphereGeometry.Sweep(` | 7: тонкая плита, повтор маршрута, Path, бюджет, одновременный контакт, монеты, финиш |
| BallSimulator: `BoostCooldownSeconds - excess` → `BoostCooldownSeconds - FixedStepSeconds - excess` | Boost-тест |
| BallSimulator: `Cos(settings.MaxSlopeRadians)` → `Cos(settings.MaxSlopeRadians + 0.09f)` (≈55°) | `SupportFlipsAroundConfiguredMaxSlope(51)` |
| BallSimulator: `level.Surfaces.OrderBy(surface => surface.Id, StringComparer.Ordinal).ToArray()` → `level.Surfaces.ToArray()` | `SimultaneousContactBudgetErrorNamesSameSurfaceForAnyOrder`; авторские тесты эту мутацию не ловят |
| GameSession: `< level.KillY \|\| hazardHit)` → `< level.KillY)` | Арена level2, опасность+финиш+монета в одном tick |
| GameSession: убрать `else` перед сбором монет и финиша | Опасность+финиш+монета в одном tick |
| GameSession: убрать `sequence = 0;` в Restart | `SequenceStartsAtOneInEverySessionAndEventKeysNeverRepeat` |
| GameSession: `sequence = nextSequence` до публикации tick | Бюджет контактов, нумерация Sequence |
| HazardCollision: убрать `if (Check(segment, 0d, 1d)) return true;` | `BeamSweepingThroughStationaryBallWithinOneTickIsAHit` |
| HazardMotion: центр без поворота offset | 3 регрессии опасностей |
| GameSession: `level.Hazards.OrderBy(...)` → `level.Hazards.ToArray()` | Нет: эквивалентная мутация — SessionSnapshot сортирует позы сам, а попадание проверяется через `Any` |

Граница CPU прежняя: визуальный вид, ручная проходимость двух локаций, FPS и слышимость
этими тестами не подтверждаются.

## Команды

```sh
dotnet test Platformer.sln -c Release --no-build --filter "FullyQualifiedName~Regression"
```
