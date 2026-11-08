# Игровой звук и связь с сеансом

Program создаёт один публичный AudioDevice на потоке окна. NativeAudioPlayback до Main Menu
загружает клипы относительно `AppContext.BaseDirectory/Assets/Audio`, без зависимости от cwd.
При завершении AudioCoordinator освобождает voices и clips, затем Program — устройство.
Smoke не создаёт аудиоустройство и не загружает клипы. Silent fallback выводит диагностику.

| Файл | Событие | Воспроизведение |
|---|---|---|
| jump.wav | Jumped | Только принятый физикой прыжок |
| coin.wav | CoinCollected | Один раз на событие монеты |
| fail.wav | Failed | Один раз при поражении |
| finish.wav | LevelCompleted | Один раз при финише каждой локации |
| music.wav | Playing / Original | Исходный loop по умолчанию, gain 0.25 |
| exploration-theme.wav | Playing / Exploration Theme | Дополнительный loop, gain 0.25 |

В Settings пункт **Music** переключается кликом, Enter/Space или Left/Right.
`Original` выбран по умолчанию; исходный `music.wav` не изменён. Выбор применяется без
Apply и сохраняется между уровнями/Restart в текущем процессе, но не записывается
в файл прогресса. В главном меню музыка сама не начинается. При смене трека из паузы
старый voice освобождается, новый запускается только после Continue. В Playing
заменяется только музыка: эффекты не перезапускаются, master volume/mute сохраняются.
Одновременно играет максимум один музыкальный loop.

Эффекты имеют gain 0.7; master gain/mute меняются существующим MenuController немедленно.
AudioCoordinator дедуплицирует упорядоченные GameEvent по SessionId/Sequence.
При новом SessionId останавливает предыдущие voices и начинает новую последовательность.
Stop при terminal transition сохраняет дедуп, поэтому повторный Consume не повторяет звук.
События terminal tick передаются после перехода: музыка остановлена, сигнал fail/finish слышен один раз.
На паузе/потере фокуса music и effects приостановлены; Continue продолжает их без второго loop.
Restart/Next/MainMenu останавливают старые voices. Одновременно удерживается максимум 16 эффектов
и одна музыка; старейший эффект освобождается при переполнении.

Исходные пять файлов — собственные процедурные ресурсы проекта под MIT, без внешних записей.
Исходник и воспроизводимое создание: `python scripts/generate_audio.py` (стандартный Python).
Формат PCM16 little-endian mono, 16000 Hz. Фон 8 секунд, края сглажены для loop;
эффекты различаются высотой/последовательностью тонов. Файлы копируются и в build, и в publish.

**Exploration Theme** — музыка Cleyton Kauffman под CC0 1.0 Universal:
[страница автора на OpenGameArt](https://opengameart.org/content/exploration-theme).
Это неизменённый PCM16 stereo 44100 Hz WAV длительностью 134.4 с из авторского ZIP;
файл только переименован в `exploration-theme.wav` (23 708 422 байта).
Хеши ZIP/WAV, источник и автор указаны в
[exploration-theme-NOTICE.txt](../src/Platformer.App/Assets/Audio/exploration-theme-NOTICE.txt),
полный текст CC0 поставляется в [CC0-1.0.txt](../src/Platformer.App/Assets/Audio/CC0-1.0.txt).
Это исключение из MIT для собственных ресурсов; генератор старых WAV новый трек не создаёт.
Оба музыкальных файла и notices поставляются с игрой; runtime-загрузка из сети не нужна.

CPU-тесты проверяют дедуп, смену сеанса, pause/resume/stop, лимит voices, события настоящего host,
Retry и маршрут обеих production-локаций. Linux renderer и OpenAL null проверяют загрузку и lifecycle.
Это не проверка слышимости: на целевом компьютере отдельно прослушать эффекты/loop, паузу,
повтор, переход, mute/0% и восстановление громкости. Полная независимая QA остаётся в #9–12.
