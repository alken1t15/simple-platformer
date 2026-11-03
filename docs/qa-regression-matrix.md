# Независимые CPU-регрессии — задача #9

Матрица связывает требования BA и наблюдаемые сценарии [приёмки](acceptance.md)
с модулями Core и независимыми тестами в `tests/Platformer.Tests/Regression`.
Эталоны выводятся из геометрии раскладки и контракта [Core v1](core-contract-v1.md),
а не из копии решателя. CPU-тесты не доказывают визуальный вид, ручную проходимость,
FPS и слышимость; для них — QA-чек-лист на Linux (#11).

Состояние на момент добавления матрицы: модули #4 и #5 реализованы и покрыты; `GameSession`,
`HazardMotion` и `HazardCollision` (#8, #20) и вторая локация (#21) ещё не существуют.
Для них ниже зафиксирован тест-дизайн; задача #9 не закрывается до их прогона.

В #8 добавлены авторские [проверки GameSession/LevelFlow](game-session.md#проверки).
Они не заменяют независимые регрессии этой матрицы; hazard-интеграция ещё ожидает #20.

## Реализованные проверки

Файлы: [физика](../tests/Platformer.Tests/Regression/PhysicsRegressionTests.cs),
[уровень](../tests/Platformer.Tests/Regression/LevelRegressionTests.cs),
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

## Результаты прогона

Окружение: Linux, контейнер `mcr.microsoft.com/dotnet/sdk:10.0` (SDK 10.0.403), Release, без дисплея и OpenGL.
Проверяется HEAD ветки `feature/9-independent-regressions` поверх main `c8f6032`;
SHA итогового commit и вывод фиксируются в описании PR. Весь набор: 86 PASS, 0 FAIL, 0 SKIP,
из них 17 — регрессии #9.

Дефектов в реализованных модулях #4/#5 не выявлено. Дефект, найденный позже, оформляется
отдельной строкой: минимальное воспроизведение, тест, issue/PR исправления.

Чувствительность проверена мутациями `src/Platformer.Core/BallSimulator.cs` в рабочей копии
(в commit не входят): правка, `dotnet test ... --filter "FullyQualifiedName~Regression"`,
обратное применение той же правки.

| Мутация (было → стало) | Упавшие регрессии |
|---|---|
| `if (SphereGeometry.Sweep(` → `if (false && SphereGeometry.Sweep(` | 7: тонкая плита, повтор маршрута, Path, бюджет, одновременный контакт, монеты, финиш |
| `BoostCooldownSeconds - excess` → `BoostCooldownSeconds - FixedStepSeconds - excess` | Boost-тест |
| `Cos(settings.MaxSlopeRadians)` → `Cos(settings.MaxSlopeRadians + 0.09f)` (≈55°) | `SupportFlipsAroundConfiguredMaxSlope(51)` |
| `level.Surfaces.OrderBy(surface => surface.Id, StringComparer.Ordinal).ToArray()` → `level.Surfaces.ToArray()` | `SimultaneousContactBudgetErrorNamesSameSurfaceForAnyOrder`; авторские тесты эту мутацию не ловят |

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
