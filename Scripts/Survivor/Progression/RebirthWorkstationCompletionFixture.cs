using System;
using System.Collections.Generic;

// A source-only deferred fixture: hold an actual positive durable boundary.
// It neither reports successful IO nor manufactures an original/receipt/effect.
internal static class RebirthWorkstationCompletionFixture
{
#if DEBUG
    private static World heldWorld;private static string phase,operation;
#endif
    internal static bool Hold(string boundary,string original)
    {
#if DEBUG
        if(ReferenceEquals(GameManager.Instance?.World,heldWorld)&&phase==boundary)
        {if(operation==null)operation=original;return operation==original;}
#endif
        return false;
    }
    internal static string Configure(string boundary)
    {
#if DEBUG
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable CodexTest only");
        if(boundary!="release"&&boundary!="skill"&&boundary!="statistics"&&boundary!="reagent"&&boundary!="reagentPublication"&&boundary!="acceptedAck"&&boundary!="metadata"&&boundary!="metadataAck")throw new ArgumentException("Unknown boundary");
        heldWorld=boundary=="release"?null:world;phase=boundary=="release"?null:boundary;operation=null;return boundary;
#else
        throw new InvalidOperationException("DEBUG fixture unavailable");
#endif
    }
}
public sealed class ConsoleCmdRebirthWorkstationCompletionFixture:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbwccompletionfixture"};
    public override string getDescription()=>"DEBUG disposable native completion boundary hold.";
    public override string getHelp()=>"rbwccompletionfixture skill|statistics|reagent|reagentPublication|acceptedAck|metadata|metadataAck|release confirm=CodexTest";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            if(args==null||args.Count!=2||args[1]!="confirm=CodexTest")throw new ArgumentException(getHelp());
            Log.Out("[REBIRTH MP015 fixture] "+RebirthWorkstationCompletionFixture.Configure(args[0]));
        }
        catch(Exception exception){Log.Error("[REBIRTH MP015 fixture] "+exception.Message);}
    }
}
