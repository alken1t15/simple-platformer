#!/usr/bin/env python3
"""Linux-ярлык: настоящий gio launch, сложные пути, изоляция и отсутствие автосборки."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import unittest

SOURCE = Path(__file__).resolve().parent.parent


class DesktopInstallerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="ball-desktop-")
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.repo = self.base / "проект space % ' $ ` back\\slash"
        self.data = self.base / "data space % ' $ ` back\\slash"
        self.desktop = self.base / "Desktop"
        self.runtime = self.base / "runtime space"
        (self.repo / "scripts").mkdir(parents=True)
        (self.repo / "assets/branding").mkdir(parents=True)
        self.runtime.mkdir()
        for name in ("Play.sh", "install_desktop.py"):
            shutil.copyfile(SOURCE / "scripts" / name, self.repo / "scripts" / name)
        shutil.copyfile(SOURCE / "assets/branding/ball-adventure.png", self.repo / "assets/branding/ball-adventure.png")
        self.assembly = self.repo / "src/Platformer.App/bin/Release/net10.0/Platformer.App.dll"
        self.assembly.parent.mkdir(parents=True)
        self.assembly.write_text("Not a real assembly: fake dotnet records arguments instead")
        self.record = self.base / "record.json"
        fake = self.runtime / "dotnet"
        fake.write_text(f"#!{sys.executable}\nimport json,os,sys\nfrom pathlib import Path\n"
                        "Path(os.environ['DESKTOP_TEST_RECORD']).write_text(json.dumps("
                        "{'args':sys.argv[1:],'cwd':os.getcwd()}))\n", encoding="utf-8")
        fake.chmod(0o755)
        user_dir = self.runtime / "xdg-user-dir"
        user_dir.write_text("#!/bin/sh\nprintf '%s\\n' \"$DESKTOP_TEST_DIR\"\n")
        user_dir.chmod(0o755)
        self.env = dict(os.environ, XDG_DATA_HOME=str(self.data),
                        DESKTOP_TEST_RECORD=str(self.record), DESKTOP_TEST_DIR=str(self.desktop),
                        PATH=str(self.runtime) + os.pathsep + os.environ["PATH"], DISPLAY=":65535")

    def install(self, *args):
        return subprocess.run([sys.executable, str(self.repo / "scripts/install_desktop.py"), *args],
                              cwd="/tmp", env=self.env, capture_output=True, text=True, timeout=10)

    def test_real_desktop_launch_and_reinstall_with_reserved_characters(self):
        result = self.install("--desktop")
        self.assertEqual(0, result.returncode, result.stderr)
        entry = self.data / "applications/org.alken1t15.BallAdventure.desktop"
        self.assertEqual(entry.read_bytes(), (self.desktop / "Ball Adventure.desktop").read_bytes())
        self.assertIn("Terminal=false", entry.read_text())
        validator = shutil.which("desktop-file-validate")
        if validator:
            validation = subprocess.run([validator, str(entry)], capture_output=True, text=True, timeout=10)
            self.assertEqual(0, validation.returncode, validation.stderr + validation.stdout)
        # Сессия GUI может иметь другой PATH, чем установивший .NET пользователь.
        launch_env = dict(self.env, PATH="/usr/bin:/bin")
        launch = subprocess.run([shutil.which("gio"), "launch", str(entry)], cwd="/tmp", env=launch_env,
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(0, launch.returncode, launch.stderr)
        deadline = time.monotonic() + 5
        while not self.record.exists() and time.monotonic() < deadline:
            time.sleep(0.02)
        self.assertTrue(self.record.exists(), launch.stderr)
        recorded = json.loads(self.record.read_text())
        self.assertEqual([str(self.assembly), "--visual"], recorded["args"])
        self.assertEqual(str(self.repo), recorded["cwd"])
        self.assertEqual(0, self.install("--desktop").returncode)

    def test_missing_build_does_not_install_or_build(self):
        self.assembly.unlink()
        result = self.install()
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(self.data.exists())
        self.assertFalse(self.record.exists())

    def test_foreign_shortcut_is_not_overwritten(self):
        entry = self.data / "applications/org.alken1t15.BallAdventure.desktop"
        entry.parent.mkdir(parents=True)
        entry.write_text("user-created file")
        result = self.install()
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("user-created file", entry.read_text())
        self.assertFalse((self.data / "ball-adventure").exists())


if __name__ == "__main__":
    if not sys.platform.startswith("linux") or not shutil.which("gio"):
        sys.exit("Для этой проверки нужны Linux и gio (libglib2.0-bin)")
    unittest.main()
