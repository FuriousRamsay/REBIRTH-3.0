using System;
using System.Collections.Generic;

// Warm terminal outbox. An uploaded native file may arrive after its request;
// retries retain the same grant/source, never infer success from request order.
internal static class RebirthHydrationRemoteCompletionServer
{
    private sealed class Pending
    {internal RebirthHydrationRemoteGrant Grant;internal RebirthHydrationTransferJournal.RemoteTerminal Terminal;internal bool Running;}
    private static readonly Dictionary<Guid,Pending> pending=new Dictionary<Guid,Pending>();
    internal static bool Acknowledge(World world,int playerId,ClientInfo sender,Guid operation,string digest)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
                manager?.IsServer!=true||sender==null||!sender.loginDone||sender.disconnecting||
                !ReferenceEquals(manager.Clients?.ForEntityId(playerId),sender))return false;
            var player=world.GetEntity(playerId) as EntityPlayer;
            if(player!=null&&RebirthHydrationTransferJournal.TryPersistColdAcknowledgment(player,operation,digest)&&
                RebirthHydrationServerSourceHold.RetireAcknowledgedHistory(player,operation,digest))
            {pending.Remove(operation);return true;}
            if(!RebirthHydrationRemoteGrant.TryGet(player,operation,digest,out var grant)||
                !pending.TryGetValue(operation,out var original)||original.Running||!ReferenceEquals(original.Grant,grant)||original.Terminal==null)return false;
            original.Running=true;
            try
            {
                if(!original.Terminal.CurrentForAcknowledgment(grant.Lease)||!RebirthHydrationTransferJournal.TryPersistAcknowledgment(original.Terminal)||
                    !RebirthHydrationServerSourceHold.TryReleaseAcknowledged(original.Terminal))return false;
                return pending.Remove(operation);
            }
            finally{original.Running=false;}
        }
        catch{return false;}
    }
    internal static bool Receive(World world,int playerId,ClientInfo sender,Guid operation,string digest)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
                manager?.IsServer!=true||sender==null||!sender.loginDone||sender.disconnecting||
                !ReferenceEquals(manager.Clients?.ForEntityId(playerId),sender))return false;
            var player=world.GetEntity(playerId) as EntityPlayer;
            if(!RebirthHydrationRemoteGrant.TryGet(player,operation,digest,out var grant))return false;
            if(!pending.TryGetValue(operation,out var original))
            {
                if(pending.Count>=64)return false;
                original=new Pending{Grant=grant};pending.Add(operation,original);
            }
            if(!ReferenceEquals(original.Grant,grant))
            {
                if(original.Running||original.Grant.Current()||original.Grant.Original.Digest!=grant.Original.Digest||!grant.Lease.Reentered)return false;
                original=new Pending{Grant=grant};pending[operation]=original;
            }
            if(original.Running)return false;
            original.Running=true;
            try
            {
                if(!grant.Current()||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),sender))return false;
                if(original.Terminal==null)
                {
                    if(!RebirthHydrationTransferJournal.RemoteTerminal.TryPublish(grant.Lease,out var terminal))return false;
                    original.Terminal=terminal;
                }
                return original.Terminal.CurrentFor(grant.Lease)&&grant.Current()&&
                    ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&ReferenceEquals(manager.Clients?.ForEntityId(playerId),sender);
            }
            finally{original.Running=false;}
        }
        catch{return false;}
    }
    internal static bool TryGetReady(RebirthHydrationRemoteGrant grant,out RebirthHydrationTransferJournal.RemoteTerminal terminal)
    {
        terminal=null;
        if(grant==null||!grant.Current()||!pending.TryGetValue(grant.Original.Operation,out var original)||original.Running||
            !ReferenceEquals(original.Grant,grant)||original.Terminal==null||!original.Terminal.CurrentFor(grant.Lease))return false;
        terminal=original.Terminal;return true;
    }
}
