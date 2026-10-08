using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class ConsoleCmdRebirthCooking : ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbcooking"};
    public override string getDescription()=>"Set cooking test timers in real seconds for this process/session.";
    public override string getHelp()=>"rbcooking times <seconds-until-overcooking> <seconds-until-burnt>\nBoth are measured after cooking finishes. Burn must be greater than overcooking.\nrbcooking reset | status | profile\nExample: rbcooking times 5 15. Defaults: 120 / 600 (900 for soup). Apply on host and clients for multiplayer testing.";
    public override void Execute(List<string> args,CommandSenderInfo senderInfo)
    {
        if(args.Count==1&&args[0]=="profile"){RebirthCookingDiagnostics.Start();return;}
        if(args.Count==1&&args[0]=="reset")RebirthCookingHeatRules.ResetDebugTimes();
        else if(args.Count==3&&args[0]=="times"&&float.TryParse(args[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float grace)&&float.TryParse(args[2],NumberStyles.Float,CultureInfo.InvariantCulture,out float burn)&&!float.IsNaN(grace)&&!float.IsNaN(burn)&&grace>=0&&burn>grace&&burn<=86400)
        { RebirthCookingHeatRules.SetDebugTimes(grace,burn); RebirthCookingDiagnostics.Start(); }
        else if(args.Count!=0&&!(args.Count==1&&args[0]=="status")){SdtdConsole.Instance.Output(getHelp());return;}
        SdtdConsole.Instance.Output("Cooking timers: overcooking "+RebirthCookingHeatRules.Grace+"s; burnt "+RebirthCookingHeatRules.BurnAfter("Pan")+"s (soup "+RebirthCookingHeatRules.BurnAfter("Soup")+"s) after completion. Existing uncollected batches use these timers.");
    }
}
