using System;

// Native EntityBuffs.Write persists nonzero underscore-prefixed CVars. Eight ushort+1
// segments are exactly representable as float; dot-prefixed vars would not survive save.
internal static class RebirthPoiNativeActorStamp
{
    private const string Prefix="_rbPoiActorGuid";
    internal static bool TryRead(EntityAlive entity,out Guid token)
    {
        token=Guid.Empty;
        if(entity==null || entity.Buffs==null)return false;
        byte[] bytes=new byte[16];
        for(int i=0;i<8;i++)
        {
            string key=Prefix+i;
            if(!entity.Buffs.HasCustomVar(key))return false;
            float value=entity.Buffs.GetCustomVar(key);
            if(float.IsNaN(value)||float.IsInfinity(value)||value<1||value>65536||value!=(float)Math.Floor(value))return false;
            int segment=(int)value-1;bytes[2*i]=(byte)segment;bytes[2*i+1]=(byte)(segment>>8);
        }
        token=new Guid(bytes);return token!=Guid.Empty;
    }
    // Only call after exact successful server-world native enrollment. Existing partial or
    // malformed stamp is refused, never silently repaired or minted into a new identity.
    internal static bool TryAssignForVerifiedEnrollment(EntityAlive entity,out Guid token)
    {
        token=Guid.Empty;
        if(!ThreadManager.IsMainThread()||entity==null||entity.Buffs==null||entity.IsDead()||
            !(entity.world is World world)||world.IsRemote()||GameManager.Instance==null||
            !ReferenceEquals(GameManager.Instance.World,world)||!ReferenceEquals(world.GetEntity(entity.entityId),entity))return false;
        if(TryRead(entity,out token))return true;
        for(int i=0;i<8;i++)if(entity.Buffs.HasCustomVar(Prefix+i))return false;
        var original=Guid.NewGuid();byte[] bytes=original.ToByteArray();
        for(int i=0;i<8;i++)entity.Buffs.SetCustomVar(Prefix+i,(bytes[2*i]|bytes[2*i+1]<<8)+1,false);
        return TryRead(entity,out token)&&token==original;
    }
    internal static bool TryAssignVerifiedRestoration(EntityAlive entity,Guid expected,out Guid token)
    {
        token=Guid.Empty;
        if(expected==Guid.Empty||!ThreadManager.IsMainThread()||entity==null||entity.Buffs==null||entity.IsDead()||
            !(entity.world is World world)||world.IsRemote()||GameManager.Instance==null||
            !ReferenceEquals(GameManager.Instance.World,world)||!ReferenceEquals(world.GetEntity(entity.entityId),entity))return false;
        if(TryRead(entity,out token))return token==expected;
        for(int i=0;i<8;i++)if(entity.Buffs.HasCustomVar(Prefix+i))return false;
        byte[] bytes=expected.ToByteArray();for(int i=0;i<8;i++)entity.Buffs.SetCustomVar(Prefix+i,(bytes[2*i]|bytes[2*i+1]<<8)+1,false);
        return TryRead(entity,out token)&&token==expected;
    }
}


