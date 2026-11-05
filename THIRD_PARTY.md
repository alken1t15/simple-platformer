# Сторонние компоненты

| Компонент | Версия | Источник | Лицензия |
|---|---|---|---|
| Simple 3D Engine | 7d097269004ad59fc014cdeb707ccd9911c63841 (input/cursor + audio + overlay API) | https://github.com/alken1t15/simple-3d-engine | MIT; `external/engine/LICENSE` |
| DejaVu Sans Mono 2.37, встроенный ASCII-атлас движка | Растр/метрики из upstream PR #53; TTF не включён | https://dejavu-fonts.github.io/ | Bitstream Vera font license; DejaVu changes public domain. [Copyright и полный текст](external/engine/src/Engine3D.OpenGL/Assets/FONT-LICENSE.txt); atlas не объявляется MIT |
| OpenTK.Audio.OpenAL | 4.9.4, транзитивно через Engine3D.Audio | https://github.com/opentk/opentk | MIT; подробности в `external/engine/THIRD_PARTY.md` |
| OpenAL Soft | Системная Linux-библиотека `libopenal.so.1` (`libopenal1`); binary не включён | https://github.com/kcat/openal-soft | LGPL-2.0-or-later; upstream notices в `external/engine/THIRD_PARTY.md` |
| libX11 | Системная `libX11.so.6` для физического keyboard rearm Linux/X11; binary не включён | https://gitlab.freedesktop.org/xorg/lib/libx11 | MIT/X11-варианты и notices системного пакета; `external/engine/THIRD_PARTY.md` |
| Microsoft.NET.Test.Sdk | 18.0.1 | https://github.com/microsoft/vstest | MIT |
| MSTest.TestAdapter / TestFramework | 4.0.2 | https://github.com/microsoft/testfx | MIT |

Транзитивные OpenTK, GLFW и StbImageSharp перечислены вместе с лицензиями в
`external/engine/THIRD_PARTY.md`. Сохраняйте исходные notices при распространении.
В текущем макете нет сторонних изображений/моделей: используется процедурный куб движка.
Сфера, цилиндр монеты и узор скинов в `GameMeshes`/`GameSkins` — собственные процедурные
ресурсы проекта (ibmpowerpc/Codex), распространяются под MIT репозитория, без внешних ассетов.
Снимки в `docs/images` получены из собственного preview, не являются runtime-ресурсами.
ImageMagick используется только для захвата снимков в Linux CI и не входит в игру.
Упаковка игры и дистрибутив runtime пока не подготовлены.
