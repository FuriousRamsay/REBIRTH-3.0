using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class ClosingNative
    {
        internal Original Source;internal EntityPlayerLocal Actor;internal World World;internal string Root,Path;internal byte[] Bytes;
        internal RebirthProductiveEarnedRemoteClosedRecord.Description Record;internal double Next;
        internal bool Current()=>ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(Actor?.world,World)&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&!Actor.IsDead()&&Actor.IsSpawned()&&
            System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir())==Root&&RebirthProductiveEarnedArtifact.WorldIdentity(World)==Record.World&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Record.Creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(Actor,out var owner)&&owner.StorageKey==Record.Owner;
    }
    private static readonly Dictionary<Guid,ClosingNative> nativeClosures=new Dictionary<Guid,ClosingNative>();
    private static byte[] NativeCloseStage(RebirthProductiveEarnedRemoteClosedRecord.Description value,string stage)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write((byte)1);writer.Write(value.Operation.ToByteArray());writer.Write(value.Nonce.ToByteArray());writer.Write(value.OriginalDigest);writer.Write(stage);writer.Flush();return stream.ToArray();}
    }
    private static bool SavedNativeAdoption(string path,RebirthProductiveEarnedRemoteClosedRecord.Description value)
        =>SavedNativeCloseStage(path,value,"nativeStationAdopted");
    private static bool SavedNativeCloseStage(string path,RebirthProductiveEarnedRemoteClosedRecord.Description value,string name)
    {
        string stage=path+"."+name;
        return Safe(stage)&&File.Exists(stage)&&!File.Exists(stage+".tmp")&&Read(stage,2048).SequenceEqual(NativeCloseStage(value,name));
    }
    // Query authority only: no station object, native row/fence or effect is
    // reconstructed from these immutable owner originals.
    private static bool RecoverNativeAdoptionQuery(string path,EntityPlayerLocal actor,string world,string owner,string creation,string root,
        RebirthProductiveEarnedObservationBudget.Decoder existing=null,RebirthProductiveEarnedRowProof.Frame decoded=null,byte[] originalImage=null)
    {
        using(var owned=existing==null?RebirthProductiveEarnedObservationBudget.TryEnterDecoder():null)
        {
            var decoder=existing??owned;
            if(decoder==null){coldObserved=false;return false;}
            byte[] image=originalImage??Read(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);var frame=decoded??RebirthProductiveEarnedRowProof.Decode(image);
            if(!frame.HasNativeBodies||!decoder.Admit(frame,image.Length)||!RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _)||frame.World!=world||
                System.IO.Path.GetFileName(path)!=frame.Operation.ToString("N")+".rew")return false;
            byte[] scope=Read(path+".scope",2048),accepted=Read(path+".accepted",2048);string digest=Hash(image);int deaths;Vector3i position;Guid nonce;
            foreach(string suffix in new[]{".rew",".scope",".accepted",".nativeOutput"})
                if(File.Exists((suffix==".rew"?path:path+suffix)+".tmp"))throw new InvalidDataException();
            using(var stream=new MemoryStream(scope,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadByte()!=1||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=owner||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512)!=creation)return false;
                deaths=reader.ReadInt32();if(deaths!=actor.Died||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44)!=world)return false;
                position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
                if(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=digest||stream.Position!=stream.Length)return false;
            }
            using(var stream=new MemoryStream(accepted,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadByte()!=1||new Guid(reader.ReadBytes(16))!=frame.Operation)return false;
                nonce=new Guid(reader.ReadBytes(16));if(nonce==Guid.Empty||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=digest||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!="accepted"||stream.Position!=stream.Length)return false;
            }
            var value=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=frame.Operation,Birth=frame.Birth,Nonce=nonce,World=world,Owner=owner,
                Creation=creation,Deaths=deaths,Position=position,Kind=1,OriginalDigest=digest,HeaderDigest=Hash(scope),HoldDigest=Hash(accepted)};
            foreach(string stage in new[]{"beforeUpload","attempt","applied","afterUpload","published","outputAttempt","nativeOutputSaved","nativeStationAdopted","nativeAdoptionQueried"})
            {
                string saved=path+"."+stage;
                if(File.Exists(saved+".tmp")||File.Exists(saved)&&(!Safe(saved)||!Read(saved,2048).SequenceEqual(NativeCloseStage(value,stage))))return false;
            }
            if(File.Exists(path+".nativeOutput"))
            {
                long length=new FileInfo(path+".nativeOutput").Length;
                if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var memory))
                {coldObserved=false;return false;}
                using(memory)if(!RebirthProductiveEarnedNativeOutputPlan.TryRead(Read(path+".nativeOutput",RebirthProductiveEarnedRowProof.MaximumArtifactBytes),frame,nonce,digest,out _))return false;
            }
            value.TerminalDigest=Hash(NativeCloseStage(value,"nativeOutputSaved"));
            if(!RebirthProductiveEarnedRemoteClosedRecord.Valid(value))return false;
            if(nativeClosures.ContainsKey(value.Operation))return true;
            if(nativeClosures.Count>=4)return false;
            if(!nativeClosures.ContainsKey(value.Operation))nativeClosures.Add(value.Operation,new ClosingNative{Actor=actor,World=actor.world,Root=root,Path=path,Record=value});
            return true;
        }
    }
    private static string HashOriginalFile(string path)
    {
        if(!Safe(path)||File.Exists(path+".tmp"))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var hash=System.Security.Cryptography.SHA256.Create())
        {if(stream.Length<1||stream.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    }
    private static bool TryReadNativeClose(string path,string world,string owner,string creation,out RebirthProductiveEarnedRemoteClosedRecord.Description record,out byte[] bytes)
    {
        record=null;bytes=null;string requested=path+".closeRequested";
        if(!File.Exists(requested))return false;
        if(!Safe(requested)||File.Exists(requested+".tmp"))throw new InvalidDataException();
        bytes=Read(requested,4096);
        if(!RebirthProductiveEarnedRemoteClosedRecord.TryRead(bytes,out var value)||value.World!=world||value.Owner!=owner||value.Creation!=creation||
            System.IO.Path.GetFileName(path)!=value.Operation.ToString("N")+".rew"||HashOriginalFile(path)!=value.OriginalDigest||
            Hash(Read(path+".scope",2048))!=value.HeaderDigest||Hash(Read(path+".accepted",2048))!=value.HoldDigest||
            Hash(Read(path+(value.Kind==1?".nativeOutputSaved":".nativePendingSaved"),8192))!=value.TerminalDigest)throw new InvalidDataException();
        if(File.Exists(path+".scope.tmp")||File.Exists(path+".accepted.tmp")||File.Exists(path+".nativeOutputSaved.tmp")||File.Exists(path+".nativePendingSaved.tmp"))throw new InvalidDataException();
        record=value;return true;
    }
    private static bool RecoverNativeClose(string path,EntityPlayerLocal actor,string world,string owner,string creation,string root)
    {
        if(!TryReadNativeClose(path,world,owner,creation,out var record,out var bytes))return false;
        string closed=path+".closed";
        if(File.Exists(closed+".tmp"))throw new InvalidDataException();
        if(File.Exists(closed))
        {
            if(!Read(closed,4096).SequenceEqual(bytes))throw new InvalidDataException();
            string released=path+".nativeReleased";
            if(File.Exists(released+".tmp"))throw new InvalidDataException();
            if(File.Exists(released))
            {if(!Read(released,64).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(Hash(bytes))))throw new InvalidDataException();return true;}
        }
        if(!nativeClosures.ContainsKey(record.Operation))
        {
            if(nativeClosures.Count>=4)throw new InvalidDataException();
            nativeClosures.Add(record.Operation,new ClosingNative{Actor=actor,World=actor.world,Root=root,Path=path,Record=record,Bytes=bytes});
        }
        return true;
    }
    private static bool PumpNativeClosed(Original original,double now)
    {
        if(nativeClosures.ContainsKey(original.Frame.Operation))return true;
        try
        {
            if(!original.Identity()&&RecoverNativeAdoptionQuery(original.Path,original.Actor,original.WorldKey,original.Owner,original.Creation,original.Root))
            {nativeClosures[original.Frame.Operation].Source=original;return true;}
            if(TryAcceptNativeStationAdoption(original))return true;
            if(!NativeFinalCurrent(original)||!TryNativeFinalDigest(original,out byte kind,out string terminal))return false;
            var value=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=original.Frame.Operation,Birth=original.Frame.Birth,Nonce=original.Nonce,
                World=original.WorldKey,Owner=original.Owner,Creation=original.Creation,Deaths=original.Deaths,Position=original.Station.ToWorldPos(),Kind=kind,
                OriginalDigest=original.Digest,HeaderDigest=Hash(Read(original.Path+".scope",2048)),HoldDigest=Hash(Read(original.Path+".accepted",2048)),TerminalDigest=terminal};
            byte[] bytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(value);
            if(!NativeFinalCurrent(original)||!WriteExact(original.Path+".closeRequested",bytes,4096))return true;
            nativeClosures.Add(value.Operation,new ClosingNative{Source=original,Actor=original.Actor,World=original.World,Root=original.Root,Path=original.Path,Record=value,Bytes=bytes});return true;
        }
        catch{return true;} // A final-source failure never falls back to new output/work.
    }
    private static bool TryAcceptNativeStationAdoption(Original source)
    {
        if(!source.Identity()||source.Nonce==Guid.Empty||!RebirthProductiveEarnedWire.TryReadReply(source.Frame.Operation,out var reply)||
            reply.State!=RebirthProductiveEarnedWire.Status.NativeClosed||reply.QueueOwner!="nativeStationAdopted"||
            reply.Operation!=source.Frame.Operation||reply.Birth!=source.Frame.Birth||reply.Nonce!=source.Nonce||reply.Digest!=source.Digest||
            reply.World!=source.WorldKey||reply.Owner!=source.Owner||reply.Creation!=source.Creation||reply.Deaths!=source.Actor.Died||
            !reply.Position.Equals(source.Station.ToWorldPos()))return false;
        // Authenticated positive host closure describes already-saved original
        // output and transfer of successor custody. It grants no owner effect.
        if(!WriteExact(source.Path+".nativeOutputSaved",StageBytes(source,"nativeOutputSaved",source.Nonce),2048)||
            !WriteExact(source.Path+".nativeStationAdopted",StageBytes(source,"nativeStationAdopted",source.Nonce),2048))return true;
        var record=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=source.Frame.Operation,Birth=source.Frame.Birth,Nonce=source.Nonce,
            World=source.WorldKey,Owner=source.Owner,Creation=source.Creation,Deaths=source.Deaths,Position=source.Station.ToWorldPos(),Kind=1,
            OriginalDigest=source.Digest,HeaderDigest=Hash(Read(source.Path+".scope",2048)),HoldDigest=Hash(Read(source.Path+".accepted",2048)),
            TerminalDigest=Hash(Read(source.Path+".nativeOutputSaved",8192))};
        byte[] bytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(record);
        if(!WriteExact(source.Path+".closeRequested",bytes,4096))return true;
        nativeClosures.Add(record.Operation,new ClosingNative{Source=source,Actor=source.Actor,World=source.World,Root=source.Root,Path=source.Path,Record=record,Bytes=bytes});
        return true;
    }
    private static void PumpNativeClosures(double now)
    {
        foreach(var pair in nativeClosures.ToArray())
        {
            var close=pair.Value;
            if(!close.Current()){nativeClosures.Remove(pair.Key);continue;}
            if(now<close.Next)continue;close.Next=now+1;
            try
            {
                var value=close.Record;
                if(close.Bytes!=null&&(!TryReadNativeClose(close.Path,value.World,value.Owner,value.Creation,out var read,out var bytes)||!bytes.SequenceEqual(close.Bytes)))continue;
                if(RebirthProductiveEarnedWire.TryReadReply(value.Operation,out var reply)&&reply.State==RebirthProductiveEarnedWire.Status.NativeClosed&&
                    reply.Operation==value.Operation&&reply.Birth==value.Birth&&reply.Nonce==value.Nonce&&reply.Digest==value.OriginalDigest&&reply.World==value.World&&
                    reply.Owner==value.Owner&&reply.Creation==value.Creation&&reply.Deaths==close.Actor.Died&&reply.Position.Equals(value.Position))
                {
                    if(close.Bytes==null&&reply.QueueOwner!="nativeStationAdopted")continue;
                    if(reply.QueueOwner=="nativeStationAdopted"&&
                        !WriteExact(close.Path+".nativeStationAdopted",NativeCloseStage(value,"nativeStationAdopted"),2048))continue;
                    if(close.Bytes==null)
                    {
                        if(HashOriginalFile(close.Path)!=value.OriginalDigest||Hash(Read(close.Path+".scope",2048))!=value.HeaderDigest||
                            Hash(Read(close.Path+".accepted",2048))!=value.HoldDigest||
                            !WriteExact(close.Path+".nativeOutputSaved",NativeCloseStage(value,"nativeOutputSaved"),2048))continue;
                        byte[] requested=RebirthProductiveEarnedRemoteClosedRecord.Encode(value);
                        if(!WriteExact(close.Path+".closeRequested",requested,4096))continue;close.Bytes=requested;
                    }
                    if(!WriteExact(close.Path+".closed",close.Bytes,4096))continue;
                    if(close.Source!=null)
                    {
                        if(!ReleaseNativeFinal(close.Source))continue;
                        if(originals.TryGetValue(close.Source.Station,out var retained)&&ReferenceEquals(retained,close.Source))originals.Remove(close.Source.Station);
                    }
                    else if(!ReleaseRecoveredNativeFinal(close.Actor,close.Path,value,close.Bytes))continue;
                    nativeClosures.Remove(pair.Key);RebirthProductiveEarnedWire.ReleaseNativeClosedStatus(value.Operation,value.Nonce,value.OriginalDigest);continue;
                }
                if(RebirthProductiveEarnedWire.SendNativeFinalStatus(close.Actor,value.Position,value.Operation,value.Nonce,value.OriginalDigest)&&close.Bytes==null)
                    WriteExact(close.Path+".nativeAdoptionQueried",NativeCloseStage(value,"nativeAdoptionQueried"),2048);
            }
            catch{/* Only the same compact positive final original is retried. */}
        }
    }
}
