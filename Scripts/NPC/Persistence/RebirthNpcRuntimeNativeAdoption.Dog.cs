using System;
using System.IO;

internal static partial class RebirthNpcRuntimeNativeAdoption
{
    private sealed class DogBoundary
    {internal EntityRebirthDogCompanion Actor;internal Stream Stream;internal long Start;internal Frame Frame;internal bool Writing;}
    internal static object EnterDogRead(EntityRebirthDogCompanion actor,PooledBinaryReader reader,StreamModeRead mode)
        =>mode==StreamModeRead.Persistency?EnterDog(actor,reader?.BaseStream,false):null;
    internal static object EnterDogWrite(EntityRebirthDogCompanion actor,PooledBinaryWriter writer,StreamModeWrite mode)
        =>mode==StreamModeWrite.Persistency?EnterDog(actor,writer?.BaseStream,true):null;
    private static object EnterDog(EntityRebirthDogCompanion actor,Stream stream,bool writing)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor==null||stream==null||!stream.CanRead||!stream.CanSeek||
                !entries.TryGetValue(actor,out var entry))return null;
            var frame=writing?entry.Written:entry.Read;
            if(frame==null||!ReferenceEquals(frame.World,GameManager.Instance?.World)||frame.DogBytes!=null)return null;
            return new DogBoundary{Actor=actor,Stream=stream,Start=stream.Position,Frame=frame,Writing=writing};
        }
        catch{return null;}
    }
    internal static void ObserveDogRead(EntityRebirthDogCompanion actor,PooledBinaryReader reader,object state,RebirthNpcStableId stable,string owner)
        =>ObserveDog(actor,reader?.BaseStream,state,stable,owner,false);
    internal static void ObserveDogWrite(EntityRebirthDogCompanion actor,PooledBinaryWriter writer,object state,RebirthNpcStableId stable,string owner)
        =>ObserveDog(actor,writer?.BaseStream,state,stable,owner,true);
    private static void ObserveDog(EntityRebirthDogCompanion actor,Stream stream,object state,RebirthNpcStableId stable,string owner,bool writing)
    {
        try
        {
            if(state is not DogBoundary source||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(source.Stream,stream)||source.Writing!=writing||
                !actor.NativeIdentityReadable||stable.IsEmpty||stable!=source.Frame.Stable||owner==null||owner.Length>4096||
                !ReferenceEquals(source.Frame.World,GameManager.Instance?.World)||
                !RebirthNpcSaveQuarantineScope.SameDirectory(source.Frame.Directory,GameIO.GetSaveGameDir())||
                !entries.TryGetValue(actor,out var entry)||!ReferenceEquals(writing?entry.Written:entry.Read,source.Frame))return;
            long end=stream.Position,length=end-source.Start;if(length<22||length>16384)return;
            var bytes=new byte[(int)length];stream.Position=source.Start;
            try{int offset=0;while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<=0)return;offset+=read;}}
            finally{stream.Position=end;}
            using(var input=new MemoryStream(bytes,false))using(var reader=new BinaryReader(input))
            {
                if(reader.ReadInt32()!=0x52424431||reader.ReadByte()!=1||
                    new RebirthNpcStableId(reader.ReadUInt64(),reader.ReadUInt64())!=stable||reader.ReadString()!=owner||input.Position!=input.Length)return;
            }
            if(writing&&(actor.RebirthRuntimeState?.StableId!=stable||(actor.RebirthRuntimeState?.OwnerId??string.Empty)!=owner||
                RuntimeImage(actor.RebirthRuntimeState)!=source.Frame.Runtime))return;
            source.Frame.DogBytes=bytes;source.Frame.DogOwner=owner;
        }
        catch{}
    }
}
