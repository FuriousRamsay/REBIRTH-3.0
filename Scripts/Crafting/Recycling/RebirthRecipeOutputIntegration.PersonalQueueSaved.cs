using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class PersonalOutputQueues
    {internal readonly List<UiOutputPending> Values=new List<UiOutputPending>();}
    private sealed class PersonalOutputCapture
    {internal UiOutputPending[] Values;internal string NativeHash,QueueHash,Root,PlayerName;internal int NativeLength;}
    private sealed class PersonalOutputSave
    {internal PersonalOutputCapture Capture;internal string Path;internal byte[] Native;internal IDisposable Memory;}
    private static readonly ConditionalWeakTable<EntityPlayerLocal,PersonalOutputQueues> personalOutputQueues=new ConditionalWeakTable<EntityPlayerLocal,PersonalOutputQueues>();
    private static readonly ConditionalWeakTable<PlayerDataFile,PersonalOutputCapture> personalOutputCaptures=new ConditionalWeakTable<PlayerDataFile,PersonalOutputCapture>();
    private static float nextPersonalOutputQueuePump;
    private const string PersonalOutputReceiptPrefix="rbRecipeOutput_";
    private static string PersonalOutputReceipt(Guid operation)=>PersonalOutputReceiptPrefix+operation.ToString("N");
    private static bool HasUiOutputPersonalPrefix(PlayerDataFile data,Guid operation,int prefix)=>prefix>=1&&prefix<=65&&
        RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,PersonalOutputReceipt(operation),out float phase,out bool present)&&present&&phase==prefix;
    private static bool IsUiOutputPersonalCard(UiOutputOriginal source)
    {
        var card=source?.Scope.Card;
        if(card==null||source.World.IsRemote()||source.Scope.Grid!=null||card.GetParentByType<XUiC_WorkstationWindowGroup>()!=null)return false;
        var cards=card.xui?.FindWindowGroupByName("crafting")?.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
        return cards!=null&&cards.Any(value=>ReferenceEquals(value,card));
    }
    private static void SaveUiOutputPersonalPrefix(UiOutputOriginal source,int index)
    {
        try
        {
            if(!IsUiOutputPersonalCard(source)||!source.Current()||source.OutputEntered||source.OutputPrefix!=index+1||!source.AcceptedOwnerCurrent())return;
            source.Actor.Buffs.SetCustomVar(PersonalOutputReceipt(source.Operation),index+1,true);
            GameManager.Instance.SaveLocalPlayerData();
        }
        catch{/* A failed native save creates no saved prefix and grants no replay. */}
    }
    private static void PumpPersonalOutputQueues(ref ModEvents.SGameUpdateData data)
    {
        try{if(ThreadManager.IsMainThread())PumpRemoteOutputColdDiscovery();}catch{/* No native source or output permission is issued by failed discovery. */}
        if(!ThreadManager.IsMainThread()||UnityEngine.Time.realtimeSinceStartup<nextPersonalOutputQueuePump)return;
        nextPersonalOutputQueuePump=UnityEngine.Time.realtimeSinceStartup+1f;
        SweepUiOutputLoadedOwners(GameManager.Instance?.World);
        PumpPersonalOutputColdTerminals();
        PumpStationOutputQueues();
        PumpStationOutputBefores();
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(actor==null||!personalOutputQueues.TryGetValue(actor,out var candidates))return;
        foreach(var pending in candidates.Values.ToArray())RegisterUiOutputPersonalQueue(pending);
    }
    private static bool IsUiOutputPersonalQueue(UiOutputPending pending)
    {
        return IsUiOutputPersonalCard(pending?.Source);
    }
    private static bool PersonalOutputQueueCurrent(UiOutputPending pending)
    {
        var source=pending?.Source;var actor=pending?.Actor;var card=source?.Scope.Card;
        return source!=null&&actor!=null&&pending.Returned&&pending.QueueObserved&&IsUiOutputPersonalQueue(pending)&&
            ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,pending.World)&&ReferenceEquals(actor.world,pending.World)&&
            ReferenceEquals(pending.World.GetPrimaryPlayer(),actor)&&ReferenceEquals(pending.World.GetEntity(actor.entityId),actor)&&actor.IsSpawned()&&!actor.IsDead()&&
            actor.Died==pending.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==pending.Creation&&
            Path.GetFullPath(GameIO.GetSaveGameDir())==source.Save&&ReferenceEquals(card.xui,actor.PlayerUI?.xui)&&
            card.GetRecipeCount()==pending.Count-1&&(pending.Count==1?card.GetRecipe()==null:ReferenceEquals(card.GetRecipe(),pending.Recipe))&&
            !source.OutputEntered&&source.OutputPrefix==source.Scope.Outputs.Count+1&&source.AcceptedOwnerCurrent()&&
            pendingUiOutputs.TryGetValue(card,out var retained)&&ReferenceEquals(retained,pending)&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==source.Owner;
    }
    private static void RegisterUiOutputPersonalQueue(UiOutputPending pending)
    {
        try
        {
            if(pending?.QueueSaved==true){TryReleasePersonalOutputQueue(pending);return;}
            if(!PersonalOutputQueueCurrent(pending))return;
            var values=personalOutputQueues.GetValue(pending.Actor,_=>new PersonalOutputQueues()).Values;
            values.RemoveAll(value=>value.Source==null);
            if(values.Count<65&&!values.Contains(pending))values.Add(pending);
            if(values.Contains(pending)&&UnityEngine.Time.realtimeSinceStartup>=pending.NextQueueSave)
            {
                pending.NextQueueSave=UnityEngine.Time.realtimeSinceStartup+1f;
                // Use the game's existing player save while this accepted
                // owner/queue postimage is still current. No custom writer.
                GameManager.Instance.SaveLocalPlayerData();
            }
        }
        catch{/* The original remains pending if native queue observation is unavailable. */}
    }
    private static void ObserveUiOutputPersonalQueueFromPlayer(PlayerDataFile data,EntityPlayerLocal actor)
    {
        try
        {
            personalOutputCaptures.Remove(data);
            if(!personalOutputQueues.TryGetValue(actor,out var candidates)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
            var values=candidates.Values.Where(PersonalOutputQueueCurrent).ToArray();if(values.Length==0)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return;
                string queue=RebirthRemoteResourceClientOriginal.QueueImage(data.craftingData);
                if(queue!=RebirthRemoteResourceClientOriginal.QueueImage(actor.PlayerUI.xui.GetCraftingData()))return;
                var bag=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,true));var belt=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,false));
                var equipment=data.equipmentData?.ToArray();
                values=values.Where(value=>HasUiOutputPersonalPrefix(data,value.Operation,value.Source.OutputPrefix)&&value.Source.AcceptedBag.SequenceEqual(bag)&&value.Source.AcceptedBelt.SequenceEqual(belt)&&
                    equipment!=null&&value.Source.AcceptedEquipment.SequenceEqual(equipment)&&PersonalOutputQueueCurrent(value)).ToArray();
                if(values.Length==0)return;
                var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                personalOutputCaptures.Add(data,new PersonalOutputCapture{Values=values,NativeHash=UiOriginalHash(native),NativeLength=native.Length,
                    QueueHash=UiOriginalHash(Encoding.UTF8.GetBytes(queue)),Root=Path.GetFullPath(GameIO.GetPlayerDataDir()),PlayerName=owner.CanonicalId});
            }
        }
        catch{/* Native capture is not changed by a failed additional-output observer. */}
    }
    private static void EnterUiOutputPersonalQueueSave(PlayerDataFile data,string directory,string playerName,bool ran,out object state)
    {
        state=null;IDisposable memory=null;
        try
        {
            if(!ran||!personalOutputCaptures.TryGetValue(data,out var capture))return;
            string root=Path.GetFullPath(directory),path=Path.GetFullPath(Path.Combine(root,playerName+"."+PlayerDataFile.EXT));
            if(root!=capture.Root||playerName!=capture.PlayerName||Path.GetDirectoryName(path)!=root||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*capture.NativeLength+65536),out memory))return;
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
            if(native.Length!=capture.NativeLength||UiOriginalHash(native)!=capture.NativeHash)return;
            state=new PersonalOutputSave{Capture=capture,Path=path,Native=native,Memory=memory};memory=null;
        }
        catch{/* Only this exact native Save can produce a queue acceptance witness. */}
        finally{memory?.Dispose();}
    }
    private static void ObserveUiOutputPersonalQueueSave(bool ran,object state)
    {
        if(state is not PersonalOutputSave save)return;
        try
        {
            if(!ran||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(save.Path,save.Native))return;
            foreach(var pending in save.Capture.Values)
            {
                var source=pending.Source;if(source==null)continue;
                var proof=new XElement("recipeOutputPersonalQueueSaved",new XAttribute("operation",pending.Operation.ToString("N")),new XAttribute("original",pending.Hash),
                    new XAttribute("owner",source.Owner),new XAttribute("creation",pending.Creation),new XAttribute("deaths",pending.Deaths),
                    new XAttribute("beforeCount",pending.Count),new XAttribute("afterCount",pending.Count-1),new XAttribute("outputPrefix",source.OutputPrefix),
                    new XAttribute("playerFile",save.Path),new XAttribute("nativeDigest",save.Capture.NativeHash),new XAttribute("nativeLength",save.Capture.NativeLength),
                    new XAttribute("queueDigest",save.Capture.QueueHash));
                proof.Add(new XAttribute("personalPrefix",source.OutputPrefix));
                byte[] bytes=UiOriginalBytes(proof);string path=pending.Path+".queue-owner-saved";
                bool published=PublishUiOriginal(path,bytes);
                if(!published)
                {
                    RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
                    if(File.Exists(path+".tmp")||File.Exists(path+".bak")||!File.Exists(path)||new FileInfo(path).Length!=bytes.Length||!File.ReadAllBytes(path).SequenceEqual(bytes))continue;
                }
                pending.QueueSaved=true;
                // Native disk completion can occur off the UI thread. Publish
                // only the saved fact there; actual queue observation performs
                // any deferred card/resource release on the owning thread.
                if(ThreadManager.IsMainThread())TryReleasePersonalOutputQueue(pending);
            }
        }
        catch{/* Failed proof publication retains the source/card; no physical output is repeated. */}
        finally{save.Memory?.Dispose();save.Memory=null;save.Native=null;}
    }
    private static void TryReleasePersonalOutputQueue(UiOutputPending pending)
    {
        if(!pending.QueueSaved||!SavedPersonalOutputReleaseCurrent(pending))return;
        var source=pending.Source;var actor=GameManager.Instance.World.GetPrimaryPlayer();bool sameLife=actor.Died==pending.Deaths;pendingUiOutputs.Remove(source.Scope.Card);
        if(personalOutputQueues.TryGetValue(pending.Actor,out var candidates))candidates.Values.Remove(pending);
        pending.Source=null;source.Dispose();
        if(sameLife&&actor.Buffs.GetCustomVar(PersonalOutputReceipt(source.Operation))==source.OutputPrefix)
        {
            actor.Buffs.SetCustomVar(PersonalOutputReceipt(source.Operation),0f,true);
            actor.Buffs.RemoveCustomVar(PersonalOutputReceipt(source.Operation));
            GameManager.Instance.SaveLocalPlayerData();
        }
    }
    private static bool SavedPersonalOutputReleaseCurrent(UiOutputPending pending)
    {
        if(PersonalOutputQueueCurrent(pending))return true;
        // A positive complete saved original may retire its warm metadata in a
        // same or later life, including legitimate later inventory movement.
        // This removes only its retained card association; it never restores
        // the old native queue or repeats the accepted inventory delivery.
        var source=pending?.Source;var world=GameManager.Instance?.World;var actor=world?.GetPrimaryPlayer();
        return source!=null&&pending.QueueSaved&&ThreadManager.IsMainThread()&&ReferenceEquals(world,pending.World)&&!world.IsRemote()&&
            actor!=null&&actor.IsSpawned()&&!actor.IsDead()&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&actor.Died>=pending.Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(actor)==pending.Creation&&Path.GetFullPath(GameIO.GetSaveGameDir())==source.Save&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==source.Owner&&
            pendingUiOutputs.TryGetValue(source.Scope.Card,out var retained)&&ReferenceEquals(retained,pending);
    }
}
