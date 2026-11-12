# Инструкции агентам

Область действия — весь репозиторий игры. Явные указания пользователя имеют приоритет.
Общение и документы — на русском; имена типов и кода — на английском.

## Контекст

Ball Adventure — 3D-платформер на C#/.NET 10: управляемая сфера, движение в X/Z,
Y вверх, мышь поворачивает камеру от третьего лица. Эта принятая концепция заменяет прежний 2D-план.
Начни с README, CONTRIBUTING,
docs/architecture.md и docs/project-scope.md; остальные документы читай по задаче.
Команда и роли — docs/team.md; задачи и зависимости — project_plan.md;
контракты проектируемых модулей — docs/game-api.md. Не выполняй назначенную участникам
реализацию без поручения: подготовка архитектуры и распределение задач не заменяют работу команды.
Различай подготовленный каркас и реализованную игру. Не придумывай требования,
сроки, владельцев задач или протоколы приёмки. Не считай предположение из прошлой сессии подтверждённым фактом.

## Границы

- `src/Platformer.Core`: CPU-логика игры, только стандартная .NET; без Engine3D/OpenTK/OpenGL/окон.
- `src/Platformer.App`: точка входа, адаптация данных к публичному API движка, жизненный цикл.
- `tests/Platformer.Tests`: CPU-тесты без окна; графический smoke запускается отдельно.
- `external/engine`: закреплённая внешняя зависимость. Не редактируй её и не обновляй gitlink без отдельной задачи.
- Старую рабочую копию движка и её личный workflow не изменять.
  Порядок engine PR → merge → game gitlink PR → проверки → интеграция: docs/engine-integration.md#engine-first; ссылки по каждой задаче — docs/task-guide.md.
- `.local/` — игнорируемые личные инструкции, состояния сессий и AI-протоколы; не публиковать.

Не добавляй прямые `GL.*`, GPU handles, reflection-доступ к окну движка или второе окно для ввода.
Клавиатура доступна через `engine.Input` только внутри `onUpdate` на потоке Create.
Используй InputKey/InputSnapshot; при потере фокуса очищай отложенные игровые команды.
После активации окна зажатые клавиши требуют отпускания и нового нажатия.
Обязательны две локации с переходами, WASD/Space/Shift/R, мышь, монеты, наклоны,
вращающиеся препятствия, звук, меню уровней/громкости/скинов; цель — Linux.
Параметры проектирования и готовность реализации различай по docs/requirements.md.
Engine snapshot `013e3e29251ae020c9ced5fdeb46ef3d10a7b403` поддерживает клавиши/мышь/CursorMode,
отдельный Engine3D.Audio и OverlayFrame/SetOverlay (upstream PR #52/#51/#53), ClientSize (#54).
Ограничения окна и SetClientSize/ClientSizeLimits (#55) подключены к Settings:
800×450 минимум (уменьшается на маленьком экране), доступные размеры фильтруются;
выбор и Apply отделены от подтверждённого ClientSize ОС.
Изменение размера перетаскиванием краёв/углов отключено через EngineOptions.Resizable=false;
SetClientSize для Settings остаётся доступным.
SetFullscreen/IsFullscreen/FullscreenSize/WindowedClientSizeLimits (#57) подключены:
Display: Windowed/Fullscreen и Apply переключают режим в том же окне; fullscreen —
текущий desktop mode без вычитания рамки/панелей, оконный размер восстанавливается.
SetClientSize в fullscreen запрещён: сначала SetFullscreen(false). На Wayland для
полноэкранного режима используется основной монитор, позицию возврата выбирает compositor.
На Wayland лимит консервативен по наименьшей логической области мониторов; панели compositor API не сообщает.
Input/cursor/audio adapters, меню/HUD и каталог Training Track/Turning Maze подключены;
AudioCoordinator использует собственные PCM16 WAV, lifecycle описан в docs/game-audio.md.
Settings / Music выбирает исходный loop (по умолчанию) или Exploration Theme Cleyton Kauffman
под CC0; notices поставляются с аудио. Выбор, громкость/Mute, открытый скин и подтверждённый режим/оконный размер сохраняются в settings-v1.json рядом с прогрессом. В паузе новый
музыкальный voice запускается только после Continue, эффекты при смене трека не сбрасываются.
С `--visual` MenuPanelKind задаёт роли панелей, TropicalMenuTheme применяет общую
тёмно-бирюзовую тему меню/HUD; главное меню слева. Геометрия и hit-test остаются в MenuLayout.
Легенды клавиш убраны из меню и Playing; управление — Settings → Controls.
Controls — локальная подстраница MenuController внутри AppScreen.Settings, Back/Escape
возвращает в Settings с прежним фокусом, сохраняя паузу/сеанс.
Подписи кнопок центрируются только вертикально по OverlayTextMetrics.MeasureInkBounds;
горизонтальные отступы сохранены. WindowBranding перед Run задаёт PNG 256×256 через
Engine.SetWindowIcon (Windows/X11), ICO встроен в ресурсы Windows apphost.
Overlay использует logical client pixels и встроенный ASCII-атлас; реальный framebuffer≠client
HiDPI проверен автоматическим native Wayland compositor с framebuffer/client=2,
включая реальные pointer coordinates/hit-test; physical монитор/устройство этим не подтверждаются.
Synthetic viewport scale=2 не заменяет такую проверку. Linux renderer-маршрут двух локаций
с 25/25 монетами проверен автоматически; ручное полное прохождение/слышимый звук/FPS не заявлены.
Новые расширения следуют docs/engine-integration.md и требуют upstream PR и точного gitlink.
Сохраняй Y вверх, правую систему координат и матричные соглашения движка.
GameplayHost интерполирует шар между physics ticks и направляет камеру на ту же
визуальную позицию. Presenter хранит физическое вращение отдельно от render transform;
не используй сглаженный transform как исходное состояние следующего tick.
Карточки Levels/Skins сразу запускают уровень/применяют скин. Выход из попытки в MainMenu
требует подтверждения с Cancel по умолчанию; последний вид уровня остаётся только фоном.
В Playing удержание Space повторяет прыжок после приземления, в меню Space остаётся фронтом.
Доработка 10.11.2026: разрывы уровней 1/2/4 — 2 единицы; борта Turning Maze —
Core Surfaces с IsBarrier и общими bounds рендера/физики. Балки Turning Maze,
Island Circuit, Windmill Pass и Temple Gauntlet имеют HazardResponse.Knockback;
смертельные препятствия Jump Circuit, Lava Causeway и Sky Storm используют Fail.
Контакт отбрасывает шар, отменяя перемещение одного tick; отменённый Path не собирает
монеты/финиш. Данные/тесты и ограничения — docs/level-refinement.md.
Расширение 12.11.2026: GameLevels содержит 13 локаций, пять новых стоят после Island
Circuit, затем Pendulum Gorge / Piston Foundry / Clockwork Summit, последним Sky Storm.
Все первые двенадцать доступны; BaseStars для открытия финала
считает только исходные первые четыре (GameLevels.IsBaseLevel), порог12 сохранён.
Всего 162 монеты / 39 звёзд, MaximumStars вычисляется из каталога; пороги скинов 6/9/15
и старые ID/сохранения не меняются. Levels показывает 13 карточек в двух колонках.
HazardMotionKind.Rotation сохраняет прежнее движение; Pendulum — качание угла,
Shuttle — поперечный ход по синусу. Amplitude в радианах/единицах; AngularSpeed — частота
фазы новых режимов. CCD/ValidateHazardBudget используют MaximumPointSpeed; Knockback —
VelocityAt с фактической скоростью и направлением. Канат следует позе из snapshot,
камень имеет OBB, стойки — Core IsBarrier вне дуги качания. Пауза/Restart сохраняют
прежний контракт времени. Устройство и проверки — docs/five-challenge-levels.md,
docs/expert-levels.md. Новые режимы реализованы в игре, gitlink движка не меняется.
GameSkins содержит 14 скинов: прежние 9 ID и пороги наград 6/9/15 сохранены;
watermelon/porcelain/magma/disco/voyager доступны сразу, без изменения физики/сохранений.
Levels показывает графические звёзды и цветную подпись Difficulty, без ASCII-дубля
рейтинга и Best coins. LevelDifficulties — оценка маршрута, не физическая настройка.
MenuTextKind.Difficulty сохраняет семантический цвет в TropicalMenuTheme.
SkinSelect использует две колонки с адаптивной высотой карточек/preview, Back виден при 800×450.
Оценки сложности откалиброваны: большинство обычных маршрутов Normal, Crumble/Temple/
Pendulum/Piston Hard, Clockwork/Sky Very hard. Постоянного Sky Storm12/12 в Levels нет:
при закрытом финале показываются условие3звезды на каждом из первых4 и число освоенных
уровней; при открытом — обычная инструкция выбора. Правило открытия не изменено.
GameplayDialogLayout рисует компактные Paused/Failed/LevelResult по MenuPage.Status.
Failed показывает только лучший успешный финиш, не начисляя звёзды за поражение;
LevelResult отдельно показывает THIS FINISH и PERSONAL BEST графическими звёздами.
ASCII-рейтинг и счёт монет на результатах убраны, HUD в Playing прежний.
Подтверждение Main menu не должно получать Status; Cancel по умолчанию сохраняет попытку.
Обычный Linux-запуск после сборки — bash scripts/Play.sh, отдельно от терминала;
--visual включает оформление со скриншотов. scripts/install_desktop.py создаёт личный
Linux-ярлык в меню приложений (опционально --desktop); общие PNG/ICO — assets/branding.
dotnet run предназначен для диагностики. Счётчик кадров печатается только в диагностических режимах.

## Работа и проверки

Перед правками: `git status --short --branch`, diff, `git fetch --prune`; работай в ветке задачи от актуального main.
Не делать pull при локальных изменениях, force-push и разрушительные Git-операции.
Используй существующий .NET 10 SDK согласно global.json. Не понижай target framework ради окружения.

```sh
git submodule update --init --recursive
dotnet restore Platformer.sln
dotnet build Platformer.sln -c Release --no-restore
dotnet test Platformer.sln -c Release --no-build
python scripts/check_docs.py
git diff --check
```

PowerShell 7: `pwsh -NoProfile -File scripts/Verify.ps1` выполняет эти проверки с остановкой при ошибке.
При изменении App/графики дополнительно запусти `--smoke` по README; отсутствие дисплея явно укажи.
Для Markdown-only изменений достаточно проверки документов и diff. Не повторяй успешные проверки без причины.
Новый дефект подтверждай воспроизводящим тестом/сценарием. Не выдавай CPU/CI за ручную GPU-проверку или FPS.

## GitHub и публикация

Перед commit/push покажи результат и проверки; получи разрешение, если публикация ещё не входит в поручение.
Согласованное разрешение действует для всего утверждённого объёма. Добавляй только файлы задачи.
После разрешённого push создавай PR, если его ещё нет. Merge требует отдельного поручения.
Не создавать issues, назначения, сроки и Project-поля без согласования.

Для PR участника назначай ровно одного проверяющего из команды и `alken1t15` как Team Lead.
Автор PR не может быть проверяющим. Для собственных PR `alken1t15` reviewer requests
должны быть пусты: подготовку, настройку, архитектуру и контракты Team Lead выполняет
и проверяет с помощью Codex, без фиктивного человеческого Approve. Team Lead — Assignee всех PR.
Для PR остальных участников нужны Approve одного проверяющего и Team Lead; AI их не заменяет.
Проверяй GraphQL reviewRequests и REST requested_reviewers по выбранным ролям.
Обязательный CI не обходить; человеческое одобрение требуется для PR участников.
Политика GitHub bypass технически определяется пользователем, выполняющим merge, а не автором PR;
не применять исключение к чужому PR без одобрений проверяющего и Team Lead.

При наличии локального Team Lead workflow используй его Plan/Review, не публикуя личные файлы и секреты.
Ошибки, пустые и обрезанные ответы AI не являются завершённым ревью. Findings проверять по коду.
В конце укажи проверки, ограничения, ветку/коммит/PR и состояние публикации; обнови локальные state/session-log.
