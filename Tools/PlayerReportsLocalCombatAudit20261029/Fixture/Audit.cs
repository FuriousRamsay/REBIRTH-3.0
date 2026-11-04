using System;
public static class Subject{
    public static void OutputCombatAudit(EntityPlayer player)
    {
        try
        {
            bool eligible = RebirthSkillAwardService.TryGetEligible(player, out var owner, out var record);
            ItemValue held = player.inventory?.holdingItemItemValue;
            string skillId = held?.ItemClass != null ? RebirthProgressionRuntimeConfig.ClassifyCombat(held) : string.Empty;
            var report = new Newtonsoft.Json.Linq.JObject
            {
                ["actor"] = player.entityId,
                ["remoteWorld"] = player.world?.IsRemote() ?? false,
                ["survivorMode"] = RebirthSurvivorMode.IsEnabledForCurrentWorld(),
                ["creationHeld"] = RebirthCharacterCreationHoldService.IsHeld(player),
                ["eligible"] = eligible,
                ["ownerKey"] = owner?.StorageKey,
                ["creation"] = record?.Origin?.CreationId,
                ["complete"] = record?.IsComplete ?? false,
                ["item"] = held?.ItemClass?.GetItemName(),
                ["type"] = held?.type ?? 0,
                ["quality"] = held?.Quality ?? 0,
                ["seed"] = held?.Seed ?? 0,
                ["meta"] = held?.Meta ?? 0,
                ["useTimes"] = held?.UseTimes ?? 0f,
                ["selectedAmmo"] = held?.SelectedAmmoTypeIndex ?? 0,
                ["skill"] = skillId,
                ["skillStatePresent"] = false
            };
            RebirthSkillRuntimeState state;
            if (record?.Progression != null && record.Progression.Skills.TryGetValue(skillId, out state) && state != null)
            {
                report["skillStatePresent"] = true;
                report["skillValue"] = state.Value;
                report["skillProgress"] = state.Progress;
                report["skillTotal"] = state.Value + state.Progress;
            }
            bool resolved = RebirthWeaponSustainedDpsService.TryGetCombatProfile(player, held, out var profile);
            report["profileResolved"] = resolved;
            if (profile != null)
                report["profile"] = new Newtonsoft.Json.Linq.JObject
                {
                    ["valid"] = profile.Valid, ["detail"] = profile.Detail, ["item"] = profile.ItemName,
                    ["skill"] = profile.SkillId, ["signature"] = profile.Signature,
                    ["damage"] = profile.DamagePerAttack, ["normalDps"] = profile.NormalAttackDps,
                    ["powerDps"] = profile.PowerAttackDps, ["sustainedDps"] = profile.SustainedDps,
                    ["apm"] = profile.AttacksPerMinute, ["rpm"] = profile.RoundsPerMinute,
                    ["reloadSeconds"] = profile.ReloadSeconds, ["magazine"] = profile.MagazineSize
                };
            report["awards"] = RebirthWeaponSustainedDpsService.CaptureAuditState(player);
            SdtdConsole.Instance.Output("[REBIRTH Skill Audit] " + report.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch (Exception error)
        {
            SdtdConsole.Instance.Output("[REBIRTH Skill Audit] unavailable: " + error.GetType().Name + ": " + error.Message);
        }
    }

}