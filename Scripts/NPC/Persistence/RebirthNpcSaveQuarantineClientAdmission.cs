using System;
using System.Collections.Generic;

internal static partial class RebirthNpcSaveQuarantineBundle
{
    private static bool IsDetachedClientMembership(Retained value)
    {
        if(value==null||!value.Detached||value.RollbackIncomplete||value.Original?.IsRemote()!=true||value.Capture!=null||
            value.Fence!=null||value.ClientRetirement==null||value.Domains?.Length!=ClientProjectionMembers.Length)return false;
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var domain in value.Domains)if(domain==null||!domain.IsDetached||!names.Add(domain.Name))return false;
        return Array.TrueForAll(ClientProjectionMembers,names.Contains);
    }
    internal static void ObserveClientNativeReentry(RebirthNpcClientNativeReentry.Source source)
    {
        if(!AdmissionEnabled||!ThreadManager.IsMainThread()||transferring||source?.Current()!=true||
            !CanObserveIncomingClientNativeScope(source))return;
        foreach(var value in retained)
        {
            if(!SameDirectory(value.Directory,source.Directory)||!IsDetachedClientMembership(value)||
                value.ClientNativeOriginals==null||value.ClientNativeHolds==null||value.TradeObservationRetirement==null||
                !value.ClientRetirement.MatchesIncomingNamespace(source)||!value.ClientRetirement.Matches(value.Original,value.Directory)||
                !Array.TrueForAll(value.ClientNativeOriginals,source.MatchesTrade)||!Array.TrueForAll(value.ClientNativeHolds,source.MatchesTrade)||
                !value.TradeObservationRetirement.MatchesIncomingAdmission(source))continue;
            // Received native Read/PlayerId/ToPlayer and current authenticated
            // transport qualify this fresh projection namespace. Original
            // financial custody is neither retired nor marked released.
            if(source.Current())value.ClientIncomingSource=source;
        }
    }
    private static bool CanObserveIncomingClientNativeScope(RebirthNpcClientNativeReentry.Source source)
    {
        if(source?.Current()!=true)return false;
        if(HasQualifiedIncomingClient(source.World,source.Directory))return true;
        foreach(var value in retained)
            if(IsDetachedClientMembership(value)&&SameDirectory(value.Directory,source.Directory)&&
                !ReferenceEquals(value.Original,source.World)&&
                CanInstallIncomingClientEpoch(value.Original,value.Directory,source.World,source.Directory))return true;
        // A different outgoing incarnation can own the currently registered
        // boundary while the matching destination group remains retained.
        return clientBoundary?.ObservesIncomingClient(source.World,source.Directory)==true||
            serverBoundary?.ObservesIncomingClient(source.World,source.Directory)==true;
    }
    private static bool HasQualifiedIncomingClient(World incoming,string directory)
    {
        if(!AdmissionEnabled||!ThreadManager.IsMainThread()||transferring||incoming?.IsRemote()!=true||
            !ReferenceEquals(incoming,GameManager.Instance?.World))return false;
        bool found=false;
        foreach(var value in retained)
        {
            if(value.RollbackIncomplete)return false;
            if(!value.Detached||!SameDirectory(value.Directory,directory))continue;
            if(!IsDetachedClientMembership(value)||value.ClientIncomingSource?.ScopeCurrent()!=true||
                !ReferenceEquals(value.ClientIncomingSource.World,incoming)||!SameDirectory(value.ClientIncomingSource.Directory,directory))return false;
            found=true;
        }
        return found;
    }
}
