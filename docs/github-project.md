# Доска и задачи игры

[Simple Platformer — разработка игры](https://github.com/users/alken1t15/projects/2).
Это отдельный Project; доска движка и её задачи не изменяются.

## Поля

| Поле | Значения / назначение |
|---|---|
| Status | Todo, In Progress, Done; Done только по приёмке |
| Priority | Critical, High, Medium, Low |
| Area | requirements, architecture, core, app, tests, docs |
| Milestone | M1 — Требования и контракты; M2 — Игровая реализация; M3 — Проверка и сдача |
| Planned owner | Плановый ответственный из согласованного распределения |
| Assignees | Фактическое назначение GitHub после получения доступа |
| Reviewer candidates | Один проверяющий + Team Lead; для собственных PR Team Lead — без reviewer requests |
| Depends on | Предшественники; подробные условия начала/завершения в issue |

Milestones не имеют дат: календарь и загрузка ещё не подтверждены. Метка `status:blocked` означает невыполненные зависимости, а не необходимость изменить статус исходной задачи.

## Созданные задачи

| Issue | Owner | Этап |
|---|---|---|
| [#2 — [Requirements] Согласовать правила и критерии приёмки 2D-MVP](https://github.com/alken1t15/simple-platformer/issues/2) | `alken1t15` | M1 — Требования и контракты |
| [#3 — [Architecture] Зафиксировать игровой API и интеграционные контракты](https://github.com/alken1t15/simple-platformer/issues/3) | `alken1t15` | M1 — Требования и контракты |
| [#4 — [Core] Реализовать модель, валидацию и начальный уровень](https://github.com/alken1t15/simple-platformer/issues/4) | `ibmpowerpc` | M2 — Игровая реализация |
| [#5 — [Core] Реализовать движение, прыжок и столкновения персонажа](https://github.com/alken1t15/simple-platformer/issues/5) | `VladimirKhmelev` | M2 — Игровая реализация |
| [#6 — [App] Подключить игровые команды и фиксированный шаг симуляции](https://github.com/alken1t15/simple-platformer/issues/6) | `Nickovlev` | M2 — Игровая реализация |
| [#7 — [App] Отобразить уровень, персонажа и боковую камеру](https://github.com/alken1t15/simple-platformer/issues/7) | `ibmpowerpc` | M2 — Игровая реализация |
| [#8 — [Core] Реализовать правила и переходы игрового сеанса](https://github.com/alken1t15/simple-platformer/issues/8) | `VladimirKhmelev` | M2 — Игровая реализация |
| [#9 — [Tests] Добавить независимые CPU-регрессии игровых модулей](https://github.com/alken1t15/simple-platformer/issues/9) | `fUS1ONd` | M3 — Проверка и сдача |
| [#10 — [Tests] Проверить интеграцию игрового цикла, ввода и CI](https://github.com/alken1t15/simple-platformer/issues/10) | `fUS1ONd` | M3 — Проверка и сдача |
| [#11 — [QA] Подготовить ручной чек-лист и провести игровую приёмку](https://github.com/alken1t15/simple-platformer/issues/11) | `prettysorrow` | M3 — Проверка и сдача |
| [#12 — [QA] Проверить запуск из чистого клона на согласованных ОС](https://github.com/alken1t15/simple-platformer/issues/12) | `prettysorrow` | M3 — Проверка и сдача |
| [#13 — [Delivery] Подготовить документацию, учёт вклада и итоговую сдачу](https://github.com/alken1t15/simple-platformer/issues/13) | `alken1t15` | M3 — Проверка и сдача |

## Доступ и назначения

Пятерым участникам отправлены приглашения Write по разрешению Team Lead. Пока приглашение не принято и GitHub не разрешает Assignee, owner сохранён в теле issue и Planned owner на доске. Эти поля не доказывают принятия приглашения. После принятия нужно синхронизировать Assignee с плановым владельцем и прочитать результат обратно.

На момент подготовки приглашения ожидают принятия; задачи Team Lead назначены `alken1t15`. Актуальное поле Assignees видно на GitHub. Не назначать все задачи Team Lead ради заполнения поля.

## Переход к работе

Начать с задачи требований и проекта API. Чек-лист и тест-дизайн можно готовить после соответствующего контракта; фактическая приёмка ожидает реализации. В issue отдельно указаны условия начала и завершения.

Существующий [PR интеграции ввода №1](https://github.com/alken1t15/simple-platformer/pull/1) является зависимостью ввода. Созданные задачи не означают, что он уже принят.

На PR участника назначаются один проверяющий и `alken1t15`; автор исключается.
На собственный PR Team Lead reviewers не назначаются; подготовку и проверку выполняет Team Lead с Codex и CI.
Team Lead также указывается Assignee каждого PR, чтобы участвовать в организации собственного PR.
Автоматическое назначение настраивается через `.github/review-policy.json` и workflow Assign reviewers.
