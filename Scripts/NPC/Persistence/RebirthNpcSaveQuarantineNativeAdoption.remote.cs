using System;
using System.Collections.Generic;

public static partial class RebirthNpcRemoteCommandService
{
    private sealed class RemoteNativeCustody
    {
        internal World World,Incoming;internal Dictionary<string,ulong> Nonces;internal Queue<string> Order;
        internal Dictionary<ulong,PendingRemote> Pending;internal readonly HashSet<ulong> Completed=new HashSet<ulong>();
        internal RebirthNpcNativeTransientAdoption Receipt;
        internal bool HasPending {get{foreach(ulong id in Pending.Keys)if(!Completed.Contains(id))return true;return false;}}
        internal bool Holds(string user)
        {if(!HasPending)return false;foreach(string key in Nonces.Keys)if(key.StartsWith(user+"|",StringComparison.Ordinal))return true;return false;}
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>StageScope(source.World);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>source?.Current()==true&&Pending.Count==0&&StageScope(source.World);
        private bool StageScope(World incoming)
        {
            lock(Sync)
            {
                if(Incoming!=null)return ReferenceEquals(Incoming,incoming)&&ReferenceEquals(remoteCommandWorld,Incoming);
                if(!CanRetireEmptyRemoteCommands||remoteCommandWorld!=null)return false;
                foreach(string key in Order){LastNonceByUser.Add(key,Nonces[key]);ReplayOrder.Enqueue(key);}
                remoteCommandWorld=Incoming=incoming;remoteNativeCustodies.Add(this);
                // The captured connection epochs and client callbacks stay in
                // Pending. They are never remapped onto a different connection.
                return true;
            }
        }
    }
    private static readonly List<RemoteNativeCustody> remoteNativeCustodies=new List<RemoteNativeCustody>();
    private static bool HasRetainedRemoteActor(string user)
    {foreach(var custody in remoteNativeCustodies)if(!ReferenceEquals(custody.World,GameManager.Instance?.World)&&custody.Holds(user))return true;return false;}
    private static void ObserveNativeRetainedTerminal(RebirthNpcCommandRecord record)
    {
        if(record==null||!RebirthNpcCommandGateway.TryGet(record.Request.CommandId,out var current)||!ReferenceEquals(record,current)||
            record.Status==RebirthNpcCommandStatus.Submitted||record.Status==RebirthNpcCommandStatus.Validated||record.Status==RebirthNpcCommandStatus.Queued||
            record.Status==RebirthNpcCommandStatus.Executing||record.Status==RebirthNpcCommandStatus.RetryScheduled)return;
        lock(Sync)foreach(var custody in remoteNativeCustodies)if(ReferenceEquals(custody.Incoming,GameManager.Instance?.World)&&custody.Pending.ContainsKey(record.Request.CommandId))
            custody.Completed.Add(record.Request.CommandId);
    }
    internal static RebirthNpcNativeTransientAdoption CaptureNativeRemoteCommandAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||remoteCommandWorld!=null&&!ReferenceEquals(remoteCommandWorld,scope.World))return null;
            var custody=new RemoteNativeCustody{World=scope.World,Nonces=LastNonceByUser,Order=ReplayOrder,Pending=PendingByCommand};
            var nonces=LastNonceByUser;var order=ReplayOrder;var pending=PendingByCommand;ulong allocator=nextClientNonce;string image=RemoteCommandImage().OuterXml;
            bool Original(){lock(Sync)return ReferenceEquals(LastNonceByUser,nonces)&&ReferenceEquals(ReplayOrder,order)&&ReferenceEquals(PendingByCommand,pending)&&
                nextClientNonce==allocator&&RemoteCommandImage().OuterXml==image;}
            bool Available(){lock(Sync)return custody.Incoming==null?CanRetireEmptyRemoteCommands&&remoteCommandWorld==null:ReferenceEquals(remoteCommandWorld,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("remote-command-routing",scope,Original,Available,()=>custody.HasPending,source=>{},custody.Stage,image.Length,()=>remoteNativeCustodies.Remove(custody),ownerStage:custody.StageOwnerScope);
            custody.Receipt=receipt;return receipt;
        }
    }
}
