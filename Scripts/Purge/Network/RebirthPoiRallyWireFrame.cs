using System;
using System.IO;
using System.Text;
using UnityEngine;

internal enum RebirthPoiRallyWireStatus:byte { Request=0,Refused=1,Completed=2,Unknown=3 }
// Detached request/response scope. Only the native authenticated server producer
// may create a Completed response; decoding alone confers no reset authority.
internal sealed class RebirthPoiRallyWireFrame
{
    internal const int MaximumBytes=4096;
    internal readonly Guid Request,World;
    internal readonly string QuestId,Unique;
    internal readonly int Player,QuestCode;
    internal readonly byte Phase;
    internal readonly Vector3 Poi;
    internal readonly RebirthPoiRallyWireStatus Status;
    internal readonly long Revision;
    internal RebirthPoiRallyWireFrame(Guid request,Guid world,int player,string questId,string unique,int questCode,byte phase,Vector3 poi,RebirthPoiRallyWireStatus status=RebirthPoiRallyWireStatus.Request,long revision=0)
    {
        if(request==Guid.Empty || world==Guid.Empty || player<0 || !RebirthPoiAuthenticatedRallyScope.ValidIdentifier(questId,256) || !RebirthPoiAuthenticatedRallyScope.ValidIdentifier(unique,512) || !Enum.IsDefined(typeof(RebirthPoiRallyWireStatus),status) || (status==RebirthPoiRallyWireStatus.Completed?revision<1:revision!=0) || !Finite(poi.x) || !Finite(poi.y) || !Finite(poi.z))throw new ArgumentException("Invalid original rally wire scope.");
        Request=request;World=world;Player=player;QuestId=questId;Unique=unique;QuestCode=questCode;Phase=phase;Poi=poi;Status=status;Revision=revision;
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    internal bool SameRequest(RebirthPoiRallyWireFrame other)=>other!=null && Request==other.Request && World==other.World && Player==other.Player && QuestId==other.QuestId && Unique==other.Unique && QuestCode==other.QuestCode && Phase==other.Phase && Poi.Equals(other.Poi);
    internal RebirthPoiRallyWireFrame Reply(RebirthPoiRallyWireStatus status,long revision=0)=>new RebirthPoiRallyWireFrame(Request,World,Player,QuestId,Unique,QuestCode,Phase,Poi,status,revision);
    internal static byte[] Encode(RebirthPoiRallyWireFrame value)
    {
        if(value==null)throw new ArgumentNullException(nameof(value));
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true))
        {
            writer.Write(0x52505231);writer.Write((byte)value.Status);writer.Write(value.Revision);writer.Write(value.Request.ToByteArray());writer.Write(value.World.ToByteArray());writer.Write(value.Player);writer.Write(value.QuestCode);writer.Write(value.Phase);writer.Write(value.Poi.x);writer.Write(value.Poi.y);writer.Write(value.Poi.z);
            foreach(var id in new[]{value.QuestId,value.Unique}){byte[] bytes=new UTF8Encoding(false,true).GetBytes(id);writer.Write((ushort)bytes.Length);writer.Write(bytes);}
            writer.Flush();if(stream.Length>MaximumBytes)throw new ArgumentException("Oversized rally wire scope.");return stream.ToArray();
        }
    }
    internal static bool TryDecode(byte[] bytes,out RebirthPoiRallyWireFrame value)
    {
        value=null;if(bytes==null || bytes.Length<72 || bytes.Length>MaximumBytes)return false;
        try
        {
            using(var stream=new MemoryStream((byte[])bytes.Clone(),false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true))
            {
                if(reader.ReadInt32()!=0x52505231)return false;
                var status=(RebirthPoiRallyWireStatus)reader.ReadByte();long revision=reader.ReadInt64();
                var request=new Guid(reader.ReadBytes(16));var world=new Guid(reader.ReadBytes(16));int player=reader.ReadInt32(),code=reader.ReadInt32();byte phase=reader.ReadByte();var poi=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                var ids=new string[2];for(int n=0;n<2;n++){int size=reader.ReadUInt16();if(size<1 || size>2048 || size>stream.Length-stream.Position)return false;ids[n]=new UTF8Encoding(false,true).GetString(reader.ReadBytes(size));}
                if(stream.Position!=stream.Length)return false;value=new RebirthPoiRallyWireFrame(request,world,player,ids[0],ids[1],code,phase,poi,status,revision);return true;
            }
        }
        catch(ArgumentException){return false;}catch(IOException){return false;}
    }
}