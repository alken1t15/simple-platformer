"""Воспроизводимые собственные PCM16-ресурсы Ball Adventure, лицензия MIT проекта."""
import math
import pathlib
import struct
import wave

RATE = 16000
OUTPUT = pathlib.Path(__file__).resolve().parents[1] / 'src/Platformer.App/Assets/Audio'


def write(name, duration, signal):
    count = round(duration * RATE)
    samples = []
    for i in range(count):
        t = i / RATE
        # Короткие fade-in/out исключают скачок на краях, включая стык loop.
        envelope = min(1, t / 0.012, (duration - t) / 0.04)
        value = 0.55 * envelope * signal(t)
        samples.append(round(max(-1, min(1, value)) * 32767))
    with wave.open(str(OUTPUT / name), 'wb') as result:
        result.setparams((1, 2, RATE, count, 'NONE', 'not compressed'))
        result.writeframes(struct.pack('<' + 'h' * count, *samples))


def tone(frequency, t):
    return math.sin(math.tau * frequency * t)


def generate():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    write('jump.wav', 0.22, lambda t: tone(280 + 700 * t, t) * math.exp(-7 * t))
    write('coin.wav', 0.3, lambda t: (tone(880, t) + 0.4 * tone(1320, t)) / 1.4 * math.exp(-9 * t))
    write('fail.wav', 0.55, lambda t: tone(240 - 140 * t, t) * math.exp(-3 * t))
    write('finish.wav', 0.9, lambda t: tone((523.25, 659.25, 783.99)[min(2, int(t / 0.3))], t)
          * 0.65 * min(1, (t % 0.3) / 0.01, (0.3 - t % 0.3) / 0.02))
    notes = (261.63, 329.63, 392, 329.63, 293.66, 349.23, 440, 349.23)
    def music(t):
        beat = t % 1
        envelope = min(1, beat / 0.04) * math.exp(-2 * beat)
        return 0.6 * tone(notes[int(t) % len(notes)], t) * envelope + 0.12 * tone(130.8125, t)
    write('music.wav', 8, music)


if __name__ == '__main__':
    generate()
