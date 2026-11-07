# Меню и HUD (#19): оконная интеграция

MenuController, MenuView, адаптивная раскладка и HudModel подключены к GameplayHost (#6)
и публичному overlay движка. Обычный запуск и `--play` открывают графический Main Menu;
это тот же Engine/Scene lifecycle, что и игра. Движок закреплён на принятом
`936b71a7393ef52b7d0d3b277af6b4c287be92c6`, upstream
[PR #54](https://github.com/alken1t15/simple-3d-engine/pull/54), игровой
[PR #39](https://github.com/alken1t15/simple-platformer/pull/39).

UI/HUD работает с настоящими LevelFlow, GameSession и Presenter. Каталог поставки
содержит Training Track и Turning Maze; WAV/AudioCoordinator подключены.
Слышимый звук и полная приёмка контента ещё требуют #21/QA. Авторские CPU-тесты/снимки с двумя контролируемыми уровнями
не подменяют настоящую Turning Maze. [Объём](project-scope.md), [контракт](app-engine-contract-v1.md#app).

## Поведение UI

- Все девять состояний используют единственный `LevelFlow.Screen`:
  MainMenu, LevelSelect, Settings, SkinSelect, Playing/HUD, Paused, Failed,
  LevelResult и GameComplete. Меню не создаёт второго GameSession или своей таблицы монет.
- Up/Down/Tab меняют выбор; Enter/Space активируют его. Mouse hover следует за движением
  указателя; неподвижная мышь не отменяет клавиатурный выбор. Клик имеет приоритет над
  Enter в том же кадре и выполняет ровно одно действие. Есть normal/focus/hover/pressed данные.
- Settings меняет громкость шагом 5% с клавиатуры либо кликом по шкале, отображает
  процент и mute. Callback получает эффективную громкость при создании контроллера,
  до первого Start, и сразу после изменения. Mute сохраняет предпочитаемую громкость.
- SkinSelect показывает цветовые образцы из материалов `GameSkins`, отдельно хранит
  предварительный выбор и применяет его кнопкой Select. Back отменяет неприменённый выбор.
  Start/Restart/Next используют выбранный скин через существующий Presenter.
- HUD читает имя уровня, CoinCount/число монет уровня и boost timers из принятого snapshot.
  Active имеет приоритет над cooldown; время отображается с точкой. Подсказка управления
  показывается первые 8 секунд симуляции: это параметр оформления этой реализации.
- LevelSelect строит карточки по переданному каталогу. Имена `level1`/`level2` — Training Track /
  Turning Maze; тесты с двумя уровнями не означают, что игровые данные level2 уже поставлены.
  Недостающая локация не заменяется копией level1.

## Переходы

| Экран / действие | Результат |
|---|---|
| MainMenu / Play | Start первого уровня каталога, новый маршрут |
| MainMenu / Levels, Settings, Skins | Соответствующее подменю |
| MainMenu / Escape | Без действия; закрывает только Exit |
| LevelSelect / карточка, Start | Выбор ID, затем Start этого уровня |
| LevelSelect / Back или Escape | Возврат в MainMenu, Failed либо GameComplete, откуда открыли выбор |
| Settings / Back или Escape | Возврат в MainMenu либо Paused; настройки уже применены |
| SkinSelect / Select, Back | Применить скин; вернуться в MainMenu |
| Playing / Escape, R | Pause либо Restart; меню не выполняет Step |
| Paused / Continue или Escape | Continue с очисткой отложенного ввода |
| Paused / Restart, Settings, MainMenu | Соответствующий переход; Settings сохраняет audio pause |
| Failed / Retry или R | Новая попытка текущего уровня |
| Failed / LevelSelect | Выбор уровня; Back сохраняет прежнее поражение |
| Failed / MainMenu или Escape | Снять сеанс и прежний маршрут |
| LevelResult / Next | Следующий уровень либо GameComplete после последнего |
| LevelResult / Retry | Повтор текущего уровня, без повторного начисления монет |
| LevelResult / MainMenu или Escape | Снять сеанс и маршрут |
| GameComplete / LevelSelect | Выбрать новый маршрут; Back сохраняет итог прежнего |
| GameComplete / MainMenu или Escape | Вернуться в главное меню |
| Потеря фокуса в Playing | Paused; активация окна сама не вызывает Continue |

`OpenLevelSelect`, `OpenSettings`, `OpenSkins`, `Back` добавлены в LevelFlow.
Недопустимые переходы отклоняются до reset callbacks. Переходы в подменю и обратно
очищают отложенный ввод, сохраняя сеанс и audio pause. Остальные callbacks и правила
подготовки/ошибок — [GameSession/LevelFlow](game-session.md#подключение-адаптеров).

## Связь с host, overlay и аудио

Program один раз читает engine.Input в onUpdate и NativeInputAdapter переносит его
в AppInputSnapshot. GameplayFrameRunner сначала обрабатывает R/Escape/focus, затем
mouse look и фиксированные ticks. MenuController получает уже маршрутизированный
InputFrame; временные клавиатурные переходы прежнего стенда #6 удалены.

Flow.Screen единственный источник состояния. После Menu.Handle и успешных ticks
host синхронизирует runner с экраном, выполняет reset/rearm, обновляет Camera/Scene
и строит MenuView. Start/Restart/Next сбрасывают камеру, Continue сохраняет ориентацию.
Terminal tick запрашивает RequestClear: catch-up прекращается после него.

На переходе из Captured в меню первая Normal позиция указателя задаёт новую базу
через ResetPointerPosition, поэтому изменение системы координат не переключает выбор
с Continue на другую кнопку. Последующее движение либо новый клик работает нормально.
Потеря фокуса ставит паузу; автоматического Continue нет.

MenuOverlayAdapter переводит панели/текст MenuView в immutable OverlayFrame и Program
вызывает SetOverlay на потоке Create. Размер берётся из WindowBounds.Width/Height
(**logical client area**); те же размеры и Bounds используются для hit-test и рисования.
FramebufferSize в UI-геометрию не подставляется. Нулевой размер даёт пустой UI-кадр.
Единственная композиция рисуется поверх 3D; отдельного HUD у Presenter нет.

Program создаёт один AudioDevice до MenuController. Callback с первого меню задаёт
MasterVolume; volume/mute действуют сразу на устройство, выбранная громкость сохраняется
в UserSettings. В smoke устройство не создаётся. Игровые WAV, голоса, pause/resume
AudioCoordinator и проверка слышимости остаются в #21; отсутствие клипов не считается
выполненным звуковым сопровождением. AudioDevice освобождается при завершении приложения.

UI использует английский ASCII согласно атласу движка; скины представлены цветными
образцами настоящих материалов GameSkins. Проекция/матрицы/GL остаются у движка.

## Проверки и оставшаяся передача

24 новых авторских CPU-теста в MenuControllerTests, MenuLayoutTests, HudModelTests и
LevelFlowMenuTests проверяют все девять состояний, реальные Session/Presenter,
Retry/Next/прямой level2 без двойного счёта, начальную громкость/mute, скин,
приоритет mouse+Enter, удержания через InputRouter, фокус, сохранение audio pause,
отклонение неверных переходов и ошибки reset callback.
Раскладка проверяется на 1280×720, 640×480, 320×200, 1920×400, 400×1200;
клик после resize и положение slider используют выданную геометрию.
Отдельная регрессия воспроизводила ложное движение неподвижной мыши после смены экрана:
позиция теперь сохраняется отдельно от hover, поэтому Enter сохраняет клавиатурный выбор.

Для полного закрытия #19 ещё требуются:

- данные Turning Maze из раннего PR #21, игровые WAV/AudioCoordinator и слышимый звук;
- ручная Linux-проверка mouse/keyboard/focus/resize и всех доступных экранов;
- учёт ограничения native HiDPI, применимое ревью/CI и принятие игровой интеграции.

Оконный прогон и снимки описаны в [input-camera-testing.md](input-camera-testing.md).
CPU-части #6/#19 проверяются совместно; данные результатов в специальных визуальных
fixtures нужны для проверки UI, а не для приёмки прохождения двух игровых локаций.
