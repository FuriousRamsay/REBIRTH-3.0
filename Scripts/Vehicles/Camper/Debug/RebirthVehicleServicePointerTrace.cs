using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

// Explicit console observation only; no frame output, saves, inputs or item writes.
[Preserve]
public sealed class ConsoleCmdRebirthVehicleServicePointerTrace : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbservicepointertrace"};
    public override string getDescription()=>"Opt-in bounded vehicle service drag release evidence.";
    public override string getHelp()=>"rbservicepointertrace on|off|clear|snapshot (active Bridge and main thread)";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            if(!RebirthGameBridge.IsRunning||!ThreadManager.IsMainThread()||args.Count!=1)throw new InvalidOperationException(getHelp());
            switch(args[0])
            {
                case "on":RebirthVehicleServicePointerDrop.ConfigureTrace(true,true);break;
                case "off":RebirthVehicleServicePointerDrop.ConfigureTrace(false,false);break;
                case "clear":RebirthVehicleServicePointerDrop.ConfigureTrace(false,true);break;
                case "snapshot":break;
                default:throw new InvalidOperationException(getHelp());
            }
            SdtdConsole.Instance.Output(RebirthVehicleServicePointerDrop.TraceSnapshot().ToString(Formatting.None));
        }
        catch(Exception error){SdtdConsole.Instance.Output("*** ERROR vehicle service pointer trace: "+error.GetType().Name);}
    }
}

