using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
#if DEBUG
    private static World cancellationScenarioWorld;private static string cancellationScenarioBoundary;private static Guid cancellationScenarioOperation;
    private static int cancellationScenarioSlot;
    private static World cancellationScenarioEventWorld;private static readonly List<CancellationScenarioEvent> cancellationScenarioEvents=new List<CancellationScenarioEvent>();private static long cancellationScenarioEventBytes;
    private sealed class CancellationScenarioEvent{public string operation,boundary,evidence;}
    private static bool CancellationScenarioObserving=>ThreadManager.IsMainThread()&&ProtocolActive&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest";
#endif
    private static void ObserveCancellationScenarioEvent(Guid operation,string boundary,string evidence,bool repeat=false)
    {
#if DEBUG
        if(!CancellationScenarioObserving)return;
        if(!ReferenceEquals(cancellationScenarioEventWorld,GameManager.Instance?.World))
        {cancellationScenarioEvents.Clear();cancellationScenarioEventBytes=0;cancellationScenarioEventWorld=GameManager.Instance?.World;}
        if(!repeat&&cancellationScenarioEvents.Any(value=>value.operation==operation.ToString("N")&&value.boundary==boundary&&value.evidence==evidence))return;
        long bytes=checked(2L*(boundary.Length+(evidence?.Length??0))+256);if(cancellationScenarioEvents.Count>=128||cancellationScenarioEventBytes+bytes>16L*1024*1024)return;
        cancellationScenarioEventBytes+=bytes;cancellationScenarioEvents.Add(new CancellationScenarioEvent{operation=operation.ToString("N"),boundary=boundary,evidence=evidence});
#endif
    }
    private static object CancellationScenarioEvents(Guid operation)
    {
#if DEBUG
        return ReferenceEquals(cancellationScenarioEventWorld,GameManager.Instance?.World)?cancellationScenarioEvents.Where(value=>value.operation==operation.ToString("N")).ToArray():new CancellationScenarioEvent[0];
#else
        return new object[0];
#endif
    }
    internal static void ObserveCancellationScenarioOwnerReturn(CancellationOwnerStep step)
    {
#if DEBUG
        if(CancellationScenarioObserving&&step?.NativeReturned==true)ObserveCancellationScenarioEvent(step.Owner.Original.Operation,"actualOwnerSetter"+step.Ordinal,System.Text.Encoding.UTF8.GetString(step.Command),true);
#endif
    }
    private static void ObserveCancellationScenarioQueue(CancellationServer original)
    {
#if DEBUG
        if(CancellationScenarioObserving)ObserveCancellationScenarioEvent(original.Operation,"actualNativeQueueCompleted",Convert.ToBase64String(original.AfterTile),true);
#endif
    }
    private static void ObserveCancellationScenarioLoad(Guid operation,string boundary,CancellationLoaded loaded)
    {
#if DEBUG
        if(CancellationScenarioObserving)ObserveCancellationScenarioEvent(operation,boundary,loaded.Encode());
#endif
    }
    private static void ObserveCancellationScenarioHydration(CancellationClient original)
    {
#if DEBUG
        if(CancellationScenarioObserving)ObserveCancellationScenarioEvent(original.Operation,"actualOwnerHydration"+original.Stage,RebirthRemoteResourceClientOriginal.ActorImage(RebirthCamperEnergyActorImage.Capture(original.Actor)));
#endif
    }
    // Fault scheduling only. A hold returns no native success, advances no
    // journal and grants no setter. Process restart clears the scheduling state.
    internal static bool HoldCancellationScenario(Guid operation,string boundary,int slot=-1)
    {
#if DEBUG
        if(ReferenceEquals(GameManager.Instance?.World,cancellationScenarioWorld)&&cancellationScenarioBoundary==boundary&&
            (slot<0||slot==cancellationScenarioSlot))
        {if(cancellationScenarioOperation==Guid.Empty)cancellationScenarioOperation=operation;return cancellationScenarioOperation==operation;}
#endif
        return false;
    }
    internal static object ConfigureCancellationScenario(string boundary,int slot,Guid operation)
    {
#if DEBUG
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World==null||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||!ProtocolActive)
            throw new InvalidOperationException("Active reviewed protocol and disposable CodexTest required");
        if(!new[]{"release","ownerPrepared","ownerReturned","ownerCheckpoint","nativeBefore","nativeIntent","nativeConsumed","savedAfter","terminalAck"}.Contains(boundary)||slot<0||slot>32)throw new ArgumentException("Unknown exact boundary");
        cancellationScenarioWorld=boundary=="release"?null:GameManager.Instance.World;cancellationScenarioBoundary=boundary;cancellationScenarioSlot=slot;cancellationScenarioOperation=operation;
        return new{boundary,slot,operation=operation.ToString("N")};
#else
        throw new InvalidOperationException("DEBUG fixture unavailable");
#endif
    }
    internal static object CaptureCancellationScenario(Guid operation)
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World==null||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable actual main-thread World required");
        if(operation==Guid.Empty)
        {
            var selected=cancellationClients.Keys.Concat(cancellationServers.Keys).Concat(restartCancellations.Keys).Distinct().ToArray();
            if(selected.Length==0)return new{available=false,active=ProtocolActive,role=GameManager.Instance.World.IsRemote()?"client":"server"};
            if(selected.Length!=1)throw new InvalidDataException("Select exactly one actual live original operation");operation=selected[0];
        }
        var journals=new List<object>();
        foreach(string purpose in new[]{"Client","Accepted","Before","Permission","Mutation","QueueReentry","OwnerPlan","After","Terminal","FinalAck"})
            CaptureScenarioJournal(journals,operation,purpose,operation);
        for(int i=-1;i<32;i++)
        {
            var key=OwnerProgressKey(operation,i);if(i>=0)CaptureScenarioJournal(journals,key,"OwnerSlot",operation);
            CaptureScenarioJournal(journals,key,"OwnerCheckpoint",operation);
        }
        foreach(string area in new[]{"NativePlacedCancellationHostCustody","NativePlacedCancellationHostRetirement","NativePlacedCancellationHostRefusal"})
        {
            string root=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",area);CompletionAncestors(root);if(!Directory.Exists(root))continue;
            // Refusal keys are purpose-derived. Read only bounded primary
            // journal files; archived copies are returned by the journal reader.
            var keys=new HashSet<Guid>{operation};
            if(area.EndsWith("HostRefusal",StringComparison.Ordinal))foreach(string path in Directory.EnumerateFiles(root,"*"))
            {if(keys.Count>256)throw new InvalidDataException("Audit bound");string name=Path.GetFileName(path);if(name.Length>=32&&Guid.TryParseExact(name.Substring(0,32),"N",out var key))keys.Add(key);}
            foreach(var key in keys)
            {
                var record=new RebirthRemoteResourceClientOriginalJournal(root).Read(key);if(record==null)continue;
                string text=new System.Text.UTF8Encoding(false,true).GetString(record.Before);
                if(area.EndsWith("HostRefusal",StringComparison.Ordinal)&&(string)RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes).Attribute("cancel")!=operation.ToString("N"))continue;
                AddScenarioRecord(journals,area,key,record,text);
            }
        }
        cancellationClients.TryGetValue(operation,out var client);cancellationServers.TryGetValue(operation,out var server);restartCancellations.TryGetValue(operation,out var archive);
        var actor=client?.Actor??server?.Actor??GameManager.Instance.World.GetPrimaryPlayer();
        object savedOwner=null;
        if(actor!=null&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&RebirthCamperEnergySavedOwner.TryRead(identity,out var savedImage,out var savedFile)&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(savedFile,CancellationReceipt(operation),out float savedReceipt,out bool savedPresent))
            savedOwner=new{owner=identity.CanonicalId,id=savedFile.id,deaths=savedFile.deaths,receipt=savedPresent?savedReceipt:0f,actor=RebirthRemoteResourceClientOriginal.ActorImage(savedImage)};
        object clientState=client==null?null:new{stage=client.Stage,current=client.Current(),held=RebirthRemoteResourceOwnerAction.HoldsPlacedCancellation(client),receipt=client.Actor.Buffs.GetCustomVar(CancellationReceipt(operation)),
            ownerAttempted=client.OwnerAttempted,restartedBefore=client.RestartBeforeOwner?.Accepted==true,resumable=client.RestartBeforeOwner?.MayResume==true,
            completed=client.OwnerProgress?.Completed,baseline=client.OwnerProgress?.BaselineSaved,steps=client.OwnerProgress?.Steps.Select(step=>new{step.Ordinal,step.Bag,step.Slot,step.NativeReturned,before=step.Before,after=step.After}).ToArray(),
            before=client.Before==null?null:RebirthRemoteResourceClientOriginal.ActorImage(client.Before),after=client.After==null?null:RebirthRemoteResourceClientOriginal.ActorImage(client.After)};
        var loaded=server?.Loaded??archive?.Load?.Loaded;var station=server?.Station??loaded?.Tile;
        object blockImage=null;if(station!=null){var block=GameManager.Instance.World.GetBlock(station.ToWorldPos());blockImage=new{raw=Convert.ToUInt64(block.rawData),block.damage};}
        object serverState=server==null?null:new{current=server.Current(),server.BeforeSaved,server.AfterSaved,server.QueueAttempted,server.QueueCompleted,server.CustodyEscaped,
            held=station!=null&&CancellationBlocks(station),receipt1Source=server.Receipt1Source!=null};
        return new{available=true,active=ProtocolActive,role=GameManager.Instance.World.IsRemote()?"client":"server",world=GameManager.Instance.World.worldState.Guid,
            operation=operation.ToString("N"),client=clientState,server=serverState,
            archive=archive==null?null:new{archive.BeforeRestart,archive.BeforeUncertain,archive.SavedAfterRestart,loaded=loaded?.Installed()==true,held=station!=null&&CancellationBlocks(station),original=archive.Original.Copy().ToString(SaveOptions.DisableFormatting)},
            nativeLoad=loaded?.Installed()==true?loaded.Encode():null,
            actor=actor is EntityPlayerLocal localActor?RebirthRemoteResourceClientOriginal.ActorImage(RebirthCamperEnergyActorImage.Capture(localActor)):null,
            receipt=actor?.Buffs.GetCustomVar(CancellationReceipt(operation)),savedOwner,native=station!=null&&CaptureUploadNative(station,out var image)?Convert.ToBase64String(image):null,
            nativeCanonical=station!=null&&RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var canonical,out _)?Convert.ToBase64String(canonical):null,
            block=blockImage,
            retainedBytes=RebirthRemoteResourcePlacedStationPreparation.RetainedAllocationBytes,events=CancellationScenarioEvents(operation),journals};
    }
    private static void CaptureScenarioJournal(List<object> rows,Guid key,string purpose,Guid operation)
    {
        var record=CancellationJournal(key,purpose).Read(key);if(record==null)return;
        AddScenarioRecord(rows,purpose,key,record,new System.Text.UTF8Encoding(false,true).GetString(record.Before));
    }
    private static void AddScenarioRecord(List<object> rows,string purpose,Guid key,RebirthCamperTransferJournal.Record record,string text)
    {
        if(!record.Before.SequenceEqual(record.After)||record.Operation!=key||record.Revision!=0)throw new InvalidDataException("Audit original mismatch");
        // Full images are diagnostic evidence only. Never truncate into a
        // misleading financial summary or allow unbounded console residency.
        long characters=rows.Sum(value=>(long)((ScenarioRecord)value).before.Length);
        if(rows.Count>=96||checked(2L*(characters+text.Length))>32L*1024*1024)throw new InvalidDataException("Complete audit image budget exceeded");
        rows.Add(new ScenarioRecord{purpose=purpose,key=key.ToString("N"),phase=record.State.ToString(),source=record.Vehicle.ToString("N"),creation=record.OwnerCreation,revision=record.Revision,before=text,after=text});
    }
    private sealed class ScenarioRecord{public string purpose,key,phase,source,creation,before,after;public long revision;}
}

public sealed class ConsoleCmdRebirthPlacedCancellationScenario:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbplacedcancelaudit","rbplacedcancelfixture"};
    public override string getDescription()=>"Read actual cancellation originals or DEBUG hold an actual native boundary in CodexTest.";
    public override string getHelp()=>"rbplacedcancelaudit operation=<32hex> | rbplacedcancelfixture ownerPrepared|ownerReturned|ownerCheckpoint|nativeBefore|nativeIntent|nativeConsumed|savedAfter|terminalAck|release slot=0 operation=<32hex> confirm=CodexTest";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            string operation=args?.SingleOrDefault(value=>value.StartsWith("operation=",StringComparison.Ordinal));Guid id=operation==null?Guid.Empty:Guid.ParseExact(operation.Substring(10),"N");
            bool fixture=args?.Contains("confirm=CodexTest")==true;
            object result;if(fixture)
            {string slot=args.SingleOrDefault(value=>value.StartsWith("slot=",StringComparison.Ordinal));result=RebirthRemoteResourcePlacedStationOriginal.ConfigureCancellationScenario(args[0],slot==null?0:int.Parse(slot.Substring(5)),id);}
            else result=RebirthRemoteResourcePlacedStationOriginal.CaptureCancellationScenario(id);
            Log.Out("[REBIRTH placed cancellation scenario] "+Newtonsoft.Json.JsonConvert.SerializeObject(result));
        }
        catch(Exception exception){Log.Error("[REBIRTH placed cancellation scenario] "+exception.Message);}
    }
}
