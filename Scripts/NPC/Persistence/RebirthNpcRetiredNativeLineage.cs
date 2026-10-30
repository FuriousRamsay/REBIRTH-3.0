using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Identity bytes only, not an incarnation/custody grant. Native chunk unload removes the
// world lookup before its retained entity list is serialized. Carry a positive original
// binding across that retirement only inside the exact native manager/snapshot writer.
internal static class RebirthNpcRetiredNativeLineage
{
    private const string Owner="rebirth.npc.native-identity.bound.v1";
    private sealed class Proof
    {
        internal World World;internal Chunk Chunk;internal RebirthNpcRuntimeState Runtime;
        internal RebirthNpcStableId Stable;internal int Entity,Class,Generation;
        internal string Profile,Directory,Fingerprint,Region;
    }
    private static readonly ConditionalWeakTable<EntityRebirthHumanoidNPC,Proof> Retired=new();
    [ThreadStatic]private static RegionFileManager writer;
    [ThreadStatic]private static Chunk snapshot;
    private static readonly MethodInfo Save=AccessTools.Method(typeof(RegionFileManager),nameof(RegionFileManager.SaveChunkSnapshot),new[]{typeof(Chunk),typeof(bool)});
    private static readonly MethodInfo Drain=AccessTools.Method(typeof(RegionFileManager),"DoSaveChunks",Type.EmptyTypes);
    private static readonly MethodInfo Update=AccessTools.Method(typeof(RegionFileChunkSnapshot),nameof(RegionFileChunkSnapshot.Update),new[]{typeof(Chunk),typeof(bool)});
    private static MethodInfo Method(string name)=>AccessTools.Method(typeof(RebirthNpcRetiredNativeLineage),name);
    private static bool Has(MethodInfo target,string prefix,string finalizer)
    {
        if(target==null)return false;var info=Harmony.GetPatchInfo(target);if(info==null)return false;
        bool p=false,f=false;
        foreach(var patch in info.Prefixes)if(patch.owner==Owner&&patch.PatchMethod==Method(prefix))p=true;
        foreach(var patch in info.Finalizers)if(patch.owner==Owner&&patch.PatchMethod==Method(finalizer))f=true;
        return p&&f;
    }
    internal static bool GuardsInstalled=>Has(Save,nameof(EnterWriter),nameof(LeaveWriter))&&Has(Drain,nameof(EnterWriter),nameof(LeaveWriter))&&Has(Update,nameof(EnterSnapshot),nameof(LeaveSnapshot));
    internal static void Install(Harmony harmony)
    {
        if(harmony==null||harmony.Id!=Owner||Save==null||Drain==null||Update==null)throw new InvalidOperationException("Exact native retired-lineage writer boundaries unavailable");
        foreach(var target in new[]{Save,Drain})if(!Has(target,nameof(EnterWriter),nameof(LeaveWriter)))
            harmony.Patch(target,prefix:new HarmonyMethod(Method(nameof(EnterWriter))),finalizer:new HarmonyMethod(Method(nameof(LeaveWriter))));
        if(!Has(Update,nameof(EnterSnapshot),nameof(LeaveSnapshot)))harmony.Patch(Update,prefix:new HarmonyMethod(Method(nameof(EnterSnapshot))),finalizer:new HarmonyMethod(Method(nameof(LeaveSnapshot))));
        if(!GuardsInstalled)throw new InvalidOperationException("Native retired-lineage writer boundaries not installed");
    }
    private static void EnterWriter(RegionFileManager __instance,out RegionFileManager __state){__state=writer;writer=__instance;}
    private static Exception LeaveWriter(Exception __exception,RegionFileManager __state){writer=__state;return __exception;}
    private static void EnterSnapshot(Chunk __0,out Chunk __state){__state=snapshot;snapshot=__0;}
    private static Exception LeaveSnapshot(Exception __exception,Chunk __state){snapshot=__state;return __exception;}
    private static string Full(string path)=>string.IsNullOrWhiteSpace(path)?null:Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    private static bool Same(string a,string b)=>a!=null&&b!=null&&string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    private static bool ContainsOnce(Chunk chunk,EntityRebirthHumanoidNPC npc)
    {
        if(chunk?.entityLists==null)return false;int count=0;
        foreach(var list in chunk.entityLists)
        {
            if(list==null)continue;
            foreach(var value in list)
            {
                if(ReferenceEquals(value,npc))count++;
                else if(!ReferenceEquals(value,null)&&value.entityId==npc.entityId)return false;
            }
        }
        return count==1;
    }
    internal static void Capture(EntityRebirthHumanoidNPC npc,World world,RebirthNpcRuntimeState runtime)
    {
        if(ReferenceEquals(npc,null))return;
        Retired.Remove(npc);
        try
        {
            if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World)||world?.IsRemote()!=false||
                SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!ReferenceEquals(npc.world,world)||
                !ReferenceEquals(world.GetEntity(npc.entityId),npc)||!ReferenceEquals(npc.RebirthRuntimeState,runtime)||
                runtime==null||runtime.StableId.IsEmpty||!npc.NativeIdentityReadable||npc.RebirthProfileId!=runtime.ProfileId||
                !npc.NativeStableIdentity.IsEmpty&&npc.NativeStableIdentity!=runtime.StableId||
                !RebirthNpcRuntimeRegistry.TryGet(npc.entityId,out var live)||!ReferenceEquals(live,runtime)||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out int id)||id!=npc.entityId||!npc.addedToChunk)return;
            var chunk=world.GetChunkSync(npc.chunkPosAddedEntityTo.x,npc.chunkPosAddedEntityTo.z) as Chunk;
            if(chunk==null||!chunk.InProgressUnloading||!ContainsOnce(chunk,npc))return;
            var scope=RebirthNpcSaveScope.ObserveCurrent();string directory=Full(scope?.SaveDirectory),region=Full(GameIO.GetSaveGameRegionDir());
            if(directory==null||region==null||string.IsNullOrEmpty(scope.Fingerprint))return;
            Retired.Add(npc,new Proof{World=world,Chunk=chunk,Runtime=runtime,Stable=runtime.StableId,Entity=npc.entityId,Class=npc.entityClass,
                Profile=npc.RebirthProfileId,Directory=directory,Fingerprint=scope.Fingerprint,Generation=scope.ProcessSaveGeneration,Region=region});
        }
        catch{Retired.Remove(npc);} // Failed positive proof never relaxes ordinary offworld framing.
    }
    internal static bool TryGet(EntityRebirthHumanoidNPC npc,out RebirthNpcStableId stable)
    {
        stable=default;
        try
        {
            if(ReferenceEquals(npc,null)||writer==null||snapshot==null||!Retired.TryGetValue(npc,out var proof)||!GuardsInstalled||
                !ReferenceEquals(proof.World,GameManager.Instance?.World)||proof.World.IsRemote()||
                SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!ReferenceEquals(npc.world,proof.World)||
                !ReferenceEquals(snapshot,proof.Chunk)||!snapshot.InProgressUnloading||!ContainsOnce(snapshot,npc)||
                !ReferenceEquals(npc.RebirthRuntimeState,proof.Runtime)||npc.entityId!=proof.Entity||npc.entityClass!=proof.Class||
                npc.RebirthProfileId!=proof.Profile||proof.Runtime.ProfileId!=proof.Profile||proof.Runtime.StableId!=proof.Stable||!npc.NativeIdentityReadable||
                !npc.NativeStableIdentity.IsEmpty&&npc.NativeStableIdentity!=proof.Stable||
                !ReferenceEquals(proof.World.GetEntity(proof.Entity),null))return false;
            var scope=RebirthNpcSaveScope.ObserveCurrent();
            if(scope==null||scope.ProcessSaveGeneration!=proof.Generation||scope.Fingerprint!=proof.Fingerprint||
                !Same(Full(scope.SaveDirectory),proof.Directory)||!Same(Full(GameIO.GetSaveGameRegionDir()),proof.Region)||
                !Same(Full(writer.saveDirectory),proof.Region))return false;
            stable=proof.Stable;return true;
        }
        catch{return false;}
    }
}
