using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Platform;

// Only the exact accepted original ChangeBlocks call-site lookup is scoped.
// Native ownership/permission checks elsewhere are never patched or bypassed.
internal static class RebirthRemoteResourceBlockActorScope
{
    internal sealed class Scope:IDisposable
    {
        internal World World;internal string Directory;internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal RebirthRemoteResourceBlockCommand.Accepted Accepted;internal List<BlockChangeInfo> Changes;internal PlatformUserIdentifierAbs NativeUser;
        internal int Depth,Calls;internal bool OriginalInvocation,InvocationCompleted;
        public void Dispose(){if(ReferenceEquals(current,this))current=null;}
    }
    [ThreadStatic]private static Scope current;
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.resource.block.actor.v1"),typeof(RebirthRemoteResourceBlockActorCallsite));installed=true;
    }
    internal static Scope Begin(World world,RebirthRemoteResourceClientOriginal.Intent original,List<BlockChangeInfo> exactOriginalChanges)
    {
        try
        {
            if(!installed||current!=null||!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var accepted)||root.Phase!=2||
                !RebirthOriginalNativeBlockChange.TryEncode(exactOriginalChanges,out var image)||image!=accepted.Changes||
                !RebirthRemoteResourceBlockAdmission.TryReadNativeUser(original,out var user))return null;
            var value=new Scope{World=world,Directory=GameIO.GetSaveGameDir(),Original=original,Accepted=accepted,Changes=exactOriginalChanges,NativeUser=user};current=value;return value;
        }
        catch{return null;}
    }
    internal static Scope Enter(PlatformUserIdentifierAbs user,List<BlockChangeInfo> changes)
    {
        var value=current;if(value==null)return null;value.Depth++;
        if(value.Depth==1)value.OriginalInvocation=ReferenceEquals(changes,value.Changes)&&user!=null&&user.Equals(value.NativeUser)&&
            RebirthOriginalNativeBlockChange.TryEncode(changes,out var image)&&image==value.Accepted.Changes;
        return value;
    }
    internal static bool MayRun(Scope value)
    {return value==null||value.Depth!=1||!value.OriginalInvocation||++value.Calls==1;}
    internal static void Completed(Scope value,bool ran)
    {if(value!=null&&value.Depth==1&&value.OriginalInvocation)value.InvocationCompleted=ran&&value.Calls==1;}
    internal static void Exit(Scope value)
    {if(value!=null){value.Depth--;if(value.Depth==0)value.OriginalInvocation=false;}}
    internal static Entity ResolveOriginalActor(World world,int entityId)
    {
        if(RebirthRemoteResourceBlockProjection.TryResolve(world,entityId,out var projectionActor))return projectionActor;
        var value=current;
        if(value==null||value.Depth!=1||!value.OriginalInvocation)return world.GetEntity(entityId);
        // Retained native primary attribution is still passed to ChangeBlocks.
        // Its only resolved-actor uses in the exact installed method are the
        // sleeping-bag SpawnPoints branch; no actor permission check is skipped.
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,value.World)||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||
            GameIO.GetSaveGameDir()!=value.Directory||world.worldState.Guid!=value.Original.World||entityId!=value.Accepted.NativeEntity)return null;
        var actor=world.GetEntity(entityId) as EntityPlayer;
        if(actor==null||actor.IsDead()||!actor.IsSpawned()||actor.Died!=value.Original.Deaths||!ReferenceEquals(actor.world,world)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.CanonicalId!=value.Original.Owner||
            !RebirthOriginalActionScope.Matches(actor,owner.StorageKey,value.Original.Creation))return null;
        return actor;
    }
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.ChangeBlocks),new[]{typeof(PlatformUserIdentifierAbs),typeof(List<BlockChangeInfo>)})]
internal static class RebirthRemoteResourceBlockActorCallsite
{
    private sealed class CallFrame{internal RebirthRemoteResourceBlockActorScope.Scope Server;internal RebirthRemoteResourceBlockProjection.Scope Projection;}
    private static bool Prefix(PlatformUserIdentifierAbs persistentPlayerId,List<BlockChangeInfo> _blocksToChange,out CallFrame __state)
    {__state=new CallFrame{Server=RebirthRemoteResourceBlockActorScope.Enter(persistentPlayerId,_blocksToChange),Projection=RebirthRemoteResourceBlockProjection.Enter(persistentPlayerId,_blocksToChange)};
        return RebirthRemoteResourceBlockActorScope.MayRun(__state.Server)&&RebirthRemoteResourceBlockProjection.MayRun(__state.Projection);}
    private static void Postfix(bool __runOriginal,CallFrame __state)
    {RebirthRemoteResourceBlockActorScope.Completed(__state?.Server,__runOriginal);RebirthRemoteResourceBlockProjection.Completed(__state?.Projection,__runOriginal);}
    private static Exception Finalizer(Exception __exception,CallFrame __state)
    {var server=__state?.Server;if(__exception!=null&&server!=null&&server.Depth==1&&server.OriginalInvocation)server.InvocationCompleted=false;
        RebirthRemoteResourceBlockActorScope.Exit(server);RebirthRemoteResourceBlockProjection.Exit(__state?.Projection,__exception);return __exception;}
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
    {
        var native=AccessTools.Method(typeof(World),nameof(World.GetEntity),new[]{typeof(int)});
        var scoped=AccessTools.Method(typeof(RebirthRemoteResourceBlockActorScope),nameof(RebirthRemoteResourceBlockActorScope.ResolveOriginalActor));
        var result=source.Select(instruction=>new CodeInstruction(instruction)).ToArray();int count=0;
        foreach(var instruction in result)if(instruction.Calls(native)){instruction.opcode=OpCodes.Call;instruction.operand=scoped;count++;}
        if(count!=1)throw new InvalidOperationException("Expected exactly one native ChangeBlocks actor lookup.");
        return result;
    }
}
