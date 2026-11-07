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
| music.wav | Playing | Единственный loop, gain 0.25 |

Эффекты имеют gain 0.7; master gain/mute меняются существующим MenuController немедленно.
AudioCoordinator дедуплицирует упорядоченные GameEvent по SessionId/Sequence.
При новом SessionId останавливает предыдущие voices и начинает новую последовательность.
Stop при terminal transition сохраняет дедуп, поэтому повторный Consume не повторяет звук.
События terminal tick передаются после перехода: музыка остановлена, сигнал fail/finish слышен один раз.
На паузе/потере фокуса music и effects приостановлены; Continue продолжает их без второго loop.
Restart/Next/MainMenu останавливают старые voices. Одновременно удерживается максимум 16 эффектов
и одна музыка; старейший эффект освобождается при переполнении.

Все пять файлов — собственные процедурные ресурсы проекта под MIT, без внешних записей.
Исходник и воспроизводимое создание: `python scripts/generate_audio.py` (стандартный Python).
Формат PCM16 little-endian mono, 16000 Hz. Фон 8 секунд, края сглажены для loop;
эффекты различаются высотой/последовательностью тонов. Файлы копируются и в build, и в publish.

CPU-тесты проверяют дедуп, смену сеанса, pause/resume/stop, лимит voices, события настоящего host,
Retry и маршрут обеих production-локаций. Linux renderer и OpenAL null проверяют загрузку и lifecycle.
Это не проверка слышимости: на целевом компьютере отдельно прослушать эффекты/loop, паузу,
повтор, переход, mute/0% и восстановление громкости. Полная независимая QA остаётся в #9–12.
