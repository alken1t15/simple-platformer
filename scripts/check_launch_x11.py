#!/usr/bin/env python3
"""Проверка Linux launcher, terminal Ctrl+C и тихого штатного выхода на изолированном X11 DISPLAY."""
import ctypes as c
import os
from pathlib import Path
import signal
import subprocess
import time

root = Path(__file__).resolve().parents[1]
x = c.CDLL("libX11.so.6")
ptr, ulong = c.c_void_p, c.c_ulong

def bind(name, result, *args):
    f = getattr(x, name)
    f.restype, f.argtypes = result, args
    return f

bind("XOpenDisplay", ptr, c.c_char_p)
bind("XDefaultRootWindow", ulong, ptr)
bind("XQueryTree", c.c_int, ptr, ulong, c.POINTER(ulong), c.POINTER(ulong), c.POINTER(c.POINTER(ulong)), c.POINTER(c.c_uint))
bind("XFetchName", c.c_int, ptr, ulong, c.POINTER(ptr))
bind("XInternAtom", ulong, ptr, c.c_char_p, c.c_int)
bind("XGetWindowProperty", c.c_int, ptr, ulong, ulong, c.c_long, c.c_long, c.c_int, ulong,
     c.POINTER(ulong), c.POINTER(c.c_int), c.POINTER(ulong), c.POINTER(ulong), c.POINTER(ptr))
bind("XFree", c.c_int, ptr)
bind("XFlush", c.c_int, ptr)
bind("XCloseDisplay", c.c_int, ptr)
bind("XSendEvent", c.c_int, ptr, ulong, c.c_int, c.c_long, ptr)

class ClientMessage(c.Structure):
    _fields_ = [("type", c.c_int), ("serial", ulong), ("send_event", c.c_int), ("display", ptr),
                ("window", ulong), ("message_type", ulong), ("format", c.c_int), ("data", c.c_long * 5)]

class Event(c.Union):
    _fields_ = [("message", ClientMessage), ("padding", c.c_long * 24)]

def alive(pid):
    try:
        # Detached children may briefly remain zombies until reaped by init.
        return Path(f"/proc/{pid}/stat").read_text().split(") ", 1)[1][0] != "Z"
    except FileNotFoundError:
        return False

display = x.XOpenDisplay(None)
if not display:
    raise RuntimeError("Requires an isolated X11 DISPLAY (xvfb-run).")

def find_game(parent):
    name = ptr()
    if x.XFetchName(display, parent, c.byref(name)) and name.value:
        try:
            if c.string_at(name).decode(errors="replace") == "Ball Adventure":
                return parent
        finally:
            x.XFree(name)
    rr, pr, count = ulong(), ulong(), c.c_uint()
    children = c.POINTER(ulong)()
    if not x.XQueryTree(display, parent, c.byref(rr), c.byref(pr), c.byref(children), c.byref(count)):
        return None
    try:
        for i in range(count.value):
            found = find_game(children[i])
            if found:
                return found
    finally:
        if children:
            x.XFree(children)
    return None

parent = None
game_pid = None
try:
    env = dict(os.environ, ALSOFT_DRIVERS="null", XDG_SESSION_TYPE="x11")
    env.pop("WAYLAND_DISPLAY", None)
    # This session emulates the source terminal, distinct from this test's own process group.
    parent = subprocess.Popen(["bash", "-c", 'trap "exit 0" INT; bash "$1"; while :; do sleep 1; done',
                               "launcher-parent", str(root / "scripts/Play.sh")], start_new_session=True,
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=env, cwd="/tmp")
    line = parent.stdout.readline().strip()
    assert "Log: " in line, line
    log = Path(line.split("Log: ", 1)[1])
    deadline = time.monotonic() + 10
    window = None
    while time.monotonic() < deadline and window is None:
        window = find_game(x.XDefaultRootWindow(display))
        time.sleep(0.02)
    assert window, f"No game window; see {log}"
    atom = x.XInternAtom(display, b"_NET_WM_PID", False)
    actual, count, remaining, fmt, data = ulong(), ulong(), ulong(), c.c_int(), ptr()
    assert x.XGetWindowProperty(display, window, atom, 0, 1, False, 0, c.byref(actual),
        c.byref(fmt), c.byref(count), c.byref(remaining), c.byref(data)) == 0
    try:
        assert count.value == 1 and fmt.value == 32
        game_pid = c.cast(data, c.POINTER(ulong))[0]
    finally:
        if data:
            x.XFree(data)
    assert os.getsid(game_pid) != os.getsid(parent.pid), "Game shares terminal session"
    assert os.getpgid(game_pid) != os.getpgid(parent.pid), "Game shares terminal process group"
    os.killpg(parent.pid, signal.SIGINT)
    parent.wait(timeout=5)
    time.sleep(0.1)
    assert alive(game_pid), "Terminal Ctrl+C killed the game"
    event = Event()
    event.message.type, event.message.window, event.message.format = 33, window, 32
    event.message.message_type = x.XInternAtom(display, b"WM_PROTOCOLS", False)
    event.message.data[0] = x.XInternAtom(display, b"WM_DELETE_WINDOW", False)
    x.XSendEvent(display, window, False, 0, c.byref(event))
    x.XFlush(display)
    deadline = time.monotonic() + 5
    while alive(game_pid) and time.monotonic() < deadline:
        time.sleep(0.02)
    assert not alive(game_pid), "Game did not exit after window close"
    output = log.read_text()
    assert "Rendered frames:" not in output and "Platformer:" not in output, output
    print("PASS: detached Linux launcher from another cwd; terminal Ctrl+C survives; normal exit has no frame counter")
finally:
    if game_pid and alive(game_pid):
        os.kill(game_pid, signal.SIGTERM)
    if parent and parent.poll() is None:
        os.killpg(parent.pid, signal.SIGTERM)
        parent.wait(timeout=5)
    x.XCloseDisplay(display)
