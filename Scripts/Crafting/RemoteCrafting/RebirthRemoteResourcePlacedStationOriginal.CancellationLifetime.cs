using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static void CancellationUpdate(ref ModEvents.SGameUpdateData data)
    {
        BindCancellationWorld();PumpRetiringCancellationHostCustodies();RebirthRemoteResourcePlacedStationPreparation.RetireDrainedNativeReads();if(!ProtocolActive)return;
        if(GameManager.Instance?.World?.IsRemote()==true){PumpCancellationClient();return;}
        foreach(var original in cancellationServers.Values.ToArray())
        {try{PumpCancellationServer(original);}catch{/* Original/custody remains unknown. */}}
    }
    private static void BindCancellationWorld()
    {
        var world=GameManager.Instance?.World;string directory=GameIO.GetSaveGameDir();
        if(ReferenceEquals(world,cancellationWorld)&&directory==cancellationDirectory)return;
        DropCancellationRestart();
        foreach(var original in cancellationClients.Values.ToArray())
        {RebirthRemoteResourceOwnerAction.DropEndedPlacedCancellation(original);DropCancellationClient(original);}
        foreach(var original in cancellationServers.Values.ToArray())DropCancellationServer(original);
        cancellationWorld=world;cancellationDirectory=directory;cancellationFlush=null;cancellationClientSetter=null;cancellationStationPermit=null;
    }
    private static void DropCancellationClient(CancellationClient original)
    {
        if(cancellationClients.TryGetValue(original.Operation,out var registered)&&ReferenceEquals(registered,original))cancellationClients.Remove(original.Operation);var memory=original.Memory;
        original.Memory=null;original.Actor=null;original.OriginalWorld=null;original.Original=null;original.Window=null;original.Card=null;original.Station=null;original.Chunk=null;original.Reentry=null;original.OwnerTerminalVersion=null;original.RestartOwner=null;original.RestartBeforeOwner=null;original.RestartSavedAfterOwner=null;original.SavedAfterProjection=null;original.OwnerSavedAfterVersion=null;
        original.OwnerProgress=null;original.Before=null;original.After=null;original.SavedPaidRow=null;original.NativeInput=null;original.NativeQueue=null;original.NativeAfter=null;
        original.Header=null;original.Permission=null;original.FullPaidPermission=null;original.TerminalEvidence=null;
        if(!original.HeaderPending)memory?.Dispose();
    }
    private static void DropCancellationServer(CancellationServer original)
    {
        if(cancellationServers.TryGetValue(original.Operation,out var registered)&&ReferenceEquals(registered,original))cancellationServers.Remove(original.Operation);var memory=original.Memory;
        RetireCancellationHostCustody(original);
        DropCancellationReentry(original);
        original.Watch?.Dispose();original.Watch=null;original.SnapshotBudget?.Close();
        if(uploadOriginals.TryGetValue(original.Operation,out var upload)&&ReferenceEquals(upload.Cancellation,original))
        {
            uploadOriginals.Remove(original.Operation);upload.Memory=null;upload.Cancellation=null;upload.Actor=null;upload.Peer=null;upload.Station=null;upload.Chunk=null;upload.Original=null;upload.Accepted=null;
        }
        original.Historical?.Dispose();original.Historical=null;original.Memory=null;original.Accepted=null;original.BeforeTile=null;original.AfterTile=null;
        original.BeforeQueue=null;original.AfterQueue=null;original.BeforeActor=null;original.AfterActor=null;original.AfterInput=null;original.Header=null;
        original.Permission=null;original.AfterEvidence=null;original.Marker=null;original.Origin=null;original.Actor=null;original.Original=null;original.Station=null;original.Chunk=null;original.Peer=null;original.SnapshotBudget=null;
        original.BeforeTwoPublication=null;original.Receipt1Source=null;original.Receipt1BeforeEvidence=null;original.QueueRecoveryPublication=null;original.QueueReentryCommand=null;
        // The account itself defers retirement while actual native callbacks
        // retain any budgeted buffers. Financial graphs are already gone.
        // A world/connection transition can retire native objects before the
        // transport queue pumps its obsolete callback. That callback still
        // owns the captured full reply and must retire this account itself.
        if(!original.FinalReplyPending)memory?.Dispose();
    }
    private static bool FinalizeCancellationHost(CancellationServer original)
    {
        if(!original.AfterSaved||!original.QueueCompleted||!original.Current()||!SavedCancellationOwner(original,3f,true,out _)||!ReadCancellationSnapshot(original,true,out _))return false;
        var terminal=CancellationJournal(original.Operation,"Terminal",MaximumApplicationCommand).Read(original.Operation);
        byte[] expected=CancellationServerTerminalBytes(original);
        if(terminal?.State!=RebirthCamperTransferJournal.Phase.Applied||terminal.Vehicle!=original.Source||terminal.OwnerCreation!=original.Original.Creation||!terminal.Before.SequenceEqual(expected)||!terminal.After.SequenceEqual(expected))return false;
        byte[] command=Encoding.UTF8.GetBytes(new XElement("placedCancellationFinalAck",new XAttribute("version",1),new XAttribute("cancel",original.Operation.ToString("N")),
            new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),new XAttribute("permission",RebirthRemoteResourceDurableJournal.Digest(original.Permission)),
            new XAttribute("terminal",Convert.ToBase64String(terminal.Before))).ToString(SaveOptions.DisableFormatting));
        if(!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"FinalAck",command,true))return false;
        if(HoldCancellationScenario(original.Operation,"terminalAck"))return true;
        // Keep the complete financial graph charged while the queued native
        // connection sender still captures it. The callback follows removal
        // of the queue's body/send references; it proves no payment or delivery.
        ReplyCancellation(original,5,false);return true;
    }
    internal static bool TryCancellationLockActor(TileEntityWorkstation station,int actorId,out bool allowed)
    {
        allowed=false;var original=cancellationServers.Values.SingleOrDefault(value=>value.Accepted!=null&&ReferenceEquals(value.Station,station)&&value.Memory?.Current==true);
        if(original==null)
        {
            var restarted=restartCancellations.Values.SingleOrDefault(value=>value.Memory?.Current==true&&ReferenceEquals(value.Load?.Loaded?.Tile,station)&&value.Load.Loaded.Installed());
            if(restarted==null)return false;
            var actor=GameManager.Instance?.World?.GetEntity(actorId) as EntityPlayer;
            allowed=ServerScope(actor,restarted.Original);return true;
        }
        allowed=original.Actor?.entityId==actorId&&original.Origin?.Current==true&&ServerScope(original.Actor,original.Original)&&
            ReferenceEquals(original.Station.GetChunk(),original.Chunk)&&original.Chunk!=null&&!original.Chunk.InProgressUnloading&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.Actor.world,station,out string birth)&&birth==original.Birth;return true;
    }
}
