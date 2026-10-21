#if REBIRTH_DEBUG
// NOT_RUN. Actual production Use/plan/native owner event/save pipeline in CodexTest.
// Seed an authorized nearby generic NPC with native full/partial consumables;
// repeat with an NPC-only source and with an identical player-bag item.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Xml.Linq;

public static class MpCNpcMetabolismCustodyScenario
{
    static readonly Type Custody=typeof(RebirthCompanionService).Assembly.GetType("RebirthNpcMetabolismCustody",true);
    static void Need(bool value,string label){if(!value)throw new InvalidOperationException(label);}
    static void Require(EntityPlayer player,EntityRebirthNPC npc)
    {
        Need(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ThreadManager.IsMainThread(),"actual disposable CodexTest main thread required");
        Need(player!=null&&npc!=null&&ReferenceEquals(player.world,GameManager.Instance.World)&&ReferenceEquals(npc.world,player.world)&&!player.world.IsRemote()&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out _),"actual host owner and loaded generic NPC required");
        Need(!(npc is EntityRebirthDogCompanion)&&!player.IsDead()&&!npc.IsDead(),"generic live NPC source required");
    }
    static RebirthConsumeResult Use(EntityPlayer player,EntityRebirthNPC npc,int itemType,string key)=>(RebirthConsumeResult)Custody.GetMethod("Use",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{player,npc.RebirthRuntimeState.StableId,itemType,key});
    static string Belt(EntityPlayer player)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.inventory.ItemGrid.items));
    static string Bag(EntityPlayer player)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.bag.ItemGrid.items));
    static string Snapshot(RebirthNpcInventorySnapshot image)=>string.Join(";",image.Quantities.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>pair.Key+"="+pair.Value))+"|"+image.NativeStacks?.Write();
    static string OriginalDirectory(string operation)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData");
        string pending=Path.Combine(root,"NpcConsumption",operation),archived=Path.Combine(root,"NpcConsumptionTerminal",operation);
        Need(!(Directory.Exists(pending)&&Directory.Exists(archived)),"duplicate original namespaces");
        return Directory.Exists(archived)?archived:pending;
    }
    static object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    public static void ActualFullStomachRefusesNpcSource(EntityPlayer player,EntityRebirthNPC npc,string key,int itemType)
    {
        Require(player,npc);string belt=Belt(player),bag=Bag(player);var before=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);string source=Snapshot(before);
        var result=Use(player,npc,itemType,key);Need(!result.Success&&result.Failure==RebirthMetabolismConsumeFailure.TooFull,"seed actual full stomach before invoking this rejection case");
        var after=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);Need(after.Revision==before.Revision&&Snapshot(after)==source,"full stomach rejection consumed/replaced NPC source");
        Need(belt==Belt(player)&&bag==Bag(player),"full stomach rejection touched matching player inventory");
    }
    public static object ArmActualRestartBoundary(EntityPlayer player,EntityRebirthNPC npc,string key,int itemType,bool uncertainIngestion)
    {
        Require(player,npc);var hook=Custody.GetField("ScenarioBoundary",BindingFlags.Static|BindingFlags.NonPublic);Need(hook!=null,"DEBUG native boundary required");
        Guid operation=Guid.Empty;string hold=uncertainIngestion?"ingestion-intent":"before-ingestion-permit";
        hook.SetValue(null,(Action<Guid,string>)((id,stage)=>{if(operation==Guid.Empty)operation=id;Need(operation==id,"restart fixture changed original");if(stage==hold)throw new IOException("CodexTest actual restart boundary retained");}));
        try
        {
            Need(!Use(player,npc,itemType,key).Success,"actual production boundary must hold before native ingestion");
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","NpcConsumption",operation.ToString("N"));
            Need(File.Exists(Path.Combine(directory,"source-saved.xml")),"actual source debit must be durably saved first");
            var control=XDocument.Load(Path.Combine(directory,"control.xml"));Need((string)control.Root.Attribute("stage")== (uncertainIngestion?"ingestion-intent":"source-saved"),"wrong durable causal boundary");
            // Leave the real DEBUG boundary armed until operator crashes this
            // process. A fixture must not emulate a restart by clearing caches.
            return new{operation=operation.ToString("N"),process=System.Diagnostics.Process.GetCurrentProcess().Id,hold};
        }
        catch{hook.SetValue(null,null);throw;}
    }
    public static void VerifyAfterActualRestart(EntityPlayer player,EntityRebirthNPC npc,string operation,int priorProcess,bool uncertainIngestion)
    {
        Require(player,npc);Need(System.Diagnostics.Process.GetCurrentProcess().Id!=priorProcess,"actual process restart required");
        Guid id=Guid.ParseExact(operation,"N");string directory=OriginalDirectory(operation);
        var prepared=XDocument.Load(Path.Combine(directory,"prepared.xml"));var original=XElement.Parse((string)prepared.Root.Element("original"));
        Need((string)original.Attribute("npc")==npc.RebirthRuntimeState.StableId.ToString(),"actual original native NPC required");
        var actual=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);
        var expected=original.Element("sourceAfter").Element("inventory");Need(actual.Revision==(uint)original.Attribute("revision")+1,"cold source debit replayed");
        var current=new XElement("inventory",actual.Quantities.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("quantity",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value))),
            actual.Reservations.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("reserved",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value))),actual.NativeStacks?.Write());
        Need(XNode.DeepEquals(current,expected),"complete native source/replacement/sibling images changed across restart");
        var audit=(IDictionary)Custody.GetField("ColdSourceAudit",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(uncertainIngestion)
        {
            Need(!audit.Contains(id)&&!File.Exists(Path.Combine(directory,"committed.xml"))&&!File.Exists(Path.Combine(directory,"owner-effect-intent.xml")),"uncertain original issued a new effect permit");
            var owners=(IDictionary)Custody.GetField("nativeOwners",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Need(owners.Values.Cast<object>().Any(value=>ReferenceEquals(Field(value,"Actor"),player)&&(bool)Field(value,"ReadReturned")&&(bool)Field(value,"Hydrated")),"actual native owner Load/Read/ToPlayer reconstruction required");
            Need(player.Buffs.GetCustomVar("rbNpcConsume_"+operation)==0f,"uncertain canonical attempt ran native owner event");
        }
        else
        {
            Need(audit.Contains(id),"actual typed owner/body cold adoption required");var receipt=audit[id];
            Need((int)Field(receipt,"Process")==System.Diagnostics.Process.GetCurrentProcess().Id&&(int)Field(receipt,"NativeOwnerBytes")>6&&(int)Field(receipt,"NativeBodyBytes")>=21,"restart used a fixture/equality stand-in for native sources");
            Need((int)Field(receipt,"SourceInvocations")==0&&(int)Field(receipt,"OwnerInvocations")==1&&(string)Field(receipt,"Stage")=="committed","cold continuation replayed source or failed actual native completion");
            foreach(string stage in new[]{"ingestion-saved","secondary-state-saved","owner-secondary-saved","owner-after-saved","committed"})Need(File.Exists(Path.Combine(directory,stage+".xml")),"cold original failed coupled durable boundary: "+stage);
        }
    }
    public static void SavedSourceRetryCompletesOnce(EntityPlayer player,EntityRebirthNPC npc,string key,int itemType)
    {
        Require(player,npc);Need(ReferenceEquals(player.world.GetPrimaryPlayer(),player),"synchronous retry fixture needs local owner; remote uses arm/verify");string belt=Belt(player),bag=Bag(player);
        var before=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);Need(before.Quantities.TryGetValue(key,out int count)&&count>0,"actual NPC source required");
        var hook=Custody.GetField("ScenarioBoundary",BindingFlags.Static|BindingFlags.NonPublic);Need(hook!=null,"DEBUG production boundary required");var prior=hook.GetValue(null);
        Guid operation=Guid.Empty;int sourceReturns=0,ownerReturns=0;string directory=null;
        hook.SetValue(null,(Action<Guid,string>)((id,stage)=>
        {
            if(operation==Guid.Empty)operation=id;Need(operation==id,"retry created another operation");
            if(stage=="source-returned"){sourceReturns++;throw new IOException("scenario interruption after actual source mutation and durable returned record");}
            if(stage=="owner-effect-returned")ownerReturns++;
        }));
        try
        {
            Need(!Use(player,npc,itemType,key).Success,"actual post-source interruption must retain original");
            Need(sourceReturns==1&&belt==Belt(player)&&bag==Bag(player),"NPC source path removed matching player inventory");
            var afterSource=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);Need(afterSource.Revision==before.Revision+1,"actual source mutation must occur exactly once");string exactAfter=Snapshot(afterSource);
            directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","NpcConsumption",operation.ToString("N"));Need(File.Exists(Path.Combine(directory,"source-returned.xml"))&&!File.Exists(Path.Combine(directory,"committed.xml")),"interrupted original was fabricated as terminal");
            hook.SetValue(null,(Action<Guid,string>)((id,stage)=>{Need(id==operation,"retry changed immutable operation");if(stage=="source-returned")sourceReturns++;if(stage=="owner-effect-returned")ownerReturns++;}));
            Need(Use(player,npc,itemType,key).Success,"same original must finish through actual native owner event and complete durable saves");
            var after=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);
            Need(sourceReturns==1&&ownerReturns==1&&after.Revision==afterSource.Revision&&Snapshot(after)==exactAfter,"retry replayed source debit or lost full native replacement/sibling metadata");
            Need(belt==Belt(player)&&bag==Bag(player),"native owner event mutated player inventory custody");
            directory=OriginalDirectory(operation.ToString("N"));
            foreach(string stage in new[]{"source-saved","ingestion-saved","secondary-state-saved","owner-before-saved","owner-after-saved","committed"})Need(File.Exists(Path.Combine(directory,stage+".xml")),"missing actual coupled saved boundary: "+stage);
            Need(File.ReadAllBytes(Path.Combine(directory,"owner-after.native")).Length>6,"missing full saved native owner image");
        }
        finally{hook.SetValue(null,prior);}
    }
    public static object ArmSavedCheckpoint(EntityPlayer player,EntityRebirthNPC npc,string key,int itemType,string boundary)
    {
        Require(player,npc);
        Need(new[]{"prepared","before-ingestion-permit","ingestion-intent","before-secondary-permit","before-owner-effect-permit","before-terminal-metadata"}.Contains(boundary),"unsupported causal checkpoint");
        var hook=Custody.GetField("ScenarioBoundary",BindingFlags.Static|BindingFlags.NonPublic);Need(hook!=null,"DEBUG actual boundary required");
        Guid operation=Guid.Empty;bool reached=false;
        hook.SetValue(null,(Action<Guid,string>)((id,stage)=>{if(operation==Guid.Empty)operation=id;Need(operation==id,"original changed");if(stage==boundary){reached=true;throw new IOException("CodexTest retained actual checkpoint "+boundary);}}));
        try
        {
            Need(!Use(player,npc,itemType,key).Success,"checkpoint unexpectedly completed");
            // Remote native owner steps may complete on later actual authenticated
            // callbacks. Keep this hook armed and inspect control before crashing.
            return new{operation=operation.ToString("N"),process=System.Diagnostics.Process.GetCurrentProcess().Id,boundary,reached};
        }
        catch{hook.SetValue(null,null);throw;}
    }
    public static void VerifySavedCheckpoint(EntityPlayer player,EntityRebirthNPC npc,string operation,int priorProcess,string boundary)
    {
        Require(player,npc);Need(System.Diagnostics.Process.GetCurrentProcess().Id!=priorProcess,"real process restart required");
        Guid id=Guid.ParseExact(operation,"N");string directory=OriginalDirectory(operation);
        var prepared=XDocument.Load(Path.Combine(directory,"prepared.xml"));var original=XElement.Parse((string)prepared.Root.Element("original"));
        Need((string)original.Attribute("npc")==npc.RebirthRuntimeState.StableId.ToString(),"wrong actual native NPC original");
        var current=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);
        bool never=boundary=="prepared",uncertain=boundary=="ingestion-intent";
        var source=original.Element(never?"sourceBefore":"sourceAfter").Element("inventory");
        var actual=new XElement("inventory",current.Quantities.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("quantity",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value))),current.Reservations.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("reserved",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value))),current.NativeStacks?.Write());
        Need(current.Revision==(uint)original.Attribute("revision")+(never?0u:1u)&&XNode.DeepEquals(source,actual),"actual complete source changed/replayed across restart");
        var audit=(IDictionary)Custody.GetField("ColdSourceAudit",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(never)
        {
            Need(File.Exists(Path.Combine(directory,"source-not-entered.xml"))&&File.Exists(Path.Combine(directory,"no-effect.xml"))&&!File.Exists(Path.Combine(directory,"source-returned.xml"))&&!File.Exists(Path.Combine(directory,"ingestion-intent.xml")),"never-issued original failed positive no-effect settlement");
            var evidence=XDocument.Load(Path.Combine(directory,"source-not-entered.xml")).Root.Element("neverIssued");Need(evidence!=null&&Convert.FromBase64String(evidence.Element("loadedNativeBody").Value).Length>=21&&!string.IsNullOrWhiteSpace(evidence.Element("savedSource").Value),"actual native body/full unchanged source evidence missing");return;
        }
        if(uncertain)
        {
            Need(!audit.Contains(id)&&!File.Exists(Path.Combine(directory,"committed.xml"))&&!File.Exists(Path.Combine(directory,"owner-effect-intent.xml")),"uncertain attempted ingestion replayed");
            var owners=(IDictionary)Custody.GetField("nativeOwners",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var bodies=(IDictionary)Custody.GetField("nativeBodies",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Need(owners.Values.Cast<object>().Any(value=>ReferenceEquals(Field(value,"Actor"),player)&&(bool)Field(value,"ReadReturned")&&(bool)Field(value,"Hydrated"))&&bodies.Values.Cast<object>().Any(value=>ReferenceEquals(Field(value,"Body"),npc)&&(bool)Field(value,"Returned")),"unknown retention must be observed after actual owner/body native reconstruction");return;
        }
        Need(audit.Contains(id),"actual native reconstruction receipt missing");var receipt=audit[id];
        Need((int)Field(receipt,"Process")==System.Diagnostics.Process.GetCurrentProcess().Id&&(int)Field(receipt,"NativeOwnerBytes")>6&&(int)Field(receipt,"SourceInvocations")==0,"cold continuation lacked native owner source or replayed debit");
        bool metadata=boundary=="before-terminal-metadata";
        Need((bool)Field(receipt,"MetadataOnly")==metadata,"wrong recovery capability");
        Need(metadata?(int)Field(receipt,"HistoricalBodyBytes")>=21:(int)Field(receipt,"NativeBodyBytes")>=21,"full historical/native body source missing");
        Need((int)Field(receipt,"OwnerInvocations")== (metadata||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)?0:1)&&(string)Field(receipt,"Stage")=="committed","actual native owner event replayed or failed terminal completion");
        foreach(string stage in new[]{"source-saved","ingestion-saved","owner-secondary-saved","owner-after-saved","committed"})Need(File.Exists(Path.Combine(directory,stage+".xml")),"missing full coupled durable stage "+stage);
    }
    public static void WarmTerminalAfterActualNpcChange(EntityPlayer player,EntityRebirthNPC npc,string operation)
    {
        Require(player,npc);Guid id=Guid.ParseExact(operation,"N");
        var originals=(IDictionary)Custody.GetField("pending",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Need(originals.Contains(npc.RebirthRuntimeState.StableId),"actual retained warm original required");var original=originals[npc.RebirthRuntimeState.StableId];
        Need((Guid)Field(original,"Operation")==id&&(bool)Field(original,"OwnerSaved")&&(bool)Field(original,"OwnerReturned"),"actual full native after must already be saved");
        Need(!(bool)Custody.GetMethod("Current",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{original}),"operator must first actually move/reassign NPC outside original admission");
        var image=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);string source=Snapshot(image),belt=Belt(player),bag=Bag(player);
        var hook=Custody.GetField("ScenarioBoundary",BindingFlags.Static|BindingFlags.NonPublic);hook.SetValue(null,null);
        Custody.GetMethod("Tick",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        Need(!originals.Contains(npc.RebirthRuntimeState.StableId)&&File.Exists(Path.Combine(OriginalDirectory(operation),"committed.xml")),"historical financial completion still depends on unrelated current NPC admission");
        Need(Snapshot(RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId))==source&&Belt(player)==belt&&Bag(player)==bag,"terminal metadata mutated current source/owner inventory");
    }
    public static void NegativeColdScanCapacity(EntityPlayer player,EntityRebirthNPC npc)
    {
        Require(player,npc);string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","NpcConsumption");
        Need(!Directory.Exists(root)||!Directory.EnumerateFileSystemEntries(root).Any(),"actual no-pending disposable baseline required");
        var has=Custody.GetMethod("HasColdHold",BindingFlags.Static|BindingFlags.NonPublic);var cache=(IDictionary)Custody.GetField("coldIndex",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Need(cache.Values.Cast<object>().All(value=>!(bool)value),"fixture cannot evict any actual positive custody");
        for(int i=0;i<257;i++)Need(!(bool)has.Invoke(null,new object[]{RebirthNpcStableId.NewId()}),"negative production scan cap turned unrelated NPC into permanent custody at "+i);
        Need(cache.Count<=256&&! (bool)has.Invoke(null,new object[]{npc.RebirthRuntimeState.StableId}),"actual native NPC admission remained blocked after negative scan capacity");
        Need(!Directory.Exists(root)||!Directory.EnumerateFileSystemEntries(root).Any(),"negative scan invented a financial original");
    }
    static Guid lostHostOperation,lostClientOperation;
    static readonly System.Collections.Generic.Dictionary<byte,int> hostCourierCalls=new System.Collections.Generic.Dictionary<byte,int>();
    static readonly System.Collections.Generic.Dictionary<byte,int> clientReturnCalls=new System.Collections.Generic.Dictionary<byte,int>();
    static int clientPdsCalls;
    public static void ArmLostOwnerReturns()
    {
        Need(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&GameManager.Instance?.World?.IsRemote()==true,"actual disposable owner client required");
        lostClientOperation=Guid.Empty;clientReturnCalls.Clear();clientPdsCalls=0;
        Custody.GetField("ScenarioDropOwnerReturn",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(Func<Guid,byte,bool>)((operation,phase)=>
        {if(lostClientOperation==Guid.Empty)lostClientOperation=operation;Need(operation==lostClientOperation,"return courier created another original");clientReturnCalls.TryGetValue(phase,out int calls);clientReturnCalls[phase]=calls+1;return calls==0;}));
        Custody.GetField("ScenarioDropOwnerPds",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(Func<Guid,bool>)(operation=>
        {Need(operation==lostClientOperation,"native PDS courier lost original");return ++clientPdsCalls<=3;}));
    }
    public static object ArmLostOwnerCouriers(EntityPlayer player,EntityRebirthNPC npc,string key,int itemType)
    {
        Require(player,npc);Need(!ReferenceEquals(player.world.GetPrimaryPlayer(),player),"actual remote owner required");lostHostOperation=Guid.Empty;hostCourierCalls.Clear();
        Custody.GetField("ScenarioDropOwnerCourier",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(Func<Guid,byte,bool>)((operation,phase)=>
        {if(lostHostOperation==Guid.Empty)lostHostOperation=operation;Need(operation==lostHostOperation,"courier replay changed financial original");hostCourierCalls.TryGetValue(phase,out int calls);hostCourierCalls[phase]=calls+1;return calls==0;}));
        Need(!Use(player,npc,itemType,key).Success,"lost first real courier must retain original");
        return new{operation=lostHostOperation.ToString("N"),process=System.Diagnostics.Process.GetCurrentProcess().Id};
    }
    public static void VerifyLostOwnerCouriers(EntityPlayer player,EntityRebirthNPC npc,string operation)
    {
        Require(player,npc);Need(Guid.ParseExact(operation,"N")==lostHostOperation,"same actual host original required");
        foreach(byte phase in new byte[]{0,4,1})Need(hostCourierCalls.TryGetValue(phase,out int calls)&&calls>=2,"actual dropped courier did not converge phase "+phase);
        string directory=OriginalDirectory(operation);foreach(string stage in new[]{"source-saved","ingestion-saved","owner-secondary-saved","owner-after-saved","committed"})Need(File.Exists(Path.Combine(directory,stage+".xml")),"actual full durable completion missing "+stage);
        var before=XDocument.Load(Path.Combine(directory,"prepared.xml")).Root.Element("original").Value;
        Need(XDocument.Load(Path.Combine(directory,"committed.xml")).Root.Element("original").Value==before,"courier rebuilt a different full original");
        Custody.GetField("ScenarioDropOwnerCourier",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
    }
    public static void VerifyLostOwnerReturns(string operation)
    {
        Need(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&GameManager.Instance?.World?.IsRemote()==true,"actual disposable owner client required");
        Guid id=Guid.ParseExact(operation,"N");Need(id==lostClientOperation,"same original client request required");
        foreach(byte phase in new byte[]{0,2,1})Need(clientReturnCalls.TryGetValue(phase,out int calls)&&calls>=2,"actual lost return was not republished phase "+phase);
        var audit=(IDictionary)Custody.GetField("OwnerNativeCallAudit",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);Need(audit.Contains(id),"actual native owner invocation source missing");var counts=(int[])audit[id];
        Need(counts[0]==1&&counts[1]==1&&clientPdsCalls>3,"courier retried actual native effect instead of saved original receipt/PDS");
        Custody.GetField("ScenarioDropOwnerReturn",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);Custody.GetField("ScenarioDropOwnerPds",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
    }
}

#endif
