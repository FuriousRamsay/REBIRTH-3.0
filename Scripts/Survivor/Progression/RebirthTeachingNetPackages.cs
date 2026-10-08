using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthTeachingSubjectsRequest : NetPackage
{
    private int instructorId, studentId; private long requestId;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthTeachingSubjectsRequest Setup(int instructor,int student,long request){instructorId=instructor;studentId=student;requestId=request;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;instructorId=b.ReadInt32();studentId=b.ReadInt32();requestId=b.ReadInt64();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(instructorId);b.Write(studentId);b.Write(requestId);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||!ValidEntityIdForSender(instructorId))return;RebirthTeachingService.ProcessSubjectsRequest(instructorId,studentId,requestId);}
    public int GetLength(){return 24;}
}

[Preserve]
public sealed class NetPackageRebirthTeachingSubjectsResponse : NetPackage
{
    private long requestId; private int studentId; private string targetName=string.Empty; private readonly List<string> skillIds=new List<string>();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthTeachingSubjectsResponse Setup(long request,int student,string name,IList<string> skills){requestId=request;studentId=student;targetName=name??string.Empty;skillIds.Clear();if(skills!=null)for(int i=0;i<skills.Count&&i<64;i++)if(!string.IsNullOrEmpty(skills[i]))skillIds.Add(skills[i]);return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;requestId=b.ReadInt64();studentId=b.ReadInt32();targetName=RebirthSurvivorNetworkCodec.ReadString(b,64);skillIds.Clear();int count=Math.Min(64,(int)b.ReadByte());for(int i=0;i<count;i++)skillIds.Add(RebirthSurvivorNetworkCodec.ReadString(b,RebirthSurvivorNetworkProtocol.MaxIdLength));}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(requestId);b.Write(studentId);RebirthSurvivorNetworkCodec.WriteString(b,targetName,64);b.Write((byte)Math.Min(64,skillIds.Count));for(int i=0;i<skillIds.Count&&i<64;i++)RebirthSurvivorNetworkCodec.WriteString(b,skillIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthTeachingUiService.ReceiveSubjects(requestId,studentId,targetName,skillIds.ToArray());}
    public int GetLength(){int n=24+RebirthSurvivorNetworkCodec.EstimateString(targetName,64);for(int i=0;i<skillIds.Count;i++)n+=RebirthSurvivorNetworkCodec.EstimateString(skillIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);return n;}
}

[Preserve]
public sealed class NetPackageRebirthTeachingOfferRequest : NetPackage
{
    private int instructorId,studentId;private string skillId=string.Empty;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthTeachingOfferRequest Setup(int instructor,int student,string skill){instructorId=instructor;studentId=student;skillId=skill??string.Empty;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;instructorId=b.ReadInt32();studentId=b.ReadInt32();skillId=RebirthSurvivorNetworkCodec.ReadString(b,RebirthSurvivorNetworkProtocol.MaxIdLength);}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(instructorId);b.Write(studentId);RebirthSurvivorNetworkCodec.WriteString(b,skillId,RebirthSurvivorNetworkProtocol.MaxIdLength);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||!ValidEntityIdForSender(instructorId))return;RebirthTeachingService.ProcessOfferRequest(instructorId,studentId,skillId);}
    public int GetLength(){return 18+RebirthSurvivorNetworkCodec.EstimateString(skillId,RebirthSurvivorNetworkProtocol.MaxIdLength);}
}

[Preserve]
public sealed class NetPackageRebirthTeachingOffer : NetPackage
{
    private long offerId;private int instructorId;private string instructorName=string.Empty,skillId=string.Empty;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthTeachingOffer Setup(long offer,int instructor,string name,string skill){offerId=offer;instructorId=instructor;instructorName=name??string.Empty;skillId=skill??string.Empty;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;offerId=b.ReadInt64();instructorId=b.ReadInt32();instructorName=RebirthSurvivorNetworkCodec.ReadString(b,64);skillId=RebirthSurvivorNetworkCodec.ReadString(b,RebirthSurvivorNetworkProtocol.MaxIdLength);}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(offerId);b.Write(instructorId);RebirthSurvivorNetworkCodec.WriteString(b,instructorName,64);RebirthSurvivorNetworkCodec.WriteString(b,skillId,RebirthSurvivorNetworkProtocol.MaxIdLength);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthTeachingOfferUiService.Receive(offerId,instructorId,instructorName,skillId);}
    public int GetLength(){return 24+RebirthSurvivorNetworkCodec.EstimateString(instructorName,64)+RebirthSurvivorNetworkCodec.EstimateString(skillId,RebirthSurvivorNetworkProtocol.MaxIdLength);}
}

[Preserve]
public sealed class NetPackageRebirthTeachingOfferResponse : NetPackage
{
    private int studentId;private long offerId;private bool accept;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthTeachingOfferResponse Setup(int student,long offer,bool accepted){studentId=student;offerId=offer;accept=accepted;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;studentId=b.ReadInt32();offerId=b.ReadInt64();accept=b.ReadBoolean();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(studentId);b.Write(offerId);b.Write(accept);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||!ValidEntityIdForSender(studentId))return;RebirthTeachingService.ProcessOfferResponse(studentId,offerId,accept);}
    public int GetLength(){return 21;}
}
