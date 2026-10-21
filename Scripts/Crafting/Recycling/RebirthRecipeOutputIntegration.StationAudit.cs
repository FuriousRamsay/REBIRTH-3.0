using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private static JArray CaptureStationOutputColdAudit(EntityPlayerLocal actor)
    {
        var rows=new JArray();
#if REBIRTH_DEBUG
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return rows;
        lock(stationNativeLoadGate)foreach(var source in stationNativeLoadRequests.Where(value=>!value.Closed&&ReferenceEquals(value.World,actor.world)&&
            (ReferenceEquals(value.Owner?.Actor,actor)||value.Owner==null&&(string)value.Image?.Attribute("owner")==identity.StorageKey&&
                (string)value.Image?.Attribute("creation")==RebirthCamperCharacterAccess.CurrentCreation(actor))).Take(65))
        {
            bool current=false;try{current=source.Loaded?.Current()==true;}catch{}
            rows.Add(new JObject{["operation"]=source.Operation.ToString("N"),["original"]=source.OriginalHash,["birth"]=source.Birth,
                ["ownerNativeLoaded"]=source.Owner?.Loaded==true&&source.Owner.ReadReturned,["ownerHydrated"]=source.Owner?.Hydrated==true,
                ["discoveredBeforeOwner"]=source.Owner==null,
                ["nativeReadCaptured"]=source.Loaded!=null,["nativeCurrent"]=current,["adopted"]=source.Adopted,["callbackReturned"]=source.CallbackReturned});
        }
#endif
        return rows;
    }
    private static JArray CaptureStationOutputBeforeAudit(EntityPlayerLocal actor)
    {
        var rows=new JArray();
#if REBIRTH_DEBUG
        foreach(var source in stationOutputBefores.Where(value=>ReferenceEquals(value.Actor,actor)).Take(65))
        {
            bool current=false;try{current=StationOutputBeforeCurrent(source);}catch{}
            rows.Add(new JObject{["operation"]=source.Operation.ToString("N"),["birth"]=source.Birth,["beforeCount"]=source.Count,
                ["returnedUpdate"]=source.ReturnedUpdate,["syncEntered"]=source.SyncEntered,["syncReturned"]=source.SyncReturned,
                ["saved"]=source.Saved,["ownerBeforeSaved"]=source.OwnerSaved!=null,["current"]=current,["body"]=source.Body==null?null:UiOriginalHash(source.Body),["queue"]=source.Queue});
        }
#endif
        return rows;
    }
    private static JArray CaptureStationOutputRecordsAudit(EntityPlayerLocal actor,string owner)
    {
        var rows=new JArray();
#if REBIRTH_DEBUG
        if(!outputAuditIds.TryGetValue(actor,out var ids))return rows;
        foreach(var operation in ids.Order.ToArray())
        {
            try
            {
                string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");
                if(!ReadPersonalOutputSidecar(path,out var bytes,out var original)||original.Name!="recipeOutputOriginal"||original.Attribute("stationBirth")==null||
                    (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("owner")!=owner||
                    (string)original.Attribute("world")!=actor.world.Guid||(string)original.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor))continue;
                string hash=UiOriginalHash(bytes),birth=(string)original.Attribute("stationBirth");
                var row=new JObject{["operation"]=operation.ToString("N"),["original"]=hash,["birth"]=birth,["unknown"]=false,
                    ["beforeCount"]=(int)original.Attribute("beforeCount"),["originalDeaths"]=(int)original.Attribute("deaths"),
                    ["callbackFence"]=(int?)original.Attribute("callbackFence")==1,["active"]=false,["gridAccepted"]=false,["terminalSaved"]=false};rows.Add(row);
                var before=original.Element("stationSavedBefore");var nativeBefore=before?.Element("recipeOutputStationBeforeSaved");
                string beforePath=null;
                if(nativeBefore!=null&&Guid.TryParseExact((string)nativeBefore.Attribute("operation"),"N",out var beforeId))
                    beforePath=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","recipe-outputs","station-before",beforeId.ToString("N")+".xml");
                row["beforeNativeSaved"]=beforePath!=null&&Path.GetFullPath((string)before.Attribute("path"))==Path.GetFullPath(beforePath)&&
                    ReadPersonalOutputSidecar(beforePath+".saved",out _,out var beforeSaved)&&XNode.DeepEquals(nativeBefore,beforeSaved);
                var ownerBefore=nativeBefore?.Element("recipeOutputStationOwnerBeforeSaved");
                row["beforeOwnerSaved"]=false;
                if(ownerBefore!=null&&beforePath!=null&&
                    ReadPersonalOutputSidecar(beforePath+".owner-saved",out _,out var ownerBeforeSaved)&&XNode.DeepEquals(ownerBefore,ownerBeforeSaved)&&
                    (string)ownerBefore.Attribute("operation")==((string)nativeBefore.Attribute("operation"))&&
                    (string)ownerBefore.Attribute("owner")==owner&&(string)ownerBefore.Attribute("creation")==((string)original.Attribute("creation"))&&
                    (int?)ownerBefore.Attribute("deaths")==((int?)original.Attribute("deaths")))
                {
                    string archive=beforePath+".owner.native";RebirthWorkstationCompletionPublication.RequireNativeSaveFile(archive);
                    int length=(int)ownerBefore.Attribute("nativeLength");
                    row["beforeOwnerSaved"]=length>=6&&length<=16*1024*1024&&!File.Exists(archive+".tmp")&&!File.Exists(archive+".bak")&&
                        File.Exists(archive)&&new FileInfo(archive).Length==length&&UiOriginalHash(File.ReadAllBytes(archive))==((string)ownerBefore.Attribute("nativeDigest"))&&
                        ReadPersonalOutputSidecar(beforePath+".owner-frame",out var frameBytes,out _)&&UiOriginalHash(frameBytes)==((string)ownerBefore.Attribute("frameDigest"));
                }
                if(nativeBefore!=null)row["beforeOperation"]=(string)nativeBefore.Attribute("operation");
                if(ids.Cards.TryGetValue(operation,out var weak)&&weak.TryGetTarget(out var card))
                {
                    row["cardCount"]=card.GetRecipeCount();
                    if(pendingUiOutputs.TryGetValue(card,out var pending)&&pending.Operation==operation)
                    {row["active"]=true;row["queueObserved"]=pending.QueueObserved;row["queueSaved"]=pending.QueueSaved;row["nativeReturned"]=pending.Returned;}
                }
                var position=new Vector3i((int)original.Attribute("stationX"),(int)original.Attribute("stationY"),(int)original.Attribute("stationZ"));
                var tile=actor.world.GetTileEntity(position) as TileEntityWorkstation;
                bool sameTile=tile!=null&&RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,tile,out var actualBirth)&&actualBirth==birth;
                row["nativeIncarnationCurrent"]=sameTile;
                if(ReadPersonalOutputSidecar(path+".grid-accepted",out _,out var accepted)&&
                    PersonalColdIdentity(accepted,"recipeOutputGridAccepted","operation,original","after",operation,hash))
                {row["gridAccepted"]=true;row["gridMatchesNative"]=sameTile&&PersonalColdImage(accepted,"after").SequenceEqual(OwningGrid(tile.Output));}
                bool entered=ReadPersonalOutputSidecar(path+".native-continuation-entered",out _,out var entry)&&
                    PersonalColdIdentity(entry,"recipeOutputNativeContinuationEntered","operation,original,outputPrefix","",operation,hash);
                bool returned=ReadPersonalOutputSidecar(path+".native-continuation-returned",out _,out var returnedImage)&&
                    PersonalColdIdentity(returnedImage,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",operation,hash)&&
                    entered&&(int?)entry.Attribute("outputPrefix")==((int?)returnedImage.Attribute("outputPrefix"));
                row["callbackEntered"]=entered;row["callbackReturned"]=returned;
                row["ownerMatchesBefore"]=PersonalColdImage(original,"bagBefore").SequenceEqual(OwningGrid(actor.bag.ItemGrid.items))&&
                    PersonalColdImage(original,"beltBefore").SequenceEqual(OwningGrid(actor.inventory.ItemGrid.items))&&
                    PersonalColdImage(original,"equipmentBefore").SequenceEqual(OwningEquipmentNative(actor));
                if(ReadPersonalOutputSidecar(path+".station-queue-saved",out _,out var saved)&&
                    PersonalColdIdentity(saved,"recipeOutputStationQueueSaved","operation,original,birth,beforeCount,afterCount,nativePath,nativeDigest,tileOffset,tileLength,tileDigest,elapsedOffset","nativeBody,outputAfter,queueAfter"+((int?)original.Attribute("callbackFence")==1?",callbackOwnerSaved":""),operation,hash)&&
                    (string)saved.Attribute("birth")==birth&&(int)saved.Attribute("afterCount")==((int)original.Attribute("beforeCount"))-1)
                {
                    row["terminalSaved"]=true;row["savedQueueMatches"]=sameTile&&(string)saved.Element("queueAfter")==RebirthRemoteResourcePlacedStationQueue.Encode(tile.Queue);
                    row["savedNativeTupleMatches"]=sameTile&&RebirthProductiveEarnedNativeBody.MatchesCurrent(tile,PersonalColdImage(saved,"nativeBody"),(int)saved.Attribute("elapsedOffset"));
                }
            }
            catch{/* Bounded read-only diagnostic; no source admission or save. */}
        }
#endif
        return rows;
    }
}
