#!/usr/bin/env bash
# Обычный Linux-запуск отдельно от терминала; сборку выполняют заранее по README.
set -euo pipefail
game_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
assembly="$game_root/src/Platformer.App/bin/Release/net10.0/Platformer.App.dll"
if [[ $# -gt 1 || ( $# -eq 1 && $1 != --visual ) ]]; then
    printf '%s\n' 'Использование: bash scripts/Play.sh [--visual]. Для диагностики используйте dotnet run по README.' >&2
    exit 2
fi
if [[ ! -f "$assembly" ]]; then
    printf '%s\n' 'Сначала соберите игру: dotnet build Platformer.sln -c Release' >&2
    exit 1
fi
command -v dotnet >/dev/null || { printf '%s\n' 'Не найден dotnet (.NET 10 Runtime).' >&2; exit 1; }
command -v setsid >/dev/null || { printf '%s\n' 'Не найден setsid (пакет util-linux).' >&2; exit 1; }
if [[ -z ${DISPLAY:-} && -z ${WAYLAND_DISPLAY:-} ]]; then
    printf '%s\n' 'Нужен графический Linux-сеанс (DISPLAY или WAYLAND_DISPLAY).' >&2
    exit 1
fi
log_dir="$game_root/artifacts/run"
mkdir -p -- "$log_dir"
log_file="$log_dir/ball-adventure-$(date +%Y%m%d-%H%M%S)-$$.log"
# Новая session/process group: Ctrl+C и закрытие исходного терминала не завершают игру.
setsid --fork dotnet "$assembly" "$@" </dev/null >"$log_file" 2>&1
printf 'Ball Adventure started. Log: %s\n' "$log_file"
