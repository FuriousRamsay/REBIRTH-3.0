using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class RemoteHostOriginal
    {
        internal static RemoteHostOriginal FromActualCold(RemoteLoadedOwner owner,LoadedStation loaded)
        {
            var peer=owner?.CurrentPeer();
            if(peer==null||loaded?.Current()!=true||!ReferenceEquals(owner.File.world,loaded.World)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||loaded.MeasureOriginal()<1||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*loaded.MeasureOriginal()+8L*owner.File.native.Length+65536),out var memory))return null;
            RemoteHostOriginal issued=null;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null)return null;byte[] image=loaded.CopyOriginal();if(image==null)return null;
                    var node=RebirthCookingRemoteAdmission.ReadProtocolXml(Encoding.UTF8.GetString(image),8*1024*1024);
                    if(node.Name!="placedRecyclerOriginal"||(int?)node.Attribute("version")!=1||(string)node.Attribute("operation")!=loaded.Operation.ToString("N")||
                        (string)node.Attribute("owner")!=identity.StorageKey||(string)node.Attribute("account")!=identity.CanonicalId||
                        (string)node.Attribute("creation")!=owner.Creation||(int?)node.Attribute("deaths")!=owner.Deaths||
                        (string)node.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(loaded.World)||
                        (string)node.Attribute("block")!=loaded.Station.block.GetBlockName()||
                        loaded.Station.ToWorldPos()!=new Vector3i((int)node.Attribute("x"),(int)node.Attribute("y"),(int)node.Attribute("z")))return null;
                    string purpose=(string)node.Attribute("purpose");if(purpose!="enqueue"&&purpose!="cancel"&&purpose!="complete")return null;
                    byte[] before=ColdBytes(node.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(node.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                    int beforeOffset=(int)node.Attribute("beforeOffset"),afterOffset=(int)node.Attribute("afterOffset");
                    if(!RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out _,out _)||!RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out _,out _))return null;
                    var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBag"),4*1024*1024));
                    var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBelt"),4*1024*1024));
                    var afterBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBag"),4*1024*1024));
                    var afterBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBelt"),4*1024*1024));
                    var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("returns"),4*1024*1024));
                    string bagBits=(string)node.Attribute("bagLocks"),beltBits=(string)node.Attribute("beltLocks");
                    if(beforeBag==null||beforeBelt==null||afterBag==null||afterBelt==null||returns==null||bagBits==null||beltBits==null||
                        bagBits.Length!=beforeBag.Length||beltBits.Length!=beforeBelt.Length||bagBits.Any(value=>value!='0'&&value!='1')||beltBits.Any(value=>value!='0'&&value!='1'))return null;
                    var bagLocks=bagBits.Select(value=>value=='1').ToArray();var beltLocks=beltBits.Select(value=>value=='1').ToArray();int beltCount=(int)node.Attribute("beltCount");
                    if(!RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,bagLocks,beltLocks,beltCount,returns,out var expectedBag,out var expectedBelt)||
                        !SameOwnerGrid(afterBag,expectedBag)||!SameOwnerGrid(afterBelt,expectedBelt))return null;
                    Guid generation=owner.CaptureGeneration(loaded);if(generation==Guid.Empty)return null;
                    var source=issued=new RemoteHostOriginal(peer,owner.Actor,loaded.Station,identity,loaded.Operation,purpose,image,before,after,beforeOffset,afterOffset,memory,generation);
                    memory=null;source.BeforeBag=beforeBag;source.BeforeBelt=beforeBelt;source.AfterBag=afterBag;source.AfterBelt=afterBelt;
                    source.BagLocks=bagLocks;source.BeltLocks=beltLocks;source.BeltCount=beltCount;
                    source.Slots=Enumerable.Range(0,2).SelectMany(area=>Enumerable.Range(0,area==0?beforeBag.Length:beforeBelt.Length)
                        .Where(index=>!Same((area==0?beforeBag:beforeBelt)[index],(area==0?afterBag:afterBelt)[index])).Select(index=>new Slot(area,index))).ToArray();
                    if(!PhaseBytes(owner.File.buffs,"rbRecyclerPlaced_"+source.Operation.ToString("N"),out float phase)||
                        !PhaseBytes(owner.File.buffs,"rbRecyclerSlot_"+source.Operation.ToString("N"),out float count)||phase!=(int)phase||count!=(int)count||
                        !(phase==0&&count==0&&loaded.Stage=="before"||ValidOwnerCheckpointStage((int)phase,(int)count,source.Slots.Length))||
                        !source.MatchesOwnerCheckpoint(owner.File.bag,owner.File.belt,(int)count)||loaded.Stage!="before"&&loaded.Stage!="after"||
                        loaded.Stage=="before"&&((phase!=0&&phase!=1)||count!=0))return null;
                    source.AcceptedStation=loaded.Stage=="after";source.ColdNeedsOwner=true;source.ColdPhase=(int)phase;source.ColdCompleted=(int)count;
                    source.ColdStation=loaded;source.PlanSaved=true;
                    var receipt=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
                        new XElement("before",XElement.Parse(Encoding.UTF8.GetString(image))),new XElement("recyclerRemoteColdOwnerOriginal",new XAttribute("account",source.Account),
                            new XAttribute("phase",source.ColdPhase),new XAttribute("completed",source.ColdCompleted),new XAttribute("actualPrimaryLoad",true),new XAttribute("actualToPlayer",true),
                            new XAttribute("freshOwnerUploadRequired",true),new XElement("native",Convert.ToBase64String(owner.File.native))));
                    string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".remote-cold-owner."+source.Nonce.ToString("N")+".xml");
                    if(!RebirthWorkstationCompletionPublication.Write(path,receipt)||!XNode.DeepEquals(receipt,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                        !source.Current()||!owner.Current()||!loaded.Current())return null;
                    issued=null;return source;
                }
            }
            catch{return null;}
            finally{issued?.Dispose();memory?.Dispose();}
        }
    }
    private static void RecoverRemoteRecyclerCold(World world)
    {
        if(world?.IsRemote()!=false||remoteRecyclerOriginals.Count>=4)return;
        LoadedStation[] stations;lock(coldStationGate)stations=loadedRecyclerStations.Where(value=>!value.Adopted&&value.SameWorld(world)).ToArray();
        foreach(var station in stations)
        {
            if(remoteRecyclerOriginals.ContainsKey(station.Operation)||authorityOriginals.Any(value=>value.Source?.Operation==station.Operation))continue;
            if(RecoverClosedPendingSource(station))continue;
            foreach(var owner in remoteLoadedRecyclerOwners.ToArray())
            {
                var source=RemoteHostOriginal.FromActualCold(owner,station);if(source==null)continue;
                bool retained=false;
                try
                {
                    if(!TryRestoreClosedAuthority(source,station))continue;
                    source.Publication=StationPublication.FromActualCold(source,station);if(source.Publication==null)continue;
                    if(source.AcceptedStation&&source.ColdPhase>1)
                    {source.OwnerBeforeCheckpoint=RemoteOwnerCheckpoint.ReadHistoricalColdBefore(source);if(source.OwnerBeforeCheckpoint==null)continue;}
                    if(!TryReadRemoteColdTerminal(source))continue;
                    source.Access=AccessHold.CaptureRemote(source,source.Publication);if(source.Access==null)continue;
                    remoteRecyclerOriginals.Add(source.Operation,source);station.Adopted=true;retained=true;
                    remoteLoadedRecyclerOwners.Remove(owner);owner.File.Dispose();break;
                }
                finally{if(!retained)source.Dispose();}
            }
        }
    }
}
