#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Scripting;

// Executes real production fixtures only in the disposable save. No state doubles.
[Preserve]
public sealed class ConsoleCmdRebirthNpcConsumptionScenario : ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbnpcconsumescenario"};
    public override string getDescription()=>"CodexTest-only actual NPC consumption custody scenarios.";
    public override string getHelp()=>"rbnpcconsumescenario warmterminal <npcEntity> <operationN> [ownerEntity]\nrbnpcconsumescenario retry|full <npcEntity> <itemKey> <itemType>\nrbnpcconsumescenario arm <npcEntity> <itemKey> <itemType> <boundary>\nrbnpcconsumescenario verify <npcEntity> <operationN> <priorProcess> <boundary> [ownerEntity]";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable CodexTest main thread required");
        if(args?.Count==1&&args[0]=="lossclient"){MpCNpcMetabolismCustodyScenario.ArmLostOwnerReturns();Log.Out("[MP043 PASS] lossclient");return;}
        if(args?.Count==2&&args[0]=="lossverifyclient"){MpCNpcMetabolismCustodyScenario.VerifyLostOwnerReturns(args[1]);Log.Out("[MP043 PASS] lossverifyclient");return;}
        if(args==null||args.Count<(args.Count>0&&(args[0]=="warmterminal"||args[0]=="lossverifyhost")?3:4))throw new ArgumentException(getHelp());
        var world=GameManager.Instance?.World;EntityPlayer player=world?.GetPrimaryPlayer();
        int baseCount=args[0]=="warmterminal"||args[0]=="lossverifyhost"?3:args[0]=="arm"||args[0]=="verify"?5:4;
        if(args.Count==baseCount+1)player=world?.GetEntity(int.Parse(args[baseCount],CultureInfo.InvariantCulture)) as EntityPlayer;
        var npc=world?.GetEntity(int.Parse(args[1],CultureInfo.InvariantCulture)) as EntityRebirthNPC;
        switch(args[0])
        {
            case "warmterminal":MpCNpcMetabolismCustodyScenario.WarmTerminalAfterActualNpcChange(player,npc,args[2]);break;
            case "negativecache":MpCNpcMetabolismCustodyScenario.NegativeColdScanCapacity(player,npc);break;
            case "lossarm":Log.Out("[MP043 ARM] "+MpCNpcMetabolismCustodyScenario.ArmLostOwnerCouriers(player,npc,args[2],int.Parse(args[3],CultureInfo.InvariantCulture)));return;
            case "lossverifyhost":MpCNpcMetabolismCustodyScenario.VerifyLostOwnerCouriers(player,npc,args[2]);break;
            case "retry":MpCNpcMetabolismCustodyScenario.SavedSourceRetryCompletesOnce(player,npc,args[2],int.Parse(args[3],CultureInfo.InvariantCulture));break;
            case "full":MpCNpcMetabolismCustodyScenario.ActualFullStomachRefusesNpcSource(player,npc,args[2],int.Parse(args[3],CultureInfo.InvariantCulture));break;
            case "arm":
                if(args.Count!=5&&args.Count!=6)throw new ArgumentException(getHelp());
                Log.Out("[MP043 ARM] "+MpCNpcMetabolismCustodyScenario.ArmSavedCheckpoint(player,npc,args[2],int.Parse(args[3],CultureInfo.InvariantCulture),args[4]));return;
            case "verify":
                if(args.Count!=5&&args.Count!=6)throw new ArgumentException(getHelp());
                MpCNpcMetabolismCustodyScenario.VerifySavedCheckpoint(player,npc,args[2],int.Parse(args[3],CultureInfo.InvariantCulture),args[4]);break;
            default:throw new ArgumentException(getHelp());
        }
        Log.Out("[MP043 PASS] "+args[0]);
    }
}
#endif
