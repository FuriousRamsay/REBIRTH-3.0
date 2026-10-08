using System;
using System.IO;

// Owner display response only. Unavailable never clears an existing display.
public sealed class RebirthBackpackSellStashViewResponse
{
    public string CreationId {get;private set;}
    public Guid RequestId {get;private set;}
    public long Revision {get;private set;}
    public RebirthBackpackLibraryViewStatus Status {get;private set;}
    private readonly RebirthBackpackSellStashView view;
    private readonly byte[] data;
    public int BodyLength=>(Guid.TryParse(CreationId,out _)?47:103)+data.Length;
    private RebirthBackpackSellStashViewResponse(string creation,Guid request,long revision,RebirthBackpackLibraryViewStatus status,RebirthBackpackSellStashView value,byte[] bytes)
    {CreationId=creation;RequestId=request;Revision=revision;Status=status;view=value;data=(byte[])bytes.Clone();}
    public static bool TryCreate(Guid creation,Guid request,long revision,RebirthBackpackLibraryViewStatus status,RebirthBackpackSellStashView view,out RebirthBackpackSellStashViewResponse response)
        =>TryCreate(creation.ToString("N"),request,revision,status,view,out response);
    public static bool TryCreate(string creation,Guid request,long revision,RebirthBackpackLibraryViewStatus status,RebirthBackpackSellStashView view,out RebirthBackpackSellStashViewResponse response)
    {
        response=null;if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||request==Guid.Empty)return false;
        byte[] bytes=new byte[0];
        if(status==RebirthBackpackLibraryViewStatus.Ready)
        {
            if(view==null||view.CreationId!=normalized||view.GearRevision!=revision||
                !RebirthBackpackSellStashViewCodec.TryEncode(view,out bytes))return false;
        }
        else if(status==RebirthBackpackLibraryViewStatus.NoBackpack){if(view!=null||revision<0)return false;}
        else if(status==RebirthBackpackLibraryViewStatus.Unavailable){if(view!=null||revision!=-1)return false;}
        else return false;
        response=new RebirthBackpackSellStashViewResponse(normalized,request,revision,status,view,bytes);return true;
    }
    public void Write(BinaryWriter writer)
    {
        writer.Write((byte)83);bool guid=Guid.TryParse(CreationId,out var creation);writer.Write((byte)(guid?1:2));
        if(guid)writer.Write(creation.ToByteArray());
        else {writer.Write((byte)71);writer.Write(System.Text.Encoding.ASCII.GetBytes(CreationId));}
        writer.Write(RequestId.ToByteArray());
        writer.Write(Revision);writer.Write((byte)Status);writer.Write(data.Length);writer.Write(data);
    }
    public static bool TryRead(BinaryReader reader,out RebirthBackpackSellStashViewResponse response)
    {
        response=null;if(reader==null)return false;
        try
        {
            if(reader.ReadByte()!=83)return false;byte version=reader.ReadByte();string creation;
            if(version==1)creation=new Guid(reader.ReadBytes(16)).ToString("N");
            else if(version==2){if(reader.ReadByte()!=71)return false;var id=reader.ReadBytes(71);if(id.Length!=71)return false;creation=System.Text.Encoding.ASCII.GetString(id);if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||normalized!=creation||Guid.TryParse(creation,out _))return false;}
            else return false;
            var request=new Guid(reader.ReadBytes(16));
            long revision=reader.ReadInt64();var status=(RebirthBackpackLibraryViewStatus)reader.ReadByte();int length=reader.ReadInt32();
            if(length<0||length>RebirthBackpackSellStashViewCodec.MaxBytes)return false;
            var bytes=reader.ReadBytes(length);if(bytes.Length!=length)return false;RebirthBackpackSellStashView view=null;
            if(status==RebirthBackpackLibraryViewStatus.Ready){if(!RebirthBackpackSellStashViewCodec.TryDecode(bytes,out view))return false;}
            else if(length!=0)return false;
            return TryCreate(creation,request,revision,status,view,out response);
        }
        catch{return false;}
    }
    public bool Deliver(RebirthBackpackSellStashViewCache cache,object session)
    {
        if(cache==null)return false;
        if(Status==RebirthBackpackLibraryViewStatus.Ready)return cache.Receive(session,CreationId,RequestId,view);
        return Status==RebirthBackpackLibraryViewStatus.NoBackpack&&cache.ReceiveNoBackpack(session,CreationId,RequestId,Revision);
    }
}