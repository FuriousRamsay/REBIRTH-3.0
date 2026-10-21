using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionOwnerFile
    {internal ActionPayment Payment;internal PlayerDataFile Data;internal string Phase,Path;internal byte[] Native;internal bool Accepted,Saved;internal int Workers,NativeEntityId;internal IDisposable Memory;}
    private sealed class ActionOwnerSave
    {internal ActionOwnerFile File;internal string Path;internal byte[] Native;internal bool Finished;}
    private static readonly object actionOwnerGate=new object();
    private static readonly ConditionalWeakTable<PlayerDataFile,ActionOwnerFile> actionOwnerData=new ConditionalWeakTable<PlayerDataFile,ActionOwnerFile>();
    private static readonly Dictionary<ActionPayment,Dictionary<string,ActionOwnerFile>> actionOwnerFiles=new Dictionary<ActionPayment,Dictionary<string,ActionOwnerFile>>();
    private static string ActionOwnerMarker(Guid operation)=>"rbActionPayment_"+operation.ToString("N");
    private sealed class ActionOwnerStream:MemoryStream
    {public override void Write(byte[] bytes,int offset,int count){if(count<0||Position+count>16L*1024*1024)throw new InvalidDataException("Action owner native image too large");base.Write(bytes,offset,count);}public override void WriteByte(byte value){if(Position>=16L*1024*1024)throw new InvalidDataException();base.WriteByte(value);}}
    private static byte[] ActionOwnerImage(PlayerDataFile data)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out var memory))throw new InvalidDataException("Action owner scratch unavailable");using(memory)
        {using var stream=new ActionOwnerStream();using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);writer.Write((byte)PlayerDataFile.cFileVersion);data.Write(writer,StreamModeWrite.Persistency);writer.Flush();}return stream.ToArray();}
    }
    private static string ExpectedActionOwnerPhase(ActionPayment payment)
    {
        if(payment.Terminal&&payment.RemoteOwner!=null)return "terminal";
        if(payment.RemoteOwner!=null)return payment.RemoteRefundEntered?"return":payment.RemoteDebitEntered?"after":"before";
        if(payment.State==ActionPaymentState.Returning&&payment.Legs.Where(leg=>leg.Local).All(leg=>leg.State==ActionLegState.Returned||leg.State==ActionLegState.Before||leg.State==ActionLegState.Refused))return "return";
        if(payment.Legs.Where(leg=>leg.Local).All(leg=>leg.State==ActionLegState.Debited))return "after";
        if(payment.Legs.All(leg=>leg.State==ActionLegState.Before))return "before";return null;
    }
    private static float ActionOwnerPhaseValue(string phase)=>phase=="before"?1f:phase=="after"?2f:phase=="refused"?8f:phase=="terminal"?6f:3f;
    private static bool ActionOwnerExpected(ActionPayment payment,PlayerDataFile data,string phase)
    {
        if(phase=="terminal")return ActionRemoteMetadataCurrent(payment);
        if(payment.OwnerBagBefore==null||payment.OwnerBeltBefore==null||data.bagData==null||data.inventoryData==null||data.bagData.Length+data.inventoryData.Length>4L*1024*1024)return false;
        if(!TryReserveActionOwnerProjection(data,out var scratch))return false;
        using(scratch)
        {
            var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
            var bagLeg=payment.Legs.FirstOrDefault(leg=>leg.Local&&!leg.Belt);var beltLeg=payment.Legs.FirstOrDefault(leg=>leg.Local&&leg.Belt);
            return SameActionItems(bag,phase=="after"&&bagLeg!=null?bagLeg.After:payment.OwnerBagBefore)&&SameActionItems(belt,phase=="after"&&beltLeg!=null?beltLeg.After:payment.OwnerBeltBefore);
        }
    }
    private static void CaptureActionOwner(ActionPayment payment,PlayerDataFile data,bool accepted)
    {
        bool terminal=payment.Terminal&&payment.RemoteOwner!=null;
        if(!(terminal?ActionRemoteMetadataCurrent(payment):payment.ActorCurrent())||payment.Original==null||data==null||data.id!=payment.Actor.entityId||(terminal?data.deaths<payment.Deaths:data.deaths!=payment.Deaths)||data.bDead||!terminal&&!payment.Legs.Any(leg=>leg.Local))return;
        string phase=ExpectedActionOwnerPhase(payment);
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(payment.Operation),out float value,out bool present)||!present)return;
        if(payment.RemoteOwner!=null&&payment.RemoteDebitEntered&&!payment.RemoteRefundEntered&&value==8f)phase="refused";
        if(phase==null||value!=ActionOwnerPhaseValue(phase)||!ActionOwnerExpected(payment,data,phase))return;
        lock(actionOwnerGate)
        {
            if(!actionOwnerFiles.TryGetValue(payment,out var phases))actionOwnerFiles.Add(payment,phases=new Dictionary<string,ActionOwnerFile>());
            if(phases.TryGetValue(phase,out var previous)&&(previous.Saved||previous.Workers!=0))return;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var transfer))return;
            using(transfer)
            {
                string path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),payment.Account+"."+PlayerDataFile.EXT));
                if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))throw new InvalidDataException("Action native owner path unavailable");
                byte[] native=ActionOwnerImage(data);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(native.LongLength+2L*path.Length+4096),out var allocation))return;
                try
                {
                    var file=new ActionOwnerFile{Payment=payment,Data=data,NativeEntityId=data.id,Phase=phase,Path=path,Native=native,Accepted=accepted,Memory=allocation};
                    phases[phase]=file;actionOwnerData.Remove(data);actionOwnerData.Add(data,file);allocation=null;
                    if(previous!=null){if(previous.Data!=null&&!ReferenceEquals(previous.Data,data))actionOwnerData.Remove(previous.Data);previous.Payment=null;previous.Native=null;previous.Data=null;previous.Memory?.Dispose();previous.Memory=null;}
                }
                finally{allocation?.Dispose();}
            }
        }
    }
    internal static void ObserveActionPaymentFromPlayer(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        if(!run||!ThreadManager.IsMainThread()||actor==null)return;
        try{CaptureActionClientCheckpoint(data,actor,run);}catch{}
        try{ObserveRemoteActionFromPlayer(data,actor,run);}catch{}
        foreach(var payment in actionPayments.Where(value=>ReferenceEquals(value.Actor,actor)&&ReferenceEquals(value.World?.GetPrimaryPlayer(),actor)).ToArray())try{CaptureActionOwner(payment,data,true);}catch{}
    }
    internal static void EnterAcceptedActionPaymentOwner(ClientInfo peer,PlayerDataFile data,bool run,out object state)
    {
        state=null;if(!run||!ThreadManager.IsMainThread()||peer==null||data==null||peer.entityId!=data.id||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner))return;
        try{var remote=EnterAcceptedRemoteActionOriginal(peer,data,run);if(remote!=null){state=remote;return;}}catch{}
        foreach(var payment in actionPayments.Where(value=>(value.ActorCurrent()||ActionRemoteMetadataCurrent(value))&&value.Actor.entityId==peer.entityId&&value.Account==owner.CanonicalId).ToArray())
            try{CaptureActionOwner(payment,data,false);if(actionOwnerData.TryGetValue(data,out var file))state=file;}catch{}
    }
    internal static void ObserveAcceptedActionPaymentOwner(ClientInfo peer,PlayerDataFile data,bool run,object state)
    {
        try{RetireSupersededActionOwnerData(peer,data,run);}catch{}
        try{ObserveAcceptedActionTerminalCouriers(peer,data,run);}catch{}
        if(state is ActionRemoteHostOriginal remote){try{ObserveAcceptedRemoteActionOriginal(peer,data,run,remote);}catch{}return;}
        if(state is not ActionOwnerFile file||!ThreadManager.IsMainThread())return;
        if(!run){file.Data=null;return;}
        try
        {
            if(peer!=null&&ReferenceEquals(peer.latestPlayerData,data)&&ReferenceEquals(file.Data,data)&&(file.Payment?.ActorCurrent()==true||ActionRemoteMetadataCurrent(file.Payment))&&peer.entityId==file.Payment.Actor.entityId&&RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var account)&&account.CanonicalId==file.Payment.Account&&file.Native.SequenceEqual(ActionOwnerImage(data)))
            {lock(actionOwnerGate){file.Accepted=true;file.Data=null;}}
        }catch{}
    }
    internal static void EnterActionPaymentOwnerSave(PlayerDataFile data,string directory,string name,bool run,out object state)
    {
        state=null;if(!run||data==null)return;
        try{var client=EnterActionClientCheckpointSave(data,directory,name,run);if(client!=null){state=client;return;}}catch{}
        try{var remote=EnterRemoteActionBeforeSave(data,directory,name,run);if(remote!=null){state=remote;return;}}catch{}
        lock(actionOwnerGate)try
        {
            if(!actionOwnerData.TryGetValue(data,out var file)||file.Payment==null||file.Payment.Terminal&&file.Phase!="terminal"||file.Payment.Retired||file.Workers>=2)return;
            string path=Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT));if(path!=file.Path||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!ActionOwnerImage(data).SequenceEqual(file.Native))return;
            var save=new ActionOwnerSave{File=file,Path=path,Native=file.Native};file.Workers++;state=save;
        }catch{}
    }
    internal static void ObserveActionPaymentOwnerSave(bool run,object state)
    {
        if(state is ActionClientCheckpointSave client){ObserveActionClientCheckpointSave(run,client);return;}
        if(state is ActionRemoteBeforeSave remote){ObserveRemoteActionBeforeSave(run,remote);return;}
        if(state is not ActionOwnerSave save)return;
        lock(actionOwnerGate)
        {
            if(save.Finished)return;save.Finished=true;
            try
            {
                if(run&&save.File.Payment?.Retired==false&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(save.Path,save.Native))
                {
                    save.File.Saved=true;
                    if(save.File.Accepted)save.File.Data=null;
                }
            }catch{}
            finally{var payment=save.File.Payment;if(save.File.Accepted)save.File.Data=null;save.File.Workers--;save.File=null;save.Native=null;if(payment?.Retired==true||payment?.Terminal==true&&(payment.RemoteOwner==null||payment.RemoteTerminalReleased))ReleaseActionOwner(payment);}
        }
    }
    private static bool SaveActionOwner(ActionPayment payment,string phase)
    {
        if(!(phase=="terminal"?ActionRemoteMetadataCurrent(payment):payment.ActorCurrent()))return false;
        if(payment.RemoteOwner!=null&&phase=="before")return SaveRemoteActionOwnerBefore(payment);
        if(!payment.Legs.Any(leg=>leg.Local)&&!(payment.RemoteOwner!=null&&phase=="terminal"))return true;
        if(payment.RemoteOwner!=null&&phase=="return"&&payment.Legs.Where(leg=>leg.RemoteOwner).All(leg=>leg.State==ActionLegState.Refused))
        {if(!SaveActionOwner(payment,"refused"))return false;ActionRecord(payment,"owner-return-saved",new XElement("nativeNoEffect",new XAttribute("source","owner-refused.native")));return true;}
        if(payment.RemoteOwner==null&&payment.Actor is not EntityPlayerLocal)return false;
        bool ready,waiting;
        lock(actionOwnerGate)
        {ready=actionOwnerFiles.TryGetValue(payment,out var captured)&&captured.TryGetValue(phase,out var existing)&&existing.Accepted&&existing.Saved;waiting=captured!=null&&captured.TryGetValue(phase,out var pending)&&pending.Workers!=0;}
        if(payment.RemoteOwner==null&&!ready&&!waiting){payment.Actor.Buffs.SetCustomVar(ActionOwnerMarker(payment.Operation),ActionOwnerPhaseValue(phase),true);GameManager.Instance.SaveLocalPlayerData();}
        lock(actionOwnerGate)
        {
            // Saved was issued by the actual once-only Save callback after full
            // file readback. Later legitimate native PDS saves cannot erase it.
            if(!actionOwnerFiles.TryGetValue(payment,out var phases)||!phases.TryGetValue(phase,out var file)||!file.Accepted||!file.Saved)return false;
            string path=Path.Combine(payment.Directory,"owner-"+phase+".native");
            if(File.Exists(path)){if(!ActionBytes(path,file.Native.Length).SequenceEqual(file.Native))return false;}
            else{using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(file.Native,0,file.Native.Length);output.Flush(true);}if(!ActionBytes(path,file.Native.Length).SequenceEqual(file.Native))return false;}
            ActionRecord(payment,"owner-"+phase+"-saved",new XElement("native",new XAttribute("length",file.Native.Length),new XAttribute("nativeId",file.NativeEntityId)));return true;
        }
    }
    private static void ReleaseActionOwner(ActionPayment payment)
    {
        lock(actionOwnerGate)
        {
            if(actionOwnerFiles.TryGetValue(payment,out var phases))
            {if(phases.Values.Any(file=>file.Workers!=0))return;actionOwnerFiles.Remove(payment);foreach(var file in phases.Values){file.Native=null;file.Data=null;file.Payment=null;file.Memory?.Dispose();file.Memory=null;}}
            // Workers can release their immutable file references here. The
            // native world/source graph and its allocation stay owned until the
            // main-thread pump actually drops those strong references.
            payment.OwnerObservationsReleased=true;
        }
    }
}
