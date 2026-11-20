#!/usr/bin/env bash
# Local jeep test on Linux, as run_jeep_test.ps1: a localhost server on the jeep test map and a
# client that joins it, spawning by the jeeps and a test kit (cmu.jeep.* in server.toml). Build
# Content.Server and Content.Client first. Logs go to bin/jeep-test-run.
#
# Without a display, run it under Xvfb (DISPLAY=:99 after `Xvfb :99 -screen 0 1600x1000x24 &`):
# SDL then needs EGL for a desktop OpenGL context, set below. To show off a turret mount, add e.g.
# `dev_turret = "CMUJeepSentryMount"` and `dev_turret_gun = "RMCSentryMini"` to [cmu.jeep].
set -euo pipefail
username="${1:-JeepTester}"
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../../.." && pwd)"
run="$repo/bin/jeep-test-run"
mkdir -p "$run"
stamp="$(date +%H%M%S)"

pkill -f "bin/Content.Server/Content.Server" || true
"$repo/bin/Content.Server/Content.Server" --mount-dir "$repo/Content.CMU/Resources" \
    --config-file "$here/server.toml" --data-dir "$run/data" > "$run/server-$stamp.log" 2>&1 &

for i in $(seq 1 300); do
    curl -s http://127.0.0.1:1222/status | grep -q "CMU Jeep Test" && break
    sleep 1
done
echo "server up after ${i}s (log server-$stamp.log)"

SDL_VIDEO_FORCE_EGL=1 "$repo/bin/Content.Client/Content.Client" --mount-dir "$repo/Content.CMU/Resources" \
    --connect --connect-address 127.0.0.1:1222 --username "$username" \
    --cvar cmu.ui_configured=true --cvar display.opengl_version=1 > "$run/client-$stamp.log" 2>&1 &
echo "client started (log client-$stamp.log)"
