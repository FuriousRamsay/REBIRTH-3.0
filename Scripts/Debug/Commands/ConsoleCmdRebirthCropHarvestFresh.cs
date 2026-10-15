#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthCropHarvestFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbcropharvestfresh", "rbcropharvest4e", "rbcrop4e" };
    }

    public override string getDescription()
    {
        return "Controls the REBIRTH Milestone 4E crop activation harvest manual-test policy.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbcropharvestfresh summary\n"
             + "  rbcropharvestfresh installpatch\n"
             + "  rbcropharvestfresh detail\n"
             + "  rbcropharvestfresh preview [count]\n"
             + "  rbcropharvestfresh classify <blockName>\n"
             + "  rbcropharvestfresh croptable [filter]\n"
             + "  rbcropharvestfresh flow\n"
             + "  rbcropharvestfresh enabletest <wildBonusPercent> <grownBonusPercent> [downgradePlayerGrown:true|false]\n"
             + "  rbcropharvestfresh disable\n"
             + "  rbcropharvestfresh reset\n"
             + "  rbcropharvestfresh safety\n"
             + "\n"
             + "Aliases: rbcropharvestfresh, rbcropharvest4e, rbcrop4e\n"
             + "\n"
             + "Default is disabled. The crop Harmony patch is not auto-installed; use installpatch or rbharvestsystemsfresh enablecrop. enabletest is explicit manual-test state only and is not saved.\n";
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


            case "installpatch":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallCropPatch());
                return;

            case "summary":
            case "status":
                Log.Out(RebirthCropActivationHarvestPolicy.GetSummaryReport());
                return;

            case "detail":
                Log.Out(RebirthCropActivationHarvestPolicy.GetDetailReport());
                return;

            case "preview":
                int count = 10;
                if (_params != null && _params.Count > 1)
                {
                    int parsed;
                    if (int.TryParse(_params[1], out parsed))
                        count = parsed;
                }

                Log.Out(RebirthCropActivationHarvestPolicy.GetPreviewReport(count));
                return;

            case "classify":
                string blockName = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthCropActivationHarvestClassifier.GetPreviewReport(blockName));
                return;


            case "croptable":
                string cropTableFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthCropActivationHarvestGeneratedCropTable.GetListReport(cropTableFilter));
                return;


            case "flow":
                Log.Out(RebirthCropActivationHarvestFlow.GetAwardBeforeMutationReport());
                return;

            case "enabletest":
                int wildPercent = 0;
                int grownPercent = 0;
                bool downgradePlayerGrown = true;

                if (_params != null && _params.Count > 1)
                    int.TryParse(_params[1], out wildPercent);

                if (_params != null && _params.Count > 2)
                    int.TryParse(_params[2], out grownPercent);

                if (_params != null && _params.Count > 3)
                    bool.TryParse(_params[3], out downgradePlayerGrown);

                RebirthCropActivationHarvestPolicy.EnableManualTest(wildPercent, grownPercent, downgradePlayerGrown);
                Log.Out("[RebirthCropActivationHarvestPolicy] manual test enabled. This state is not saved.");
                Log.Out(RebirthCropActivationHarvestPolicy.GetSummaryReport());
                return;

            case "disable":
            case "reset":
                RebirthCropActivationHarvestPolicy.Reset();
                Log.Out("[RebirthCropActivationHarvestPolicy] reset to disabled defaults.");
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthCropActivationHarvestPolicy.GetSafetyReport());
                Log.Out(RebirthCropActivationHarvestClassifier.GetSafetyReport());
                Log.Out(RebirthCropActivationHarvestCompat.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthCropActivationHarvestPolicy] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
