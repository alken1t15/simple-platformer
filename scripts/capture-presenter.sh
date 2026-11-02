#!/usr/bin/env bash
# Снимки собственного Xvfb-дисплея после подтверждения трёх показанных кадров
set -euo pipefail
output=${1:-artifacts/presenter}
mkdir -p "$output"
demo_pid=
cleanup() {
    if [[ -n "$demo_pid" ]]; then
        kill -TERM "$demo_pid" 2>/dev/null || true
        wait "$demo_pid" 2>/dev/null || true
        demo_pid=
    fi
}
trap cleanup EXIT

capture() {
    local name=$1 skin=$2
    shift 2
    dotnet src/Platformer.App/bin/Release/net10.0/Platformer.App.dll \
        --presenter --still --skin "$skin" "$@" >"$output/$name.log" 2>&1 &
    demo_pid=$!
    for ((attempt = 0; attempt < 100; attempt++)); do
        if grep -q 'Presenter preview ready' "$output/$name.log"; then break; fi
        if ! kill -0 "$demo_pid" 2>/dev/null; then
            cat "$output/$name.log"
            return 1
        fi
        sleep 0.1
    done
    if ! grep -q 'Presenter preview ready' "$output/$name.log"; then
        cat "$output/$name.log"
        return 1
    fi
    import -window root "$output/$name.png"
    # Пустой одноцветный дисплей не считается изображением сцены
    local colors
    colors=$(identify -format '%k' "$output/$name.png")
    [[ "$colors" -gt 10 ]]
    cleanup
}

capture classic-blue classic-blue
capture orange orange
capture overview classic-blue --overview
