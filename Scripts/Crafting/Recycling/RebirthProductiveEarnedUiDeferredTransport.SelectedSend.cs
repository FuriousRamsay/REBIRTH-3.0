using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private static bool selectedSendInstalled;
    internal static void InstallSelectedSendObserver()
    {
        if(selectedSendInstalled)return;
        var harmony=new Harmony("rebirth.productive.ui.deferred.native.transport");
        foreach(var type in new[]{typeof(NativeSetupSource),typeof(NativeSourceFree),typeof(ReceiveDequeue),typeof(DirectSend),typeof(BroadcastSend),
            typeof(BroadcastListSend),typeof(SimpleSend),typeof(SteamSend),typeof(RebirthProductiveEarnedUiOwnedNativeSend.ClientSend)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        selectedSendInstalled=true;
    }
    private sealed class OuterSend
    {internal OuterSend Previous;internal readonly List<NetPackage> Held=new List<NetPackage>();}
    [ThreadStatic]private static OuterSend outerSend;
    private static OuterSend BeginOuter(IEnumerable<NetPackage> packets,bool run)
    {
        if(!run||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return null;
        var state=new OuterSend{Previous=outerSend};
        try
        {
            foreach(var packet in packets)
                if(packet is NetPackageTileEntity native&&(RebirthProductiveEarnedUiRemote.IsQueueResponseCandidate(native)||selectedNativeSources.TryGetValue(native,out var source)&&source.Candidate()))
                {state.Held.Add(packet);packet.RegisterSendQueue();}
            if(state.Held.Count==0)return null;outerSend=state;return state;
        }
        catch{foreach(var packet in state.Held)packet.SendQueueHandled();return null;}
    }
    private static Exception EndOuter(Exception exception,OuterSend state)
    {
        if(state==null)return exception;
        if(ReferenceEquals(outerSend,state))outerSend=state.Previous;else outerSend=null;
        foreach(var packet in state.Held)try{packet.SendQueueHandled();}catch{/* Preserve original send failure. */}
        state.Held.Clear();return exception;
    }
    [HarmonyPatch(typeof(ClientInfo),nameof(ClientInfo.SendPackage),new[]{typeof(NetPackage)})]
    private static class DirectSend
    {
        private static void Prefix(NetPackage _package,bool __runOriginal,out OuterSend __state)=>__state=BeginOuter(new[]{_package},__runOriginal);
        private static Exception Finalizer(Exception __exception,OuterSend __state)=>EndOuter(__exception,__state);
    }
    [HarmonyPatch(typeof(ConnectionManager),nameof(ConnectionManager.SendPackage),new[]{typeof(NetPackage),typeof(bool),typeof(int),typeof(int),typeof(int),typeof(UnityEngine.Vector3?),typeof(int),typeof(bool)})]
    private static class BroadcastSend
    {
        private static void Prefix(NetPackage _package,bool __runOriginal,out OuterSend __state)=>__state=BeginOuter(new[]{_package},__runOriginal);
        private static Exception Finalizer(Exception __exception,OuterSend __state)=>EndOuter(__exception,__state);
    }
    [HarmonyPatch(typeof(ConnectionManager),nameof(ConnectionManager.SendPackage),new[]{typeof(List<NetPackage>),typeof(bool),typeof(int),typeof(int),typeof(int),typeof(UnityEngine.Vector3?),typeof(int),typeof(bool)})]
    private static class BroadcastListSend
    {
        private static void Prefix(List<NetPackage> _packages,bool __runOriginal,out OuterSend __state)=>__state=BeginOuter(_packages??new List<NetPackage>(),__runOriginal);
        private static Exception Finalizer(Exception __exception,OuterSend __state)=>EndOuter(__exception,__state);
    }
    private sealed class GroupSend
    {
        internal NetConnectionAbs Connection;internal NetPackage[] Packets;internal object Gate;internal bool Locked;
        internal bool NativeUpload;
        internal int ExistingCount;internal bool[] Ran;internal int[] InitialReferences;internal GroupSend Previous;
    }
    private sealed class MemberSend {internal GroupSend Group;internal int Index;}
    private sealed class SendScopes {internal MemberSend Selected;internal object RemoteInsertion,RemoteClose;internal NetConnectionAbs Connection;internal NetPackage Packet;}
    private static bool EnterSendScopes(NetConnectionAbs connection,NetPackage packet,bool run,out SendScopes state)
    {
        state=new SendScopes{Connection=connection,Packet=packet};
        EnterRemoteRecipientCloseSend(connection,packet,run,out state.RemoteClose);
        RebirthProductiveEarnedUiQueueOwner.EnterRemoteNativeSend(connection,packet,run,out state.RemoteInsertion);
        return EnterSelectedSend(connection,packet,run,out state.Selected);
    }
    private static void ObserveSendScopes(bool run,SendScopes state)
    {
        ObserveMember(run,state?.Selected);
        ObserveRemoteRecipientCloseSend(run,state?.RemoteClose);
        if(state!=null)RebirthProductiveEarnedUiQueueOwner.ObserveRemoteNativeSend(state.Connection,state.Packet,run,state.RemoteInsertion);
    }
    private static Exception EndSendScopes(Exception exception,SendScopes state)
    {RebirthProductiveEarnedUiQueueOwner.EndRemoteNativeSend(state?.RemoteInsertion);EndRemoteRecipientCloseSend(state?.RemoteClose);return exception;}
    [ThreadStatic]private static GroupSend sendingGroup;
    private static object QueueGate(NetConnectionAbs connection)=>connection is NetConnectionSimple simple?simple.writerListLockObj:
        connection is NetConnectionSteam steam?steam.packetsToSend:null;
    private static NetPackage[] QueueImage(NetConnectionAbs connection)=>connection is NetConnectionSimple simple?simple.writerListPending.ToArray():
        connection is NetConnectionSteam steam?steam.packetsToSend.ToArray():Array.Empty<NetPackage>();
    private static void RestoreQueue(NetConnectionAbs connection,NetPackage[] image)
    {
        if(connection is NetConnectionSimple simple){simple.writerListPending.Clear();simple.writerListPending.AddRange(image);}
        else if(connection is NetConnectionSteam steam){steam.packetsToSend.Clear();foreach(var packet in image)steam.packetsToSend.Enqueue(packet);}
    }
    private static bool EnterSelectedSend(NetConnectionAbs connection,NetPackage packet,bool run,out MemberSend member)
    {
        member=null;
        var active=sendingGroup;
        if(active!=null&&ReferenceEquals(active.Connection,connection))
        {
            int index=Array.FindIndex(active.Packets,value=>ReferenceEquals(value,packet));
            if(index>=0){member=new MemberSend{Group=active,Index=index};return run;}
        }
        if(!run||!ThreadManager.IsMainThread()||packet is not NetPackageTileEntity native)return true;
        bool upload=GameManager.Instance?.World?.IsRemote()==true;
        NetPackage[] wrapped=null;
        if(upload)
        {
            // Only the producer's private retained same-packet resume scope
            // can select a client upload. A header or current equal TE cannot.
            if(!RebirthWorkstationCompletionPipeline.TryWrapRemoteNativeUpload(native,connection,out wrapped))return true;
        }
        else
        {
            if(outerSend==null||!outerSend.Held.Any(value=>ReferenceEquals(value,native)))return true;
            var peer=connection.cInfo;
            if(peer==null||connection.bDisconnected||native.Channel!=connection.channel||peer.netConnection==null||
                connection.channel<0||connection.channel>=peer.netConnection.Length||!ReferenceEquals(peer.netConnection[connection.channel],connection))return true;
            if(!TryWrapNativeHydration(peer,connection,native,out wrapped))return true;
        }
        if(wrapped==null)return false; // Positively known but not ready: retain custody.
        var group=new GroupSend{Connection=connection,Packets=wrapped,Gate=QueueGate(connection),Previous=sendingGroup,NativeUpload=upload};
        NetPackage[] before=null;bool complete=false;
        try
        {
            if(group.Gate==null||wrapped.Length<1||wrapped.Length>1+(NetPackageRebirthUiDeferredHydrationV1.Maximum+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk||wrapped.Any(value=>value==null||ReferenceEquals(value,native)||
                !IsSelectedFragment(value,upload)||value.Channel!=native.Channel||value.inSendQueuesCount!=0)||wrapped.Distinct().Count()!=wrapped.Length)return false;
            group.Ran=new bool[wrapped.Length];group.InitialReferences=new int[wrapped.Length];
            Monitor.Enter(group.Gate);group.Locked=true;before=QueueImage(connection);group.ExistingCount=before.Length;
            for(int index=0;index<wrapped.Length;index++){wrapped[index].RegisterSendQueue();group.InitialReferences[index]=1;}
            sendingGroup=group;
            for(int index=0;index<wrapped.Length;index++)
            {
                connection.AddToSendQueue(wrapped[index]);
                var current=QueueImage(connection);
                if(!group.Ran[index]||current.Length!=before.Length+index+1||wrapped[index].inSendQueuesCount!=2||
                    !current.Take(before.Length).SequenceEqual(before))return false;
                for(int prior=0;prior<=index;prior++)if(!ReferenceEquals(current[before.Length+prior],wrapped[prior]))return false;
            }
            // Every child original has returned and every private fragment is
            // still in exact contiguous native queue order under its real lock.
            foreach(var value in wrapped)NoteSelectedFragment(value,connection,upload);
            complete=true;return false;
        }
        finally
        {
            if(ReferenceEquals(sendingGroup,group))sendingGroup=group.Previous;
            if(group.Locked)
            {
                try
                {
                if(!complete&&before!=null)
                {
                    // The serializer cannot have observed a partial group.
                    var privateGroup=new HashSet<NetPackage>(wrapped);
                    RestoreQueue(connection,QueueImage(connection).Where(value=>!privateGroup.Contains(value)).ToArray());
                    if(!QueueImage(connection).Any(privateGroup.Contains))ReleaseUnenqueuedSelected(wrapped,upload);
                    for(int index=0;index<wrapped.Length;index++)
                        if(group.InitialReferences[index]==1&&wrapped[index].inSendQueuesCount==2)wrapped[index].SendQueueHandled();
                }
                for(int index=0;index<wrapped.Length;index++)
                    if(group.InitialReferences!=null&&group.InitialReferences[index]==1)wrapped[index].SendQueueHandled();
                }
                finally{group.Locked=false;Monitor.Exit(group.Gate);}
            }
            else
            {
                if(group.Gate!=null)
                {
                    lock(group.Gate)
                    {
                        var privateGroup=new HashSet<NetPackage>(wrapped);
                        if(wrapped.All(value=>value!=null&&IsSelectedFragment(value,upload)&&value.inSendQueuesCount==0)&&
                            !QueueImage(connection).Any(privateGroup.Contains))ReleaseUnenqueuedSelected(wrapped,upload);
                    }
                }
                foreach(var value in wrapped.Distinct())if(value!=null&&IsSelectedFragment(value,upload)&&value.inSendQueuesCount==0)NetPackageManager.FreePackage(value);
            }
        }
    }
    private static bool IsSelectedFragment(NetPackage packet,bool upload)=>upload?RebirthWorkstationCompletionPipeline.IsRemoteNativeEnvelope(packet):packet is NetPackageRebirthUiDeferredHydrationV1;
    private static void NoteSelectedFragment(NetPackage packet,INetConnection connection,bool upload)
    {if(upload)RebirthWorkstationCompletionPipeline.NoteRemoteNativeEnqueued(packet,connection);else NoteHydrationEnqueued(packet,connection);}
    private static void ReleaseUnenqueuedSelected(NetPackage[] packets,bool upload)
    {if(upload)RebirthWorkstationCompletionPipeline.ReleaseUnenqueuedRemoteNative(packets);else ReleaseUnenqueuedHydration(packets);}
    private static void ObserveMember(bool run,MemberSend member)
    {if(run&&member!=null&&ReferenceEquals(sendingGroup,member.Group)&&member.Group.Locked)member.Group.Ran[member.Index]=true;}
    [HarmonyPatch(typeof(NetConnectionSimple),nameof(NetConnectionSimple.AddToSendQueue),new[]{typeof(NetPackage)})]
    private static class SimpleSend
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(NetConnectionSimple __instance,NetPackage _package,bool __runOriginal,out SendScopes __state)
            =>EnterSendScopes(__instance,_package,__runOriginal,out __state);
        [HarmonyPriority(Priority.Last)]private static void Postfix(bool __runOriginal,SendScopes __state)=>ObserveSendScopes(__runOriginal,__state);
        private static Exception Finalizer(Exception __exception,SendScopes __state)=>EndSendScopes(__exception,__state);
    }
    [HarmonyPatch(typeof(NetConnectionSteam),nameof(NetConnectionSteam.AddToSendQueue),new[]{typeof(NetPackage)})]
    private static class SteamSend
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(NetConnectionSteam __instance,NetPackage _package,bool __runOriginal,out SendScopes __state)
            =>EnterSendScopes(__instance,_package,__runOriginal,out __state);
        [HarmonyPriority(Priority.Last)]private static void Postfix(bool __runOriginal,SendScopes __state)=>ObserveSendScopes(__runOriginal,__state);
        private static Exception Finalizer(Exception __exception,SendScopes __state)=>EndSendScopes(__exception,__state);
    }
}


