using System;
using System.Collections.Generic;
using System.Globalization;

[UnityEngine.Scripting.Preserve]
public sealed class ConsoleCmdRebirthMiniMapOpacity : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands() => new[] { "rbminimapopacity" };
    public override string getDescription() => "Live whole-minimap opacity tuning.";
    public override string getHelp() => "rbminimapopacity <0-100> | status | reset | debug. Lower means more transparent. Session-only; affects the whole minimap. Default/reset: 100% opacity (0% transparency).";
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        if (!sender.IsLocalGame || sender.RemoteClientInfo != null || sender.NetworkConnection != null)
        { SdtdConsole.Instance.Output("Run this in your local in-game console."); return; }
        string arg = args.Count == 0 ? "status" : args[0].Trim();
        if (args.Count > 1) { SdtdConsole.Instance.Output(getHelp()); return; }
        if (arg.Equals("help", StringComparison.OrdinalIgnoreCase)) { SdtdConsole.Instance.Output(getHelp()); return; }
        if(arg.Equals("debug",StringComparison.OrdinalIgnoreCase))
        {
            XUiC_RebirthMiniMap.ReportDiagnostics("command");
            XUiC_RebirthMiniMap.DiagnosticPending=true;
            SdtdConsole.Instance.Output("Close the console to capture one visible-frame report. No continuous logging enabled.");
            return;
        }
        if(arg.Equals("reset",StringComparison.OrdinalIgnoreCase)) XUiC_RebirthMiniMap.TerrainOpacity=XUiC_RebirthMiniMap.DefaultOpacity;
        else if(!arg.Equals("status",StringComparison.OrdinalIgnoreCase))
        {
            float percent;
            if(!float.TryParse(arg.TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out percent)
                ||float.IsNaN(percent)||float.IsInfinity(percent)||percent<0||percent>100)
            {SdtdConsole.Instance.Output("Expected 0-100, status, reset or debug. Unchanged.");return;}
            XUiC_RebirthMiniMap.TerrainOpacity=percent/100f;
        }
        string message="[REBIRTH Minimap Opacity PC229] Requested "+(XUiC_RebirthMiniMap.TerrainOpacity*100f).ToString("0.#",CultureInfo.InvariantCulture)+"% for terrain, border, markers and labels. Close console to apply.";
        SdtdConsole.Instance.Output(message);Log.Out(message);
        XUiC_RebirthMiniMap.DiagnosticPending=true;
    }
}
