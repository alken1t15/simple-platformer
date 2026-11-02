# Ball Adventure — план на три недели

Старт 02.11.2026, 21 день до23.11.2026; рабочее окно02–22.11. Дата самого показа не назначена.
План основан на утверждённой концепции, а не на прежнем 2D-backlog. Реально готов только
технический каркас. Все реализации ниже — будущие задачи команды.

Календарь начала/окончания всех 18 задач и рабочие интервалы — [schedule.md](docs/schedule.md).
Даты также записаны в issues и Date-полях Project. #2–3 выполняет Team Lead с Codex.

## Контрольные точки

| Неделя | Цель и работы | Результат / резерв |
|---|---|---|
| 1: 02–08.11 | TL: требования/API и минимальный engine-контракт. Николай: input, затем overlay; Владимир: physics prototype и audio spike; Роман: OBB/уровень1, затем шар и опасность. QA: тест-дизайн | Проверенный технический путь sphere/наклон/mouse/UI/audio. К08.11 отдельные проверки осуществимости ввода/UI/audio/наклонов; полный вертикальный срез к15.11. Если расширение задержано, TL меняет план, а не скрывает блокер |
| 2: 09–15.11 | Интеграция engine input/overlay/audio по отдельным PR; камера/цикл; сеанс; presenter/скины; второй уровень и sound mapping; меню | Меню → level1 → level2 → результат, все обязательные механики. Не добавлять третью локацию и новые виды препятствий |
| 3: 16–22.11 | QA-регрессии, реальные Linux mouse/audio/UI, чистый запуск; балансировка/исправления, лицензии/README, репетиция | С16.11 только обязательные функции и исправления. 20–21.11 приёмка/чистый запуск, 22.11 резерв и показовая сборка; готовность не позднее23.11 |

Это контрольные ориентиры, не гарантия календаря конкретного исполнителя. Оценки ниже —
предварительная трудоёмкость в днях сосредоточенной работы, не обещание ежедневной доступности.
Неизвестны часы участников. Три программиста имеют по нескольку последовательных задач:
Николай — input→overlay→camera/menu, Владимир — physics/audio→session, Роман — level1→presenter/obstacle→level2.
QA начинает тест-дизайн после API, не ожидает конца третьей недели. На ревью и дефекты оставлен
интеграционный резерв; если суммарная загрузка не помещается, TL согласует перераспределение.

## Архитектура и подготовка Team Lead

Требования/архитектура/API и настройка репозитория остаются у alken1t15 с Codex.
Участники реализуют согласованные модули. Собственные PR TL без reviewer requests;
PR участника — один проверяющий и TL, фактический автор исключается. AI не заменяет human Approve.

## Задачи и зависимости

Существующие issues2–13 обновляются на месте. Новые закрывают реальные пробелы движка/контента,
не дублируют исходные задачи. Новые issues16–21 опубликованы; номера прежних задач2–13 сохранены. [Доска](https://github.com/users/alken1t15/projects/2).

| Issue | Задача | Owner | Проверяющий + TL | Начало / завершение | Предварительная оценка |
|---|---|---|---|---|---|
| [#2](https://github.com/alken1t15/simple-platformer/issues/2) | [Requirements] Зафиксировать Ball Adventure: 3D, два уровня, звук и меню | alken1t15 | Без requests: TL | — | 0.5–1 день TL |
| [#3](https://github.com/alken1t15/simple-platformer/issues/3) | [Architecture] Контракты Ball Adventure и расширений движка | alken1t15 | Без requests: TL | [#2](https://github.com/alken1t15/simple-platformer/issues/2) | 1–1.5 дня TL |
| [#4](https://github.com/alken1t15/simple-platformer/issues/4) | [Core] 3D-модель уровня и первая локация с мостами и наклонами | ibmpowerpc | VladimirKhmelev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3) | 1–2 дня Роман |
| [#5](https://github.com/alken1t15/simple-platformer/issues/5) | [Core] Физика 3D-шарика: движение, прыжок, boost и наклоны | VladimirKhmelev | Nickovlev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3), [#4](https://github.com/alken1t15/simple-platformer/issues/4) | 3–4 дня Владимир |
| [#6](https://github.com/alken1t15/simple-platformer/issues/6) | [App] WASD/мышь, фиксированный шаг и камера третьего лица | Nickovlev | VladimirKhmelev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3), [#4](https://github.com/alken1t15/simple-platformer/issues/4), [#16](https://github.com/alken1t15/simple-platformer/issues/16) | 2–3 дня Николай |
| [#7](https://github.com/alken1t15/simple-platformer/issues/7) | [App] Сфера, скины и 3D-представление Ball Adventure | ibmpowerpc | Nickovlev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3), [#4](https://github.com/alken1t15/simple-platformer/issues/4); закрытие: [#5](https://github.com/alken1t15/simple-platformer/issues/5), [#6](https://github.com/alken1t15/simple-platformer/issues/6), [#17](https://github.com/alken1t15/simple-platformer/issues/17) | 1.5–2 дня Роман |
| [#8](https://github.com/alken1t15/simple-platformer/issues/8) | [Core/App] Монеты, финиш, поражение, reset и переходы уровней | VladimirKhmelev | Nickovlev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3), [#4](https://github.com/alken1t15/simple-platformer/issues/4), [#5](https://github.com/alken1t15/simple-platformer/issues/5) | 1.5–2 дня Владимир |
| [#9](https://github.com/alken1t15/simple-platformer/issues/9) | [Tests] Независимые регрессии 3D-физики, уровней и сеанса | fUS1ONd | VladimirKhmelev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3); закрытие: [#4](https://github.com/alken1t15/simple-platformer/issues/4), [#5](https://github.com/alken1t15/simple-platformer/issues/5), [#8](https://github.com/alken1t15/simple-platformer/issues/8), [#20](https://github.com/alken1t15/simple-platformer/issues/20), [#21](https://github.com/alken1t15/simple-platformer/issues/21) | 2–3 дня QA распределённо |
| [#10](https://github.com/alken1t15/simple-platformer/issues/10) | [Tests] Интеграция камеры, меню, звука, двух уровней и CI | fUS1ONd | Nickovlev, alken1t15 | [#6](https://github.com/alken1t15/simple-platformer/issues/6), [#7](https://github.com/alken1t15/simple-platformer/issues/7), [#8](https://github.com/alken1t15/simple-platformer/issues/8), [#19](https://github.com/alken1t15/simple-platformer/issues/19), [#18](https://github.com/alken1t15/simple-platformer/issues/18), [#20](https://github.com/alken1t15/simple-platformer/issues/20), [#21](https://github.com/alken1t15/simple-platformer/issues/21) | 1.5–2 дня QA |
| [#11](https://github.com/alken1t15/simple-platformer/issues/11) | [QA] Linux-приёмка Ball Adventure: управление, меню, звук, два уровня | prettysorrow | fUS1ONd, alken1t15 | [#2](https://github.com/alken1t15/simple-platformer/issues/2); закрытие: [#10](https://github.com/alken1t15/simple-platformer/issues/10) | 1–2 дня QA плюс ретест |
| [#12](https://github.com/alken1t15/simple-platformer/issues/12) | [QA] Чистый Linux-запуск и репетиция демонстрации Ball Adventure | prettysorrow | fUS1ONd, alken1t15 | [#10](https://github.com/alken1t15/simple-platformer/issues/10), [#11](https://github.com/alken1t15/simple-platformer/issues/11) | 0.5–1 день QA |
| [#13](https://github.com/alken1t15/simple-platformer/issues/13) | [Delivery] Интегрировать Ball Adventure и подготовить показ до 23 ноября | alken1t15 | Без requests: TL | [#9](https://github.com/alken1t15/simple-platformer/issues/9), [#10](https://github.com/alken1t15/simple-platformer/issues/10), [#11](https://github.com/alken1t15/simple-platformer/issues/11), [#12](https://github.com/alken1t15/simple-platformer/issues/12) | 1–2 дня TL |
| [#16](https://github.com/alken1t15/simple-platformer/issues/16) | [Engine] Публичный ввод WASD/Shift/мыши и захват курсора | Nickovlev | VladimirKhmelev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3) | 1–2 дня Николай |
| [#17](https://github.com/alken1t15/simple-platformer/issues/17) | [Engine] Минимальный overlay для меню, текста и HUD | Nickovlev | VladimirKhmelev, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3) | 2–3 дня Николай |
| [#18](https://github.com/alken1t15/simple-platformer/issues/18) | [Engine] Аудиосервис для Linux: эффекты, фон, громкость и lifecycle | VladimirKhmelev | fUS1ONd, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3) | 1.5–2 дня Владимир |
| [#19](https://github.com/alken1t15/simple-platformer/issues/19) | [App] Главное меню, пауза, выбор уровней, звука и скина | Nickovlev | prettysorrow, alken1t15 | [#3](https://github.com/alken1t15/simple-platformer/issues/3); закрытие: [#16](https://github.com/alken1t15/simple-platformer/issues/16), [#17](https://github.com/alken1t15/simple-platformer/issues/17), [#18](https://github.com/alken1t15/simple-platformer/issues/18), [#8](https://github.com/alken1t15/simple-platformer/issues/8) | 1.5–2 дня Николай |
| [#20](https://github.com/alken1t15/simple-platformer/issues/20) | [Core/App] Вращающиеся препятствия и синхронные столкновения | ibmpowerpc | VladimirKhmelev, alken1t15 | [#4](https://github.com/alken1t15/simple-platformer/issues/4), [#5](https://github.com/alken1t15/simple-platformer/issues/5) | 1–1.5 дня Роман |
| [#21](https://github.com/alken1t15/simple-platformer/issues/21) | [Content/App] Вторая локация, audio events и полный маршрут двух уровней | ibmpowerpc | prettysorrow, alken1t15 | [#4](https://github.com/alken1t15/simple-platformer/issues/4); закрытие: [#5](https://github.com/alken1t15/simple-platformer/issues/5), [#8](https://github.com/alken1t15/simple-platformer/issues/8), [#20](https://github.com/alken1t15/simple-platformer/issues/20), [#18](https://github.com/alken1t15/simple-platformer/issues/18) | 1.5–2 дня Роман |

### Начало и завершение проверок

CPU-регрессии стартуют после API; закрытие — после physics/session/obstacles/двух уровней.
Ручной чек-лист стартует после требований; протокол PASS — после готовой интеграции.
Presenter может начать 3D-геометрию до overlay, но закрывается только с видимым HUD.
Это зависимости завершения, а не циклы. Все engine задачи закрываются после upstream merge
и проверенного game gitlink, не по одному локальному spike. Принятие PR происходит по полномочиям TL.

## Критический путь и готовность

API → input/OBB/physics → camera/session/presenter → menu/level2/audio integration → Linux QA → сдача.
Overlay блокирует видимое меню/HUD, audio service — звук/громкость, input — мышь иW/S/Shift.
Критерий готовности — BA-01–13, успешные CI, реально слышимый звук и управляемая игра наLinux,
прохождение двух уровней в одном процессе и чистая демонстрационная сборка.
Числа FPS из движка и technical preview не доказывают готовность Ball Adventure.

## Риск загрузки

Ориентировочная сумма реализации у Николая — 6.5–10 дней (input, overlay, камера/цикл, меню),
у Владимира — 6–8 дней (physics, audio, session), у Романа — 5–8 дней (level1, presenter,
obstacles, level2). Это суммы работ, не доказательство даты завершения: ожидания API/ревью,
проверка общих engine изменений и дефекты добавляются отдельно. Минимальный путь
проектируется с параллельной подготовкой на интерфейсах; оценка должна быть сверена
с фактическими часами команды в начале недели1. Сумма человеко-дней всей команды
не равна длине критического пути, но календарный запас тоже не гарантируется.
Если к08.11 не проверен путь UI/input/audio/наклоны, TL сразу фиксирует риск и перераспределение.
Обязательные меню, звук и два уровня не вычёркиваются молча ради зелёного статуса.
