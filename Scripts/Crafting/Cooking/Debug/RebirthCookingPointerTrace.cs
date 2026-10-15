using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

// Explicit console observation only; no frame output, saves, inputs or item writes.
[Preserve]
public sealed class ConsoleCmdRebirthCookingPointerTrace : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbcookingpointertrace"};
    public override string getDescription()=>"Opt-in bounded cooking drag release evidence.";
    public override string getHelp()=>"rbcookingpointertrace on|off|clear|snapshot (active Bridge and main thread)";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            if(!RebirthGameBridge.IsRunning||!ThreadManager.IsMainThread()||args.Count!=1)throw new InvalidOperationException(getHelp());
            switch(args[0])
            {
                case "on":RebirthCookingPointerDrop.ConfigureTrace(true,true);break;
                case "off":RebirthCookingPointerDrop.ConfigureTrace(false,false);break;
                case "clear":RebirthCookingPointerDrop.ConfigureTrace(false,true);break;
                case "snapshot":break;
                default:throw new InvalidOperationException(getHelp());
            }
            SdtdConsole.Instance.Output(RebirthCookingPointerDrop.TraceSnapshot().ToString(Formatting.None));
        }
        catch(Exception error){SdtdConsole.Instance.Output("*** ERROR cooking pointer trace: "+error.GetType().Name);}
    }
}
