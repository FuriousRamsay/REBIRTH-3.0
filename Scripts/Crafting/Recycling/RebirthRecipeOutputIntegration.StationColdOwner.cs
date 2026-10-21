using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class StationOutputColdHold
    {internal UiOutputLoadedOwner Owner;internal Guid Operation;internal Vector3i Position;internal string Birth,Creation;}
    private static readonly System.Collections.Generic.List<StationOutputColdHold> stationOutputColdHolds=new System.Collections.Generic.List<StationOutputColdHold>();
    // Called only after the actual native owner Load/Read/final-file chain.
    // A root file alone never qualifies an actor, physical prefix or callback.
    private static int PrepareStationOutputNativeLoads(UiOutputLoadedOwner owner)
    {
        int prepared=0;string root=Path.Combine(owner.Save,"rebirth","recipe-outputs","ui");
        if(!Directory.Exists(root))return 0;
        string ownerHash=UiOriginalHash(owner.Native);
        foreach(string path in Directory.EnumerateFiles(root,"*.xml",SearchOption.TopDirectoryOnly))
        {
            IDisposable memory=null,extraMemory=null;
            try
            {
                if(!ReadPersonalOutputSidecar(path,out var bytes,out var image)||image.Name!="recipeOutputOriginal"||
                    (string)image.Attribute("sink")!="Output"||(int?)image.Attribute("callbackFence")!=1||(bool?)image.Attribute("cached")!=false||
                    (string)image.Attribute("world")!=owner.World.Guid||(string)image.Attribute("owner")!=owner.Owner||
                    (int?)image.Attribute("deaths")!=owner.Deaths||!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||
                    Path.GetFileName(path)!=operation.ToString("N")+".xml"||image.Element("stationSavedBefore")==null)continue;
                string hash=UiOriginalHash(bytes);
                if(ReadPersonalOutputSidecar(path+".station-queue-saved",out _,out var terminal)&&
                    PersonalColdIdentity(terminal,"recipeOutputStationQueueSaved","operation,original,birth,beforeCount,afterCount,nativePath,nativeDigest,tileOffset,tileLength,tileDigest,elapsedOffset", "nativeBody,outputAfter,queueAfter,callbackOwnerSaved",operation,hash)&&
                    (string)terminal.Attribute("birth")==((string)image.Attribute("stationBirth"))&&(int?)terminal.Attribute("afterCount")==((int?)image.Attribute("beforeCount"))-1)continue;
                if(!ReadPersonalOutputSidecar(path+".effect-entered",out _,out var entered)||
                    !PersonalColdIdentity(entered,"recipeOutputEffectEntered","operation,effect,original","",operation,hash))continue;
                var holdPosition=new Vector3i((int)image.Attribute("stationX"),(int)image.Attribute("stationY"),(int)image.Attribute("stationZ"));
                lock(stationNativeLoadGate)stationOutputColdHolds.Add(new StationOutputColdHold{Owner=owner,Operation=operation,Position=holdPosition,
                    Birth=(string)image.Attribute("stationBirth"),Creation=(string)image.Attribute("creation")});
                prepared++;
                if(!ReadPersonalOutputSidecar(path+".effect-observed",out _,out var effect)||
                    !PersonalColdIdentity(effect,"recipeOutputEffectObserved","operation,nativeRan,original","primaryAfterEffects,stationReturnedEffect",operation,hash)||
                    !(bool)effect.Attribute("nativeRan")||!ReadPersonalOutputSidecar(path+".grid-accepted",out _,out var accepted)||
                    !PersonalColdIdentity(accepted,"recipeOutputGridAccepted","operation,original","after",operation,hash))continue;
                var returnedEffect=effect.Element("stationReturnedEffect");
                if(!ExactPersonalOutputFields(returnedEffect,"stationReturnedEffect","birth,queueIndex,ownerDigest,ownerLength,elapsedOffset","nativeBody")||
                    (string)returnedEffect.Attribute("birth")!=((string)image.Attribute("stationBirth"))||
                    (int?)returnedEffect.Attribute("queueIndex")!=((int?)image.Attribute("stationQueueIndex")))continue;
                int prefix=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs")).Length;
                bool callback=ReadPersonalOutputSidecar(path+".native-continuation-returned",out var callbackBytes,out var callbackImage)&&
                    PersonalColdIdentity(callbackImage,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",operation,hash)&&
                    (int?)callbackImage.Attribute("outputPrefix")==prefix;
                if(callback&&(!ReadPersonalOutputSidecar(path+".native-continuation-entered",out _,out var callbackEntered)||
                    !PersonalColdIdentity(callbackEntered,"recipeOutputNativeContinuationEntered","operation,original,outputPrefix","",operation,hash)||
                    (int?)callbackEntered.Attribute("outputPrefix")!=prefix))continue;
                if(!callback&&!PersonalColdAbsent(path+".native-continuation-entered"))continue;
                if(callback)
                {
                    if(!ReadPersonalOutputSidecar(path+".output-"+(prefix-1)+".owner-saved",out _,out var saved)||
                        !PersonalColdIdentity(saved,"recipeOutputOwnerPrefixSaved","operation,index,original,accepted,owner,creation,deaths,entity,playerFile,nativeDigest,nativeLength","",operation,hash)||
                        (string)saved.Attribute("owner")!=owner.Owner||(string)saved.Attribute("creation")!=((string)image.Attribute("creation"))||
                        (int?)saved.Attribute("deaths")!=owner.Deaths||(int?)saved.Attribute("index")!=prefix-1||
                        (string)saved.Attribute("accepted")!=UiOriginalHash(callbackBytes)||(string)saved.Attribute("nativeDigest")!=ownerHash||
                        (int?)saved.Attribute("nativeLength")!=owner.Native.Length||Path.GetFullPath((string)saved.Attribute("playerFile"))!=owner.Path)continue;
                }
                else if((string)returnedEffect.Attribute("ownerDigest")!=ownerHash||(int?)returnedEffect.Attribute("ownerLength")!=owner.Native.Length)continue;
                byte[] body=PersonalColdImage(returnedEffect,"nativeBody");int elapsed=(int)returnedEffect.Attribute("elapsedOffset");
                if(!RebirthProductiveEarnedNativeBody.TryMeasure(body,elapsed,out long beforeGraph,out long beforeScratch))continue;
                long retained=checked(12L*(body.Length+bytes.Length)+beforeGraph+beforeScratch+65536);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(retained,out memory)||
                    !RebirthProductiveEarnedNativeBody.TryRead(body,elapsed,out var native,out ulong clock))continue;
                int index=(int)image.Attribute("stationQueueIndex");
                if(index<0||index>=native.Queue.Length||RebirthRemoteResourcePlacedStationQueue.Encode(new[]{native.Queue[index]})!=image.Element("recipe").Value||
                    !OwningGrid(native.Output).SequenceEqual(PersonalColdImage(image,"outputBefore")))continue;
                var outputs=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs"));
                if(outputs.Length<2||outputs.Length>65||!RebirthNativeItemCodec.TryDecode(effect.Element("primaryAfterEffects").Value,out var primary)||
                    !PredictOwningOutput(RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputBefore")),
                        new ItemStack(primary,native.Queue[index].Recipe.count),outputs.Skip(1).ToArray(),false,out var predicted)||
                    !OwningGrid(predicted).SequenceEqual(PersonalColdImage(accepted,"after")))continue;
                native.Output=predicted;body=RebirthProductiveEarnedNativeBody.Encode(native,clock,out elapsed);
                if(!RebirthProductiveEarnedNativeBody.TryMeasure(body,elapsed,out long graph,out long scratch))continue;
                long afterRetained=checked(12L*(body.Length+bytes.Length)+graph+scratch+65536);
                if(afterRetained>retained&&!RebirthProductiveEarnedObservationBudget.TryRetainDetached(afterRetained-retained,out extraMemory))continue;
                var position=new Vector3i((int)image.Attribute("stationX"),(int)image.Attribute("stationY"),(int)image.Attribute("stationZ"));
                var request=new StationOutputNativeRequest{Operation=operation,World=owner.World,Save=owner.Save,Birth=(string)image.Attribute("stationBirth"),
                    OriginalHash=hash,Original=bytes,Image=image,Grid=OwningGrid(predicted),Owner=owner,CallbackReturned=callback,
                    X=position.x>>4,Z=position.z>>4,Position=position,Body=body,Elapsed=elapsed,ReturnedSpan=new byte[body.Length+10],Graph=graph,Scratch=scratch,Memory=memory,ExtraMemory=extraMemory};
                lock(stationNativeLoadGate)
                {
                    var earlier=stationNativeLoadRequests.Where(value=>!value.Closed&&value.Owner==null&&value.Operation==operation&&
                        ReferenceEquals(value.World,owner.World)&&value.Save==owner.Save&&value.Birth==request.Birth&&value.Position==position&&
                        value.OriginalHash==hash&&value.Original.SequenceEqual(bytes)&&value.Body.SequenceEqual(body)&&value.Elapsed==elapsed&&
                        value.Grid.SequenceEqual(request.Grid)&&value.CallbackReturned==callback).ToArray();
                    if(earlier.Length==1)
                    {
                        // Join two independent actual native capabilities. The
                        // earlier region load cannot invent an owner; this
                        // branch has already validated its actual Load/Read.
                        earlier[0].Owner=owner;
                    }
                    else{stationNativeLoadRequests.Add(request);memory=null;extraMemory=null;}
                }
            }
            catch{/* An unknown historical stage supplies no reconstruction capability. */}
            finally{memory?.Dispose();extraMemory?.Dispose();}
        }
        return prepared;
    }
    private static void DisposeStationOutputNativeLoads(UiOutputLoadedOwner owner)
    {
        lock(stationNativeLoadGate)stationOutputColdHolds.RemoveAll(value=>ReferenceEquals(value.Owner,owner)&&
            !stationNativeLoadRequests.Any(request=>request.Adopted&&request.Operation==value.Operation&&ReferenceEquals(request.Owner,owner)));
        lock(stationNativeLoadGate)foreach(var request in stationNativeLoadRequests.Where(value=>ReferenceEquals(value.Owner,owner)&&!value.Adopted).ToArray())
        {request.Dispose();if(!request.Loading)stationNativeLoadRequests.Remove(request);}
    }
    internal static bool CanAdvanceStationOutputColdNative(TileEntityWorkstation tile)
    {
        if(tile==null)return true;
        lock(stationNativeLoadGate)if(stationNativeLoadRequests.Any(value=>!value.Closed&&value.Owner==null&&
            ReferenceEquals(value.World,GameManager.Instance?.World)&&value.Position==tile.ToWorldPos()&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(value.World,tile,out var born)&&born==value.Birth))return false;
        lock(stationNativeLoadGate)return !stationOutputColdHolds.Any(value=>(!value.Owner.Closed||stationNativeLoadRequests.Any(request=>!request.Closed&&request.Adopted&&request.Operation==value.Operation&&ReferenceEquals(request.Owner,value.Owner)))&&ReferenceEquals(value.Owner.World,GameManager.Instance?.World)&&
            (!value.Owner.Hydrated||value.Owner.Creation==value.Creation)&&value.Position==tile.ToWorldPos()&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(value.Owner.World,tile,out var birth)&&birth==value.Birth);
    }
    private static bool CanAdvanceStationOutputColdCard(XUiC_RecipeStack card)
    {
        var tile=card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity as TileEntityWorkstation;
        if(tile==null)return true;
        if(!CanAdvanceStationOutputColdNative(tile)&&!stationOutputColdHolds.Any(value=>ReferenceEquals(value.Owner.Actor,card.xui?.playerUI?.entityPlayer)&&value.Position==tile.ToWorldPos()))return false;
        lock(stationNativeLoadGate)foreach(var hold in stationOutputColdHolds)
        {
            var active=stationNativeLoadRequests.FirstOrDefault(value=>!value.Closed&&value.Adopted&&value.Operation==hold.Operation&&ReferenceEquals(value.Owner,hold.Owner));
            var actualOwner=hold.Owner.Actor??active?.ActiveOriginal?.Actor;
            if(hold.Owner.Closed&&active==null||!ReferenceEquals(actualOwner,card.xui?.playerUI?.entityPlayer)||hold.Position!=tile.ToWorldPos()||
                RebirthCamperCharacterAccess.CurrentCreation(actualOwner)!=hold.Creation||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(hold.Owner.World,tile,out var birth)||birth!=hold.Birth)continue;
            if(!pendingUiOutputs.TryGetValue(card,out var pending)||pending.Operation!=hold.Operation||pending.Source==null)return false;
        }
        return true;
    }
}
