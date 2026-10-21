using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    internal static string OutputOwnerAuditHash(EntityPlayerLocal player)=>UiOriginalHash(OwningGrid(player.bag.ItemGrid.items).Concat(OwningGrid(player.inventory.ItemGrid.items)).Concat(OwningEquipmentNative(player)).ToArray());
    private sealed class OutputAuditIds{internal readonly Queue<Guid> Order=new Queue<Guid>();internal readonly HashSet<Guid> Values=new HashSet<Guid>();internal readonly Dictionary<Guid,WeakReference<XUiC_RecipeStack>> Cards=new Dictionary<Guid,WeakReference<XUiC_RecipeStack>>();}
    private static readonly ConditionalWeakTable<EntityPlayerLocal,OutputAuditIds> outputAuditIds=new ConditionalWeakTable<EntityPlayerLocal,OutputAuditIds>();
    private static void ObserveOutputAuditOriginal(EntityPlayerLocal actor,Guid operation,XUiC_RecipeStack card=null)
    {
#if REBIRTH_DEBUG
        if(actor==null||!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")return;
        var ids=outputAuditIds.GetValue(actor,_=>new OutputAuditIds());if(card!=null)ids.Cards[operation]=new WeakReference<XUiC_RecipeStack>(card);if(!ids.Values.Add(operation))return;
        ids.Order.Enqueue(operation);while(ids.Order.Count>65){var stale=ids.Order.Dequeue();ids.Values.Remove(stale);ids.Cards.Remove(stale);}
#endif
    }
    internal static JObject CaptureOutputAuditState(EntityPlayer actor)
    {
        var result=new JObject{["schema"]=1,["available"]=false};
#if REBIRTH_DEBUG
        try
        {
            if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&actor!=null&&
                ReferenceEquals(actor.world,GameManager.Instance?.World)&&actor.world?.IsRemote()==false&&
                ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&actor is not EntityPlayerLocal)
            {result["remoteHostSources"]=RebirthWorkstationCompletionPipeline.CaptureRemoteOutputHostAudit(actor);result["available"]=true;return result;}
            if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actor is not EntityPlayerLocal player||
                !ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return result;
            var cards=player.PlayerUI?.xui?.FindWindowGroupByName("crafting")?.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
            if(cards==null||cards.Length>255)return result;
            foreach(var card in cards)if(card!=null&&pendingUiOutputs.TryGetValue(card,out var pending))ObserveOutputAuditOriginal(player,pending.Operation);
            foreach(var load in uiOutputLoadedOwners.Where(value=>ReferenceEquals(value.Actor,player)&&value.Hydrated&&!value.Closed))
                foreach(var id in load.Operations)ObserveOutputAuditOriginal(player,id);
            var rows=new JArray();result["records"]=rows;result["deaths"]=player.Died;
            result["stationBefore"]=CaptureStationOutputBeforeAudit(player);
            result["stationRecords"]=CaptureStationOutputRecordsAudit(player,owner.StorageKey);
            result["stationCold"]=CaptureStationOutputColdAudit(player);
            result["milling"]=RebirthCookingMillingPublication.CaptureMillingAudit(player);
            result["remotePrefixes"]=RebirthWorkstationCompletionPipeline.CaptureRemoteOutputPrefixAudit(player);
            result["remoteColdFinals"]=CaptureRemoteOutputColdFinalAudit(player);
            result["remoteOwnerNativeLoaded"]=loadedRemoteOutputOwner?.EpisodeCurrent()==true&&ReferenceEquals(loadedRemoteOutputOwner.Actor,player);
            result["remoteOwnerMatchesLoaded"]=loadedRemoteOutputOwner?.Current()==true&&ReferenceEquals(loadedRemoteOutputOwner.Actor,player);
            byte[] bag=OwningGrid(player.bag.ItemGrid.items),belt=OwningGrid(player.inventory.ItemGrid.items),equipment=OwningEquipmentNative(player);
            string queue=RebirthRemoteResourceClientOriginal.QueueImage(player.PlayerUI.xui.GetCraftingData());
            result["ownerImage"]=UiOriginalHash(bag.Concat(belt).Concat(equipment).ToArray());result["queueImage"]=UiOriginalHash(Encoding.UTF8.GetBytes(queue));
            if(!outputAuditIds.TryGetValue(player,out var ids)){result["available"]=true;return result;}
            foreach(var operation in ids.Order.ToArray())
            {
                var row=new JObject{["operation"]=operation.ToString("N"),["unknown"]=true};rows.Add(row);
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null)continue;
                    string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");
                    if(!ReadPersonalOutputSidecar(path,out var bytes,out var original))continue;
                    if(original.Name=="recipeOutputOriginal"&&original.Attribute("stationBirth")!=null&&original.Attribute("personalQueueIndex")==null){rows.Remove(row);continue;}
                    if(!ExactPersonalOutputOriginal(original)||
                        (string)original.Attribute("owner")!=owner.StorageKey||(string)original.Attribute("world")!=player.world.Guid||
                        (string)original.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player))continue;
                    string hash=UiOriginalHash(bytes);row["original"]=hash;row["originalDeaths"]=(int)original.Attribute("deaths");row["slot"]=(int)original.Attribute("personalQueueIndex");
                    row["beforeCount"]=(int)original.Attribute("beforeCount");row["marker"]=player.Buffs.GetCustomVar(PersonalOutputReceipt(operation));
                    var outputs=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(original,"outputs"));row["outputs"]=outputs.Length;
                    int prefix=0,savedPrefix=0;bool ownerMatches=false;XElement lastAccepted=null;
                    while(prefix<outputs.Length&&ReadPersonalOutputSidecar(path+".output-"+prefix+".accepted",out var acceptedBytes,out var accepted)&&
                        PersonalColdIdentity(accepted,"recipeOutputAccepted","operation,index,attempt,original","bag,belt,equipment",operation,hash)&&(int?)accepted.Attribute("index")==prefix)
                    {
                        lastAccepted=accepted;
                        ownerMatches=PersonalColdImage(accepted,"bag").SequenceEqual(bag)&&PersonalColdImage(accepted,"belt").SequenceEqual(belt)&&PersonalColdImage(accepted,"equipment").SequenceEqual(equipment);
                        if(ReadPersonalOutputSidecar(path+".output-"+prefix+".owner-saved",out _,out var ownerSaved)&&
                            PersonalColdIdentity(ownerSaved,"recipeOutputOwnerPrefixSaved","operation,index,original,accepted,owner,creation,deaths,entity,playerFile,nativeDigest,nativeLength,personalPrefix","",operation,hash)&&
                            (int?)ownerSaved.Attribute("index")==prefix&&(int?)ownerSaved.Attribute("personalPrefix")==prefix+1&&
                            (string)ownerSaved.Attribute("accepted")==UiOriginalHash(acceptedBytes))savedPrefix=prefix+1;
                        prefix++;
                    }
                    row["acceptedPrefix"]=prefix;row["nativeSavedPrefix"]=savedPrefix;row["ownerMatchesAccepted"]=ownerMatches;row["active"]=false;
                    foreach(var card in cards)if(card!=null&&pendingUiOutputs.TryGetValue(card,out var pending)&&pending.Operation==operation)
                    {row["active"]=true;row["cardCount"]=card.GetRecipeCount();row["nativeCallbackEntered"]=pending.NativeContinuationEntered;row["nativeReturned"]=pending.Returned;row["queueObserved"]=pending.QueueObserved;row["queueSaved"]=pending.QueueSaved;}
                    bool callbackEntered=ReadPersonalOutputSidecar(path+".native-continuation-entered",out _,out var callbackEntry)&&
                        PersonalColdIdentity(callbackEntry,"recipeOutputNativeContinuationEntered","operation,original,outputPrefix","",operation,hash)&&(int?)callbackEntry.Attribute("outputPrefix")==outputs.Length;
                    bool callbackReturned=ReadPersonalOutputSidecar(path+".native-continuation-returned",out _,out var callbackReturn)&&
                        PersonalColdIdentity(callbackReturn,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",operation,hash)&&(int?)callbackReturn.Attribute("outputPrefix")==outputs.Length;
                    if(!PersonalColdAbsent(path+".native-continuation-entered")&&!callbackEntered||!PersonalColdAbsent(path+".native-continuation-returned")&&!callbackReturned)continue;
                    row["callbackEntered"]=callbackEntered;row["callbackReturned"]=callbackReturned;
                    if(callbackReturned&&prefix==outputs.Length&&lastAccepted!=null&&
                        RebirthRemoteResourcePlacedStationQueue.TryDecode((string)original.Element("recipe"),out var originalRows)&&originalRows.Length==1&&originalRows[0]?.Recipe!=null&&
                        TryReadScrapCompletedEquipment(path,operation,hash,originalRows[0].Recipe,prefix,PersonalColdImage(lastAccepted,"equipment"),out var completedEquipment))
                        row["ownerMatchesAccepted"]=PersonalColdImage(lastAccepted,"bag").SequenceEqual(bag)&&PersonalColdImage(lastAccepted,"belt").SequenceEqual(belt)&&completedEquipment.SequenceEqual(equipment);
                    bool terminal=false;
                    if(ReadPersonalOutputSidecar(path+".queue-owner-saved",out _,out var saved)&&
                        PersonalColdIdentity(saved,"recipeOutputPersonalQueueSaved","operation,original,owner,creation,deaths,beforeCount,afterCount,outputPrefix,playerFile,nativeDigest,nativeLength,queueDigest,personalPrefix","",operation,hash)&&
                        (string)saved.Attribute("owner")==owner.StorageKey&&(string)saved.Attribute("creation")==RebirthCamperCharacterAccess.CurrentCreation(player)&&
                        (int)saved.Attribute("outputPrefix")==outputs.Length&&(int)saved.Attribute("afterCount")==((int)original.Attribute("beforeCount"))-1)
                    {
                        terminal=true;row["savedQueueMatches"]=(string)saved.Attribute("queueDigest")==UiOriginalHash(Encoding.UTF8.GetBytes(queue));
                        string native=(string)saved.Attribute("playerFile");
                        if(Path.GetFullPath(native)!=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),owner.CanonicalId+"."+PlayerDataFile.EXT)))continue;
                        RebirthWorkstationCompletionPublication.RequireNativeSaveFile(native);
                        var info=new FileInfo(native);row["nativeFileMatches"]=info.Exists&&info.Length==(int)saved.Attribute("nativeLength")&&info.Length<=16*1024*1024&&UiOriginalHash(File.ReadAllBytes(native))==(string)saved.Attribute("nativeDigest");
                    }
                    row["terminalSaved"]=terminal;row["unknown"]=false;
                }
            }
            result["available"]=true;
        }
        catch(Exception error){result["error"]=error.GetType().Name;}
#endif
        return result;
    }
}
