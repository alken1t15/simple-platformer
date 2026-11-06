# Ball Adventure: контракты App и расширений движка v1

Контракт задачи #3 принят в PR #22 (02.11.2026); реализация остаётся задачами команды. Контракт будущей реализации, не заявление о текущих возможностях.
Core определён в [core-contract-v1.md](core-contract-v1.md); принятый engine gitlink — 7d09726.
В #6 реализованы [ввод/runner/камера и оконный host](input-camera.md): NativeInputAdapter
переносит публичный InputSnapshot в CPU-границу AppInputSnapshot, InputRouter имеет оба overload.
`--play` связывает GameSession/LevelFlow с окном и курсором; AppScreen используется из LevelFlow.
Полные меню/HUD/звук и вторая локация ещё требуют отдельных задач.
В #7 реализованы GameScenePresenter, SkinDefinition/GameSkins и контрактный
[3D-preview](scene-presenter.md). В #8 добавлены LevelFlow, AppScreen и UserSettings —
[таблица переходов и callbacks](game-session.md#каталог-и-app). Это CPU-модель с Presenter,
оконный стенд подключён в #6, полный игровой маршрут ещё не реализован. Опасности #20 подключены к сеансу и Presenter;
меню/ввод/звук интегрируются владельцами #6/#19/#21.

<a id="app"></a>

## App: команды, шаг и камера

Все методы App и публичные Engine методы вызываются с потока Create. Core не знает Engine3D.

| Тип / метод | Контракт |
|---|---|
| `InputFrame InputRouter.Map(InputSnapshot input, AppScreen screen, float yawRadians)` | Возвращает ненулевое значение InputFrame для активного экрана; не двигает шар |
| `FixedStepRunner.Advance(double frameSeconds, GameCommand latestHeld, Action<GameCommand> step)` | Возвращает число ticks; хранит только последние удержания и OR фронтов Jump/Boost; фронты потребляются первым tick, последующие их не повторяют |
| `FixedStepRunner.Clear()` | Сбрасывает аккумулятор, удержания и фронты вне Step при pause/focus/restart/смене уровня |
| `FixedStepRunner.RequestClear()` | Callback LevelFlow: внутри Step откладывает очистку до конца текущего tick и прекращает catch-up; вне Step вызывает Clear |
| `CameraPose CameraRig.Update(Vector2 relativeMouseDelta, Vector3 target, LevelDefinition level, float frameSeconds)` | Один вызов за render frame в Running; yaw затем GameCommand, pitch clamped, позиция с obstruction radius |
| `LevelFlow.Start(string levelId)` | Новые GameSession/SessionId и Scene; никакого переноса coins/velocity; настройки App сохраняются |
| `LevelFlow.Next()` | Допустимо после LevelComplete; следующий ID из каталога, после последнего — GameComplete |
| `LevelFlow.Restart()` | Restart текущего GameSession, очищает runner/mapper/audio; остаётся тот же level ID |
| `GameScenePresenter.Apply(SessionSnapshot snapshot, SkinDefinition skin)` | Обновляет transforms/visibility; не мутирует Core; spheres/boxes читаются из общего LevelDefinition |
| `void AudioCoordinator.Consume(Guid sessionId, IReadOnlyList<GameEvent> events)` | Дедуп по SessionId/Sequence, событие играет один раз; смена SessionId останавливает старые gameplay sources |

`AppScreen`: MainMenu, LevelSelect, Settings, SkinSelect, Playing, Paused, Failed,
LevelResult, GameComplete. Core SessionPhase не содержит эти UI-состояния.
UserSettings: MasterVolume∈[0,1], Muted, выбранный SkinId; всего минимум два skin IDs.
SkinDefinition: Id, DisplayName, материал/опциональный asset ID; не содержит Radius/Mass/Collider.
Сначала достаточно цветных материалов, отдельная новая текстура необязательна.
Минимальные SkinId: classic-blue и orange; материалы различаются синим/оранжевым базовым цветом.
GameScenePresenter (#7) скрывает собранные ID тем же Apply и отвечает за 3D-объекты.
UI/HUD (#19) читает тот же snapshot: CoinCount / level.Coins.Count, boost timers; он владеет
раскладкой и overlay. Presenter не создаёт второй HUD. SkinDefinition использует Engine3D.Core.MaterialData в App.
CameraPose — App readonly value (Vector3 Position, Vector3 Target, float YawRadians, float PitchRadians);
Update возвращает позу, из её YawRadians в этом же кадре строится GameCommand.

### Input и FixedStep

Результат маршрутизации — value types в App (не в Core и не в движке):

```csharp
public readonly record struct InputFrame(GameCommand Gameplay, bool RestartPressed,
    bool PausePressed, MenuInput Menu);
public readonly record struct MenuInput(int HorizontalStep, int VerticalStep,
    bool ActivatePressed, bool BackPressed, bool PointerPressed, Vector2 PointerPosition);
```

В Playing Menu=default; в меню Gameplay=default и PausePressed=false.
RestartPressed разрешён для Playing/Paused/Failed; в остальных экранах false.
PausePressed — Escape только в Playing. Menu.BackPressed — Escape в меню;
ArrowUp/Down/Tab дают VerticalStep −1/+1/+1, Left/Right дают HorizontalStep −1/+1,
противоположные фронты отменяются. PointerPressed — фронт левой кнопки, позиция берётся
из того же snapshot. Menu.ActivatePressed отражает Enter/Space; выбор цели и подавление
дублирующего click выполняет UI selection model ниже. При IsFocused=false весь InputFrame=default.
R/Escape обрабатываются до runner; после перехода текущий InputFrame не передаётся в Step.

W/S задают forward +1/−1, D/A right +1/−1, противоположные направления отменяются.
При yaw=0 forward=(0,0,−1), right=(1,0,0).
При yaw θ forward=(−sinθ,0,−cosθ), right=(cosθ,0,−sinθ).
Сначала обновить yaw текущим mouse delta, затем нормализовать WASD и преобразовать в world X/Z.
Pitch не влияет на направление движения. R и Escape обрабатываются App как переходы,
не повторяются в каждом tick; LeftShift/RightShift агрегируются в один BoostPressed.

Mouse delta измеряется в логических пикселях client area; положительный X — поворот вправо:
yaw -= delta.X*0.003 рад/пиксель, pitch += delta.Y*0.003.
Вертикаль без инверсии: мышь вверх (delta.Y < 0) поднимает взгляд, мышь вниз опускает.
Положение камеры: target + (sin(yaw)*cos(pitch), sin(pitch), cos(yaw)*cos(pitch))*distance.
Default pitch=0.4 рад, clamp[-0.35,1.2], distance6, target=ball.Center+(0,0.3,0).
Pitch меняет высоту камеры, соглашение forward соответствует взгляду камеры на target.
Wall obstruction — sphere cast radius0.2 вдоль target→desired camera; позиция ставится до первого
контакта с зазором0.05. Если target внутри препятствия, fallback возле шара и диагностируемое
ограничение расстояния; NaN/нулевой view direction не допускаются. Восстановление distance
плавное по frameSeconds, не вмешивается в скорость шара.

Advance принимает finite frameSeconds≥0; больше0.1 ограничивается0.1. Negative/NaN/Infinity —
ArgumentOutOfRangeException до изменения состояния runner. Шаг — тот же settings.FixedStepSeconds,
не копия константы. Максимум8 ticks. Если после8 остаётся ≥dt, отбрасываются полные лишние ticks,
дробный остаток <dt сохраняется; счёт DroppedSeconds доступен для диагностики.
В кадре без ticks сохраняются Jump/Boost, но удержания заменяются свежими.
Clear вызывается до передачи нового snapshot при смене focus/screen, поэтому прежние фронты
не попадают в новый сеанс. Pause не накапливает время; пользователь продолжает явным Continue.
Исключение step прекращает обработку, новый кадр не продолжает частично проваленный сеанс.

## Меню и переходы

- MainMenu: Play (level1), LevelSelect (оба ID), Settings, Skins, Exit.
- LevelSelect: начать любой из двух уровней, Back.
- Settings: master slider0–100%, mute, Back; применяется немедленно, не требует Apply.
- Skins: два видимых образца, Select, Back; сохраняется между уровнями в процессе.
- Playing: HUD collected/total, имя уровня, boost ready/remaining/cooldown; Escape→Paused, R→Restart.
- Paused: Continue, Restart, Settings, MainMenu. Возврат из настроек возвращает Paused.
- Failed: Retry, LevelSelect, MainMenu. R действует как Retry, Escape→MainMenu.
- LevelResult: число монет, Next, Retry, MainMenu; после последнего Next→GameComplete.
- GameComplete: общий итог по текущему последовательному прохождению, LevelSelect, MainMenu.
  При прямом выборе level2 результат учитывает только начатый маршрут, не вымышленные монеты level1.

Mouse/клавиатура используют один UI selection model. ArrowUp/Down/Tab — выбор элемента,
Left/Right — slider с шагом5%, Enter/Space или LeftMousePressed — активация.
Если пришли и Enter, и mouse в одном frame, действие выполняется один раз для выбранной цели;
приоритет mouse target, затем keyboard selection. Hit-test использует ту же logical geometry,
что и overlay. Escape возвращает предыдущий экран; из MainMenu открывает подтверждение выхода
или действует только кнопка Exit (для минимума — только Exit).
В menu cursor Normal; в Playing Captured только после явного start/continue.
Focus loss→Paused, release cursor и pause audio; focus gain не продолжает игру сам.

<a id="input"></a>

## Engine input/cursor: задача #16

Namespace Engine3D.OpenGL, без публичных OpenTK enum/handles.

- Существующие численные значения InputKey A..Escape сохраняются. Добавить W,S,LeftShift,
  RightShift,Enter,Up,Down,Tab; старые Left/Right используются и для меню.
- `MouseButton`: Left, Right, Middle.
- `PointerSnapshot`: immutable `Vector2 Position`, `Vector2 Delta`, `bool IsInsideClient`,
  `IsDown/WasPressed/WasReleased(MouseButton)`; default пустой, invalid enum→ArgumentOutOfRangeException.
- `InputSnapshot.Pointer` добавляется к существующим клавишам/IsFocused. Не меняет сохранённый
  snapshot после следующих frames. Position/Delta в logical client pixels; framebuffer/DPI scaling
  скрыт движком и применяется согласованно overlay. Capture delta не умножается на dt.
- `CursorMode`: Normal, Captured; `Engine.CursorMode` get/set на owner thread, до Run или в update.
  После Run set→InvalidOperationException, после Dispose→ObjectDisposedException.
  При focus loss actual mode Normal; app получает focus=false и явно решает повторный capture.
- В snapshot focus loss очищает кнопки/дельту; первое событие после mode/focus change не даёт
  скачка мыши. Уже зажатая клавиша/кнопка при активации требует release/new press.
- `Engine.Input` доступен только в onUpdate; существующая Run(scene,camera,Action<float>?) совместима.

<a id="overlay"></a>

## Engine overlay: задача #17

Универсальные immutable данные, реализованные в закреплённом движке из
[upstream PR #53](https://github.com/alken1t15/simple-3d-engine/pull/53):

```csharp
public readonly record struct UiRect(float X, float Y, float Width, float Height);
public readonly record struct OverlayPanel(UiRect Bounds, ColorRgba Color);
public readonly record struct OverlayText(Vector2 Position, string Text, float PixelHeight, ColorRgba Color);
public readonly record struct OverlayImage(UiRect Bounds, TextureData Texture, ColorRgba Tint);
public sealed class OverlayFrame
{
    public OverlayFrame(IReadOnlyList<OverlayPanel> panels,
        IReadOnlyList<OverlayImage> images, IReadOnlyList<OverlayText> texts);
    public ReadOnlySpan<OverlayPanel> Panels { get; }
    public ReadOnlySpan<OverlayImage> Images { get; }
    public ReadOnlySpan<OverlayText> Texts { get; }
}
// Engine: public void SetOverlay(OverlayFrame? frame);
```

SetOverlay допустим до Run и внутри onUpdate на owner thread. null очищает overlay.
Frame копирует входные коллекции, изображения используют immutable TextureData.
Координаты от левого верхнего угла logical client area, Y вниз; width/height≥0,
PixelHeight>0, позиции конечны. Вне экрана clipping, не исключение.
Порядок слоёв: panels→images→texts, внутри списка порядок сохраняется. Для меню этого достаточно;
игра сама раскладывает элементы и выполняет hit-test. Никаких MenuScreen в engine API.
Шрифт — один встроенный лицензированный atlas с ASCII: первое меню на английском,
чтобы не обещать Unicode; неподдерживаемый символ рисуется как '?'. Источник/лицензия в THIRD_PARTY.
Overlay рисуется после3D, корректно возвращает depth/blend/viewport; общий размер framebuffer
и logical client dimensions применяются только на стороне renderer. При свёрнутом окне кадр не рисуется.
Refresh рисует последний установленный слой без дополнительного onUpdate; null очищает его.
API доступен, но игровые layout/hit-test/экраны/HUD остаются #19. Проверки scale=1/2 на GPU
не заменяют пока не выполненный native HiDPI-прогон с framebuffer≠client.

<a id="audio"></a>

## Engine audio: задача #18

Универсальный backend проверяется на Linux. Основная форма API — отдельный сервис без связи с Scene:

```csharp
public sealed class AudioOptions { public bool AllowSilentFallback { get; init; } = true; }
public sealed class AudioDevice : IDisposable
{
    public static AudioDevice Create(AudioOptions options);
    public bool IsSilent { get; }
    public string? Diagnostic { get; }
    public float MasterVolume { get; set; } // 0..1
    public AudioClip LoadWave(string path);
    public AudioVoice Play(AudioClip clip, bool loop = false, float volume = 1f);
    public void StopAll();
}
public sealed class AudioClip : IDisposable { }
public sealed class AudioVoice : IDisposable
{
    public void Pause();
    public void Resume();
    public void Stop();
}
```

Namespace Engine3D.Audio в отдельном проекте без Core-зависимости; UI/graphics
не получают прямые AL вызовы. Backend выбирается и проверяется spike, публичный namespace
и формы методов сохраняются. Инициализация не происходит автоматически при Engine.Create.
AudioDevice создаётся App на том же основном потоке, ownerthread проверяется до backend вызова.
AppStartup создаёт один AudioDevice и загружает clips до показа MainMenu; затем создаёт
AudioCoordinator. Устройство живёт до завершения приложения, не пересоздаётся при Next/Restart.
Начальная MasterVolume берётся из UserSettings; настройки доступны уже в первом меню.
PCM WAV: RIFF/WAVE, integer16bit little-endian, mono/stereo, sample rate8–48kHz; неподдерживаемый
формат→InvalidDataException с path, missingfile→FileNotFoundException.
Короткий фон тоже WAV/loop, streaming не требуется. Одновременно минимум8 voices, повторный Play
возвращает отдельную voice; при лимите32 сначала удаляется самая старая закончившаяся voice,
если свободной нет — самая старая не-loop; при32 активных loop→InvalidOperationException.
MasterVolume/voice gain finite[0,1]; итог — произведение, mute App задаёт эффективный gain0,
сохранив предпочитаемую громкость пользователя. Изменение действует на уже играющие voices.

AllowSilentFallback=true и нет устройства: IsSilent=true/Diagnostic, valid Play/Stop/volume остаются
доступны без звука; ошибки WAV/аргументов всё равно проверяются. false→InvalidOperationException.
Это режим работоспособности, не успешная проверка звука. Audio clip принадлежит своему device;
чужой/освобождённый clip отклоняется. Clip.Dispose останавливает связанные voices,
Device.Dispose останавливает/удаляет voices, затем buffers, context/device; повторный Dispose безопасен.
Stop/Dispose voice повторяемы; после Dispose остальные методы→ObjectDisposedException.
App удерживает одну loop music voice, при pause приостанавливает её, resume не создаёт вторую.
Нативные пакеты и лицензии подтверждаются spike до принятия реализации #18.

## Границы готовности

InputSnapshot.Pointer, расширенные InputKey, CursorMode и Engine3D.Audio существуют в закреплённом
`7d09726`; native input/cursor adapter подключён в #6, игровой audio adapter ещё впереди.
Overlay из раздела UI принят в upstream #53 и game gitlink PR #35; меню/HUD подключаются в #19.
Настоящий HiDPI с framebuffer≠client остаётся непроверенным в #16/#17.
Для следующих расширений нужны отдельный upstream PR, engine regression tests и точный game gitlink.
QA проверяет no-device отдельно от слышимости;
никакая UI/audio/input реализация не должна зависеть от старой рабочей копии проекта.
