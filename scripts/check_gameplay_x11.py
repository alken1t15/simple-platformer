#!/usr/bin/env python3
"""Нативный X11/XTest-прогон управления и меню #6/#19 (адаптация engine check_input_x11.py, MIT). Запускать на отдельном тестовом дисплее.

Требует собранный Platformer.App, DISPLAY, libX11 и libXtst; Python-пакеты не нужны.
Синтезирует ввод и переключает фокус; это не ручная проверка устройства/WM/HiDPI.
"""

import ctypes as c
import argparse
import json
import os
from pathlib import Path
import queue
import shutil
import subprocess
import sys
import threading
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/input-camera", help="Каталог лога и снимка сцены.")
    output = Path(parser.parse_args().output)
    output.mkdir(parents=True, exist_ok=True)
    trace = (output / "trace.log").open("w", encoding="utf-8")
    if not sys.platform.startswith("linux") or not os.environ.get("DISPLAY"):
        raise RuntimeError("Нужны Linux и X11 DISPLAY (например, xvfb-run).")
    x = c.CDLL("libX11.so.6")
    xt = c.CDLL("libXtst.so.6")
    pointer, window = c.c_void_p, c.c_ulong

    def bind(lib, name, result, *args):
        fn = getattr(lib, name)
        fn.restype, fn.argtypes = result, args
        return fn

    bind(x, "XOpenDisplay", pointer, c.c_char_p)
    bind(x, "XCloseDisplay", c.c_int, pointer)
    bind(x, "XDefaultRootWindow", window, pointer)
    bind(x, "XQueryTree", c.c_int, pointer, window, c.POINTER(window), c.POINTER(window), c.POINTER(c.POINTER(window)), c.POINTER(c.c_uint))
    bind(x, "XFetchName", c.c_int, pointer, window, c.POINTER(pointer))
    bind(x, "XFree", c.c_int, pointer)
    bind(x, "XSetInputFocus", c.c_int, pointer, window, c.c_int, c.c_ulong)
    bind(x, "XGetInputFocus", c.c_int, pointer, c.POINTER(window), c.POINTER(c.c_int))
    bind(x, "XMapRaised", c.c_int, pointer, window)
    bind(x, "XCreateSimpleWindow", window, pointer, window, c.c_int, c.c_int,
         c.c_uint, c.c_uint, c.c_uint, c.c_ulong, c.c_ulong)
    bind(x, "XDestroyWindow", c.c_int, pointer, window)
    bind(x, "XStoreName", c.c_int, pointer, window, c.c_char_p)
    bind(x, "XResizeWindow", c.c_int, pointer, window, c.c_uint, c.c_uint)
    bind(x, "XWarpPointer", c.c_int, pointer, window, window, c.c_int, c.c_int, c.c_uint, c.c_uint, c.c_int, c.c_int)
    bind(x, "XQueryPointer", c.c_int, pointer, window, c.POINTER(window), c.POINTER(window),
         c.POINTER(c.c_int), c.POINTER(c.c_int), c.POINTER(c.c_int), c.POINTER(c.c_int), c.POINTER(c.c_uint))
    bind(x, "XStringToKeysym", c.c_ulong, c.c_char_p)
    bind(x, "XKeysymToKeycode", c.c_uint, pointer, c.c_ulong)
    bind(x, "XFlush", c.c_int, pointer)
    bind(x, "XSync", c.c_int, pointer, c.c_int)
    class XError(c.Structure):
        _fields_ = [("type", c.c_int), ("display", pointer), ("resource", window),
                    ("serial", c.c_ulong), ("code", c.c_ubyte), ("request", c.c_ubyte), ("minor", c.c_ubyte)]

    errors = []
    error_callback = c.CFUNCTYPE(c.c_int, pointer, c.POINTER(XError))

    @error_callback
    def on_error(_, event):
        errors.append((event.contents.code, event.contents.request))
        return 0

    bind(x, "XSetErrorHandler", pointer, error_callback)(on_error)
    bind(xt, "XTestFakeKeyEvent", c.c_int, pointer, c.c_uint, c.c_int, c.c_ulong)
    bind(xt, "XTestFakeButtonEvent", c.c_int, pointer, c.c_uint, c.c_int, c.c_ulong)
    bind(xt, "XTestFakeRelativeMotionEvent", c.c_int, pointer, c.c_int, c.c_int, c.c_ulong)
    display = x.XOpenDisplay(None)
    if not display:
        raise RuntimeError("Не удалось открыть DISPLAY.")
    root = x.XDefaultRootWindow(display)
    old_focus, revert = window(), c.c_int()
    x.XGetInputFocus(display, c.byref(old_focus), c.byref(revert))
    rr, child = window(), window()
    rx, ry, wx, wy, mask = c.c_int(), c.c_int(), c.c_int(), c.c_int(), c.c_uint()
    restore_pointer = x.XQueryPointer(display, root, c.byref(rr), c.byref(child), c.byref(rx), c.byref(ry), c.byref(wx), c.byref(wy), c.byref(mask))
    process = None
    focus_sink = None
    held_keys, held_buttons = set(), set()
    messages = queue.Queue()

    def read_lines():
        for line in process.stdout:
            trace.write(line)
            trace.flush()
            try:
                messages.put(json.loads(line))
            except json.JSONDecodeError:
                pass

    def receive(timeout):
        try:
            value = messages.get(timeout=timeout)
        except queue.Empty:
            raise AssertionError("Нет ожидаемого snapshot; Platformer.App завершился или завис.") from None
        if value.get("kind") == "error":
            raise AssertionError(value["text"])
        return value

    def until(predicate, label):
        deadline = time.monotonic() + 4
        last = None
        while time.monotonic() < deadline:
            try:
                last = receive(max(0.01, deadline - time.monotonic()))
            except AssertionError as error:
                raise AssertionError(f"{label}: {error}; последний snapshot {last}") from error
            if predicate(last):
                return last
        raise AssertionError(f"{label}: последний snapshot {last}")

    def collect(seconds=0.08):
        result = []
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            try:
                item = messages.get(timeout=max(0.001, deadline - time.monotonic()))
            except queue.Empty:
                break
            if item.get("kind") == "error":
                raise AssertionError(item["text"])
            result.append(item)
        return [item for item in result if item.get("kind") == "frame"]

    def focus(target):
        # WM может ещё отображать/переподчинять окно после MapNotify.
        for _ in range(20):
            errors.clear()
            x.XSetInputFocus(display, target, 1, 0)
            x.XSync(display, False)
            if not errors:
                return
            time.sleep(0.05)
        raise AssertionError(f"Не удалось установить фокус: X11 {errors}")

    def key(symbol, down):
        code = x.XKeysymToKeycode(display, x.XStringToKeysym(symbol.encode()))
        assert code, symbol
        xt.XTestFakeKeyEvent(display, code, down, 0)
        (held_keys.add if down else held_keys.discard)(code)
        x.XFlush(display)

    def button(number, down):
        xt.XTestFakeButtonEvent(display, number, down, 0)
        (held_buttons.add if down else held_buttons.discard)(number)
        x.XFlush(display)

    def find_window(parent, title):
        name = pointer()
        if x.XFetchName(display, parent, c.byref(name)) and name.value:
            try:
                if c.string_at(name).decode(errors="replace") == title:
                    return parent
            finally:
                x.XFree(name)
        parent_return, root_return, count = window(), window(), c.c_uint()
        children = c.POINTER(window)()
        if not x.XQueryTree(display, parent, c.byref(root_return), c.byref(parent_return), c.byref(children), c.byref(count)):
            return None
        try:
            for i in range(count.value):
                found = find_window(children[i], title)
                if found:
                    return found
        finally:
            if children:
                x.XFree(children)
        return None

    try:
        assembly = Path(__file__).resolve().parents[1] / "src/Platformer.App/bin/Release/net10.0/Platformer.App.dll"
        env = dict(os.environ, XDG_SESSION_TYPE="x11", ALSOFT_DRIVERS="null")
        env.pop("WAYLAND_DISPLAY", None)
        process = subprocess.Popen(["dotnet", str(assembly), "--play", "--trace-input"], stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, text=True, env=env)
        reader = threading.Thread(target=read_lines, daemon=True)
        reader.start()
        until(lambda f: f.get("frame", 0) >= 3, "startup/render")
        native = find_window(root, "Ball Adventure")
        assert native, "Окно Ball Adventure не найдено."
        x.XMapRaised(display, native)
        focus(native)
        until(lambda f: f["focused"], "focus")
        collect()

        def has(frame, field, name):
            return name in frame[field].split(", ")

        def press(symbol):
            collect()
            key(symbol, True)
            time.sleep(0.05)
            key(symbol, False)

        def latest():
            frames = collect(0.05)
            return frames[-1] if frames else receive(1)

        def click_control(control_id, screen):
            frame = latest()
            control = next(control for control in frame["controls"] if control["id"] == control_id)
            px, py = int(control["x"] + control["width"] / 2), int(control["y"] + control["height"] / 2)
            x.XWarpPointer(display, 0, native, 0, 0, 0, 0, px, py)
            x.XFlush(display)
            until(lambda f: abs(f["pointerX"] - px) < 1 and abs(f["pointerY"] - py) < 1, "hover " + control_id)
            button(1, True)
            clicked = until(lambda f: has(f, "pressed", "PointerLeft"), "click " + control_id)
            button(1, False)
            # Exit завершает Run в этом callback: следующий snapshot уже не обязан появиться.
            if clicked["screen"] == screen:
                return clicked
            return until(lambda f: f["screen"] == screen, "screen after " + control_id)

        def capture(name):
            if not shutil.which("import"):
                return
            frame = latest()
            until(lambda f: f["frame"] >= frame["frame"] + 2, "render before " + name)
            subprocess.run(["import", "-window", str(native), str(output / (name + ".png"))], check=True)

        for symbol, name in [("w", "W"), ("a", "A"), ("s", "S"), ("d", "D"),
                             ("Shift_L", "LeftShift"), ("Shift_R", "RightShift"),
                             ("Up", "Up"), ("Down", "Down"), ("Left", "Left"),
                             ("Right", "Right"), ("Tab", "Tab")]:
            key(symbol, True)
            f = until(lambda f: has(f, "down", name) and has(f, "pressed", name), name + " down")
            assert f["screen"] == "MainMenu" and f["tick"] is None and f["cursor"] == "Normal"
            key(symbol, False)
            until(lambda f: has(f, "released", name), name + " up")
        x.XWarpPointer(display, 0, native, 0, 0, 0, 0, 100, 100)
        x.XFlush(display)
        until(lambda f: abs(f["pointerX"] - 100) < 1 and abs(f["pointerY"] - 100) < 1, "pointer mapping")
        button(1, True)
        until(lambda f: has(f, "pressed", "PointerLeft"), "pointer press")
        button(1, False)
        until(lambda f: has(f, "released", "PointerLeft"), "pointer release")
        print("PASS: native adapter, клавиши/фронты/указатель; меню без ticks/capture")

        capture("main-menu")
        levels = click_control("levels", "LevelSelect")
        assert {control["id"] for control in levels["controls"] if control["id"].startswith("level:")} == {"level:level1", "level:level2"}
        click_control("level:level2", "LevelSelect")
        direct = click_control("start", "Playing")
        assert direct["level"] == "level2" and direct["coins"] == 0
        press("Escape")
        until(lambda f: f["screen"] == "Paused", "direct level2 pause")
        click_control("main-menu", "MainMenu")
        click_control("levels", "LevelSelect")
        x.XResizeWindow(display, native, 80, 48)
        x.XFlush(display)
        until(lambda f: f["clientWidth"] == 800 and f["clientHeight"] == 450, "minimum resize constraint")
        x.XResizeWindow(display, native, 10000, 10000)
        x.XFlush(display)
        until(lambda f: 800 <= f["clientWidth"] <= 1280 and 450 <= f["clientHeight"] <= 800, "maximum resize constraint")
        x.XResizeWindow(display, native, 800, 450)
        x.XFlush(display)
        until(lambda f: f["clientWidth"] == 800 and f["clientHeight"] == 450, "menu resize logical geometry")
        capture("level-select")
        click_control("back", "MainMenu")
        click_control("settings", "Settings")
        click_control("volume", "Settings")
        until(lambda f: abs(f["audioVolume"] - 0.5) < 0.02, "volume slider -> AudioDevice")
        press("Left")
        until(lambda f: abs(f["audioVolume"] - 0.45) < 0.02, "keyboard volume -> AudioDevice")
        click_control("mute", "Settings")
        until(lambda f: f["muted"] and f["audioVolume"] == 0, "mute -> AudioDevice")
        click_control("mute", "Settings")
        until(lambda f: not f["muted"] and abs(f["audioVolume"] - 0.45) < 0.02, "unmute restores volume")
        chosen = click_control("resolution", "Settings")
        until(lambda f: any(control["label"] == "Resolution: < 960 x 540 >" for control in f["controls"]), "resolution mouse selection")
        assert chosen["clientWidth"] == 800 and chosen["clientHeight"] == 450, "selection must wait for Apply"
        click_control("apply-resolution", "Settings")
        until(lambda f: f["clientWidth"] == 960 and f["clientHeight"] == 540
              and "Current window: 960 x 540" in f["uiTexts"], "apply chosen resolution")
        click_control("resolution", "Settings")
        press("Right")
        until(lambda f: any(control["label"] == "Resolution: < 1024 x 576 >" for control in f["controls"]), "resolution keyboard selection")
        click_control("apply-resolution", "Settings")
        until(lambda f: f["clientWidth"] == 1024 and f["clientHeight"] == 576, "apply keyboard resolution")
        capture("settings")
        click_control("back", "MainMenu")
        click_control("skins", "SkinSelect")
        click_control("skin:orange", "SkinSelect")
        click_control("select", "SkinSelect")
        until(lambda f: f["skin"] == "orange", "apply skin")
        capture("skins")
        click_control("back", "MainMenu")
        x.XResizeWindow(display, native, 960, 640)
        x.XFlush(display)
        until(lambda f: f["clientWidth"] == 960 and f["clientHeight"] == 640, "restore menu size")
        print("PASS: видимые MainMenu/LevelSelect/Settings/Skins, resize hit-test, master volume/mute и skin")

        start = click_control("play", "Playing")
        assert start["cursor"] == "Captured"
        assert any(text.startswith("Coins ") for text in start["uiTexts"]), "HUD не подключён"
        key("w", True)
        until(lambda f: f["z"] < start["z"] - 0.03 and f["moveZ"] < -0.9, "W movement")
        key("w", False)
        until(lambda f: has(f, "released", "W"), "W release")
        # Engine намеренно подавляет первое событие motion после смены cursor mode.
        before = collect(0.1)[-1]
        xt.XTestFakeRelativeMotionEvent(display, 2, 2, 0)
        x.XFlush(display)
        first_motion = until(lambda f: abs(f["pointerX"] - before["pointerX"]) > 1, "first capture motion")
        assert first_motion["yaw"] == 0 and first_motion["pitch"] == 0.4, "Скачок на первом motion"
        collect()
        xt.XTestFakeRelativeMotionEvent(display, 40, -20, 0)
        x.XFlush(display)
        look = until(lambda f: abs(f["yaw"] + 0.12) < 0.002 and abs(f["pitch"] - 0.34) < 0.002, "mouse yaw/pitch without inversion")
        xt.XTestFakeRelativeMotionEvent(display, 0, 40, 0)
        x.XFlush(display)
        until(lambda f: abs(f["yaw"] - look["yaw"]) < 0.002 and abs(f["pitch"] - 0.46) < 0.002,
              "mouse down lowers view")
        key("d", True)
        until(lambda f: f["moveX"] > 0.98 and f["moveZ"] > 0.1, "yaw-relative D")
        key("d", False)
        until(lambda f: has(f, "released", "D"), "D release")
        x.XResizeWindow(display, native, 800, 600)
        x.XFlush(display)
        resized = until(lambda f: f["width"] == 800 and f["height"] == 600, "resize")
        assert resized["cursor"] == "Captured" and abs(resized["yaw"] - look["yaw"]) < 0.002
        print("PASS: Enter/Start/capture, движение, mouse yaw/pitch и resize")

        press("Escape")
        paused = until(lambda f: f["screen"] == "Paused" and f["cursor"] == "Normal", "pause")
        assert all(f["tick"] == paused["tick"] for f in collect(0.15)), "Пауза продвигает время"
        capture("paused")
        click_control("settings", "Settings")
        assert all(f["tick"] == paused["tick"] for f in collect(0.1)), "Настройки сняли паузу"
        click_control("back", "Paused")
        key("w", True)
        until(lambda f: has(f, "down", "W"), "W held in pause")
        press("Return")
        until(lambda f: f["screen"] == "Playing" and f["cursor"] == "Captured", "continue")
        frames = collect(0.15)
        assert frames and all(f["moveX"] == f["moveZ"] == 0 for f in frames), "W не заблокирована после Continue"
        key("w", False)
        until(lambda f: has(f, "released", "W"), "W release after continue")
        key("w", True)
        until(lambda f: f["moveZ"] < -0.9, "new W after continue")
        key("w", False)
        until(lambda f: has(f, "released", "W"), "W up")

        press("r")
        restarted = until(lambda f: f["session"] != start["session"] and f["screen"] == "Playing", "restart")
        assert restarted["tick"] == 0 and restarted["yaw"] == 0 and restarted["pitch"] == 0.4, restarted
        until(lambda f: f["tick"] >= 2, "ground contact")
        key("space", True)
        until(lambda f: f["jump"] and has(f, "pressed", "Space"), "jump edge")
        until(lambda f: f["y"] > 0.6, "jump physics")
        assert all(not f["jump"] for f in collect(0.2)), "Повтор прыжка при удержании"
        key("space", False)
        until(lambda f: has(f, "released", "Space"), "jump release")
        for symbol, name in [("Shift_L", "LeftShift"), ("Shift_R", "RightShift")]:
            press("r")
            previous = restarted["session"]
            restarted = until(lambda f: f["session"] != previous, "restart before " + name)
            key(symbol, True)
            until(lambda f: f["boostPressed"] and has(f, "pressed", name), name + " boost edge")
            until(lambda f: f["boost"] > 0, name + " boost physics")
            assert all(not f["boostPressed"] for f in collect(0.15)), "Повтор boost при удержании"
            key(symbol, False)
            until(lambda f: has(f, "released", name), name + " release")
        print("PASS: pause/Continue/rearm, R reset, Space и оба Shift через реальную физику")

        # Удержание W при focus loss; отдельное тестовое окно не зависит от наличия WM.
        focus_sink = x.XCreateSimpleWindow(display, root, 10, 10, 160, 90, 0, 0, 0)
        x.XStoreName(display, focus_sink, b"Platformer input focus sink")
        x.XMapRaised(display, focus_sink)
        x.XSync(display, False)
        key("w", True)
        focus(focus_sink)
        lost = until(lambda f: not f["focused"], "focus loss")
        assert lost["screen"] == "Paused" and lost["cursor"] == "Normal"
        focus(native)
        until(lambda f: f["focused"], "focus gain")
        frames = collect(1.3)
        assert frames and all(f["screen"] == "Paused" and f["cursor"] == "Normal" and f["tick"] == lost["tick"]
                              and not has(f, "down", "W") for f in frames), "Возврат сам продолжил игру или rearm пропустил W"
        press("Return")
        until(lambda f: f["screen"] == "Playing" and f["cursor"] == "Captured", "explicit Continue after focus")
        assert all(f["moveZ"] == 0 for f in collect(0.1)), "Удержание прошло через focus rearm"
        key("w", False)
        collect(0.1)  # Непринятая native клавиша не обязана выдавать Released.
        key("w", True)
        until(lambda f: f["moveZ"] < -0.9, "new W after focus rearm")
        key("w", False)
        until(lambda f: has(f, "released", "W"), "final W release")
        print("PASS: focus loss/gain, без auto-resume, удержание дольше autorepeat и release/new press")

        x.XDestroyWindow(display, focus_sink)
        focus_sink = None
        x.XSync(display, False)
        collect()
        capture("training-track")
        press("Escape")
        until(lambda f: f["screen"] == "Paused", "final pause")
        click_control("main-menu", "MainMenu")
        press("Escape")
        assert all(f["screen"] == "MainMenu" for f in collect()), "Escape закрыл MainMenu"
        click_control("exit", "MainMenu")
        assert process.wait(timeout=5) == 0, "Нештатный exit"
        reader.join(timeout=2)
        print("PASS: возврат к старту, release capture и штатный exit")
    finally:
        for code in held_keys:
            xt.XTestFakeKeyEvent(display, code, False, 0)
        for number in held_buttons:
            xt.XTestFakeButtonEvent(display, number, False, 0)
        x.XSync(display, False)
        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=3)
        if focus_sink:
            x.XDestroyWindow(display, focus_sink)
        if old_focus.value not in (0, 1):
            focus(old_focus.value)
        if restore_pointer:
            x.XWarpPointer(display, 0, root, 0, 0, 0, 0, rx.value, ry.value)
        x.XSync(display, False)
        x.XCloseDisplay(display)
        if process is not None:
            reader.join(timeout=2)
        trace.close()


if __name__ == "__main__":
    main()
