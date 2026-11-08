# Интеграционные стыки — задача #10

Проверка совместной работы ввода, меню, LevelFlow, сеанса, представления, звука и запуска
приложения. CPU-сценарии идут через настоящий `GameplayHost` с искусственными вводом,
часами и audio double; рендер и X11-ввод проверяются под Xvfb, как в CI graphics-smoke.
Эти проверки не подтверждают слышимость, вид на реальном мониторе и FPS: это ручная
Linux-приёмка [#11](https://github.com/alken1t15/simple-platformer/issues/11).

Тесты: [драйвер и audio double](../tests/Platformer.Tests/Integration/IntegrationDriver.cs),
[маршрут меню](../tests/Platformer.Tests/Integration/MenuRouteIntegrationTests.cs),
[snapshot/HUD/скин](../tests/Platformer.Tests/Integration/PresentationIntegrationTests.cs),
[звук](../tests/Platformer.Tests/Integration/AudioLifecycleIntegrationTests.cs),
[CLI](../tests/Platformer.Tests/Integration/CliProcessIntegrationTests.cs),
[аудиоресурсы](../tests/Platformer.Tests/Integration/NativeAudioAssetsIntegrationTests.cs).

Тестовый каталог драйвера: level1 — монета на spawn и финиш справа (D); level2 — финиш
справа и неподвижная балка слева (A ведёт к Failed). Прохождение настоящих Training Track
и Turning Maze через тот же host уже покрывает `ProductionRouteTests`.

## Результат по стыкам

Основа: main `cb3c258`, engine `04cd6fd`. Linux, .NET SDK 10.0.403, Release.
SHA проверенного commit указывается в описании PR.

| Стык | Сценарий | Проверка | Итог |
|---|---|---|---|
| Router → Menu → LevelFlow | Start, Pause, Settings из Pause, Back в Pause, Continue без накопленных ticks, LevelResult/Retry, Next, Failed/R, GameComplete, Main menu | `VerticalRouteFromMainMenuThroughBothLevelsEndsInGameComplete` | PASS |
| Router → Menu → LevelFlow | Прямой запуск level2 без итога level1 | `DirectSecondLevelStartsWithoutFirstLevelResultAndCompletesGame` | PASS |
| Двойная активация | Enter+click в одном кадре на Next — ровно один переход, level2 не пропускается | `EnterAndClickInOneFrameOnLevelResultAdvanceExactlyOneLevel` | PASS |
| Двойная активация | Enter+click+R на Retry — один новый сеанс; удержание в Playing не создаёт второй | `EnterAndClickInOneFrameOnFailedCreateExactlyOneRetrySession` | PASS |
| Двойная активация | Клик по Settings и Enter на Continue в одном кадре — одно действие, клик | `ClickOnSettingsWinsOverEnterOnDefaultContinueInSameFrame` | PASS |
| Старые команды | Space, удержанный через финиш, вместе с автоповтором `Pressed` не активирует Next; после отпускания новый фронт активирует | `ActivationKeyHeldThroughLevelEndDoesNotTriggerNext` | PASS |
| Старые команды | Escape+R в одном кадре: Pause раньше Step и раньше R; Escape, удержанный через Continue и финиш, вместе с автоповтором не ставит паузу и не открывает выход | `PauseIsDecidedBeforeTicksSoTerminalFrameCannotSwallowOrDuplicateIt` | PASS |
| Focus | Потеря фокуса в Playing: Pause, отброшенные Space/Shift; активация с зажатыми клавишами не продолжает игру; на экране результата не нажимает Next | `FocusLossInPlayingAndResultClearsPendingCommandsAndNeverResumesByItself` | PASS |
| Snapshot → HUD → Scene | CoinCount, текст HUD и удаление монеты из сцены — в одном кадре; Restart возвращает монету | `CoinCountHudAndCoinVisibilityChangeInTheSameFrame` | PASS |
| Snapshot → HUD | HUD скрыт на экране результата, итог из того же snapshot; после Next HUD показывает level2 | `HudFollowsCurrentLevelAfterNextAndDisappearsOutsidePlaying` | PASS |
| Presenter/settings | Скин и Mute сохраняются при Restart/Next; обе локации используют `GameScenePresenter` и один `Scene`; объекты level1 убраны | `SelectedSkinAndVolumeSurviveRestartNextAndBothLocationsShareOneScene` | PASS |
| Event → clip | Каждое событие звучит один раз через кадры и Pause/Continue; запущенный Shift boost не создаёт voice | `EachEventPlaysItsClipOnceAcrossFramesPauseAndContinue` | PASS |
| Music loop | Один loop, пауза в Pause/Settings/потере фокуса, Continue не удваивает | `MusicLoopIsSingleAndPausedThroughPauseSettingsAndFocusLoss` | PASS |
| SessionId | Новый сеанс после Restart снова звучит с Sequence 1; запоздавшее событие старого SessionId и повтор Sequence не звучат | `NewSessionEventsPlayAfterRestartAndLateOldSessionEventsAreIgnored` | PASS |
| Освобождение | Next оставляет только новый loop; выход в меню закрывает музыку и эффект в полёте; `AudioCoordinator.Dispose` (в Program — `using`) закрывает backend | `NextMainMenuAndExitCloseEveryVoiceAndPlayback` | PASS |
| Ресурсы | Все WAV разбираются и запускаются от `AppContext.BaseDirectory` через публичный `AudioDevice` (настоящий или silent backend); нет файла — `FileNotFoundException` с именем | `BundledAudioLoadsFromBaseDirectoryThroughPublicAudioDevice`, `MissingAudioFileFailsPlaybackConstructionWithFileName`, `BundledAudioShipsLicenseNextToAssets` | PASS |
| CLI | `--help` из чужого cwd: exit 0, usage | `HelpFromForeignWorkingDirectoryExitsZeroWithUsage` | PASS |
| CLI | 8 ошибочных комбинаций и неизвестный скин: exit 1, одна строка `Platformer: …`, до создания окна | `InvalidArgumentsFailFastWithOneReadableLine`, `UnknownSkinFailsBeforeWindowWithNamedValue` | PASS |
| CLI | Запуск без дисплея: ограниченное время, exit 1, последняя строка — диагностика без stack trace (только Linux, иначе Inconclusive) | `LaunchWithoutDisplayEndsBoundedWithNonZeroExitAndDiagnostic` | PASS |
| Графика, Xvfb | `--smoke`, `--play --smoke`, `--presenter --smoke`, `--presenter --skin orange --overview --smoke`, `--legacy --smoke`: 3 кадра, exit 0; ручной замер 0.9–2 с | команды CI graphics-smoke | PASS |
| Камера | Знаки мыши и ограничение pitch, независимость от частоты кадров, стены между шаром и камерой (`CameraRigTests`); мышь в том же кадре и сброс очереди при Pause/Restart/Next (`GameplayFrameRunnerTests`) | существующие тесты, новых нет | PASS |
| X11-ввод, Xvfb | Меню, HUD, камера, focus, auto-jump, launcher из другого cwd (`cwd=/tmp`, gameplay с аудио) | `scripts/check_gameplay_x11.py`, `scripts/check_launch_x11.py` | PASS |
| CI Windows/Linux | compile ×2, documents, graphics-smoke | статус CI в PR | после push: итог в PR |
| Native audio | Слышимость эффектов и музыки на реальном устройстве | ручная Linux-приёмка #11 | SKIP: в окружении нет звуковой карты (ALSA: no card 0) |
| Native overlay/mouse | Реальный монитор, захват мыши, оконный менеджер | ручная Linux-приёмка #11 | SKIP: только Xvfb |
| Защита репозитория | Настройки main и обязательные проверки | — | Не входит: выполняет TL |

Дефектов в модулях #6/#7/#8/#18/#19/#20/#21 не выявлено.

Наблюдения без изменения кода:

- Без звукового устройства gameplay-режим перед ошибкой окна выводит строки ALSA/OpenAL
  (их печатает нативная библиотека), затем `Audio: silent mode …`. Exit code и итоговая
  диагностика корректны; для чистого запуска (#12) это ожидаемый шум окружения.
- `--help` распознаётся только как единственный аргумент; `--help --smoke` — ошибка аргументов.
- Escape/R в кадре обрабатываются раньше Step: кадр паузы не продвигает сеанс,
  поэтому финиш и пауза в одном кадре не конфликтуют.

## Чувствительность

Мутации в рабочей копии (в commit не входят): правка, прогон
`--filter "FullyQualifiedName~Platformer.Tests.Integration"`, `git checkout` файла.

| Файл: мутация | Упавшие тесты |
|---|---|
| AudioCoordinator: `music ??= PlayMusic()` → `music = PlayMusic()` | 3: loop, освобождение, новый сеанс |
| AudioCoordinator: убрать проверку `SessionId` события | Запоздавшее событие старого сеанса |
| AudioCoordinator: убрать проверку `Sequence <= sequence` | Повторная доставка Sequence |
| LevelFlow: убрать `stopGameplayAudio()` из перехода | Выход в меню закрывает эффекты |
| InputRouter: `Clear` не блокирует удержанные кнопки | 3: удержанные Space и Escape, Focus |
| GameplayHost: убрать `RefreshView` в `Advance` (View отстаёт на кадр) | 10 сценариев меню, HUD и звука |
| Program: печатать исключение целиком | Все CLI-сценарии с ошибкой |

Эквивалентные мутации (не ловятся, защита дублируется в другом слое): InputRouter без
повторной блокировки после фокуса — runner очищает router при смене фокуса; host без
применения скина — presenter применяет скин в Start/Restart/Next; `OpenSettings` со
снятием паузы — host снова ставит паузу через `SetScreen`; выбор опции по
`selectedIndex` вместо `hit` — клик уже переносит выбор на `hit`.

## Команды

Полный набор проверок — раздел «Работа и проверки» в [AGENTS.md](../AGENTS.md)
(`pwsh -NoProfile -File scripts/Verify.ps1`) и `node --test scripts/review-policy.test.cjs`.
Стыки этой задачи:

```sh
dotnet test Platformer.sln -c Release --no-build --filter "FullyQualifiedName~Platformer.Tests.Integration"
LIBGL_ALWAYS_SOFTWARE=true timeout 60s xvfb-run -a dotnet run --project src/Platformer.App -c Release --no-build -- --smoke
timeout 60s xvfb-run -a -s '-screen 0 1280x800x24' python3 scripts/check_gameplay_x11.py
timeout 30s xvfb-run -a python3 scripts/check_launch_x11.py
```
