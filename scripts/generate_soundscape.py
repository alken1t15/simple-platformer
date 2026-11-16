"""Собственные PCM16 mono ресурсы Ball Adventure (MIT), воспроизводимый seed."""
import math
import pathlib
import random
import struct
import wave

RATE = 16000
ROOT = pathlib.Path(__file__).resolve().parents[1] / 'src/Platformer.App/Assets/Audio'

def make(name, duration, kind):
    rng = random.Random(173 + sum(map(ord, name)))
    samples = []
    low = 0.0
    for i in range(round(duration * RATE)):
        t = i / RATE
        noise = rng.uniform(-1, 1)
        low = low * .92 + noise * .08
        if kind == 'winter':
            value = low * .26
            for start, freq in ((37, 660),):
                age = t - start
                if age > 0:
                    value += math.sin(age * 2 * math.pi * freq) * .055 * math.exp(-age * 3)
        else:
            value = low * .2
            for start in (27,):
                age = t - start
                if 0 < age < .3:
                    value += math.sin(age * 2 * math.pi * (1500 + 1400 * age)) * .075 * math.sin(age / .3 * math.pi)
        # Мягкие края позволяют безопасно замыкать фон.
        envelope = min(1, t / .025, (duration - t) / .025)
        samples.append(struct.pack('<h', round(max(-1, min(1, value * envelope)) * 32767)))
    with wave.open(str(ROOT / (name + '.wav')), 'wb') as output:
        output.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        output.writeframes(b''.join(samples))

make('winter-air', 60, 'winter')
make('tropical-air', 60, 'tropical')
