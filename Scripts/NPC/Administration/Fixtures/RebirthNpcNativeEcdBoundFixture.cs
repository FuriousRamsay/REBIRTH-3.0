#if DEBUG
using System;
using System.IO;

// PREPARED NOT RUN. Actual native ECD.write/read and installed Harmony prefix.
// Only detached native snapshots are padded; no live NPC/item/receipt is edited.
internal static class RebirthNpcNativeEcdBoundFixture
{
    internal static void Verify(string confirmation,EntityRebirthNPC actor)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);
        var world=GameManager.Instance?.World;
        Require(actor!=null&&ReferenceEquals(actor.world,world)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&actor.NativeIdentityReadable);
        Require(RebirthNpcNativeStableIdentityCodec.NativeGuardInstalled);
        var creation=new EntityCreationData(actor,StreamModeWrite.Persistency);
        byte[] original=creation.entityData.ToArray();Require(original.Length>0&&original.Length<=ushort.MaxValue);
        try
        {
            var boundary=new byte[ushort.MaxValue];Array.Copy(original,boundary,original.Length);
            creation.entityData=new MemoryStream(boundary,false);
            byte[] wire=Write(creation);
            var read=new EntityCreationData();
            using(var stream=new MemoryStream(wire))using(var reader=MemoryPools.poolBinaryReader.AllocSync(_bReset:false))
            {
                reader.SetBaseStream(stream);read.read(reader,StreamModeRead.Persistency);
                Require(stream.Position==stream.Length&&read.entityData.Length==ushort.MaxValue);
                byte[] actual=read.entityData.ToArray();Require(actual.Length==boundary.Length);
                for(int i=0;i<actual.Length;i++)Require(actual[i]==boundary[i]);
            }
            // Re-keying a detached native ECD to -1 must not bypass exact NPC
            // class classification. This is the native clone/spawn shape.
            creation.id=-1;
            var overflow=new byte[ushort.MaxValue+1];Array.Copy(original,overflow,original.Length);
            creation.entityData=new MemoryStream(overflow,false);
            using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(_bReset:false))
            {
                writer.SetBaseStream(stream);bool rejected=false;
                try{creation.write(writer,StreamModeWrite.Persistency);}catch(InvalidDataException){rejected=true;}
                Require(rejected&&stream.Position==0&&stream.Length==0&&creation.entityData.Length==65536);
            }
            creation.entityData=null;
            using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(_bReset:false))
            {
                writer.SetBaseStream(stream);bool rejected=false;
                try{creation.write(writer,StreamModeWrite.Persistency);}catch(InvalidDataException){rejected=true;}
                Require(rejected&&stream.Position==0);
            }
        }
        finally
        {
            Require(ReferenceEquals(world.GetEntity(actor.entityId),actor));
            Require(actor.NativeIdentityReadable); // no ApplyToEntity ever called
        }
    }
    private static byte[] Write(EntityCreationData value)
    {
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(_bReset:false))
        {writer.SetBaseStream(stream);value.write(writer,StreamModeWrite.Persistency);writer.Flush();return stream.ToArray();}
    }
    private static void Require(bool condition){if(!condition)throw new InvalidOperationException("Actual native NPC ECD bound fixture failed");}
}

#endif
