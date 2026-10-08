#!/usr/bin/env python3
"""Pure-data/static contract vectors for REBIRTH Survivor Post-Revision-2 Chunk 5 Skill Wave A.

This tool does not load game assemblies and therefore never claims C# compile/runtime validation.
It verifies the signed tuning math plus the authored XML/C# integration contracts that can be
checked outside 7DTD.
"""
from __future__ import annotations

import argparse
import math
import re
import sys
from pathlib import Path
import xml.etree.ElementTree as ET


class Results:
    def __init__(self):
        self.passed = 0
        self.failures: list[str] = []

    def check(self, name: str, ok: bool, detail: str = "") -> None:
        if ok:
            self.passed += 1
        else:
            self.failures.append(name + (f": {detail}" if detail else ""))


def fattr(node: ET.Element, name: str) -> float:
    raw = node.get(name)
    if raw is None:
        raise KeyError(name)
    return float(raw)


def signed(value: float, negative_at_minus50: float, positive_at100: float) -> float:
    v = min(100.0, max(-50.0, value))
    if v < 0.0:
        return negative_at_minus50 * (-v / 50.0)
    if v > 0.0:
        return positive_at100 * (v / 100.0)
    return 0.0


def near(a: float, b: float, eps: float = 1e-6) -> bool:
    return abs(a - b) <= eps


def run(root: Path) -> Results:
    r = Results()
    cfg_path = root / "Config/_Survivor/skill_sources.xml"
    buffs_path = root / "Config/buffs.xml"
    service_path = root / "Scripts/Survivor/Progression/RebirthSkillWaveAService.cs"
    patches_path = root / "Scripts/Survivor/Progression/RebirthSkillWaveAPatches.cs"
    net_path = root / "Scripts/Survivor/Progression/RebirthSkillWaveANetPackages.cs"
    installer_path = root / "Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs"
    router_path = root / "Scripts/Survivor/Progression/RebirthSkillEventRouter.cs"
    debug_path = root / "Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs"
    csharp_vectors = root / "Scripts/Survivor/Progression/RebirthSkillWaveAVectorHarness.cs"

    required = [cfg_path, buffs_path, service_path, patches_path, net_path, installer_path, router_path, debug_path, csharp_vectors]
    for p in required:
        r.check(f"file exists {p.relative_to(root)}", p.exists())
    if any(not p.exists() for p in required):
        return r

    cfg_root = ET.parse(cfg_path).getroot()
    wave = cfg_root.find("wave_a")
    r.check("wave_a config exists", wave is not None)
    if wave is None:
        return r

    # Signed curve universal invariants.
    r.check("zero neutral", near(signed(0, .5, -.4), 0))
    r.check("minus50 endpoint", near(signed(-50, .5, -.4), .5))
    r.check("plus100 endpoint", near(signed(100, .5, -.4), -.4))
    r.check("negative interpolation", near(signed(-25, .5, -.4), .25))
    r.check("positive interpolation", near(signed(50, .5, -.4), -.2))
    r.check("lower clamp", near(signed(-500, .5, -.4), .5))
    r.check("upper clamp", near(signed(500, .5, -.4), -.4))

    # Runtime cadence / hot-path guard.
    sample = fattr(wave, "sample_seconds")
    passive_sync = fattr(wave, "passive_sync_seconds")
    max_sample = fattr(wave, "max_sample_distance")
    r.check("movement sample cadence bounded", .25 <= sample <= 2.0, str(sample))
    r.check("passive sync cadence bounded", .25 <= passive_sync <= 2.0, str(passive_sync))
    r.check("passive sync not per-frame", passive_sync >= .25, str(passive_sync))
    r.check("max sample teleport guard", 3.0 <= max_sample <= 20.0, str(max_sample))

    # Lockpicking.
    lnt, lpt = fattr(wave, "lockpick_negative_time"), fattr(wave, "lockpick_positive_time")
    lnb, lpb = fattr(wave, "lockpick_negative_break"), fattr(wave, "lockpick_positive_break")
    weak_time, strong_time = 1 + signed(-50, lnt, lpt), 1 + signed(100, lnt, lpt)
    weak_break, strong_break = 1 + signed(-50, lnb, lpb), 1 + signed(100, lnb, lpb)
    r.check("lock weak slower", weak_time > 1)
    r.check("lock strong faster nonzero", 0 < strong_time < 1)
    r.check("lock weak more break prone", weak_break > 1)
    r.check("lock strong break chance nonzero", 0 < strong_break < 1)
    r.check("lock award bounded", 0 < fattr(wave, "lockpick_success_award") <= 1)

    # Bartering.
    bn, bp = fattr(wave, "barter_negative"), fattr(wave, "barter_positive")
    r.check("barter weak worsens native modifier", signed(-50, bn, bp) < 0)
    r.check("barter strong improves bounded modifier", 0 < signed(100, bn, bp) <= .20)
    base_award = fattr(wave, "barter_base_award")
    value_cap = fattr(wave, "barter_value_award_cap")
    global_cd = fattr(wave, "barter_global_seconds")
    repeat_cd = fattr(wave, "barter_repeat_seconds")
    loop_cd = fattr(wave, "barter_loop_seconds")
    r.check("barter award bounded", 0 < base_award and base_award + value_cap <= 1)
    r.check("barter global cooldown active", global_cd >= 1)
    r.check("barter repeat cooldown >= global", repeat_cd >= global_cd)
    r.check("barter buy-sell loop guard active", loop_cd >= 60)
    r.check("barter value scale positive", fattr(wave, "barter_value_scale") > 0)

    # Athletics.
    anj, apj = fattr(wave, "athletics_negative_jump"), fattr(wave, "athletics_positive_jump")
    ans, aps = fattr(wave, "athletics_negative_stamina"), fattr(wave, "athletics_positive_stamina")
    anf, apf = fattr(wave, "athletics_negative_fall"), fattr(wave, "athletics_positive_fall")
    r.check("athletics weak jump penalty", signed(-50, anj, apj) < 0)
    r.check("athletics strong jump benefit", signed(100, anj, apj) > 0)
    r.check("athletics weak stamina penalty", signed(-50, ans, aps) > 0)
    r.check("athletics strong stamina benefit", signed(100, ans, aps) < 0)
    r.check("athletics weak fall worse", signed(-50, anf, apf) > 0)
    r.check("athletics strong fall better", signed(100, anf, apf) < 0)
    r.check("athletics distance anti-macro", fattr(wave, "athletics_distance") >= 10)
    r.check("athletics award bounded", 0 < fattr(wave, "athletics_award") <= .25)

    # Stealth.
    sn, sp = fattr(wave, "stealth_negative_noise"), fattr(wave, "stealth_positive_noise")
    sln, slp = fattr(wave, "stealth_negative_light"), fattr(wave, "stealth_positive_light")
    weak_noise = 1 + signed(-50, sn, sp)
    strong_noise = 1 + signed(100, sn, sp)
    strong_light = 1 + signed(100, sln, slp)
    r.check("stealth weak noisier", weak_noise > 1)
    r.check("stealth strong quieter not silent", .5 < strong_noise < 1)
    r.check("stealth strong light not invisible", .5 < strong_light < 1)
    r.check("stealth threat radius required", fattr(wave, "stealth_threat_radius") > 0)
    r.check("stealth movement threshold required", fattr(wave, "stealth_distance") >= 5)
    r.check("stealth award bounded", 0 < fattr(wave, "stealth_award") <= .25)

    # Armor proficiency: recovery is deliberately partial.
    anb, apr = fattr(wave, "armor_negative_burden"), fattr(wave, "armor_positive_recovery")
    weak_factor, strong_factor = signed(-50, anb, apr), signed(100, anb, apr)
    for name, burden in [("medium mobility", .05), ("heavy mobility", .075)]:
        weak_net = burden - burden * weak_factor
        strong_net = burden - burden * strong_factor
        r.check(f"armor weak increases {name}", weak_factor < 0 and weak_net > burden)
        r.check(f"armor strong only partly restores {name}", 0 < strong_factor < 1 and 0 < strong_net < burden)
    r.check("armor recovery capped at half", strong_factor <= .500001)
    r.check("armor movement threshold required", fattr(wave, "armor_distance") >= 10)
    r.check("armor movement award bounded", 0 < fattr(wave, "armor_award") <= .25)
    r.check("armor combat award bounded", 0 < fattr(wave, "armor_combat_award") <= .25)

    service = service_path.read_text(encoding="utf-8", errors="replace")
    patches = patches_path.read_text(encoding="utf-8", errors="replace")
    net = net_path.read_text(encoding="utf-8", errors="replace")
    installer = installer_path.read_text(encoding="utf-8", errors="replace")
    router = router_path.read_text(encoding="utf-8", errors="replace")
    debug = debug_path.read_text(encoding="utf-8", errors="replace")

    # Source-audited standard burden constants, not invented proxy values.
    r.check("medium authored mobility burden", "b.Mobility += 0.05f" in service)
    r.check("medium authored walk burden", "b.StaminaWalk += 0.0281f" in service)
    r.check("medium authored run burden", "b.StaminaRun += 0.0562f" in service)
    r.check("medium authored noise burden", "b.Noise += 0.10f" in service)
    r.check("heavy authored mobility burden", "b.Mobility += 0.075f" in service)
    r.check("heavy authored walk burden", "b.StaminaWalk += 0.045f" in service)
    r.check("heavy authored run burden", "b.StaminaRun += 0.09f" in service)
    r.check("heavy authored noise burden", "b.Noise += 0.20f" in service)

    # Performance and server-side LBD contract markers.
    r.check("passive bridge has explicit throttle state", "nextPassiveSync" in service and "WaveAPassiveSyncSeconds" in service)
    r.check("movement sampler uses horizontal distance", "Mathf.Sqrt(dx * dx + dz * dz)" in service)
    r.check("athletics rejects vehicle movement", "player.AttachedToEntity != null" in service)
    r.check("stealth requires crouch", 'GetCustomVar("_crouching")' in service)
    r.check("stealth requires zombie threat", "EntityZombie" in service and "StealthThreatRadius" in service)
    r.check("barter global award guard present", "LastBarterAwardByPlayer" in service and "BarterGlobalSeconds" in service)
    r.check("barter same item guard present", "BarterRepeatSeconds" in service)
    r.check("barter reversal loop guard present", "BarterLoopSeconds" in service)
    r.check("armor combat independent route present", "RebirthSkillWaveAService.OnArmoredCombat" in router)

    # Harmony/native call-site bridges and install wiring.
    patch_types = ["RebirthSkillWaveALockServerPatch", "RebirthSkillWaveANetLockServerPatch", "RebirthSkillWaveALockSuccessPatch", "RebirthSkillWaveABuyPatch", "RebirthSkillWaveASellPatch"]
    for t in patch_types:
        r.check(f"installer patches {t}", f"typeof({t})" in installer)
    r.check("lock server patch resolves native method silently without compile-time member dependency", '"TELockServer"' in patches and 'typeof(GameManager).GetMethod' in patches and 'AccessTools.Method(typeof(GameManager), "TELockServer"' not in patches and 'nameof(GameManager.TELockServer)' not in patches)
    r.check("lock success patch resolves native secure loot event silently without compile-time type dependency", 'FindTypeSilently(typeof(Block).Assembly, "BlockSecureLoot")' in patches and "EventData_Event" in patches and 'AccessTools.TypeByName("BlockSecureLoot")' not in patches and "typeof(BlockSecureLoot)" not in patches)
    r.check("buy patch native trader action", "ItemActionEntryPurchase" in patches and "OnActivated" in patches)
    r.check("sell patch native trader action", "ItemActionEntrySell" in patches and "OnActivated" in patches)
    r.check("client evidence package validates sender", "ValidEntityIdForSender(playerId)" in net and "PackageDirection.ToServer" in net)

    # Buff bridge exact surface: 13 passive entries, zero-neutral CVars, no hidden supernatural effects.
    buffs_root = ET.parse(buffs_path).getroot()
    buff = buffs_root.find(".//buff[@name='RebirthSurvivorSkillWaveAPassives']")
    r.check("wave a passive buff exists", buff is not None)
    if buff is not None:
        passives = buff.findall(".//passive_effect")
        r.check("wave a passive count", len(passives) == 13, f"count={len(passives)}")
        expected = {
            ("LockPickTime", "", "@$rbSurvivorSkillLockpickTime"),
            ("LockPickBreakChance", "", "@$rbSurvivorSkillLockpickBreak"),
            ("BarteringBuying", "", "@$rbSurvivorSkillBarterBuying"),
            ("BarteringSelling", "", "@$rbSurvivorSkillBarterSelling"),
            ("JumpStrength", "", "@$rbSurvivorSkillAthleticsJump"),
            ("StaminaLoss", "jumping,running,swimmingRun", "@$rbSurvivorSkillAthleticsStamina"),
            ("FallDamageReduction", "", "@$rbSurvivorSkillAthleticsFall"),
            ("NoiseMultiplier", "", "@$rbSurvivorSkillStealthNoise"),
            ("LightMultiplier", "", "@$rbSurvivorSkillStealthLight"),
            ("Mobility", "", "@$rbSurvivorSkillArmorMobility"),
            ("StaminaChangeOT", "walking", "@$rbSurvivorSkillArmorStaminaWalk"),
            ("StaminaChangeOT", "running", "@$rbSurvivorSkillArmorStaminaRun"),
            ("NoiseMultiplier", "", "@$rbSurvivorSkillArmorNoise"),
        }
        actual = {(p.get("name", ""), p.get("tags", ""), p.get("value", "")) for p in passives}
        r.check("wave a passive surface exact", actual == expected, f"missing={sorted(expected-actual)} extra={sorted(actual-expected)}")
        r.check("wave a buff hidden", buff.get("hidden") == "true")

    # Debug/vector discoverability.
    r.check("debug wavea command", 'mode=="wavea"' in debug and "RebirthSkillWaveAService.BuildDebugReport" in debug)
    r.check("debug wavea vectors command", "RebirthSkillWaveAVectorHarness.RunAll()" in debug)

    return r


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = ap.parse_args()
    result = run(args.root.resolve())
    total = result.passed + len(result.failures)
    status = "PASS" if not result.failures else "FAIL"
    print(f"REBIRTH Survivor Chunk 5 Skill Wave A vectors: {status}")
    print(f"passed={result.passed}/{total}")
    print("compile_validation_claimed=False")
    for failure in result.failures:
        print("FAIL: " + failure)
    return 0 if not result.failures else 1


if __name__ == "__main__":
    sys.exit(main())
