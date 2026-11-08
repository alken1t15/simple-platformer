# Значок Ball Adventure

- [ball-adventure.png](ball-adventure.png) — исходный PNG с прозрачностью, 1254×1254.
- [ball-adventure.ico](ball-adventure.ico) — Windows ICO, размеры 16/32/48/64/128/256.
- [ball-adventure-window.png](ball-adventure-window.png) — неизменённый PNG payload 256×256 из этого ICO для окна.
- [icon-prompt.txt](icon-prompt.txt) — полный промпт генерации.

Значок создан для проекта встроенным imagegen: оранжевый шар с тёмными полосами и
звездой, шахматная дорожка и воздушный остров. Сторонние изображения-референсы не
использовались. Это сгенерированная иллюстрация, а не скриншот игры.
Ассет включён под лицензией MIT проекта; ICO получен преобразованием PNG через ImageMagick.

Linux использует PNG через [установщик](../../scripts/install_desktop.py), Windows-ярлык — ICO.
App также передаёт PNG 256×256 самому окну через Engine.SetWindowIcon (Windows/X11).
ICO встроен в ресурсы Windows apphost. На Wayland/macOS оформление окна зависит от
desktop entry/app bundle; закреплённые старые ярлыки могут сохранять прежнюю идентичность.
