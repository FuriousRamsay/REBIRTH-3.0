using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

// Actual native serialization/read/activation provenance. No runtime, actor,
// inventory, scheduler, callback or old-world binding is restored here.
internal static partial class RebirthNpcRuntimeNativeAdoption
{
    internal sealed class Frame
    {
        internal World World;internal string Directory,Profile,Runtime;internal int Class;
        internal RebirthNpcStableId Stable;internal byte[] Bytes,DogBytes;internal string DogOwner;
    }
    private sealed class Entry {internal Frame Written,Read,CapturedWritten;internal Outgoing Captured;internal bool Added,Held,WasHeld,OriginalAi;}
    private sealed class Boundary {internal EntityRebirthNPC Actor;internal Stream Stream;internal long Start;internal Frame Frame;}
    private static readonly ConditionalWeakTable<EntityRebirthNPC,Entry> entries=new ConditionalWeakTable<EntityRebirthNPC,Entry>();
    internal sealed class Outgoing
    {
        private readonly EntityRebirthNPC actor;private readonly RebirthNpcRuntimeState runtime;private readonly Frame frame;private bool detached;
        private Outgoing(EntityRebirthNPC actor,RebirthNpcRuntimeState runtime,Frame frame){this.actor=actor;this.runtime=runtime;this.frame=new Frame{World=frame.World,Directory=frame.Directory,Profile=frame.Profile,
            Runtime=frame.Runtime,Class=frame.Class,Stable=frame.Stable,Bytes=(byte[])frame.Bytes.Clone(),
            DogBytes=frame.DogBytes==null?null:(byte[])frame.DogBytes.Clone(),DogOwner=frame.DogOwner};}
        internal RebirthNpcStableId StableId=>frame.Stable;
        internal World World=>frame.World;
        internal string Directory=>frame.Directory;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(actor.world,frame.World)&&
            ReferenceEquals(frame.World.GetEntity(actor.entityId),actor)&&ReferenceEquals(actor.RebirthRuntimeState,runtime)&&
            actor.NativeIdentityReadable&&(actor.ObservedNativeStableIdentity.IsEmpty||actor.ObservedNativeStableIdentity==frame.Stable)&&runtime.StableId==frame.Stable&&actor.entityClass==frame.Class&&
            actor.RebirthProfileId==frame.Profile&&RuntimeImage(runtime)==frame.Runtime&&
            (!(actor is EntityRebirthDogCompanion)||frame.DogBytes!=null&&frame.DogOwner==(runtime.OwnerId??string.Empty));
        internal bool IsDetached=>detached;
        internal bool RegisterDetached()
        {
            if(detached)return true;
            if(!Current()||!RebirthNpcSaveQuarantineBundle.IsDetachedServerRuntimeMembership(this))return false;
            detached=true;return true;
        }
        internal bool MatchesSavedRuntime(RebirthNpcRuntimeState candidate)=>detached&&candidate!=null&&RuntimeImage(candidate)==frame.Runtime;
        internal bool Matches(NativeReentry source)=>(detached||Current())&&source!=null&&source.Current()&&
            !ReferenceEquals(frame.World,source.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(frame.Directory,source.Directory)&&
            source.frame.Stable==frame.Stable&&source.frame.Class==frame.Class&&source.frame.Profile==frame.Profile&&
            frame.Bytes.SequenceEqual(source.frame.Bytes)&&
            (!(actor is EntityRebirthDogCompanion)||source.Actor is EntityRebirthDogCompanion&&frame.DogBytes!=null&&source.frame.DogBytes!=null&&
                frame.DogOwner==source.frame.DogOwner&&frame.DogBytes.SequenceEqual(source.frame.DogBytes));
        internal static Outgoing Capture(EntityRebirthNPC actor,RebirthNpcRuntimeState runtime)
        {
            if(actor==null||runtime==null||!entries.TryGetValue(actor,out var entry)||entry.Written==null)return null;
            if(entry.Captured!=null&&ReferenceEquals(entry.CapturedWritten,entry.Written)&&entry.Captured.Current())return entry.Captured;
            var value=new Outgoing(actor,runtime,entry.Written);if(!value.Current())return null;
            entry.Captured=value;entry.CapturedWritten=entry.Written;return value;
        }
    }
    internal sealed class NativeReentry
    {
        private readonly EntityRebirthNPC actor;internal readonly Frame frame;
        private NativeReentry(EntityRebirthNPC actor,Frame frame){this.actor=actor;this.frame=frame;}
        internal World World=>frame.World;internal string Directory=>frame.Directory;
        internal EntityRebirthNPC Actor=>actor;internal RebirthNpcStableId StableId=>frame.Stable;
        internal bool HasDogOwner=>actor is EntityRebirthDogCompanion&&frame.DogBytes!=null;
        internal string NativeDogOwner=>HasDogOwner?frame.DogOwner:null;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,frame.World)&&
            frame.World?.IsRemote()==false&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&
            ReferenceEquals(actor.world,frame.World)&&ReferenceEquals(frame.World.GetEntity(actor.entityId),actor)&&
            actor.NativeIdentityReadable&&actor.ObservedNativeStableIdentity==frame.Stable&&!frame.Stable.IsEmpty&&
            actor.entityClass==frame.Class&&actor.RebirthProfileId==frame.Profile&&
            RebirthNpcSaveQuarantineScope.SameDirectory(frame.Directory,GameIO.GetSaveGameDir())&&
            entries.TryGetValue(actor,out var entry)&&ReferenceEquals(entry.Read,frame)&&entry.Added&&
            (!(actor is EntityRebirthDogCompanion)||frame.DogBytes!=null);
        internal bool Matches(Outgoing original)=>original?.Matches(this)==true;
        internal bool MatchesSavedBodyFrame(byte[] saved)=>Current()&&saved!=null&&saved.SequenceEqual(frame.Bytes);
        internal static NativeReentry Capture(EntityRebirthNPC actor)
        {
            if(actor==null||!entries.TryGetValue(actor,out var entry)||entry.Read==null||!entry.Added)return null;
            var value=new NativeReentry(actor,entry.Read);return value.Current()?value:null;
        }
    }
    internal static Outgoing CaptureOutgoing(EntityRebirthNPC actor,RebirthNpcRuntimeState runtime)
    {try{return Outgoing.Capture(actor,runtime);}catch{return null;}}
    private static string RuntimeImage(RebirthNpcRuntimeState runtime)
    {
        if(runtime==null)return null;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {RebirthNpcPersistenceCodec.Write(writer,runtime);writer.Flush();if(stream.Length>1024*1024)throw new InvalidDataException("Runtime source bound");return Convert.ToBase64String(stream.ToArray());}
    }
    private static Boundary Enter(EntityRebirthNPC actor,Stream stream,bool writing)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||actor==null||world?.IsRemote()!=false||stream==null||!stream.CanSeek||!stream.CanRead||
                writing&&(!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)))return null;
            string directory=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathRooted(directory))return null;
            return new Boundary{Actor=actor,Stream=stream,Start=stream.Position,Frame=new Frame{World=world,
                Directory=RebirthNpcSaveQuarantineScope.ScopeDirectory(directory),Class=actor.entityClass,Profile=actor.RebirthProfileId,
                Runtime=writing?RuntimeImage(actor.RebirthRuntimeState):null}};
        }
        catch{return null;}
    }
    internal static object EnterRead(EntityRebirthNPC actor,PooledBinaryReader reader,StreamModeRead mode)
        =>mode==StreamModeRead.Persistency?Enter(actor,reader?.BaseStream,false):null;
    internal static object EnterWrite(EntityRebirthNPC actor,PooledBinaryWriter writer,StreamModeWrite mode)
        =>mode==StreamModeWrite.Persistency?Enter(actor,writer?.BaseStream,true):null;
    private static Frame Finish(EntityRebirthNPC actor,Stream stream,object state,bool writing)
    {
        if(state is not Boundary source||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(source.Stream,stream)||
            !ReferenceEquals(source.Frame.World,GameManager.Instance?.World)||!actor.NativeIdentityReadable||
            !writing&&actor.ObservedNativeStableIdentity.IsEmpty||actor.entityClass!=source.Frame.Class||actor.RebirthProfileId!=source.Frame.Profile||
            !RebirthNpcSaveQuarantineScope.SameDirectory(source.Frame.Directory,GameIO.GetSaveGameDir())||
            writing&&RuntimeImage(actor.RebirthRuntimeState)!=source.Frame.Runtime)return null;
        long end=stream.Position,length=end-source.Start;if(length<1||length>ushort.MaxValue)return null;
        var bytes=new byte[(int)length];stream.Position=source.Start;
        try{int offset=0;while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<=0)return null;offset+=read;}}
        finally{stream.Position=end;}
                RebirthNpcStableId stable=actor.ObservedNativeStableIdentity;
        if(writing)
        {
            if(bytes.Length<RebirthNpcNativeStableIdentityCodec.Size)return null;
            using(var input=new MemoryStream(bytes,false))using(var reader=new BinaryReader(input))
            {input.Position=bytes.Length-RebirthNpcNativeStableIdentityCodec.Size;stable=RebirthNpcNativeStableIdentityCodec.Read(reader);if(input.Position!=input.Length)return null;}
            if(stable.IsEmpty||actor.RebirthRuntimeState?.StableId!=stable)return null;
        }
        source.Frame.Bytes=bytes;source.Frame.Stable=stable;return source.Frame;
    }
    internal static void ObserveRead(EntityRebirthNPC actor,PooledBinaryReader reader,object state)
    {
        try{var frame=Finish(actor,reader?.BaseStream,state,false);if(frame==null)return;var entry=entries.GetValue(actor,value=>new Entry());entry.Read=frame;entry.Added=false;}
        catch{}
    }
    internal static void ObserveWrite(EntityRebirthNPC actor,PooledBinaryWriter writer,object state)
    {
        try{var frame=Finish(actor,writer?.BaseStream,state,true);if(frame!=null)entries.GetValue(actor,value=>new Entry()).Written=frame;}
        catch{}
    }
    internal static bool HoldIncoming(NativeReentry source)
    {
        if(source==null||!source.Current()||!entries.TryGetValue(source.Actor,out var entry))return false;
        if(!entry.WasHeld){entry.OriginalAi=source.Actor.NativeAdoptionOriginalAi;entry.WasHeld=true;}
        entry.Held=true;source.Actor.hasAI=false;return true;
    }
    internal static bool IsIncomingHeld(EntityRebirthNPC actor)
        =>actor!=null&&entries.TryGetValue(actor,out var entry)&&entry.Held;
    internal static bool HasIncomingActivationHistory(EntityRebirthNPC actor)
        =>actor!=null&&entries.TryGetValue(actor,out var entry)&&entry.WasHeld;
    // This only removes the source-specific hold after the bundle owner has
    // independently admitted every required incoming domain. Native activation
    // still runs through its existing deferred lifecycle, never OnAdded twice.
    internal static bool ReleaseIncoming(NativeReentry source)
    {
        if(source==null||!source.Current()||!entries.TryGetValue(source.Actor,out var entry)||!entry.Held||
            !RebirthNpcSaveQuarantineBundle.CanReleaseServerRuntimeNativeReentry(source))return false;
        entry.Held=false;return true;
    }
    internal static void CompleteNativeActivation(EntityRebirthNPC actor)
    {
        if(!ThreadManager.IsMainThread()||actor==null||!entries.TryGetValue(actor,out var entry)||entry.Held||!entry.WasHeld)return;
        var source=NativeReentry.Capture(actor);
        if(source==null||!RebirthNpcSaveQuarantineBundle.CanReleaseServerRuntimeNativeReentry(source))return;
        actor.hasAI=entry.OriginalAi;entry.WasHeld=false;
    }
    internal static void ObserveAdded(EntityRebirthNPC actor)
    {
        try
        {
            if(actor==null||!entries.TryGetValue(actor,out var entry)||entry.Read==null)return;
            entry.Added=true;var source=NativeReentry.Capture(actor);
            if(source!=null)RebirthNpcSaveQuarantineBundle.ObserveServerRuntimeNativeReentry(source);
        }
        catch{}
    }
}
