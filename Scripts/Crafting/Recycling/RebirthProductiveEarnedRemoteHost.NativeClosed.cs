using System;using System.IO;using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    // Positive compact historical status. Never hydrates a native source or
    // interprets missing state as unused; all original files remain retained.
    private static bool TryClosedEarly(Early hold,out RebirthProductiveEarnedRemoteClosedRecord.Description closed,bool requireRelease=true)
    {
        closed=null;
        if(hold==null)return false;
        string header=hold.Path.Substring(0,hold.Path.Length-5)+".header",path=header+".closed";
        if(!Safe(path)||File.Exists(path+".tmp"))throw new InvalidDataException();
        if(!File.Exists(path))return false;
        if(!RebirthProductiveEarnedRemoteClosedRecord.TryRead(ReadNativeOutputMetadata(path,4096),out var value)||
            value.Operation!=hold.Operation||value.Birth!=hold.Birth||value.Nonce!=hold.Nonce||value.Owner!=hold.Owner||value.Creation!=hold.Creation||
            value.World!=hold.World||value.Deaths!=hold.Deaths||!value.Position.Equals(hold.Position)||value.HoldDigest!=Digest(hold.Bytes)||
            !Manifest(hold.Manifest,out _,out _,out _,out _,out var originalDigest)||value.OriginalDigest!=originalDigest||
            !Safe(header)||File.Exists(header+".tmp")||!File.Exists(header)||value.HeaderDigest!=Digest(ReadNativeOutputMetadata(header,2048)))throw new InvalidDataException();
        string terminal=header+(value.Kind==1?".nativeOutputSaved":".nativePendingSaved");
        if(!Safe(terminal)||File.Exists(terminal+".tmp")||!File.Exists(terminal)||value.TerminalDigest!=Digest(ReadNativeOutputMetadata(terminal,8192)))throw new InvalidDataException();
        string released=header+".nativeReleased";
        if(File.Exists(released+".tmp"))return false;
        if(!File.Exists(released)){if(requireRelease)return false;closed=value;return true;}
        if(!ReadNativeOutputMetadata(released,64).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(Digest(RebirthProductiveEarnedRemoteClosedRecord.Encode(value)))))throw new InvalidDataException();
        closed=value;return true;
    }
    internal static bool TryClosedStatus(EntityPlayer actor,ClientInfo sender,string creation,Vector3i position,Guid operation,Guid nonce,string digest,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        try
        {
            if(!RebirthProductiveEarnedCoordinator.Enabled||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                sender?.loginDone!=true||sender.disconnecting||sender.netConnection?.Length<1||sender.netConnection?[0]==null||sender.netConnection[0].IsDisconnected()||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||
                !RebirthOriginalActionScope.Matches(actor,owner.StorageKey,creation)||sender.entityId!=actor.entityId||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(actor.entityId),sender))return false;
            string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned","remote",owner.StorageKey,operation.ToString("N")+".hold");
            if(!Safe(path)||File.Exists(path+".tmp")||!File.Exists(path))return false;
            var hold=ReadEarly(path);
            if(hold.Operation!=operation||hold.Owner!=owner.StorageKey||hold.Creation!=creation||hold.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||
                hold.Nonce!=nonce||!hold.Position.Equals(position)||!TryClosedEarly(hold,out var closed)||closed.OriginalDigest!=digest)return false;
            // Reply's life is current transport identity. The historical receipt
            // remains bound to its original life; this status invokes no effects.
            bool refresh=pending.ContainsKey(operation)||coldEarly.Any(value=>value.Operation==operation);
            if(pending.TryGetValue(operation,out var retained)&&!ReleaseNativeFinal(retained))return false;
            pending.Remove(operation);
            early.Remove(operation);coldEarly.RemoveAll(value=>value.Operation==operation);
            if(refresh)RefreshEarlyAfterNativeClosure();
            reply=new RebirthProductiveEarnedWire.Reply{Operation=operation,Birth=closed.Birth,Nonce=nonce,Digest=digest,World=closed.World,Owner=closed.Owner,
                Creation=creation,Deaths=actor.Died,Position=position,State=RebirthProductiveEarnedWire.Status.NativeClosed,
                QueueOwner=HasNativeStationAdoption(path.Substring(0,path.Length-5)+".header",closed.OriginalDigest,closed.HeaderDigest)||TryNativeInheritedCustody(hold)?"nativeStationAdopted":null};return true;
        }
        catch{return false;}
    }
    internal static bool AcknowledgeNativeFinal(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,string digest,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        try
        {
            if(TryClosedStatus(actor,sender,creation,station.ToWorldPos(),operation,nonce,digest,out reply))return true;
            if(!pending.ContainsKey(operation)&&!TryRebindQuery(actor,sender,station,creation,operation,nonce,digest))return false;
            if(!pending.TryGetValue(operation,out var source)||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(source.Sender,sender)||
                !ReferenceEquals(source.Station,station)||source.Nonce!=nonce||source.Creation!=creation||Digest(source.Image)!=digest||!source.Current())return false;
            string holdPath=source.HeaderPath.Substring(0,source.HeaderPath.Length-7)+".hold";
            if(!Safe(holdPath)||File.Exists(holdPath+".tmp")||!File.Exists(holdPath))return false;
            var hold=ReadEarly(holdPath);var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
            if(hold.Operation!=operation||hold.Nonce!=nonce||hold.Birth!=frame.Birth||hold.Owner!=source.Owner||hold.Creation!=creation||hold.Deaths!=source.Deaths||
                hold.World!=source.WorldKey||!hold.Position.Equals(station.ToWorldPos())||!Manifest(hold.Manifest,out _,out _,out _,out _,out var heldDigest)||heldDigest!=digest)return false;
            if(TryClosedEarly(hold,out _,false))
            {
                if(!RestoreNativeOutputDescription(source)||File.Exists(source.HeaderPath+".nativePending")&&!RestoreNativePendingDescription(source)||
                    !ReleaseNativeFinal(source)||!TryClosedEarly(hold,out _))return false;
                pending.Remove(operation);early.Remove(operation);coldEarly.RemoveAll(value=>value.Operation==operation);
                RefreshEarlyAfterNativeClosure();
                return TryClosedStatus(actor,sender,creation,station.ToWorldPos(),operation,nonce,digest,out reply);
            }
            if(!NativeFinalCurrent(source)||!TryNativeFinalDigest(source,out byte kind,out string terminal))return false;
            var closed=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=operation,Birth=frame.Birth,Nonce=nonce,World=source.WorldKey,Owner=source.Owner,
                Creation=creation,Deaths=source.Deaths,Position=station.ToWorldPos(),Kind=kind,OriginalDigest=digest,HeaderDigest=Digest(source.Header),HoldDigest=Digest(hold.Bytes),TerminalDigest=terminal};
            if(!NativeFinalCurrent(source)||!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".closed",RebirthProductiveEarnedRemoteClosedRecord.Encode(closed)))return false;
            // The producer owns any saved child handoff. Closing the parent
            // retires only its transient native observers and financial holds.
            if(!ReleaseNativeFinal(source)||!TryClosedEarly(hold,out _))return false;
            pending.Remove(operation);early.Remove(operation);coldEarly.RemoveAll(value=>value.Operation==operation);
            RefreshEarlyAfterNativeClosure();
            return TryClosedStatus(actor,sender,creation,station.ToWorldPos(),operation,nonce,digest,out reply);
        }
        catch{return false;}
    }
    private static void RefreshEarlyAfterNativeClosure()
    {
        var previous=early.Values.ToArray();earlyWorld=null;ObserveEarly();
        // Rescan all exact retained metadata. An unrelated scanner failure is
        // rediscovered and remains unknown; no error flag is blindly cleared.
        foreach(var value in previous)
        {
            var current=coldEarly.FirstOrDefault(item=>item.Operation==value.Operation&&item.Bytes.SequenceEqual(value.Bytes));
            if(current!=null)early[value.Operation]=current;
        }
    }
}
