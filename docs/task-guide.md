# Где искать задание, контракт и порядок работы

Обязательный результат — [ТЗ](requirements.md); внешний вид и содержание —
[макет](content-design.md); разделение модулей — [архитектура](architecture.md).
[Контракты v1](game-api.md) задают типы/порядок событий/ошибки; это будущая реализация,
а не API уже работающей Ball Adventure. Даты — [календарь](schedule.md), роли —
[команда](team.md), начало и завершение зависимостей — [план](../project_plan.md).

## Для каждой задачи

| Задача | Сначала прочитать | Проверяемый результат |
|---|---|---|
| [#2](https://github.com/alken1t15/simple-platformer/issues/2), [#3](https://github.com/alken1t15/simple-platformer/issues/3) | [ТЗ](requirements.md), [Core v1](core-contract-v1.md), [App/engine v1](app-engine-contract-v1.md) | Подготовка принята в PR #22; это не закрытие реализации |
| [#4](https://github.com/alken1t15/simple-platformer/issues/4) | [Core: данные](core-contract-v1.md#data), [Training Track](content-design.md#level1) | LevelDefinition/OBB, маршрут level1, spawn/монеты/finish/KillY и CPU-валидация |
| [#5](https://github.com/alken1t15/simple-platformer/issues/5) | [Core: настройки и Step](core-contract-v1.md), [аналитические примеры](acceptance.md#cpu-examples) | BallSimulator/Path, контакты, прыжок/boost, rollback и авторские CPU-тесты |
| [#6](https://github.com/alken1t15/simple-platformer/issues/6) | [App: InputFrame/runner/камера](app-engine-contract-v1.md#app) | Ввод/камера/фиксированный шаг; окончательная интеграция после #5/#8 |
| [#7](https://github.com/alken1t15/simple-platformer/issues/7) | [Модели](content-design.md#models), [snapshot](core-contract-v1.md) | 3D-представление и скины; экраны и HUD принадлежат #19 |
| [#8](https://github.com/alken1t15/simple-platformer/issues/8) | [Core: Step/reset](core-contract-v1.md), [переходы](app-engine-contract-v1.md#меню-и-переходы) | Сеанс/события/reset/LevelFlow, итог маршрута без повторного начисления |
| [#9](https://github.com/alken1t15/simple-platformer/issues/9) | [Core v1](core-contract-v1.md), [приёмка](acceptance.md) | Независимые CPU-регрессии; нет утверждений о GPU/слышимости |
| [#10](https://github.com/alken1t15/simple-platformer/issues/10) | [App v1](app-engine-contract-v1.md), [CI/процесс](development-process.md), [приёмка](acceptance.md) | Проверенные стыки модулей и интеграционные сценарии |
| [#11](https://github.com/alken1t15/simple-platformer/issues/11) | [A01–20](acceptance.md), [содержание](content-design.md) | Реальное Linux-прохождение, вид/звук/меню с commit и PASS/FAIL/SKIP |
| [#12](https://github.com/alken1t15/simple-platformer/issues/12) | [Запуск](../README.md), [Linux](development-process.md#linux-и-wsl), [ресурсы](content-design.md#audio) | Чистый clone/build/run и репетиция из другого cwd |
| [#13](https://github.com/alken1t15/simple-platformer/issues/13) | [ТЗ](requirements.md), [календарь](schedule.md), [ревью](development-process.md) | Интеграция и сдача Team Lead; принятые PR и реальные протоколы |
| [#16](https://github.com/alken1t15/simple-platformer/issues/16) | [Input/cursor](app-engine-contract-v1.md#input), [engine-first](engine-integration.md#engine-first) | Upstream input PR, затем принятый game gitlink |
| [#17](https://github.com/alken1t15/simple-platformer/issues/17) | [Overlay](app-engine-contract-v1.md#overlay), [engine-first](engine-integration.md#engine-first) | Универсальный renderer, не игровые экраны; upstream PR + game gitlink |
| [#18](https://github.com/alken1t15/simple-platformer/issues/18) | [Audio](app-engine-contract-v1.md#audio), [engine-first](engine-integration.md#engine-first) | Универсальный AudioDevice, не игровые WAV; upstream PR + game gitlink |
| [#19](https://github.com/alken1t15/simple-platformer/issues/19) | [Меню/HUD](content-design.md#menus), [InputFrame/переходы](app-engine-contract-v1.md) | Все экраны и HUD, работающие с готовым Presenter/LevelFlow/audio |
| [#20](https://github.com/alken1t15/simple-platformer/issues/20) | [Балка](content-design.md#hazard), [HazardMotion/Path](core-contract-v1.md) | Поза/модель/опасный swept contact и reset фазы |
| [#21](https://github.com/alken1t15/simple-platformer/issues/21) | [Turning Maze](content-design.md#level2), [звуки](content-design.md#audio) | Данные level2, комплект WAV/лицензии, AudioCoordinator и интегрированное прохождение |

Заголовки «для начала» и «для завершения» в issue означают разные условия.
До готовности finish-зависимости допустимы данные/авторские тесты/контрактные двойники;
полноценная интеграция и закрытие ждут её принятой реализации.
Если результат предшественника принят в тот же календарный день, его передача и проверка
должны закончиться до финального прогона зависимой задачи. Даты не заменяют эти условия.
#21 сначала передаёт данные level2 отдельным ранним PR для LevelSelect, а его итоговая
интеграция ждёт #19. Меню не ждёт закрытия всего #21: это передача данных, не цикл задач.
#8 сдаёт Core/LevelFlow с CPU-проверками; итоговое GPU-прохождение проверяется в #10/#21/#11.

## Если проблема в движке

Для любого участника действует [engine-first](engine-integration.md#engine-first):
воспроизведение → отдельный PR **движка** → проверки/ревью → merge движка →
отдельный PR **игры** с точным gitlink и проверками → merge игры → интеграция зависимой функции.
Нельзя заменить это правкой `external/engine`, прямым GL/OpenTK или ссылкой на непринятый PR.
Независимая от дефекта работа команды может продолжаться.

Номера issues в двух репозиториях различаются. В upstream PR указывать полную ссылку
на задачу игры, а в игровой задаче — полную ссылку на upstream PR и PR обновления зависимости.
Не создавать фиктивные ссылки на ещё не открытый PR: до появления писать «ожидает создания».
