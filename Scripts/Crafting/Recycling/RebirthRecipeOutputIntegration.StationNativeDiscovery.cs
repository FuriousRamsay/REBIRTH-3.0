using System;
using System.IO;
using System.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    // Actual region-manager construction precedes initial chunks. Discovery
    // supplies only a read-only native-source purpose; Owner stays null until
    // the independent actual native owner Load/Read qualifies that same source.
    internal static void DiscoverStationOutputNativeSources(RegionFileManager manager)
    {
        if(!installed||!ThreadManager.IsMainThread()||GameManager.Instance?.World is not World world||world.IsRemote()||manager?.snapshotUtil==null||
            manager.regionFileAccess is not RegionFileAccessSectorBased||Path.GetFullPath(manager.saveDirectory)!=Path.GetFullPath(GameIO.GetSaveGameRegionDir()))return;
        RebirthCookingMillingPublication.DiscoverStaticNativeSources(manager);
        RebirthWorkstationCompletionPipeline.DiscoverRemoteOutputHostSources(manager);
        RebirthProductiveEarnedUiDeferredTransport.DiscoverAuthorityLocalLoadedSources(manager);
        string save=Path.GetFullPath(GameIO.GetSaveGameDir()),root=Path.Combine(save,"rebirth","recipe-outputs","ui");
        lock(stationNativeLoadGate)foreach(var old in stationNativeLoadRequests.Where(value=>!ReferenceEquals(value.World,world)).ToArray())
        {old.Dispose();if(!old.Loading)stationNativeLoadRequests.Remove(old);}
        if(!Directory.Exists(root))return;
        foreach(string path in Directory.EnumerateFiles(root,"*.xml",SearchOption.TopDirectoryOnly))
        {
            IDisposable memory=null,extra=null;
            try
            {
                if(!ReadPersonalOutputSidecar(path,out var bytes,out var image)||image.Name!="recipeOutputOriginal"||
                    (string)image.Attribute("sink")!="Output"||(int?)image.Attribute("callbackFence")!=1||(bool?)image.Attribute("cached")!=false||
                    (string)image.Attribute("world")!=world.Guid||image.Element("stationSavedBefore")==null||
                    !Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||Path.GetFileName(path)!=operation.ToString("N")+".xml")continue;
                lock(stationNativeLoadGate)if(stationNativeLoadRequests.Any(value=>!value.Closed&&ReferenceEquals(value.World,world)&&value.Operation==operation))continue;
                string hash=UiOriginalHash(bytes);
                if(ReadPersonalOutputSidecar(path+".station-queue-saved",out _,out var terminal)&&
                    PersonalColdIdentity(terminal,"recipeOutputStationQueueSaved","operation,original,birth,beforeCount,afterCount,nativePath,nativeDigest,tileOffset,tileLength,tileDigest,elapsedOffset", "nativeBody,outputAfter,queueAfter,callbackOwnerSaved",operation,hash)&&
                    (string)terminal.Attribute("birth")==((string)image.Attribute("stationBirth"))&&
                    (int?)terminal.Attribute("beforeCount")==((int?)image.Attribute("beforeCount"))&&
                    (int?)terminal.Attribute("afterCount")==((int?)image.Attribute("beforeCount"))-1)continue;
                if(!ReadPersonalOutputSidecar(path+".effect-entered",out _,out var entered)||
                    !PersonalColdIdentity(entered,"recipeOutputEffectEntered","operation,effect,original","",operation,hash)||
                    !ReadPersonalOutputSidecar(path+".effect-observed",out _,out var effect)||
                    !PersonalColdIdentity(effect,"recipeOutputEffectObserved","operation,nativeRan,original","primaryAfterEffects,stationReturnedEffect",operation,hash)||
                    (bool?)effect.Attribute("nativeRan")!=true||!ReadPersonalOutputSidecar(path+".grid-accepted",out _,out var accepted)||
                    !PersonalColdIdentity(accepted,"recipeOutputGridAccepted","operation,original","after",operation,hash))continue;
                var returned=effect.Element("stationReturnedEffect");
                if(!ExactPersonalOutputFields(returned,"stationReturnedEffect","birth,queueIndex,ownerDigest,ownerLength,elapsedOffset","nativeBody")||
                    (string)returned.Attribute("birth")!=(string)image.Attribute("stationBirth")||
                    (int?)returned.Attribute("queueIndex")!=(int?)image.Attribute("stationQueueIndex"))continue;
                int prefix=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs")).Length;
                bool callback=ReadPersonalOutputSidecar(path+".native-continuation-returned",out _,out var callbackImage)&&
                    PersonalColdIdentity(callbackImage,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",operation,hash)&&
                    (int?)callbackImage.Attribute("outputPrefix")==prefix;
                if(callback&&(!ReadPersonalOutputSidecar(path+".native-continuation-entered",out _,out var callbackEntered)||
                    !PersonalColdIdentity(callbackEntered,"recipeOutputNativeContinuationEntered","operation,original,outputPrefix","",operation,hash)||
                    (int?)callbackEntered.Attribute("outputPrefix")!=prefix))continue;
                if(!callback&&!PersonalColdAbsent(path+".native-continuation-entered"))continue;
                byte[] body=PersonalColdImage(returned,"nativeBody");int elapsed=(int)returned.Attribute("elapsedOffset");
                if(!RebirthProductiveEarnedNativeBody.TryMeasure(body,elapsed,out long graph,out long scratch)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(body.Length+bytes.Length)+graph+scratch+65536),out memory)||
                    !RebirthProductiveEarnedNativeBody.TryRead(body,elapsed,out var native,out ulong clock))continue;
                int index=(int)image.Attribute("stationQueueIndex");
                if(index<0||index>=native.Queue.Length||native.Queue[index]?.Recipe==null||
                    RebirthRemoteResourcePlacedStationQueue.Encode(new[]{native.Queue[index]})!=image.Element("recipe").Value||
                    !OwningGrid(native.Output).SequenceEqual(PersonalColdImage(image,"outputBefore")))continue;
                var outputs=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs"));
                if(outputs.Length<2||outputs.Length>65||!RebirthNativeItemCodec.TryDecode(effect.Element("primaryAfterEffects").Value,out var primary)||
                    !PredictOwningOutput(RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputBefore")),new ItemStack(primary,native.Queue[index].Recipe.count),
                        outputs.Skip(1).ToArray(),false,out var predicted)||!OwningGrid(predicted).SequenceEqual(PersonalColdImage(accepted,"after")))continue;
                native.Output=predicted;byte[] expected=RebirthProductiveEarnedNativeBody.Encode(native,clock,out int afterElapsed);
                if(!RebirthProductiveEarnedNativeBody.TryMeasure(expected,afterElapsed,out long afterGraph,out long afterScratch)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*expected.Length+afterGraph+afterScratch+65536),out extra))continue;
                var position=new Vector3i((int)image.Attribute("stationX"),(int)image.Attribute("stationY"),(int)image.Attribute("stationZ"));
                var request=new StationOutputNativeRequest{Operation=operation,World=world,Save=save,Birth=(string)image.Attribute("stationBirth"),
                    OriginalHash=hash,Original=bytes,Image=image,Grid=OwningGrid(predicted),CallbackReturned=callback,
                    X=position.x>>4,Z=position.z>>4,Position=position,Body=expected,Elapsed=afterElapsed,ReturnedSpan=new byte[expected.Length+10],
                    Graph=afterGraph,Scratch=afterScratch,Memory=memory,ExtraMemory=extra};
                lock(stationNativeLoadGate)stationNativeLoadRequests.Add(request);memory=extra=null;
            }
            catch{/* Discovery alone supplies no owner action or source setter. */}
            finally{memory?.Dispose();extra?.Dispose();}
        }
    }
}
