using System;

// Thread-scoped identity input for a future validated prepared-spawn dispatcher.
internal static class RebirthNpcPreparedCreationScope
{
    [ThreadStatic] private static Lease active;
    private sealed class Lease : IDisposable
    {
        internal int EntityClass,NativeId;internal string Profile;internal RebirthNpcStableId Stable;
        internal Func<bool> OriginalVerifier;
        public void Dispose(){if(ReferenceEquals(active,this))active=null;}
    }
    internal static bool TryEnter(int entityClass,string profile,RebirthNpcStableId stable,out IDisposable scope)
    {
        scope=null;
        if(active!=null||stable.IsEmpty||string.IsNullOrWhiteSpace(profile)||profile.Length>128||profile!=profile.Trim())return false;
        foreach(char ch in profile)if(char.IsControl(ch))return false;
        var lease=new Lease{EntityClass=entityClass,Profile=profile,Stable=stable};active=lease;scope=lease;return true;
    }
    internal static bool TryEnter(int entityClass,string profile,RebirthNpcStableId stable,int nativeId,out IDisposable scope)
    {
        scope=null;if(nativeId<=0||!TryEnter(entityClass,profile,stable,out scope))return false;
        active.NativeId=nativeId;return true;
    }
    // Only the durable original-person constructor may supply this validator. A generic
    // identity lease is not evidence that native initialization events should be held.
    internal static bool TryBindOriginalRestoration(Func<bool> verifier)
    {
        var lease=active;
        if(lease==null||lease.NativeId<=0||lease.OriginalVerifier!=null||verifier==null||!verifier())return false;
        lease.OriginalVerifier=verifier;return true;
    }
    internal static bool HasOriginalRestorationLease(int entityClass)
    {var lease=active;return lease!=null&&lease.EntityClass==entityClass&&lease.OriginalVerifier!=null;}
    internal static bool TryGetOriginalRestorationNativeId(int entityClass,out int nativeId)
    {
        nativeId=0;var lease=active;
        if(lease==null||lease.OriginalVerifier==null||!lease.OriginalVerifier())return false;
        return TryGetNativeEntityId(entityClass,out nativeId);
    }
    internal static bool TryGetNativeEntityId(int entityClass,out int nativeId)
    {
        nativeId=0;var lease=active;
        if(lease==null||lease.EntityClass!=entityClass||lease.NativeId<=0)return false;
        nativeId=lease.NativeId;return true;
    }
    internal static bool TryGet(int entityClass,string profile,out RebirthNpcStableId stable)
    {
        stable=default(RebirthNpcStableId);
        var lease=active;
        if(lease==null||lease.EntityClass!=entityClass||!string.Equals(lease.Profile,profile,StringComparison.OrdinalIgnoreCase))return false;
        stable=lease.Stable;return true;
    }
}