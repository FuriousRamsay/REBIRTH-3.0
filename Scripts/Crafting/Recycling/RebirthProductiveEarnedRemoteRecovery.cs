using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static bool TryRebindNativeManifest(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,Guid operation,Guid nonce)
    {
        if(!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||nonce==Guid.Empty)return false;
        string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned","remote",owner.StorageKey,operation.ToString("N")+".hold");
        if(!Safe(path)||!File.Exists(path)||File.Exists(path+".tmp"))return false;
        var hold=ReadEarly(path);
        if(hold.Operation!=operation||hold.Nonce!=nonce||hold.Owner!=owner.StorageKey||hold.Creation!=creation||
            !hold.Position.Equals(station.ToWorldPos())||!Manifest(hold.Manifest,out _,out _,out _,out _,out string digest))return false;
        return TryRebindQuery(actor,peer,station,creation,operation,nonce,digest);
    }
    // Rebinds observations of an existing exact original only. No prepare, native
    // upload, wear, marker recreation or claim of never-attempted from absence.
    internal static bool TryRebindQuery(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,
        Guid operation,Guid nonce,string digest)
    {
        RebirthProductiveEarnedObservationBudget.Decoder decoder=null;
        try
        {
            if(!RebirthProductiveEarnedCoordinator.Enabled||!installed||nonce==Guid.Empty||
                !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return false;
            ObserveWorld();ObserveEarly();
            var hold=coldEarly.FirstOrDefault(value=>value.Operation==operation);
            if(hold==null)
            {
                string known=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned","remote",identity.StorageKey,operation.ToString("N")+".hold");
                if(!Safe(known)||!File.Exists(known)||File.Exists(known+".tmp"))return false;
                hold=ReadEarly(known); // Exact requested original; no absence inference or new admission.
            }
            if(hold==null||hold.Nonce!=nonce||hold.Owner!=identity.StorageKey||hold.Creation!=creation||hold.Deaths!=actor.Died||
                hold.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||!hold.Position.Equals(station.ToWorldPos())||
                !Read(hold.Path).SequenceEqual(hold.Bytes)||!Manifest(hold.Manifest,out var birth,out _,out _,out _,out var heldDigest)||heldDigest!=digest)return false;
            // Once the actual native tick has taken this paid original, the
            // old owner can query compact metadata but cannot revive its
            // financial context alongside the native coordinator.
            if(TryNativeInheritedCustody(hold)||RebirthProductiveEarnedCoordinator.HoldsAdoptedNativeTick(station))return false;
            if(pending.TryGetValue(operation,out var retained))
            {
                if(retained.Nonce!=nonce||retained.Owner!=hold.Owner||retained.Creation!=creation||retained.Deaths!=actor.Died||
                    !ReferenceEquals(retained.Station,station)||retained.WorldKey!=hold.World||Digest(retained.Image)!=digest)return false;
                retained.Actor=actor;retained.Sender=sender;early[operation]=hold;return retained.Current();
            }
            decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();
            if(decoder==null)return false; // No artifact read/hydration before admission.
            if(pending.Count>=4||pending.Values.Any(value=>ReferenceEquals(value.World,actor.world)&&ReferenceEquals(value.Station,station))||
                (!RebirthProductiveEarnedArtifact.TryReadPreparedIdentity(station,birth,operation,out var artifact,out var image)&&
                 !RebirthProductiveEarnedArtifact.TryReadRemoteObservedOriginal(station,birth,operation,out artifact,out image))||Digest(image)!=digest)return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(image);
            if(!decoder.Admit(frame,image.Length))return false;
            string directory=Path.GetFullPath(GameIO.GetSaveGameDir());
            string path=Path.Combine(directory,"RebirthProductiveEarned","remote",identity.StorageKey,operation.ToString("N")+".header");
            if(!Safe(path)||!File.Exists(path)||File.Exists(path+".tmp"))return false;
            byte[] header=Read(path);
            if(!TryReadHeader(header,identity.StorageKey,creation,actor.Died,station,frame,digest,out var savedNonce)||savedNonce!=nonce)return false;
            var memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,frame);
            if(memory==null)return false;
            var context=new Context{Memory=memory,World=actor.world,Actor=actor,Sender=sender,Station=station,Artifact=artifact,Operation=operation,Nonce=nonce,
                Owner=identity.StorageKey,Creation=creation,Deaths=actor.Died,Directory=directory,HeaderPath=path,Header=header,Image=image,WorldKey=frame.World};
            if(!context.Current()){memory.Dispose();return false;}early[operation]=hold;pending.Add(operation,context);
            // Already-existing exact native marker/full After is mandatory. This
            // scope starts only a fresh real save observer, never stamps a marker.
            if(RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out var marker)&&RebirthProductiveEarnedRowProof.MatchesMarker(marker,image)&&
                artifact.MatchesAfter(station)&&context.AfterCurrent())
            {
                context.Accepted=true;var previous=uploading;uploading=context;
                try{context.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(station,artifact);}
                finally{uploading=previous;}
            }
            return true;
        }
        catch{return false;}
        finally{decoder?.Dispose();}
    }
}
