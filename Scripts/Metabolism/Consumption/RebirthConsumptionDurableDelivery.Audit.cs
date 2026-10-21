using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
#if REBIRTH_DEBUG
    private static readonly Dictionary<Guid,int[]> inventoryNativeCalls=new Dictionary<Guid,int[]>();
    private static readonly Dictionary<Guid,EntityPlayer> inventoryAuditActors=new Dictionary<Guid,EntityPlayer>();
    private static readonly Dictionary<Guid,int> inventoryAuditPurposeBytes=new Dictionary<Guid,int>();
    private static readonly Dictionary<EntityPlayer,JObject> inventoryRefusals=new Dictionary<EntityPlayer,JObject>();private static long inventoryRefusalSequence;
#endif
    private static string CaptureInventoryAuditFingerprint(EntityPlayer actor)
    {
#if REBIRTH_DEBUG
        try{if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")return RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items))+"|"+Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items)));}catch{}
#endif
        return null;
    }
    private static void ObserveInventoryRefusal(EntityPlayer actor,RebirthMetabolismStateRepository.NpcServingState token,string before,RebirthConsumeResult result)
    {
#if REBIRTH_DEBUG
        try
        {
            if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||token==null||before==null||inventoryRefusals.Count>=128&&!inventoryRefusals.ContainsKey(actor))return;
            string canonical=RebirthMetabolismStateRepository.TryGet(actor,out var state)?RebirthMetabolismStateRepository.DescribeNpcServing(token.Id,state):null;
            inventoryRefusals[actor]=new JObject{["sequence"]=++inventoryRefusalSequence,["sourceBefore"]=before,["sourceAfter"]=CaptureInventoryAuditFingerprint(actor),
                ["canonicalBefore"]=RebirthRemoteResourceDurableJournal.Digest(token.Before),["canonicalAfter"]=canonical==null?null:RebirthRemoteResourceDurableJournal.Digest(canonical),
                ["failure"]=(int)result.Failure,["success"]=result.Success,["message"]=result.Message,["sourcePermissionIssued"]=false};
        }catch{}
#endif
    }
    private static void ObserveInventoryAdmission(Guid operation,EntityPlayer actor,string purpose)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&inventoryAuditActors.Count<128)
        {inventoryAuditActors[operation]=actor;inventoryAuditPurposeBytes[operation]=System.Text.Encoding.UTF8.GetByteCount(purpose);}
#endif
    }
    private static void ObserveInventoryCall(Guid operation,int kind)
    {
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")return;
        if(!inventoryNativeCalls.TryGetValue(operation,out var calls))
        {if(inventoryNativeCalls.Count>=128)return;inventoryNativeCalls.Add(operation,calls=new int[5]);}
        calls[kind]++;
#endif
    }
    internal static JObject CaptureInventoryAuditState(EntityPlayer actor,Guid selected)
    {
        var result=new JObject{["schema"]=1,["available"]=false};
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actor?.world==null||
            !ReferenceEquals(actor.world,GameManager.Instance?.World)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor))return result;
        var ids=new HashSet<Guid>();if(selected!=Guid.Empty)ids.Add(selected);
        else
        {
            foreach(var original in inventoryOriginals.Values.Where(value=>ReferenceEquals(value.Actor,actor)))ids.Add(original.Operation);
            foreach(var client in inventoryClients.Values.Where(value=>ReferenceEquals(value.Actor,actor)))ids.Add(client.Operation);
            foreach(var load in inventoryNativeLoads.Where(value=>ReferenceEquals(value.Actor,actor)))foreach(var id in load.Operations)ids.Add(id);
            foreach(var observed in inventoryAuditActors.Where(value=>ReferenceEquals(value.Value,actor)))ids.Add(observed.Key);
        }
        var rows=new JArray();result["records"]=rows;result["active"]=InventoryProtocolActive;
        result["inventoryFingerprint"]=CaptureInventoryAuditFingerprint(actor);
        if(inventoryRefusals.TryGetValue(actor,out var refusal))result["lastRefusal"]=refusal.DeepClone();
        if(actor is EntityPlayerLocal)result["feedback"]=RebirthMetabolismUiFeedback.CaptureConsumptionAuditFeedback();
        if(ids.Count>32){result["truncated"]=true;return result;}
        foreach(var operation in ids.OrderBy(value=>value))
        {
            var row=new JObject{["operation"]=operation.ToString("N"),["activeOriginal"]=false,["activeClient"]=false,["diskPresent"]=false};rows.Add(row);
            if(inventoryAuditPurposeBytes.TryGetValue(operation,out int purposeBytes))row["purposeBytes"]=purposeBytes;
            if(inventoryOriginals.TryGetValue(operation,out var original)&&ReferenceEquals(original.Actor,actor))
            {
                row["activeOriginal"]=true;row["current"]=original.Current();row["binding"]=RebirthRemoteResourceDurableJournal.Digest(original.Text);
                row["sourcePrefix"]=original.QualifiedPrefix;row["sourceCommands"]=original.Inventory.Commands().Length;
                row["sourceSaved"]=original.SourceSaved;row["canonicalSaved"]=original.CanonicalSaved;row["deferredReturned"]=original.DeferredReturned;
                row["secondarySaved"]=original.SecondarySaved;row["nativeSaved"]=original.NativeSaved;row["committed"]=original.Committed;
            }
            if(inventoryClients.TryGetValue(operation,out var client)&&ReferenceEquals(client.Actor,actor))
            {row["activeClient"]=true;row["clientCurrent"]=client.Current();row["binding"]=RebirthCookingRemoteAdmission.ReadProtocolXml(client.Text,MaximumInventoryPurpose).Attribute("binding")?.Value;}
            if(actor is EntityPlayerLocal local)
            {
                row["beforeMarker"]=local.Buffs.GetCustomVar(InventoryBeforeReceipt(operation));row["sourceMarker"]=local.Buffs.GetCustomVar(InventorySourceReceipt(operation));
                row["secondaryMarker"]=local.Buffs.GetCustomVar(InventorySecondaryReceipt(operation));row["useMarker"]=local.Buffs.GetCustomVar(InventoryUseReceipt(operation));
            }
            if(inventoryNativeCalls.TryGetValue(operation,out var calls))
            {row["sourceCalls"]=calls[0];row["canonicalCalls"]=calls[1];row["deferredCalls"]=calls[2];row["hydrationCalls"]=calls[3];row["useCalls"]=calls[4];}
            else foreach(string field in new[]{"sourceCalls","canonicalCalls","deferredCalls","hydrationCalls","useCalls"})row[field]=0;
            if(!actor.world.IsRemote()&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))
            {
                string directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","PlayerConsumption",owner.StorageKey,operation.ToString("N"));
                if(!System.IO.Directory.Exists(directory))continue;row["diskPresent"]=true;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out var memory)){row["unknown"]=true;continue;}
                using(memory)try
                {
                    var history=ReadInventoryHistory(directory,operation,out string text);var image=RebirthCookingRemoteAdmission.ReadProtocolXml(text,16*1024*1024);
                    if((string)image.Attribute("owner")!=owner.CanonicalId||(string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor))throw new InvalidDataException();
                    string binding=RebirthRemoteResourceDurableJournal.Digest(text);if(row["binding"]!=null&&(string)row["binding"]!=binding)throw new InvalidDataException();
                    row["binding"]=binding;row["originalDeaths"]=(int)image.Attribute("deaths");
                    row["stages"]=new JArray(history.Keys);row["diskCommitted"]=history.ContainsKey("committed");row["diskRetired"]=history.ContainsKey("retired");row["diskRefused"]=history.ContainsKey("refused-before-source");
                    row["sourceCommands"]=history.Keys.Count(value=>value.StartsWith("source-command-",StringComparison.Ordinal));
                }
                catch(Exception error){row["unknown"]=true;row["error"]=error.GetType().Name;}
            }
        }
        result["available"]=true;result["truncated"]=false;
#endif
        return result;
    }
}
