# Независимые CPU-регрессии — задача #9

Матрица связывает требования BA и наблюдаемые сценарии [приёмки](acceptance.md)
с модулями Core и независимыми тестами в `tests/Platformer.Tests/Regression`.
Эталоны выводятся из геометрии раскладки и контракта [Core v1](core-contract-v1.md),
а не из копии решателя. CPU-тесты не доказывают визуальный вид, ручную проходимость,
FPS и слышимость; для них — QA-чек-лист на Linux (#11).

Состояние на момент добавления матрицы: модули #4 и #5 реализованы и покрыты; `GameSession`,
`HazardMotion` и `HazardCollision` (#8, #20) и вторая локация (#21) ещё не существуют.
Для них ниже зафиксирован тест-дизайн; задача #9 не закрывается до их прогона.

## Реализованные проверки

Файлы: [физика](../tests/Platformer.Tests/Regression/PhysicsRegressionTests.cs),
[уровень](../tests/Platformer.Tests/Regression/LevelRegressionTests.cs).

| BA / сценарий | Модуль | Тест | Эталон и допуск |
|---|---|---|---|
| BA-07, A07 | SphereGeometry, BallSimulator | `FlatTopFaceGivesUpNormalForAnyEquivalentBoxOrientation` | Одна плита тремя ориентациями OBB: normal +Y (1e-5), центр в [r−PenetrationSlop, r+SkinWidth+PositionEpsilon], 1 с покоя без дрейфа (1e-6) |
| BA-07, A07 | BallSimulator, Training Track | `TrainingTrackRampNormalAndUphillDirectionFollowTwentyDegreeGeometry` | Рампа из раскладки (2,0)→(5,3·tan20°): normal (−sin20°, cos20°, 0); vy/vx = tan20° (1e-4); |v| ≤ MoveSpeed; расстояние до плоскости в зазоре опоры |
| BA-07, A07 | BallSimulator | `SupportFlipsAroundConfiguredMaxSlope` | MaxSlope 50°: 49° — опора, 51° — нет |
| BA-02, A03 | BallSimulator | `JumpApexMatchesClosedFormSemiImplicitEulerSum` | H = dt·(N·v0 − g·dt·N(N+1)/2), N = ⌊v0/(g·dt)⌋; default 0.975 точно; допуск 1e-4 (накопление float за ≤80 ticks); посадка через 2N ticks ±2; повторный фронт Space в воздухе не даёт прыжка. Отдельно `DefaultJumpApexIsExactlyPointNineSevenFive` |
| BA-03, A04 (часть Core) | BallSimulator | `BoostPressEveryTickActivatesOncePerDurationPlusCooldownAndCapsSpeed` | Фронт BoostPressed каждый tick 10 с: активации на ticks 0, 480, 960; скорость ≤ 8.1, на 119-м tick 8.1, на 479-м 4.5 (1e-4); boost и cooldown не пересекаются. Удержание клавиши → один фронт проверяет App/runner, не Core |
| BA-02/07, A09 (частично) | SphereGeometry sweep | `MaxFallSpeedNeverTunnelsThroughThinPlateAtAnyTickPhase` | Плита 0.002, падение 30: 37 фаз старта, шар нигде не ниже r−PenetrationSlop, посадка на r (SkinWidth+PositionEpsilon). Горизонтальный boost и рёбра/углы — авторские `MaximumConfiguredBoostCannotTunnelThroughThinWall` и SphereGeometryTests |
| Контракт Step | BallSimulator | `TrainingTrackRouteReplayIsBitIdenticalForAnySurfaceOrder` | Маршрут с рампой, прыжком, boost: побитовое совпадение Ball и Path при повторе, новом симуляторе и 4 перестановках surfaces |
| Контракт Path (вход #8/#20) | BallSimulator | `RoutePathIsContinuousCoversWholeTickAndNeverEntersSurfaces` | Path непрерывен (PositionEpsilon), доли покрывают [0,1] без зазоров, 9 точек каждого отрезка вне всех OBB |
| Контракт ошибок (прокси для Snapshot #8) | BallSimulator | `EarlierResultsSurviveLaterStepsAndFailedTicks` | Исчерпание MaxContactIterations: InvalidOperationException с ID `wall`; прежний результат и Path не меняются, Path read-only, повтор совпадает |
| BA-08, A10 | LevelDefinition, Training Track | `EveryTrainingTrackCoinIsTouchedByBallRestingBelowIt` | Шар, опущенный под монету по реальной физике, касается её: расстояние ≤ r + CoinRadius. Число монет из данных уровня |
| BA-06, A13 | Training Track | `BallRestingOnFinishPlatformOverlapsFinishVolume` | Шар на финишной площадке пересекает Finish; spawn — нет |
| Неизменяемость уровня | LevelDefinition | авторский `AllInputListsAreCopiedAndCannotBeMutatedThroughReturnedInterfaces` | Покрыто #4; независимый дубль не добавлялся |
| Неизменяемость уровня | TrainingTrack | `TrainingTrackFactoryReturnsIndependentEquivalentLevels` | Два вызова фабрики — разные эквивалентные экземпляры |

## Результаты прогона

Окружение: Linux, контейнер `mcr.microsoft.com/dotnet/sdk:10.0` (SDK 10.0.403), Release, без дисплея и OpenGL.
База — main `c8f6032`: весь набор 85 PASS, 0 FAIL, 0 SKIP, из них 16 — регрессии #9.

Локальная проверка чувствительности (мутации не коммитятся, исходник затем восстанавливается
через `git checkout`): правка в `src/Platformer.Core/BallSimulator.cs`, затем фильтр `Regression`.

| Мутация | Упавшие регрессии |
|---|---|
| Sweep отключён: `if (false && SphereGeometry.Sweep(...))` | 6: тонкая плита, повтор маршрута, Path, монеты, финиш, бюджет |
| Cooldown на tick короче: `BoostCooldownSeconds - FixedStepSeconds - excess` | Boost-тест |
| Порог опоры `Cos(MaxSlopeRadians + 0.09f)` (≈55°) | `SupportFlipsAroundConfiguredMaxSlope(51)` |

## Тест-дизайн, ожидающий реализации

| BA / сценарий | Модуль | Планируемая проверка |
|---|---|---|
| BA-09, A11 | HazardCollision (#20) | Неподвижный шар, по которому проходит балка за один tick: обе позы в endpoints вне шара, `IntersectsPath` = true (swept hazard). Эталон — пример acceptance: offset 2, halfExtents (1,0.1,0.1), ω 2 рад/с, радиус конца ≈3.01, путь за tick ≈0.0502 |
| BA-09, A11 | HazardMotion (#20) | `Evaluate` при угле 0, π/2, большом времени: центр = pivot + Rotate(offset); определение не меняется |
| BA-09 | HazardCollision (#20) | Невалидный Path (зазор долей, разрыв End/Start, пустой) — ArgumentException; бюджет — InvalidOperationException с hazard ID |
| BA-08, A10 | GameSession (#8) | Монета на пути внутри tick считается один раз, повторное прохождение не даёт счёта; тонкая монета между endpoints засчитывается по Path |
| BA-09, A11 | GameSession (#8) | Опасность и Finish в одном tick → Failed, без LevelCompleted и монет этого tick |
| BA-04, A12 | GameSession (#8) | Restart: новый SessionId, Tick 0, пустая очередь, монеты/boost/hazard time сброшены; старый Snapshot не изменился |
| BA-04 | GameSession (#8) | Sequence событий начинается с 1 в каждом SessionId; ключ (SessionId, Sequence) не повторяется |
| Контракт ошибок | GameSession (#8) | Исчерпание бюджета: Snapshot, Tick, счёт и очередь событий без частичных изменений |
| BA-06/07, A13 | Вторая локация (#21) | Те же проверки монет/финиша/рампы на данных level2, число монет из данных |
| Независимость порядка | GameSession (#8) | Перестановка surfaces/coins/hazards не меняет события и Snapshot |

## Команды

```sh
dotnet test Platformer.sln -c Release --no-build --filter "FullyQualifiedName~Regression"
```
