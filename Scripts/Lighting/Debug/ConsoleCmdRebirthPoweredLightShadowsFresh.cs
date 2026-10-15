#if DEBUG
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthPoweredLightShadowsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbpoweredlightshadows", "rbpls", "rbpoweredshadows" };
    }

    public override string getDescription()
    {
        return "Client-side diagnostic toggle for rendered shadows on powered lights.";
    }

    public override string getHelp()
    {
        return "Usage: rbpoweredlightshadows <on|off|toggle|status>\n"
             + "Aliases: rbpls, rbpoweredshadows\n"
             + "Diagnostic only. OFF suppresses Unity rendered shadows for BlockPoweredLight LightLOD instances. It does not change power, BLOCK light, or crop logic.";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string action = (_params != null && _params.Count > 0) ? (_params[0] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (action == "off" || action == "disable" || action == "disabled" || action == "false" || action == "0")
        {
            Log.Out("[AdvancedFarming] " + Harmony_RebirthPoweredLightShadowDiagnosticsPatch.SetDisabled(true));
        }
        else if (action == "on" || action == "enable" || action == "enabled" || action == "true" || action == "1")
        {
            Log.Out("[AdvancedFarming] " + Harmony_RebirthPoweredLightShadowDiagnosticsPatch.SetDisabled(false));
        }
        else if (action == "toggle" || action == "switch")
        {
            Log.Out("[AdvancedFarming] " + Harmony_RebirthPoweredLightShadowDiagnosticsPatch.Toggle());
        }
        else if (action != "status" && action != "report" && action != "state")
        {
            Log.Out(getHelp());
            return;
        }

        Log.Out(Harmony_RebirthPoweredLightShadowDiagnosticsPatch.BuildStatus());
    }
}
#endif
