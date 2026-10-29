"""Check repository docs, CPU dependency boundary and initialized pinned engine."""
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
errors = []
required = [
    "README.md", "AGENTS.md", "CONTRIBUTING.md", "LICENSE", "THIRD_PARTY.md",
    "docs/architecture.md", "docs/project-scope.md", "docs/development-process.md",
    "Platformer.sln", "global.json", ".gitmodules",
    "scripts/Verify.ps1", ".github/workflows/ci.yml",
]
for name in required:
    if not (ROOT / name).is_file():
        errors.append(f"Missing required file: {name}")

documents = [*ROOT.glob("*.md"), *ROOT.glob("docs/**/*.md"), *ROOT.glob(".github/*.md")]
for path in documents:
    content = re.sub(r"```.*?```", "", path.read_text(encoding="utf-8"), flags=re.S)
    for match in re.finditer(r"!?\[[^\]]*\]\(([^)]+)\)", content):
        target = match.group(1).strip().strip("<>")
        if re.match(r"(?:[a-zA-Z][\w+.-]*:|#)", target):
            continue
        target = unquote(target.split("#", 1)[0])
        if target and not (path.parent / target).exists():
            errors.append(f"Broken local link: {path.relative_to(ROOT)} -> {target}")

core = ROOT / "src/Platformer.Core"
try:
    project = ET.parse(core / "Platformer.Core.csproj").getroot()
    if any(project.findall(f".//{tag}") for tag in ("ProjectReference", "PackageReference", "Reference")):
        errors.append("Platformer.Core must use only standard .NET")
    for path in core.rglob("*.cs"):
        if any(part in ("bin", "obj") for part in path.relative_to(core).parts):
            continue
        if re.search(r"\b(?:Engine3D|OpenTK)\b", path.read_text(encoding="utf-8")):
            errors.append(f"Engine dependency in CPU Core: {path.name}")
except (OSError, ET.ParseError) as error:
    errors.append(f"Cannot inspect Core: {error}")

def git(*args):
    result = subprocess.run(
        ["git", "-c", f"safe.directory={ROOT.as_posix()}",
         "-c", f"safe.directory={(ROOT / 'external/engine').as_posix()}", *args],
        cwd=ROOT, text=True, capture_output=True, check=True,
    )
    return result.stdout.strip()

try:
    entry = git("ls-files", "--stage", "--", "external/engine").split()
    if len(entry) != 4 or entry[0] != "160000":
        errors.append("Engine must be a tracked submodule")
    elif git("-C", "external/engine", "rev-parse", "HEAD") != entry[1]:
        errors.append("Engine checkout differs from pinned gitlink; initialize submodules")
    elif git("-C", "external/engine", "status", "--porcelain"):
        errors.append("Engine submodule has local modifications")
except (OSError, subprocess.CalledProcessError) as error:
    errors.append(f"Cannot verify engine submodule: {error}")

if errors:
    print("\n".join(errors), file=sys.stderr)
    sys.exit(1)
print(f"PASS: {len(required)} required files, {len(documents)} Markdown files, Core boundary, pinned engine")
