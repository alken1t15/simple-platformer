# Разработка и проверки

## Цикл работы

Состав и роли — [team.md](team.md), распределение и зависимости — [project_plan.md](../project_plan.md).
В задаче нужны цель, checklist, подход, границы, приёмка, проверка, один Owner,
reviewer-кандидаты, блокирующие/зависимые задачи и интеграционный стык.
Реализацию начинают после выполнения её зависимостей; открытая задача не означает разрешения
Codex выполнить за участника весь назначенный модуль.

Уточнение задачи → ветка → реализация → локальные проверки → независимое AI-ревью
по локальному workflow → проверка findings → PR → применимые человеческие одобрения → CI → merge по поручению.
Если AI-сервис недоступен, фиксировать сбой и продолжать независимую работу;
не объявлять обязательное ревью успешным и не заменять его другим сервисом молча.
Личные prompts, логи и ключи находятся за пределами отслеживаемых файлов, в `.local/`.

Для PR участника требуются Approve одного выбранного проверяющего и Team Lead `alken1t15`.
Автор не может быть проверяющим. Для собственного PR Team Lead требуется один другой человек.
Team Lead указан Assignee каждого PR; для PR остальных участников он также reviewer.
AI-ревью и CI сохраняются. Создание PR не разрешает merge.

Workflow Assign reviewers назначает роли при открытии/обновлении готового PR на main.
Он читает только защищённую базовую ветку, не выполняет код из PR и не устанавливает пакеты.
При ссылке `Refs #N` или `Closes #N` выбирается проверяющий соответствующей issue;
иначе используется распределение по автору. Совпадение с фактическим автором исключается.
Если назначенный человек ещё не получил доступ, workflow сообщает ошибку, а не подменяет его молча.
Для draft Team Lead назначается Assignee, reviewer requests отправляются после Ready for review.

## Защита main

Два независимых ruleset применяются к `refs/heads/main`:

1. Обязательный PR, CI `build`, `documents`, `graphics-smoke`, запрет удаления и non-fast-forward.
   Минимум одно независимое человеческое одобрение. Нет bypass, в том числе для Team Lead.
2. Два одобрения для PR участника, включая code owner `alken1t15` из `.github/CODEOWNERS`:
   один проверяющий + Team Lead. Bypass только для пользователя `alken1t15` и только через PR.

GitHub применяет bypass по пользователю, выполняющему merge. Поэтому Team Lead по процессу
использует его только для собственного PR после одного независимого Approve;
для чужого проверяет Approve выбранного проверяющего и собственное одобрение Team Lead.
Правила не отключаются ради одного PR. При начальной подготовке пустого репозитория
нужно создать первую main до включения защиты; последующие изменения идут через PR.
Факт применения правил проверяется через GitHub API и записывается в локальный state.

Воспроизводимая настройка через GitHub CLI с правом администрирования репозитория:

```powershell
pwsh -NoProfile -File scripts/Configure-MainProtection.ps1
pwsh -NoProfile -File scripts/Configure-MainProtection.ps1 -Apply
```

Первая команда только показывает payload; вторая создаёт/обновляет два именованных ruleset
только в `alken1t15/simple-platformer` и проверяет прочитанные обратно правила.

## CI

`build`: Linux и Windows, restore/build/CPU tests; стабильный итоговый job `build`.
`documents`: обязательные документы, локальные Markdown-ссылки, архитектурные зависимости и pinned submodule.
`graphics-smoke`: Linux, Xvfb/Mesa, запуск App `--smoke` с таймаутом.
Все три required checks запускаются на любом PR без path filters. Checkout загружает submodule.
Движок собирается как зависимость App; полная upstream suite принадлежит репозиторию движка.
TRX загружаются в artifacts даже при неуспехе CPU-тестов.

## Linux и WSL

Нужны .NET 10 SDK, дисплей и драйвер OpenGL 3.3; для software smoke:

```sh
sudo apt-get update
sudo apt-get install -y xvfb xauth libgl1 libgl1-mesa-dri libx11-6 libxcursor1 libxext6 libxi6 libxinerama1 libxrandr2
LIBGL_ALWAYS_SOFTWARE=true XDG_SESSION_TYPE=x11 xvfb-run -a dotnet run --project src/Platformer.App -c Release --no-build -- --smoke
```

Для обычного графического запуска в WSLg при проблемах Wayland используйте `XDG_SESSION_TYPE=x11`
по инструкции движка `external/engine/docs/spikes.md`. Не переносите workaround на все платформы.
Xvfb проверяет жизненный цикл и показ кадров; внешний вид и FPS целевого GPU проверяются отдельно.
