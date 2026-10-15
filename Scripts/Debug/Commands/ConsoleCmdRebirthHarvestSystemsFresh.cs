#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthHarvestSystemsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbharvestsystemsfresh", "rbharvest5a", "rbhs5a" };
    }

    public override string getDescription()
    {
        return "Controls the REBIRTH Milestone 5A bundled harvest/salvage manual patch systems.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbharvestsystemsfresh summary\n"
             + "  rbharvestsystemsfresh detail\n"
             + "  rbharvestsystemsfresh installcrop\n"
             + "  rbharvestsystemsfresh installattack\n"
             + "  rbharvestsystemsfresh enableattackobserve\n"
             + "  rbharvestsystemsfresh enableattackreplacementmode [vanillaEquivalentOnly:true|false]\n"
             + "  rbharvestsystemsfresh disableattack\n"
             + "  rbharvestsystemsfresh attackaudit\n"
             + "  rbharvestsystemsfresh attackcounters\n"
             + "  rbharvestsystemsfresh resetattackcounters\n"
             + "  rbharvestsystemsfresh enableattackbonus <destroyBonusPercent> <harvestBonusPercent> [applyDestroy:true|false] [applyHarvest:true|false]\n"
             + "  rbharvestsystemsfresh disableattackbonus\n"
             + "  rbharvestsystemsfresh attackbonus [sampleCount]\n"
             + "  rbharvestsystemsfresh installstump\n"
             + "  rbharvestsystemsfresh enablestumpcontext [requireVehicleAttached:true|false]\n"
             + "  rbharvestsystemsfresh disablestumpcontext\n"
             + "  rbharvestsystemsfresh stumpcontext\n"
             + "  rbharvestsystemsfresh clearstumpcontext\n"
             + "  rbharvestsystemsfresh enablestumprewardpreview [consumeOnPreview:true|false] [requireVehicleAttached:true|false]\n"
             + "  rbharvestsystemsfresh enablestumpreward [itemName] [count] [consumeContext:true|false] [requireVehicleAttached:true|false]\n"
             + "  rbharvestsystemsfresh grantstumpcontext [x y z|newest]\n"
             + "  rbharvestsystemsfresh disablestumprewardpreview\n"
             + "  rbharvestsystemsfresh previewstumpcontext [x y z|newest]\n"
             + "  rbharvestsystemsfresh installall\n"
             + "  rbharvestsystemsfresh uninstallall\n"
             + "  rbharvestsystemsfresh enablecrop <wildBonusPercent> <grownBonusPercent> [downgradePlayerGrown]\n"
             + "  rbharvestsystemsfresh disablecrop\n"
             + "  rbharvestsystemsfresh croptable [filter]\n"
             + "  rbharvestsystemsfresh cropflow\n"
             + "  rbharvestsystemsfresh safety\n"
             + "\n"
             + "No patches are installed automatically. Manual install commands are explicit test state.\n";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string cmd = (_params == null || _params.Count == 0)
            ? "summary"
            : (_params[0] ?? string.Empty).Trim().ToLowerInvariant();

        switch (cmd)
        {
            case "help":
            case "?":
                Log.Out(getHelp());
                return;

            case "summary":
            case "status":
                Log.Out(RebirthHarvestSystemsPolicy.GetSummaryReport());
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.GetStatusReport());
                return;

            case "detail":
                Log.Out(RebirthHarvestSystemsPolicy.GetDetailReport());
                return;

            case "installcrop":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallCropPatch());
                return;


            case "enableattackobserve":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAttackHarvestPatch());
                RebirthAttackHarvestPolicy.EnableObserveOnly();
                Log.Out("[RebirthAttackHarvestPolicy] observe-only enabled.");
                Log.Out(RebirthAttackHarvestPolicy.GetSummaryReport());
                return;

            case "enableattackreplacementmode":
                bool vanillaEquivalentOnly = true;
                if (_params != null && _params.Count > 1)
                    bool.TryParse(_params[1], out vanillaEquivalentOnly);

                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAttackHarvestPatch());
                RebirthAttackHarvestPolicy.EnableReplacementMode(vanillaEquivalentOnly);
                Log.Out("[RebirthAttackHarvestPolicy] replacement mode enabled. Current service is vanilla-equivalent and falls back to vanilla on exception.");
                Log.Out(RebirthAttackHarvestPolicy.GetSummaryReport());
                return;

            case "disableattack":
                RebirthAttackHarvestPolicy.Disable();
                Log.Out("[RebirthAttackHarvestPolicy] disabled.");
                Log.Out(RebirthAttackHarvestPolicy.GetSummaryReport());
                return;


            case "enableattackbonus":
                int destroyBonusPercent = 0;
                int harvestBonusPercent = 0;
                bool applyDestroyBonus = true;
                bool applyHarvestBonus = true;

                if (_params != null && _params.Count > 1)
                    int.TryParse(_params[1], out destroyBonusPercent);

                if (_params != null && _params.Count > 2)
                    int.TryParse(_params[2], out harvestBonusPercent);

                if (_params != null && _params.Count > 3)
                    bool.TryParse(_params[3], out applyDestroyBonus);

                if (_params != null && _params.Count > 4)
                    bool.TryParse(_params[4], out applyHarvestBonus);

                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAttackHarvestPatch());
                RebirthAttackHarvestPolicy.EnableReplacementMode(false);
                RebirthAttackHarvestBonusPolicy.EnableManualTest(destroyBonusPercent, harvestBonusPercent, applyDestroyBonus, applyHarvestBonus);
                Log.Out("[RebirthAttackHarvestBonusPolicy] enabled. Attack replacement mode was also enabled.");
                Log.Out(RebirthAttackHarvestPolicy.GetSummaryReport());
                Log.Out(RebirthAttackHarvestBonusPolicy.GetSummaryReport());
                return;

            case "disableattackbonus":
                RebirthAttackHarvestBonusPolicy.Disable();
                Log.Out("[RebirthAttackHarvestBonusPolicy] disabled.");
                Log.Out(RebirthAttackHarvestBonusPolicy.GetSummaryReport());
                return;

            case "attackbonus":
                int sampleCount = 10;
                if (_params != null && _params.Count > 1)
                    int.TryParse(_params[1], out sampleCount);

                Log.Out(RebirthAttackHarvestBonusPolicy.GetPreviewReport(sampleCount));
                return;

            case "attackaudit":
                Log.Out(RebirthAttackHarvestTargetAudit.GetReport());
                return;

            case "attackcounters":
                Log.Out(RebirthAttackHarvestCounters.GetDetailReport());
                return;

            case "resetattackcounters":
                RebirthAttackHarvestCounters.Reset();
                Log.Out("[RebirthAttackHarvestCounters] reset.");
                return;

            case "installattack":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAttackHarvestPatch());
                return;

            case "installstump":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallStumpHarvestContextPatch());
                return;


            case "enablestumpcontext":
                bool requireVehicle = true;
                if (_params != null && _params.Count > 1)
                    bool.TryParse(_params[1], out requireVehicle);

                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallStumpHarvestContextPatch());
                RebirthStumpHarvestContextPolicy.EnableManualTest(requireVehicle);
                Log.Out("[RebirthHarvestSystemsPolicy] stump context manual policy enabled.");
                Log.Out(RebirthStumpHarvestContextPolicy.GetSummaryReport());
                return;

            case "disablestumpcontext":
                RebirthStumpHarvestContextPolicy.Disable();
                Log.Out("[RebirthHarvestSystemsPolicy] stump context manual policy disabled.");
                return;

            case "stumpcontext":
                Log.Out(RebirthStumpHarvestContextPolicy.GetSummaryReport());
                Log.Out(RebirthStumpHarvestContextStore.GetListReport());
                return;

            case "clearstumpcontext":
                RebirthStumpHarvestContextStore.Clear();
                Log.Out("[RebirthStumpHarvestContextStore] cleared.");
                return;

            case "installall":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAll());
                return;

            case "uninstall":
            case "uninstallall":
            case "rollback":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.UninstallAll());
                return;

            case "enablecrop":
                int wildPercent = 0;
                int grownPercent = 0;
                bool downgradePlayerGrown = true;

                if (_params != null && _params.Count > 1)
                    int.TryParse(_params[1], out wildPercent);

                if (_params != null && _params.Count > 2)
                    int.TryParse(_params[2], out grownPercent);

                if (_params != null && _params.Count > 3)
                    bool.TryParse(_params[3], out downgradePlayerGrown);

                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallCropPatch());
                RebirthCropActivationHarvestPolicy.EnableManualTest(wildPercent, grownPercent, downgradePlayerGrown);
                Log.Out("[RebirthHarvestSystemsPolicy] crop manual policy enabled.");
                Log.Out(RebirthCropActivationHarvestPolicy.GetSummaryReport());
                return;

            case "disablecrop":
                RebirthCropActivationHarvestPolicy.Reset();
                Log.Out("[RebirthHarvestSystemsPolicy] crop manual policy reset to disabled.");
                return;

            case "croptable":
                string tableFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthCropActivationHarvestGeneratedCropTable.GetListReport(tableFilter));
                return;

            case "cropflow":
                Log.Out(RebirthCropActivationHarvestFlow.GetAwardBeforeMutationReport());
                return;


            case "enablestumprewardpreview":
                bool consumeOnPreview = false;
                bool requireVehicleContext = true;

                if (_params != null && _params.Count > 1)
                    bool.TryParse(_params[1], out consumeOnPreview);

                if (_params != null && _params.Count > 2)
                    bool.TryParse(_params[2], out requireVehicleContext);

                RebirthStumpHarvestRewardPolicy.EnablePreviewOnly(consumeOnPreview, requireVehicleContext);
                Log.Out("[RebirthStumpHarvestRewardPolicy] reward preview policy enabled.");
                Log.Out(RebirthStumpHarvestRewardPolicy.GetSummaryReport());
                return;


            case "enablestumpreward":
                string rewardItemName = "resourceHoney";
                int rewardCount = 1;
                bool consumeRewardContext = true;
                bool requireRewardVehicleContext = true;

                if (_params != null && _params.Count > 1)
                    rewardItemName = _params[1];

                if (_params != null && _params.Count > 2)
                    int.TryParse(_params[2], out rewardCount);

                if (_params != null && _params.Count > 3)
                    bool.TryParse(_params[3], out consumeRewardContext);

                if (_params != null && _params.Count > 4)
                    bool.TryParse(_params[4], out requireRewardVehicleContext);

                RebirthStumpHarvestRewardPolicy.EnableManualTest(rewardItemName, rewardCount, consumeRewardContext, requireRewardVehicleContext, true);
                Log.Out("[RebirthStumpHarvestRewardPolicy] manual reward policy enabled.");
                Log.Out(RebirthStumpHarvestRewardPolicy.GetSummaryReport());
                return;

            case "grantstumpcontext":
                string grantReport;

                if (_params != null && _params.Count >= 4)
                {
                    int gx;
                    int gy;
                    int gz;
                    if (int.TryParse(_params[1], out gx) && int.TryParse(_params[2], out gy) && int.TryParse(_params[3], out gz))
                    {
                        RebirthStumpHarvestRewardService.TryGrant(new Vector3i(gx, gy, gz), out grantReport);
                    }
                    else
                    {
                        Log.Out("[RebirthStumpHarvestRewardService] invalid coordinates.");
                        return;
                    }
                }
                else
                {
                    RebirthStumpHarvestRewardService.TryGrantNewest(out grantReport);
                }

                Log.Out(grantReport);
                return;

            case "disablestumprewardpreview":
                RebirthStumpHarvestRewardPolicy.Disable();
                Log.Out("[RebirthStumpHarvestRewardPolicy] reward preview policy disabled.");
                return;

            case "previewstumpcontext":
                Vector3i previewPos;
                RebirthStumpHarvestContextStore.Context newestContext;

                if (_params != null && _params.Count >= 4)
                {
                    int px;
                    int py;
                    int pz;
                    if (int.TryParse(_params[1], out px) && int.TryParse(_params[2], out py) && int.TryParse(_params[3], out pz))
                    {
                        previewPos = new Vector3i(px, py, pz);
                    }
                    else
                    {
                        Log.Out("[RebirthStumpHarvestRewardPreview] invalid coordinates.");
                        return;
                    }
                }
                else if (!RebirthStumpHarvestContextStore.TryGetNewest(out previewPos, out newestContext))
                {
                    Log.Out("[RebirthStumpHarvestRewardPreview] no stored context.");
                    return;
                }

                string previewReport;
                RebirthStumpHarvestRewardService.TryPreview(previewPos, out previewReport);
                Log.Out(previewReport);
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthHarvestSystemsPolicy.GetSafetyReport());
                Log.Out(RebirthAttackHarvestReplacementService.GetSafetyReport());
                Log.Out(RebirthAttackHarvestBonusPolicy.GetDetailReport());
                Log.Out(RebirthStumpHarvestContextStore.GetSafetyReport());
                Log.Out(RebirthStumpHarvestRewardService.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthHarvestSystemsPolicy] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
