using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static bool TryHistoricalViewerToolsReply(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,
        Guid operation,Guid nonce,byte[] plan,out RebirthProductiveEarnedWire.Reply reply)
        =>TryHistoricalViewerToolsReply(actor,peer,station.ToWorldPos(),creation,operation,nonce,plan,out reply);
    internal static bool TryHistoricalViewerToolsReply(EntityPlayer actor,ClientInfo peer,Vector3i position,string creation,
        Guid operation,Guid nonce,byte[] plan,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;IDisposable memory=null;
        try
        {
            if(nonce==Guid.Empty||!ViewerToolMetadataCaller(actor,peer,creation)||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var payer))return false;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir()),headerPath=Path.Combine(save,"RebirthProductiveEarned","remote",payer.StorageKey,"native-viewer",operation.ToString("N")+".header");
            if(!File.Exists(headerPath+".nativeToolsSaved"))return false;
            byte[] header=Read(headerPath);Guid birth;
            using(var stream=new MemoryStream(header,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadByte()!=1||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=payer.StorageKey||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512)!=creation||reader.ReadInt32()<0||new Guid(reader.ReadBytes(16))!=operation)return false;
                birth=new Guid(reader.ReadBytes(16));
                if(new Guid(reader.ReadBytes(16))!=nonce||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44)!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world))return false;
                RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                if(!new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()).Equals(position)||stream.Position!=stream.Length)return false;
            }
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;
                string original=Path.Combine(save,"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
                byte[] image=ReadNativeOutputMetadata(original,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);var frame=RebirthProductiveEarnedRowProof.Decode(image);
                if(!decoder.Admit(frame,image.Length)||frame.Operation!=operation||frame.Birth!=birth||frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(headerPath+".nativeTools",plan,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(headerPath+".nativeToolsSaved",System.Text.Encoding.ASCII.GetBytes(Digest(plan))))return false;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*(image.Length+plan.Length)+65536),out memory))return false;
                byte[] source=ReadNativeOutputMetadata(headerPath+".nativeViewerSource",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(!RebirthProductiveEarnedViewerToolSource.TryRead(original,image,source,nonce,out var paid)||
                    !RebirthProductiveEarnedRemoteToolPlan.TryRead(plan,operation,birth,nonce,Digest(image),paid.Native,paid.Offset,out var tools))return false;
                byte[] hold=System.Text.Encoding.UTF8.GetBytes(headerPath+"\n"+Digest(header)+"\n"+Digest(source));
                if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original+".viewer-tools-hold",hold)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original+".viewer-tools-released",System.Text.Encoding.ASCII.GetBytes(Digest(hold)))||
                    !RebirthProductiveEarnedRemoteToolRebaseRecord.TryRead(original,image,tools.After,tools.AfterOffset,out var history,out _))return false;
                using(history)if(history.Header!=headerPath||!history.PlanImage.SequenceEqual(plan))return false;
                reply=new RebirthProductiveEarnedWire.Reply{Operation=operation,Birth=birth,Nonce=nonce,Digest=Digest(image),World=frame.World,Owner=payer.StorageKey,
                    Creation=creation,Deaths=actor.Died,Position=position,State=RebirthProductiveEarnedWire.Status.NativeToolSaved,QueueOwner="ownerSaved"};return true;
            }
        }
        catch{return false;}
        finally{memory?.Dispose();}
    }
    internal static bool ViewerToolMetadataCaller(EntityPlayer actor,ClientInfo peer,string creation)
        =>RebirthProductiveEarnedCoordinator.Enabled&&actor?.world?.IsRemote()==false&&!actor.IsDead()&&actor.IsSpawned()&&
            ReferenceEquals(actor.world,GameManager.Instance?.World)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
            peer?.loginDone==true&&!peer.disconnecting&&peer.netConnection?.Length>0&&peer.netConnection[0]!=null&&!peer.netConnection[0].IsDisconnected()&&
            peer.entityId==actor.entityId&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(actor.entityId),peer)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)&&RebirthOriginalActionScope.Matches(actor,owner.StorageKey,creation);
}
