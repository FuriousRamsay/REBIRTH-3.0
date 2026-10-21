using System;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationProposalFragment : NetPackage
{
    private int playerId,total,count,index;private Guid operation;private string digest,part;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthHydrationProposalFragment Setup(RebirthHydrationProposalServer.Proposal proposal,int fragmentIndex)
    {
        if(proposal?.Lease==null||!proposal.Current()||!proposal.Lease.Current())throw new InvalidOperationException("Current saved hydration proposal required");
        playerId=proposal.Player.entityId;operation=proposal.Original.Operation;digest=proposal.Body.Hash;total=proposal.Body.Length;count=proposal.Body.Count;index=fragmentIndex;part=proposal.Body.Part(index);return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=part=null;playerId=reader.ReadInt32();var body=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(body,out var id,out var hash))throw new InvalidDataException("Invalid hydration proposal header");
        total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();int length=reader.ReadUInt16();
        if(!Shape(total,count,index)||length<1||length>RebirthRemoteResourceFragments.ChunkText)throw new InvalidDataException("Invalid hydration proposal fragment size");
        byte[] bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new InvalidDataException("Truncated hydration proposal fragment");
        foreach(byte value in bytes)if(value>127)throw new InvalidDataException("Noncanonical hydration proposal fragment text");
        operation=id;digest=hash;part=Encoding.ASCII.GetString(bytes);
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0||!Shape(total,count,index)||part==null||part.Length<1||part.Length>RebirthRemoteResourceFragments.ChunkText)throw new InvalidOperationException("Missing hydration proposal fragment");
        byte[] body=RebirthHydrationOperationFrame.Encode(operation,digest),bytes=Encoding.ASCII.GetBytes(part);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(body);((BinaryWriter)writer).Write(total);((BinaryWriter)writer).Write(count);((BinaryWriter)writer).Write(index);((BinaryWriter)writer).Write((ushort)bytes.Length);((BinaryWriter)writer).Write(bytes);
    }
    private static bool Shape(int total,int count,int index)=>total>0&&total<=RebirthHydrationOriginalTransport.MaximumBytes&&count==(total+RebirthRemoteResourceFragments.ChunkBytes-1)/RebirthRemoteResourceFragments.ChunkBytes&&index>=0&&index<count;
    public int GetLength()=>116+(part?.Length??0);
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthHydrationProposalClient.ReceiveFragment(world,playerId,operation,digest,total,count,index,part);
}
