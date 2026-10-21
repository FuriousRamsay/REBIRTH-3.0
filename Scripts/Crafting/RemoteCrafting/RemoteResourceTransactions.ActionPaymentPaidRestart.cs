using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private static RebirthElectricalServiceRecord ReadActionElectricalCandidate(XElement configuration,Vector3i position)
        =>new RebirthElectricalServiceRecord{ClrIdx=(int)configuration.Attribute("clr"),Position=position,Kind=(RebirthElectricalServiceKind)checked((byte)(int)configuration.Attribute("kind")),
            Condition=(float)configuration.Attribute("condition"),Workmanship=(float)configuration.Attribute("workmanship"),LastServiceWorldTime=(ulong)configuration.Attribute("time"),ServiceCount=(int)configuration.Attribute("count"),
            SourceBlock=checked((ushort)(int)configuration.Attribute("block")),SourceIncarnation=(string)configuration.Attribute("incarnation"),ConfiguredByStableId=(string)configuration.Attribute("author"),
            ConfiguredByBackgroundId=(string)configuration.Attribute("background"),ConfiguredByBonusId=(string)configuration.Attribute("bonus"),ConfiguredSkillValue=(float)configuration.Attribute("skill")};
    // This scope cannot qualify Paying/Returning or an owner mutation. It binds
    // a fully saved paid obligation to its actual reconstructed native target,
    // allowing only its original publication after the owner has a later life.
    internal sealed class ActionSavedPaidPublication
    {
        private readonly ActionPayment payment;private readonly ActionHostOwnerLoad owner;private readonly ActionTargetNativeOriginal target;private readonly int deaths;
        internal ActionSavedPaidPublication(ActionPayment payment,ActionHostOwnerLoad owner,ActionTargetNativeOriginal target)
        {this.payment=payment;this.owner=owner;this.target=target;deaths=owner.Actor.Died;}
        internal bool Current=>ThreadManager.IsMainThread()&&actionPayments.Contains(payment)&&ReferenceEquals(target.Payment,payment)&&ReferenceEquals(payment.Actor,owner.Actor)&&
            ActionHostLoadedCurrent(owner)&&owner.Actor.Died==deaths&&deaths>=payment.Deaths&&target.Current()&&payment.State==ActionPaymentState.Paid&&
            payment.OwnerAfterSaved&&payment.Legs.All(value=>value.State==ActionLegState.Debited&&value.AfterSaved)&&!payment.RemoteRefundEntered&&payment.Events.ContainsKey("paid");
    }
    private static bool ActionHostLoadedCurrent(ActionHostOwnerLoad source)=>ThreadManager.IsMainThread()&&source?.Joined==true&&source.Loaded&&source.Hydrated&&source.Spawn?.Returned==true&&
        actionHostOwnerLoads.Contains(source)&&ReferenceEquals(GameManager.Instance?.World,source.Spawn.World)&&
        ReferenceEquals(source.Spawn.World.GetEntity(source.Actor.entityId),source.Actor)&&!source.Actor.IsDead()&&source.Actor.Died==source.Deaths&&
        ReferenceEquals(source.Actor.bag.ItemGrid,source.BagGrid)&&ReferenceEquals(source.Actor.inventory.ItemGrid,source.BeltGrid)&&
        (source.Spawn.Local?source.Actor is EntityPlayerLocal&&ReferenceEquals(source.Spawn.World.GetPrimaryPlayer(),source.Actor)&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var localAccount)&&localAccount.CanonicalId==source.Spawn.Account:
            ReferenceEquals(source.Spawn.Peer?.netConnection?[0],source.Spawn.Connection)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(source.Actor.entityId),source.Spawn.Peer)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(source.Spawn.Peer,out var account)&&account.CanonicalId==source.Spawn.Account)&&source.Spawn.Save==Path.GetFullPath(GameIO.GetSaveGameDir());
    private static bool ActionColdOwnerNativeMatches(byte[] native,int id,int deaths,Guid operation,float phase,ItemStack[] bag,ItemStack[] belt)
    {
        if(native==null||native.Length<6||native.Length>16*1024*1024)return false;
        var data=new PlayerDataFile();
        using(var capture=new RebirthCamperEnergySavedOwner.Capture())using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);
            if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0'||reader.ReadByte()!=PlayerDataFile.cFileVersion)return false;
            data.Read(reader,PlayerDataFile.cFileVersion,global::StreamModeRead.Persistency);
            return stream.Position==stream.Length&&!capture.Failed&&data.id==id&&data.deaths==deaths&&!data.bDead&&data.dragAndDropItem!=null&&
                capture.Items.TryGetValue(data.dragAndDropItem,out var cursor)&&cursor.IsEmpty()&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(operation),out float observed,out bool present)&&present&&observed==phase&&
                SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,true),bag)&&SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,false),belt);
        }
    }
    private static Dictionary<string,int> ReadActionPaidHistory(ActionTargetNativeOriginal target,bool localOwner=false,bool ownerContribution=true)
    {
        var head=ReadActionBoundedXml(target.Head,24*1024*1024);int last=(int)head.Attribute("sequence");
        string headStage=(string)head.Attribute("stage");
        if(last<0||last>=256||(target.BeforeFirstGrant?headStage!="target-before-saved":headStage!="paid"&&headStage!="publication-returned"&&headStage!="service-after-saved"&&headStage!="target-after-saved"&&headStage!="training-saved"&&headStage!="training-not-required")||head.Element("original")?.Value!=target.Original)throw new InvalidDataException();
        var result=new Dictionary<string,int>(StringComparer.Ordinal);int files=0;
        foreach(string path in Directory.EnumerateFiles(target.Directory,"*.xml"))
        {
            string file=Path.GetFileName(path);if(file.Length<12||file[6]!='-'||!int.TryParse(file.Substring(0,6),out int sequence))continue;
            if(++files>256||sequence<0||sequence>last||file.Substring(0,6)!=sequence.ToString("D6"))throw new InvalidDataException("Later/unknown causal event cannot be replayed");
            string stage=file.Substring(7,file.Length-11);if(stage.Length<1||stage.Any(c=>c!='-'&&(c<'a'||c>'z')&&(c<'0'||c>'9'))||result.ContainsKey(stage)||result.ContainsValue(sequence))throw new InvalidDataException();
            ActionPath(path);long size=new FileInfo(path).Length;if(size<1||size>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(6*size+65536),out var memory))throw new InvalidDataException();
            using(memory)
            {
                byte[] bytes=ActionBytes(path,(int)size);var image=ReadActionBoundedXml(bytes,24*1024*1024);
                if(image.Name!="actionPaymentEvent"||(int?)image.Attribute("version")!=1||(int?)image.Attribute("sequence")!=sequence||(string)image.Attribute("stage")!=stage||image.Element("original")?.Value!=target.Original||
                    sequence==last&&!bytes.SequenceEqual(target.Head))throw new InvalidDataException();
            }
            result.Add(stage,sequence);
        }
        string[] required=target.BeforeFirstGrant?(ownerContribution?new[]{"prepared","owner-before-saved","target-before-saved"}:new[]{"prepared","target-before-saved"}):
            !ownerContribution?new[]{"prepared","target-before-saved","paying","paid"}:
            localOwner?new[]{"prepared","owner-before-saved","target-before-saved","paying","owner-after-saved","paid"}:
            new[]{"prepared","owner-before-saved","target-before-saved","paying","owner-debit-intent","owner-debited","owner-after-saved","paid"};
        if(result.Count!=last+1||Enumerable.Range(0,last+1).Any(index=>!result.ContainsValue(index))||required.Any(stage=>!result.ContainsKey(stage))||
            required.Where((stage,index)=>index>0&&result[stage]<=result[required[index-1]]).Any()||
            target.BeforeFirstGrant&&(result["prepared"]!=0||
                result.Keys.Any(stage=>stage!="prepared"&&stage!="owner-before-saved"&&stage!="target-before-saved"&&!(stage.StartsWith("source-",StringComparison.Ordinal)&&stage.EndsWith("-before-saved",StringComparison.Ordinal))))||
            result.ContainsKey("owner-return-intent"))throw new InvalidDataException();
        if(result.ContainsKey("publication-intent")&&(!result.TryGetValue("publication-returned",out int publicationReturned)||publicationReturned<=result["publication-intent"]||result["publication-intent"]<=result["paid"]))throw new InvalidDataException();
        return result;
    }
    private static int ReadActionHistoricalOwnerReceiptId(ActionTargetNativeOriginal target,Dictionary<string,int> events,string stage,long nativeLength)
    {
        if(!events.TryGetValue(stage,out int sequence))throw new InvalidDataException();
        string path=Path.Combine(target.Directory,sequence.ToString("D6")+"-"+stage+".xml");ActionPath(path);long length=new FileInfo(path).Length;
        if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(6*length+65536),out var memory))throw new InvalidDataException();
        using(memory)
        {
            var image=ReadActionBoundedXml(ActionBytes(path,(int)length),24*1024*1024);var receipt=image.Element("native");
            if(image.Name!="actionPaymentEvent"||(int?)image.Attribute("version")!=1||(int?)image.Attribute("sequence")!=sequence||(string)image.Attribute("stage")!=stage||image.Element("original")?.Value!=target.Original||
                receipt==null||(long?)receipt.Attribute("length")!=nativeLength||receipt.Attribute("nativeId")==null)throw new InvalidDataException();
            return (int)receipt.Attribute("nativeId");
        }
    }
    private static void PumpActionPaidRestart()
    {
        if(!ThreadManager.IsMainThread())return;
        ActionTargetNativeOriginal[] targets;lock(actionTargetLoadGate)targets=actionTargetNativeOriginals.Where(value=>value.Active&&value.Installed&&value.Payment==null).ToArray();
        foreach(var target in targets)
        {
            if(!target.Current()||actionPayments.Any(value=>value.Operation==target.Operation)||actionRemoteHostOriginals.ContainsKey(target.Operation))continue;
                foreach(var owner in SnapshotActionHostOwnerLoads().Where(ActionHostLoadedCurrent))
            {
                bool contribution=ReadRemoteActionOriginal(target.Original).Element("ownerOriginal")!=null;
                if(contribution&&(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(owner.Buffs,ActionOwnerMarker(target.Operation),out float phase,out bool present)||!present||phase!=(target.BeforeFirstGrant?1f:2f)))continue;
                TryRestorePaidRemoteAction(target,owner);
                if(target.Payment!=null)break;
            }
        }
    }
    private static void TryRestorePaidRemoteAction(ActionTargetNativeOriginal target,ActionHostOwnerLoad owner)
    {
        IDisposable scratch=null,sourceMemory=null,paymentMemory=null;ActionRemoteHostOriginal remote=null;ActionPayment payment=null;
        try
        {
            if(!target.Current()||!ActionHostLoadedCurrent(owner)||!ReferenceEquals(target.World,owner.Spawn.World)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out scratch))return;
            var image=ReadRemoteActionOriginal(target.Original);var configuration=image.Element("electrical");
            int originalDeaths=(int)image.Attribute("deaths"),originalNativeId=(int)image.Attribute("nativeId");bool laterLife=owner.Actor.Died>originalDeaths;
            if(target.BeforeFirstGrant&&laterLife)return;
            if((string)image.Attribute("account")!=owner.Spawn.Account||owner.Actor.Died<originalDeaths||
                (string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(owner.Actor))return;
            var rows=image.Element("sources")?.Elements("source").ToArray();if(rows==null||rows.Length<1||rows.Length>32)return;
            var before=image.Element("ownerOriginal");bool ownerContribution=before!=null;
            if(!ownerContribution&&rows.Any(row=>(int?)row.Attribute("kind")==0))return;
            var events=ReadActionPaidHistory(target,owner.Spawn.Local,ownerContribution);
            // Measure every native frame before any native item hydration.
            var frames=rows.SelectMany(row=>new[]{RemoteActionFrameBytes(row.Element("before")),RemoteActionFrameBytes(row.Element("after")),RemoteActionFrameBytes(row.Element("paid"))}).ToArray();
            byte[] bagFrame=ownerContribution?RemoteActionFrameBytes(before.Element("bag")):RebirthCamperInventorySnapshot.Capture(Array.Empty<ItemStack>()),beltFrame=ownerContribution?RemoteActionFrameBytes(before.Element("belt")):RebirthCamperInventorySnapshot.Capture(Array.Empty<ItemStack>());
            long graph=checked(frames.Sum(frame=>MeasureActionRetainedFrame(frame))+MeasureActionRetainedFrame(bagFrame)+MeasureActionRetainedFrame(beltFrame));
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(graph+16L*(frames.Sum(frame=>(long)frame.Length)+bagFrame.Length+beltFrame.Length)+16L*1024*1024)))return;
            var bagBefore=RebirthCamperInventorySnapshot.Restore(bagFrame);var beltBefore=RebirthCamperInventorySnapshot.Restore(beltFrame);
            var legs=new List<ActionLeg>();var seen=new HashSet<bool>();var sourceIds=new HashSet<string>(StringComparer.Ordinal);
            var available=target.BeforeFirstGrant&&rows.Any(row=>(int?)row.Attribute("kind")!=0)?RemoteResourceSourceDiscovery.ResolveNearby(target.World,owner.Actor,RemoteResourcesRuntimePolicy.Radius):null;
            for(int index=0;index<rows.Length;index++)
            {
                var row=rows[index];bool belt=(bool)row.Attribute("belt"),local=(int?)row.Attribute("kind")==0;
                if(!sourceIds.Add((string)row.Attribute("id"))||local&&(!seen.Add(belt)||(string)row.Attribute("id")!=(belt?"owner-belt":"owner-bag")))return;
                var legBefore=RebirthCamperInventorySnapshot.Restore(frames[index*3]);var after=RebirthCamperInventorySnapshot.Restore(frames[index*3+1]);var paid=RebirthCamperInventorySnapshot.Restore(frames[index*3+2]);
                if(local&&!SameActionItems(legBefore,belt?beltBefore:bagBefore))return;
                var takes=row.Element("takes")?.Elements("take").ToArray();string locks=row.Element("locks")?.Value;
                if(takes==null||takes.Length<1||takes.Length!=paid.Length||locks==null||locks.Length!=legBefore.Length||locks.Any(value=>value!='0'&&value!='1'))return;
                var slots=takes.Select(value=>(int)value.Attribute("slot")).ToArray();var expected=ItemStack.Clone(legBefore);var unique=new HashSet<int>();
                for(int n=0;n<slots.Length;n++)
                {
                    int slot=slots[n],count=(int)takes[n].Attribute("count");
                if(slot<0||slot>=expected.Length||locks[slot]=='1'||!unique.Add(slot)||count<1||count>expected[slot].count||paid[n].count!=count||
                        !RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(expected[slot].itemValue.Clone(),count),paid[n]))return;
                    expected[slot].count-=count;if(expected[slot].count==0)expected[slot]=ItemStack.Empty.Clone();
                }
                if(!SameActionItems(expected,after))return;
                if(!local)
                {
                    var restored=target.BeforeFirstGrant?TryRestoreUnenteredActionSource(target,owner,row,legBefore,after,locks.Select(value=>value=='1').ToArray(),slots,paid,available):
                        RestoreHistoricalPaidActionSource(target,row,index,legBefore,after,locks.Select(value=>value=='1').ToArray(),slots,paid,events);
                    if(restored==null)return;legs.Add(restored);continue;
                }
                var leg=new ActionLeg((string)row.Attribute("id"),null,legBefore,after,locks.Select(value=>value=='1').ToArray(),slots,paid,owner.Actor,owner.Spawn.Local?(belt?owner.BeltGrid:owner.BagGrid):null,null,true,belt,(int)row.Attribute("revision"),
                    Guid.ParseExact((string)row.Attribute("debit"),"N"),Guid.ParseExact((string)row.Attribute("return"),"N")){RemoteOwner=!owner.Spawn.Local,State=target.BeforeFirstGrant?ActionLegState.Before:ActionLegState.Debited,
                        BeforeSaved=true,AfterSaved=!target.BeforeFirstGrant,Progress=target.BeforeFirstGrant?legBefore:after,NativeLifeEnded=laterLife};
                if(!target.BeforeFirstGrant)for(int n=0;n<slots.Length;n++){leg.AttemptedSlots[n]=true;leg.DebitedSlots[n]=true;}legs.Add(leg);
            }
            var bagAfter=legs.FirstOrDefault(value=>value.Local&&!value.Belt)?.After??bagBefore;var beltAfter=legs.FirstOrDefault(value=>value.Local&&value.Belt)?.After??beltBefore;
            for(int index=0;index<legs.Count;index++)if(!legs[index].Local&&(!events.TryGetValue("source-"+index+"-before-saved",out int savedBefore)||savedBefore>=events[ownerContribution?"owner-before-saved":"target-before-saved"]))return;
            if(owner.Spawn.Local&&!target.BeforeFirstGrant)
            {
                for(int index=0;index<legs.Count;index++)if(legs[index].Local)foreach(int slot in legs[index].Slots)
                {
                    string prefix="source-"+index+"-slot-"+slot;
                    if(!events.TryGetValue(prefix+"-debit-intent",out int intent)||!events.TryGetValue(prefix+"-debited",out int returned)||
                        intent<=events["paying"]||returned<=intent||returned>=events["owner-after-saved"]||events.ContainsKey(prefix+"-return-intent"))return;
                }
            }
            string sourceDirectory=Path.Combine(target.Save,"RebirthData","ActionOwnerOriginals",target.Operation.ToString("N"));ActionPath(sourceDirectory);
            string beforePath=Path.Combine(target.Directory,"owner-before.native"),afterPath=Path.Combine(target.Directory,"owner-after.native"),confirmedPath=Path.Combine(sourceDirectory,"confirmed.native");
            long beforeSize=ownerContribution?new FileInfo(beforePath).Length:0,afterSize=ownerContribution&&!target.BeforeFirstGrant?new FileInfo(afterPath).Length:0,confirmedSize=ownerContribution&&!owner.Spawn.Local?new FileInfo(confirmedPath).Length:0;
            if(ownerContribution&&(beforeSize<6||beforeSize>16L*1024*1024||!target.BeforeFirstGrant&&(afterSize<6||afterSize>16L*1024*1024)||!owner.Spawn.Local&&(confirmedSize<14||confirmedSize>24L*1024*1024+12)))return;
            long preparedGraph=checked(graph*4+2L*target.Original.Length+frames.Sum(frame=>(long)frame.Length)+bagFrame.Length+beltFrame.Length+65536);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(preparedGraph+6*(beforeSize+afterSize+confirmedSize))))return;
            byte[] nativeBefore=ownerContribution?ActionBytes(beforePath,(int)beforeSize):Array.Empty<byte>(),nativeAfter=ownerContribution&&!target.BeforeFirstGrant?ActionBytes(afterPath,(int)afterSize):null,confirmed=ownerContribution&&!owner.Spawn.Local?ActionBytes(confirmedPath,(int)confirmedSize):Array.Empty<byte>();
            string clickText=null;ItemStack[] price=null;
            if(ownerContribution&&!owner.Spawn.Local)
            {
            ReadActionRemoteEnvelope(confirmed,out clickText,out byte[] confirmedBefore);
            if(!nativeBefore.SequenceEqual(confirmedBefore))return;confirmedBefore=null;
            var click=ReadRemoteActionOriginal(clickText);price=RemoteActionFrame(click.Element("price"));
            if((string)click.Attribute("operation")!=target.Operation.ToString("N")||(string)click.Attribute("account")!=owner.Spawn.Account||
                (int?)click.Attribute("nativeId")!=originalNativeId||(string)click.Attribute("creation")!=(string)image.Attribute("creation")||(int?)click.Attribute("deaths")!=originalDeaths||
                !SameActionItems(RemoteActionFrame(click.Element("bag")),bagBefore)||!SameActionItems(RemoteActionFrame(click.Element("belt")),beltBefore))return;
            foreach(var requirement in price)
            {
                var paid=legs.SelectMany(value=>value.Paid).Where(value=>MatchesConsumable(value,requirement.itemValue)).ToArray();
                if(requirement.IsEmpty()||paid.Sum(value=>(long)value.count)!=requirement.count)return;
            }
            if(legs.SelectMany(value=>value.Paid).Any(value=>!price.Any(required=>MatchesConsumable(value,required.itemValue))))return;
            }
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(preparedGraph+nativeBefore.LongLength+(nativeAfter?.LongLength??0)+confirmed.LongLength+2L*(clickText?.Length??0)+(price==null?0:MeasureActionRetainedItems(price)))))return;
            int afterNativeId=target.BeforeFirstGrant||!ownerContribution?originalNativeId:ReadActionHistoricalOwnerReceiptId(target,events,"owner-after-saved",afterSize);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.DecoderMaximum,out var decoder))return;
            using(decoder)
                if(ownerContribution&&(!ActionColdOwnerNativeMatches(nativeBefore,originalNativeId,originalDeaths,target.Operation,1f,bagBefore,beltBefore)||
                    !target.BeforeFirstGrant&&!ActionColdOwnerNativeMatches(nativeAfter,afterNativeId,originalDeaths,target.Operation,2f,bagAfter,beltAfter)||
                    !laterLife&&!ActionColdOwnerNativeMatches(owner.Native,owner.NativeId,owner.Deaths,target.Operation,target.BeforeFirstGrant?1f:2f,target.BeforeFirstGrant?bagBefore:bagAfter,target.BeforeFirstGrant?beltBefore:beltAfter)))return;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out sourceMemory)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out paymentMemory))return;
            if(ownerContribution&&!owner.Spawn.Local)
            remote=new ActionRemoteHostOriginal{Operation=target.Operation,World=target.World,Actor=owner.Actor,Peer=owner.Spawn.Peer,Connection=owner.Spawn.Connection,Target=target.Body,Position=target.Position,
                Account=owner.Spawn.Account,Creation=(string)image.Attribute("creation"),Save=target.Save,Click=clickText,Path=owner.Path,Directory=sourceDirectory,NativeWorld=target.NativeWorld,Deaths=originalDeaths,
                BagBefore=ItemStack.Clone(bagBefore),BeltBefore=ItemStack.Clone(beltBefore),Price=price,NativeBefore=nativeBefore,Accepted=true,Saved=true,Confirmed=true,AdmissionIssued=true,
                Recovery=RebirthNpcRecoveryGeneration.RequireCurrent(),Memory=sourceMemory,RestartNativeSource=owner};
            if(remote!=null&&!NarrowActionRemoteHostMemory(remote))return;
            var candidate=new RebirthElectricalServiceRecord{ClrIdx=(int)configuration.Attribute("clr"),Position=target.Position,Kind=(RebirthElectricalServiceKind)checked((byte)(int)configuration.Attribute("kind")),
                Condition=(float)configuration.Attribute("condition"),Workmanship=(float)configuration.Attribute("workmanship"),LastServiceWorldTime=(ulong)configuration.Attribute("time"),ServiceCount=(int)configuration.Attribute("count"),
                SourceBlock=checked((ushort)(int)configuration.Attribute("block")),SourceIncarnation=(string)configuration.Attribute("incarnation"),ConfiguredByStableId=(string)configuration.Attribute("author"),
                ConfiguredByBackgroundId=(string)configuration.Attribute("background"),ConfiguredByBonusId=(string)configuration.Attribute("bonus"),ConfiguredSkillValue=(float)configuration.Attribute("skill")};
            float trainingRaw=ReadActionTraining(image,target.Operation,out bool trainingRequired);
            bool returnedPublication=events.ContainsKey("publication-returned");
            if(returnedPublication&&image.Element("serviceBefore")==null)return;
            payment=new ActionPayment(owner.Actor,owner.Spawn.Account,(string)image.Attribute("creation"),target.Save,legs,target.Body,candidate,paymentMemory,ownerContribution?bagBefore:null,ownerContribution?beltBefore:null,remote,originalDeaths,trainingRaw,trainingRequired,target.Operation)
            {Original=target.Original,OriginalBytesBound=Encoding.UTF8.GetByteCount(target.Original),State=target.BeforeFirstGrant?ActionPaymentState.Prepared:ActionPaymentState.Paid,
                OwnerBeforeSaved=true,OwnerAfterSaved=!target.BeforeFirstGrant,TargetBeforeSaved=true,
                RemoteDebitEntered=!target.BeforeFirstGrant,RestartTargetSource=target,Sequence=events.Count,OwnerNativeLifeEnded=laterLife,
                ServiceBefore=image.Element("serviceBefore")==null?null:new XElement(image.Element("serviceBefore")),PublicationEntered=returnedPublication,PublicationReturned=returnedPublication,ReturnedPublicationNeedsRestore=returnedPublication};
            foreach(var pair in events)payment.Events.Add(pair.Key,pair.Value);
            if(returnedPublication)
            {
                if(events.TryGetValue("service-after-saved",out int serviceSaved))
                {
                    if(serviceSaved<=events["publication-returned"]||!ReadHistoricalActionSourceArtifact(target,"service-after-saved",serviceSaved,24,"service-after.native")||
                        !RebirthInfrastructureWorkService.ValidatePaidActionArtifact(payment,Path.Combine(target.Directory,"service-after.native")))return;
                    payment.ServiceAfterSaved=true;
                }
                if(events.TryGetValue("target-after-saved",out int targetSaved))
                {
                    if(!payment.ServiceAfterSaved||targetSaved<=serviceSaved||!target.LoadedAfterPublication||!ReadHistoricalActionSourceArtifact(target,"target-after-saved",targetSaved,48))return;
                    payment.TargetAfterSaved=true;
                }
            }
            if(!NarrowActionPaymentMemory(payment)||!target.Current()||!ActionHostLoadedCurrent(owner))return;
            lock(actionOwnerGate)
            {
                if(actionRemoteHostOriginals.ContainsKey(target.Operation)||actionPayments.Any(value=>value.Operation==target.Operation))return;
                if(remote!=null)actionRemoteHostOriginals.Add(target.Operation,remote);
                target.Payment=payment;actionPayments.Add(payment);if(laterLife)payment.SavedPaidPublication=new ActionSavedPaidPublication(payment,owner,target);
                if(!RebirthInfrastructureWorkService.ReservePaidAction(payment)){actionPayments.Remove(payment);target.Payment=null;actionRemoteHostOriginals.Remove(target.Operation);return;}
#if REBIRTH_DEBUG
                if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&owner.Actor.Buffs.GetCustomVar("rbActionRestartFixtureHold")==1f)
                {payment.ScenarioPaused=true;payment.ScenarioRetain=true;actionScenarioOriginals[payment.Operation]=payment;}
#endif
                if(remote!=null)sourceMemory=null;paymentMemory=null;
            }
        }
        catch{/* Only this fully proved paid original may publish once; unknown holds. */}
        finally{scratch?.Dispose();sourceMemory?.Dispose();paymentMemory?.Dispose();}
    }
}
