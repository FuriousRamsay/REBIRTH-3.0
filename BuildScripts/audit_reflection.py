#!/usr/bin/env python3
"""Fail when unapproved reflection is introduced into REBIRTH source.

Approved exceptions are deliberately narrow:
- four transpilers that inspect/replace IL operands during Harmony installation;
- the Block Pickup subclass-override installer, which must enumerate concrete
  Block overrides because Harmony does not propagate a base virtual patch into
  overridden methods.

No approved file may use MethodInfo.Invoke, FieldInfo.GetValue/SetValue, or
PropertyInfo.GetValue/SetValue in gameplay code.
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "Scripts"

APPROVED_FILES = {
    "Scripts/Rebirth/Modules/Combat/HeadshotOnly/RebirthHeadshotOnlyBehavior.cs",
    "Scripts/Rebirth/Patching/RebirthBlockPickupPatchInstaller.cs",
    "Scripts/Rebirth/Patching/RebirthUniformAtmospherePatchInstaller.cs",
    "Scripts/Rebirth/Patching/RebirthWeatherFogPatchInstaller.cs",
    "Scripts/Rebirth/Spawning/RebirthSleeperSpawnMultiplier.cs",
}

REFLECTION_PATTERNS = [
    re.compile(r"\busing\s+System\.Reflection(?:\.Emit)?\s*;"),
    re.compile(r"\bBindingFlags\b"),
    re.compile(r"\b(?:FieldInfo|PropertyInfo|MethodInfo|ConstructorInfo|ParameterInfo)\b"),
    re.compile(r"\bAccessTools\."),
    re.compile(r"\.(?:GetField|GetFields|GetProperty|GetProperties|GetMethod|GetMethods|GetConstructors|GetTypes)\s*\("),
    re.compile(r"\bActivator\.CreateInstance\s*\("),
]

FORBIDDEN_EVERYWHERE = [
    re.compile(r"\b(?:MethodInfo|ConstructorInfo)\b[^;\n]*\.Invoke\s*\("),
    re.compile(r"\b(?:FieldInfo|PropertyInfo)\b[^;\n]*\.(?:GetValue|SetValue)\s*\("),
]


def scrub_comments_and_strings(text: str) -> str:
    out: list[str] = []
    i = 0
    state = "code"
    while i < len(text):
        c = text[i]
        n = text[i + 1] if i + 1 < len(text) else ""
        if state == "code":
            if c == "/" and n == "/":
                state = "line"
                out.extend("  ")
                i += 2
                continue
            if c == "/" and n == "*":
                state = "block"
                out.extend("  ")
                i += 2
                continue
            if c == '"':
                state = "string"
                out.append(" ")
                i += 1
                continue
            if c == "'":
                state = "char"
                out.append(" ")
                i += 1
                continue
            out.append(c)
            i += 1
            continue
        if state == "line":
            if c == "\n":
                state = "code"
                out.append("\n")
            else:
                out.append(" ")
            i += 1
            continue
        if state == "block":
            if c == "*" and n == "/":
                state = "code"
                out.extend("  ")
                i += 2
            else:
                out.append("\n" if c == "\n" else " ")
                i += 1
            continue
        if c == "\\" and i + 1 < len(text):
            out.extend("  ")
            i += 2
            continue
        if (state == "string" and c == '"') or (state == "char" and c == "'"):
            state = "code"
        out.append("\n" if c == "\n" else " ")
        i += 1
    return "".join(out)


def main() -> int:
    violations: list[str] = []
    approved_hits: dict[str, int] = {path: 0 for path in APPROVED_FILES}

    for path in sorted(SOURCE_ROOT.rglob("*.cs")):
        rel = path.relative_to(ROOT).as_posix()
        raw = path.read_text(encoding="utf-8", errors="replace")
        text = scrub_comments_and_strings(raw)

        for number, line in enumerate(text.splitlines(), 1):
            if any(pattern.search(line) for pattern in FORBIDDEN_EVERYWHERE):
                violations.append(f"{rel}:{number}: reflective invocation/member access is forbidden")

            if any(pattern.search(line) for pattern in REFLECTION_PATTERNS):
                if rel in APPROVED_FILES:
                    approved_hits[rel] += 1
                else:
                    violations.append(f"{rel}:{number}: unapproved reflection metadata use")

    for rel, count in approved_hits.items():
        if count == 0:
            violations.append(f"{rel}: approved reflection exception is stale and should be removed")

    if violations:
        print("Reflection audit failed:")
        for violation in violations:
            print("  " + violation)
        return 1

    print("Reflection audit passed: only five documented install-time/IL exceptions remain; no reflective invocation remains.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
