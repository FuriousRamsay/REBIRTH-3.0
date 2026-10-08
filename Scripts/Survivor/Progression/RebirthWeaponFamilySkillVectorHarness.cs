using System;
using System.Text;

#nullable disable

public static class RebirthWeaponFamilySkillVectorHarness
{
    private sealed class Result { public int Pass; public int Fail; public readonly StringBuilder Text = new StringBuilder(); }

    public static string RunAll()
    {
        Result r = new Result();
        Check(r, "signed zero neutral", Near(RebirthWeaponFamilySkillService.SignedEndpoint(0f, 0.15f, -0.20f), 0f));
        Check(r, "signed lower clamp", Near(RebirthWeaponFamilySkillService.SignedEndpoint(-500f, 0.15f, -0.20f), 0.15f));
        Check(r, "signed upper clamp", Near(RebirthWeaponFamilySkillService.SignedEndpoint(500f, 0.15f, -0.20f), -0.20f));
        Check(r, "signed negative interpolation", Near(RebirthWeaponFamilySkillService.SignedEndpoint(-25f, 0.20f, -0.30f), 0.10f));
        Check(r, "signed positive interpolation", Near(RebirthWeaponFamilySkillService.SignedEndpoint(50f, 0.20f, -0.30f), -0.15f));

        string[] melee = RebirthWeaponFamilySkillService.GetMeleeSkillIds();
        string[] ranged = RebirthWeaponFamilySkillService.GetRangedSkillIds();
        Check(r, "ten melee families", melee.Length == 10);
        Check(r, "eight ranged families", ranged.Length == 8);
        for (int i = 0; i < melee.Length; i++)
        {
            Check(r, "melee recognized " + melee[i], RebirthWeaponFamilySkillService.IsMeleeSkill(melee[i]) && RebirthWeaponFamilySkillService.IsChunk7Skill(melee[i]) && !RebirthWeaponFamilySkillService.IsRangedSkill(melee[i]));
        }
        for (int i = 0; i < ranged.Length; i++)
        {
            Check(r, "ranged recognized " + ranged[i], RebirthWeaponFamilySkillService.IsRangedSkill(ranged[i]) && RebirthWeaponFamilySkillService.IsChunk7Skill(ranged[i]) && !RebirthWeaponFamilySkillService.IsMeleeSkill(ranged[i]));
        }
        Check(r, "deployable turret excluded", !RebirthWeaponFamilySkillService.IsChunk7Skill("skill.deployable_turrets"));
        Check(r, "drone excluded", !RebirthWeaponFamilySkillService.IsChunk7Skill("skill.drone_operations"));
        Check(r, "explosives excluded", !RebirthWeaponFamilySkillService.IsChunk7Skill("skill.explosives"));

        float meleeWeakStamina = RebirthWeaponFamilySkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.MeleeStaminaNegative, RebirthProgressionRuntimeConfig.MeleeStaminaPositive);
        float meleeStrongStamina = RebirthWeaponFamilySkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.MeleeStaminaNegative, RebirthProgressionRuntimeConfig.MeleeStaminaPositive);
        float meleeWeakSpeed = RebirthWeaponFamilySkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.MeleeSpeedNegative, RebirthProgressionRuntimeConfig.MeleeSpeedPositive);
        float meleeStrongSpeed = RebirthWeaponFamilySkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.MeleeSpeedNegative, RebirthProgressionRuntimeConfig.MeleeSpeedPositive);
        Check(r, "melee weak costs more stamina", meleeWeakStamina > 0f);
        Check(r, "melee strong costs less stamina", meleeStrongStamina < 0f);
        Check(r, "melee weak slower action", meleeWeakSpeed < 0f);
        Check(r, "melee strong faster action", meleeStrongSpeed > 0f);

        float reloadWeak = RebirthWeaponFamilySkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.RangedReloadNegative, RebirthProgressionRuntimeConfig.RangedReloadPositive);
        float reloadStrong = RebirthWeaponFamilySkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.RangedReloadNegative, RebirthProgressionRuntimeConfig.RangedReloadPositive);
        float handlingWeak = RebirthWeaponFamilySkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.RangedHandlingNegative, RebirthProgressionRuntimeConfig.RangedHandlingPositive);
        float handlingStrong = RebirthWeaponFamilySkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.RangedHandlingNegative, RebirthProgressionRuntimeConfig.RangedHandlingPositive);
        float spreadWeak = RebirthWeaponFamilySkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.RangedSpreadNegative, RebirthProgressionRuntimeConfig.RangedSpreadPositive);
        float spreadStrong = RebirthWeaponFamilySkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.RangedSpreadNegative, RebirthProgressionRuntimeConfig.RangedSpreadPositive);
        float recoilWeak = RebirthWeaponFamilySkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.RangedRecoilNegative, RebirthProgressionRuntimeConfig.RangedRecoilPositive);
        float recoilStrong = RebirthWeaponFamilySkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.RangedRecoilNegative, RebirthProgressionRuntimeConfig.RangedRecoilPositive);
        Check(r, "ranged weak reload slower", reloadWeak < 0f);
        Check(r, "ranged strong reload faster", reloadStrong > 0f);
        Check(r, "ranged weak handling recovery lower", handlingWeak < 0f);
        Check(r, "ranged strong handling recovery higher", handlingStrong > 0f);
        Check(r, "ranged weak spread worse", spreadWeak > 0f);
        Check(r, "ranged strong spread better", spreadStrong < 0f);
        Check(r, "ranged weak recoil worse", recoilWeak > 0f);
        Check(r, "ranged strong recoil better", recoilStrong < 0f);

        Check(r, "no direct damage endpoint field", !HasForbiddenField("Damage"));
        Check(r, "no firearm rpm endpoint field", !HasForbiddenField("RoundsPerMinute"));
        Check(r, "no magazine endpoint field", !HasForbiddenField("Magazine"));
        Check(r, "no jam endpoint field", !HasForbiddenField("Jam"));
        Check(r, "no projectile count endpoint field", !HasForbiddenField("Projectile") && !HasForbiddenField("Pellet"));

        // Sustained-DPS and training normalization vectors. These are pure deterministic helpers so
        // they can catch balancing/math regressions without a world, player or weapon prefab.
        Check(r, "melee dps derives from effective damage and cadence",
            Near(RebirthWeaponSustainedDpsService.CalculateMeleeDps(50f, 120f, 0.1f), 100f));
        Check(r, "melee effective apm supersedes legacy action delay",
            Near(RebirthWeaponSustainedDpsService.CalculateMeleeDps(50f, 240f, 0.5f), 200f));
        Check(r, "melee action delay remains cadence fallback",
            Near(RebirthWeaponSustainedDpsService.CalculateMeleeDps(50f, 0f, 0.5f), 100f));

        int attacks, reloads;
        float noReload = RebirthWeaponSustainedDpsService.CalculateRangedSustainedDps(50f, 120f, 0f, 30, 0f, 1, 60f, out attacks, out reloads);
        Check(r, "ranged no-reload 60s projection", Near(noReload, 100f) && attacks == 120 && reloads == 0);
        float revolver = RebirthWeaponSustainedDpsService.CalculateRangedSustainedDps(80f, 120f, 0f, 6, 3f, 1, 60f, out attacks, out reloads);
        Check(r, "ranged reload downtime lowers sustained dps", Near(revolver, 87.27273f) && attacks == 66 && reloads == 10);
        float semiAuto = RebirthWeaponSustainedDpsService.CalculateRangedSustainedDps(45f, 300f, 0f, 15, 1.8f, 1, 60f, out attacks, out reloads);
        Check(r, "magazine and reload cadence differentiate faster pistol", Near(semiAuto, 146.7391f) && attacks == 197 && reloads == 13);

        float pLow = RebirthWeaponSustainedDpsService.CalculateCombatProgress(50f, 50f, 10f);
        float pHigh = RebirthWeaponSustainedDpsService.CalculateCombatProgress(100f, 100f, 10f);
        Check(r, "equal productive combat seconds normalize across weapon dps", Near(pLow, pHigh));
        float oneHeavyHit = RebirthWeaponSustainedDpsService.CalculateCombatProgress(50f, 10f, 10f);
        float fiveLightHits = 5f * RebirthWeaponSustainedDpsService.CalculateCombatProgress(10f, 10f, 10f);
        Check(r, "damage splitting preserves gain below event ceiling", Near(oneHeavyHit, fiveLightHits));
        float cappedHit = RebirthWeaponSustainedDpsService.CalculateCombatProgress(100f, 10f, 10f);
        float expectedCap = RebirthProgressionRuntimeConfig.CombatMaxEquivalentSecondsPerHit * RebirthProgressionRuntimeConfig.CombatProgressPerSustainedSecond * RebirthWeaponSustainedDpsService.CombatSkillMultiplier(10f);
        Check(r, "single hit respects equivalent-seconds ceiling", Near(cappedHit, expectedCap));
        float pMaster = RebirthWeaponSustainedDpsService.CalculateCombatProgress(100f, 100f, 95f);
        Check(r, "mastery progression slows late skill gain", pMaster > 0f && Near(pMaster / pHigh, RebirthProgressionRuntimeConfig.CombatSkillMultiplier90To100 / RebirthProgressionRuntimeConfig.CombatSkillMultiplier0To24));

        StringBuilder b = new StringBuilder();
        b.AppendLine("REBIRTH Survivor Chunk 7 weapon-family vectors: " + (r.Fail == 0 ? "PASS" : "FAIL"));
        b.AppendLine("passed=" + r.Pass + " failed=" + r.Fail + " compile_validation_claimed=False");
        b.Append(r.Text);
        return b.ToString();
    }

    private static bool HasForbiddenField(string token)
    {
        var fields = typeof(RebirthProgressionRuntimeConfig).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        for (int i = 0; i < fields.Length; i++)
            if (fields[i].Name.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase) && fields[i].Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static bool Near(float a, float b) { return Math.Abs(a - b) < 0.0001f; }
    private static void Check(Result r, string name, bool ok)
    {
        if (ok) r.Pass++; else r.Fail++;
        r.Text.AppendLine((ok ? "PASS " : "FAIL ") + name);
    }
}
