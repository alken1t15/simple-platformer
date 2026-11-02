# Ball Adventure — доска и распределение

[Доска игры](https://github.com/users/alken1t15/projects/2). Прежний Project движка не изменяется.

## Поля и этапы

| Поле | Назначение |
|---|---|
| Status | Todo / In Progress / Done; переработка описания не означает начало или завершение реализации |
| Priority / Area | Critical или High; requirements / architecture / core / app / tests / docs |
| Milestone | M1: контроль08.11; M2: контроль15.11; M3: финальная приёмка22.11 |
| Planned owner / Assignees | Плановый исполнитель и фактическое назначение GitHub |
| Reviewer candidates | Один проверяющий + Team Lead; собственные задачи TL без reviewer requests |
| Planned start / Planned finish | Даты начала и окончания каждой задачи, тип Date; [календарь](schedule.md) |
| Depends on | Отдельные условия начала и завершения; номера issues совпадают с телом задачи |

Окно проекта02–22.11.2026, готовность не позднее23.11. Этапы — ориентиры; реальную доступность команды нужно сверить. Для стартовых зависимостей есть status:blocked; finish-зависимости не мешают раннему тест-дизайну/подготовке.

## Задачи

| Issue | Owner | Проверяющие |
|---|---|---|
| [#2 — [Requirements] Зафиксировать Ball Adventure: 3D, два уровня, звук и меню](https://github.com/alken1t15/simple-platformer/issues/2) | alken1t15 | Не назначаются: TL |
| [#3 — [Architecture] Контракты Ball Adventure и расширений движка](https://github.com/alken1t15/simple-platformer/issues/3) | alken1t15 | Не назначаются: TL |
| [#4 — [Core] 3D-модель уровня и первая локация с мостами и наклонами](https://github.com/alken1t15/simple-platformer/issues/4) | ibmpowerpc | VladimirKhmelev, alken1t15 |
| [#5 — [Core] Физика 3D-шарика: движение, прыжок, boost и наклоны](https://github.com/alken1t15/simple-platformer/issues/5) | VladimirKhmelev | Nickovlev, alken1t15 |
| [#6 — [App] WASD/мышь, фиксированный шаг и камера третьего лица](https://github.com/alken1t15/simple-platformer/issues/6) | Nickovlev | VladimirKhmelev, alken1t15 |
| [#7 — [App] Сфера, скины и 3D-представление Ball Adventure](https://github.com/alken1t15/simple-platformer/issues/7) | ibmpowerpc | Nickovlev, alken1t15 |
| [#8 — [Core/App] Монеты, финиш, поражение, reset и переходы уровней](https://github.com/alken1t15/simple-platformer/issues/8) | VladimirKhmelev | Nickovlev, alken1t15 |
| [#9 — [Tests] Независимые регрессии 3D-физики, уровней и сеанса](https://github.com/alken1t15/simple-platformer/issues/9) | fUS1ONd | VladimirKhmelev, alken1t15 |
| [#10 — [Tests] Интеграция камеры, меню, звука, двух уровней и CI](https://github.com/alken1t15/simple-platformer/issues/10) | fUS1ONd | Nickovlev, alken1t15 |
| [#11 — [QA] Linux-приёмка Ball Adventure: управление, меню, звук, два уровня](https://github.com/alken1t15/simple-platformer/issues/11) | prettysorrow | fUS1ONd, alken1t15 |
| [#12 — [QA] Чистый Linux-запуск и репетиция демонстрации Ball Adventure](https://github.com/alken1t15/simple-platformer/issues/12) | prettysorrow | fUS1ONd, alken1t15 |
| [#13 — [Delivery] Интегрировать Ball Adventure и подготовить показ до 23 ноября](https://github.com/alken1t15/simple-platformer/issues/13) | alken1t15 | Не назначаются: TL |
| [#16 — [Engine] Публичный ввод WASD/Shift/мыши и захват курсора](https://github.com/alken1t15/simple-platformer/issues/16) | Nickovlev | VladimirKhmelev, alken1t15 |
| [#17 — [Engine] Минимальный overlay для меню, текста и HUD](https://github.com/alken1t15/simple-platformer/issues/17) | Nickovlev | VladimirKhmelev, alken1t15 |
| [#18 — [Engine] Аудиосервис для Linux: эффекты, фон, громкость и lifecycle](https://github.com/alken1t15/simple-platformer/issues/18) | VladimirKhmelev | fUS1ONd, alken1t15 |
| [#19 — [App] Главное меню, пауза, выбор уровней, звука и скина](https://github.com/alken1t15/simple-platformer/issues/19) | Nickovlev | prettysorrow, alken1t15 |
| [#20 — [Core/App] Вращающиеся препятствия и синхронные столкновения](https://github.com/alken1t15/simple-platformer/issues/20) | ibmpowerpc | VladimirKhmelev, alken1t15 |
| [#21 — [Content/App] Вторая локация, audio events и полный маршрут двух уровней](https://github.com/alken1t15/simple-platformer/issues/21) | ibmpowerpc | prettysorrow, alken1t15 |

## Назначения и проверка

Фактический Assignee сверяется с GitHub. Если участник ещё не принял приглашение, Planned owner сохраняет распределение, но это не объявляется предоставленным доступом. Переработка задач сохраняет их обсуждение и номера.

PR участника: указанный reviewer + alken1t15; автор не может проверять собственный код. Собственные PR TL без reviewer requests, проверка Codex/AI и CI. Team Lead — Assignee всех PR.

Расширение движка отслеживается задачей игры, реализуется отдельным upstream PR и подключается точным gitlink после приёмки. В старой рабочей копии движка изменения не выполняются.
