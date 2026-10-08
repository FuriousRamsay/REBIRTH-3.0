#!/usr/bin/env python3
"""Static regression gate for 2026-08-26 Survivor Compile Fix 02.

This does not claim a C# compile. It specifically prevents recurrence of the compile failures
reported from the installed RebirthUtils project by checking restored dependency definitions,
SDK default source inclusion, and removal of version-specific direct API dependencies.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


class Results:
    def __init__(self) -> None:
        self.passed = 0
        self.failures: list[str] = []

    def check(self, name: str, ok: bool, detail: str = "") -> None:
        if ok:
            self.passed += 1
        else:
            self.failures.append(name + (f": {detail}" if detail else ""))


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def run(root: Path) -> Results:
    r = Results()
    paths = {
        "factory": root / "Scripts/Survivor/Definitions/RebirthSkillAptitudeTraitFactory.cs",
        "diet": root / "Scripts/Survivor/UI/RebirthDietFoodCatalogue.cs",
        "models": root / "Scripts/Survivor/Domain/RebirthSurvivorDefinitionModels.cs",
        "wavea": root / "Scripts/Survivor/Progression/RebirthSkillWaveAPatches.cs",
        "weapon": root / "Scripts/Survivor/Progression/RebirthWeaponFamilySkillService.cs",
        "project": root / "RebirthUtils.csproj",
    }
    for name, path in paths.items():
        r.check(f"{name} file exists", path.exists(), str(path.relative_to(root)))
    if any(not p.exists() for p in paths.values()):
        return r

    factory = read(paths["factory"])
    diet = read(paths["diet"])
    models = read(paths["models"])
    wavea = read(paths["wavea"])
    weapon = read(paths["weapon"])
    project = read(paths["project"])

    # Dependency-restoration failures from the attached compiler output.
    r.check("aptitude factory definition restored", "public static class RebirthSkillAptitudeTraitFactory" in factory)
    r.check("diet food entry definition restored", "public sealed class RebirthDietFoodEntry" in diet)
    r.check("diet food catalogue definition restored", "public static class RebirthDietFoodCatalogue" in diet)
    r.check("negative trait refund property restored", "public int MaxNegativeTraitRefund { get; private set; }" in models)
    r.check("negative trait refund constructor assignment restored", "MaxNegativeTraitRefund=Math.Max(0,maxNegativeTraitRefund)" in models)

    # Project must use SDK default Compile item discovery so newly restored files are compiled.
    r.check("SDK-style project", '<Project Sdk="Microsoft.NET.Sdk">' in project)
    r.check("default Compile items not disabled", "<EnableDefaultCompileItems>false</EnableDefaultCompileItems>" not in project.replace(" ", ""))
    r.check("Survivor sources not globally removed", '<Compile Remove="Scripts\\Survivor\\**"' not in project)

    # Version-sensitive lock/trader APIs from the compiler output must not be direct dependencies.
    r.check("no direct secure-loot tile type", "TileEntitySecureLootContainer" not in wavea)
    r.check("no direct two-arg GetTileEntity call", "world.GetTileEntity(clrIdx, blockPos)" not in wavea)
    r.check("tile lookup resolved reflectively", '"GetTileEntity"' in wavea and "ReadPickTimeLeft" in wavea)
    r.check("no direct trader entity member", ".TraderEntity" not in wavea)
    r.check("NPC trader qualification version-tolerant", "HasNpcTrader" in wavea and 'AccessTools.Field(model.GetType(), "Trader")' in wavea)
    r.check("TELockServer remains reflection-only", 'AccessTools.Method(typeof(GameManager), "TELockServer"' in wavea and "nameof(GameManager.TELockServer)" not in wavea)
    r.check("BlockSecureLoot remains reflection-only", 'AccessTools.TypeByName("BlockSecureLoot")' in wavea and "typeof(BlockSecureLoot)" not in wavea)

    # Definite-assignment failures from the weapon-family service.
    r.check("weapon passive value initialized", weapon.count("float value = 0f;") >= 2, f"count={weapon.count('float value = 0f;')}")
    r.check("old uninitialized weapon declaration absent", "float value;\n        bool hasSkill" not in weapon and "float value;\n        bool available" not in weapon)

    return r


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = ap.parse_args()
    result = run(args.root.resolve())
    total = result.passed + len(result.failures)
    status = "PASS" if not result.failures else "FAIL"
    print(f"REBIRTH Survivor Compile Fix 02 vectors: {status}")
    print(f"passed={result.passed}/{total}")
    print("compile_validation_claimed=False")
    for failure in result.failures:
        print("FAIL: " + failure)
    return 0 if not result.failures else 1


if __name__ == "__main__":
    sys.exit(main())
