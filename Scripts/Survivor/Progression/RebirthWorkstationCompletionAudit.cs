using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Read-only original-file evidence. No fixture grant, receipt synthesis, save,
// retry, migration, native setter, or outbox promotion is performed here.
internal static class RebirthWorkstationCompletionAudit
{
    internal static object Capture(string operation)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null)throw new InvalidOperationException("Actual main-thread World required");
        if(operation!=null&&(!Guid.TryParseExact(operation,"N",out var id)||id==Guid.Empty))throw new InvalidDataException("Exact operation required");
        string role=world.IsRemote()?"client":"server",root=RebirthWorkstationCompletionOutbox.Root(role);
        var rows=new List<object>();
        object native=null;
        foreach(string area in new[]{"intents","pending","intentArchive","archive","nativeReagent","nativeOwners"})
        {
            string folder=area=="nativeOwners"?Path.Combine(root,"intents","nativeOwners"):Path.Combine(root,area);if(!Directory.Exists(folder))continue;
            RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);
            var files=operation==null?Directory.EnumerateFiles(folder,"*.xml"):
                (area=="nativeOwners"?new[]{Path.Combine(folder,operation+".xml"),Path.Combine(folder,operation+".xml.terminal.xml")}:
                    new[]{Path.Combine(folder,operation+".xml")}).Where(File.Exists);
            int count=0;foreach(string path in files)
            {
                if(++count>4096)throw new InvalidDataException("Completion observation bound");
                string text=null;XElement image=null;string error=null;
                try{text=RebirthWorkstationCompletionPublication.ReadEvidence(path);image=RebirthWorkstationCompletionPublication.ReadPrimary(path);}catch(Exception exception){error=exception.GetType().Name;}
                rows.Add(new{area,operation=Path.GetFileNameWithoutExtension(path),hash=text==null?null:RebirthWorkstationCompletionOriginal.Hash(text),
                    temporary=File.Exists(path+".tmp"),error,xml=image?.ToString(SaveOptions.DisableFormatting)});
                if(operation!=null&&native==null&&image?.Name=="workstationCompletionOutbox"&&!world.IsRemote())
                {
                    try
                    {
                        var original=image.Element("original")?.Element("workstationCompletionOriginal");
                        var position=new Vector3i((int)original.Attribute("x"),(int)original.Attribute("y"),(int)original.Attribute("z"));
                        var tile=world.GetTileEntity(position) as TileEntityWorkstation;
                        string savedBirth=(string)image.Attribute("nativeStationBirth");
                        string actualBirth=null;if(tile!=null)RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,tile,out actualBirth);
                        if(tile!=null&&RebirthProductiveEarnedNativeBody.TryCaptureCurrent(tile,out var bytes,out int offset))
                            native=new{available=true,operation,worldState=world.worldState?.Guid,output=RebirthWorkstationCompletionPipeline.Grid(tile.Output),
                                body=Convert.ToBase64String(bytes),bodyOffset=offset,nativeStationBirth=savedBirth,actualStationBirth=actualBirth,
                                sameNativeStation=savedBirth!=null&&actualBirth==savedBirth};
                    }
                    catch{/* Diagnostic lookup is not native reconstruction authority. */}
                }
            }
        }
        return new{available=true,role,world=RebirthWorkstationCompletionOutbox.CurrentGuid(world),save=Path.GetFullPath(GameIO.GetSaveGameDir()),
            rows,native,frames=RebirthWorkstationCompletionPipeline.CaptureFrames(),cached=RebirthWorkstationCompletionPipeline.CaptureCachedCompletions(operation),nativeOwners=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredOwners(operation)};
    }
}

public sealed class ConsoleCmdRebirthWorkstationCompletionAudit:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbwccompletionaudit"};
    public override string getDescription()=>"Read exact durable native workstation completion evidence.";
    public override string getHelp()=>"rbwccompletionaudit [operation=<32 lowercase hex>]";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            string operation=args?.SingleOrDefault(value=>value.StartsWith("operation=",StringComparison.Ordinal));
            Log.Out("[REBIRTH MP015 audit] "+Newtonsoft.Json.JsonConvert.SerializeObject(RebirthWorkstationCompletionAudit.Capture(operation?.Substring(10))));
        }
        catch(Exception exception){Log.Error("[REBIRTH MP015 audit] "+exception.Message);}
    }
}
