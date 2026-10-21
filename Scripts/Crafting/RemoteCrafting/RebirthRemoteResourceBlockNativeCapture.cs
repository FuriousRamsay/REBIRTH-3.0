using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Platform;

// Purpose-scoped native send interception. This is not installed until the
// complete Block producer/host/owner settlement path installs it. The native
// optimistic local ChangeBlocks/texture effects remain native method calls.
internal static class RebirthRemoteResourceBlockNativeCapture
{
    internal sealed class Scope:IDisposable
    {
        internal EntityPlayerLocal Player;internal World World;internal string Directory;
        internal ItemActionRepair Action;internal ItemActionRepair.InventoryDataRepair Data;
        internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal Vector3i Position;internal int Slot;internal Func<bool> Permission;
        internal string BeforeTool,AfterTool,Changes;internal bool Failed,Texture,Invoked,ApplyingLocal;
        internal bool Current()=>!Failed&&ReferenceEquals(current,this)&&Permission?.Invoke()==true&&
            RebirthRemoteResourceClientOriginal.Current(Player,Original)&&ReferenceEquals(Player.world,World)&&Directory==GameIO.GetSaveGameDir()&&
            ReferenceEquals(Data?.invData?.holdingEntity,Player)&&ReferenceEquals(Player.inventory.holdingItemData,Data.invData)&&ReferenceEquals(Data.invData.world,World)&&Data.invData.slotIdx==Slot&&Player.inventory.holdingItemIdx==Slot&&
            Data.indexInEntityOfAction>=0&&Data.indexInEntityOfAction<Data.invData.item.Actions.Length&&ReferenceEquals(Data.invData.item.Actions[Data.indexInEntityOfAction],Action)&&
            Data.invData.actionData!=null&&Data.indexInEntityOfAction<Data.invData.actionData.Count&&ReferenceEquals(Data.invData.actionData[Data.indexInEntityOfAction],Data)&&
            (RebirthNativeItemCodec.Encode(Data.invData.itemValue)==BeforeTool||RebirthNativeItemCodec.Encode(Data.invData.itemValue)==AfterTool);
        internal bool TryGet(out string exactNativeChanges,out bool clearTexture)
        {exactNativeChanges=null;clearTexture=false;try{if(!Current()||!Invoked||Changes==null)return false;exactNativeChanges=Changes;clearTexture=Texture;return true;}catch{Failed=true;return false;}}
        public void Dispose(){if(ReferenceEquals(current,this))current=null;}
    }
    [ThreadStatic]private static Scope current;
    private static bool installed;
    internal static bool IsApplyingOptimistic(RebirthRemoteResourceClientOriginal.Intent original)
        =>ThreadManager.IsMainThread()&&current?.ApplyingLocal==true&&current.Invoked&&current.Original.Digest==original?.Digest;
    internal static bool ConsumeInvocation(ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,RebirthRemoteResourceClientOriginal.Intent original)
    {
        var scope=current;
        if(scope==null||scope.Invoked||!ReferenceEquals(scope.Action,action)||!ReferenceEquals(scope.Data,data)||scope.Original.Digest!=original?.Digest||
            !scope.Current()||!RebirthRemoteResourceOwnerAction.HasExecutingBlock(original))return false;
        scope.Invoked=true;return true;
    }
    internal static void Install()
    {
        if(installed)return;RebirthRemoteResourceBlockOptimisticView.Install();var harmony=new Harmony("rebirth.remote.resources.original.block.capture.v1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRemoteResourceOriginalBlockSend));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRemoteResourceOriginalBlockTextureSend));installed=true;
    }
    internal static Scope Begin(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,
        RebirthRemoteResourceClientOriginal.Intent original,Func<bool> actualSavedHostPermission)
    {
        try
        {
            if(!installed||current!=null||!RebirthRemoteResourceClientOriginal.Current(player,original)||RebirthRemoteResourceBlockOriginal.RejectedOrUnknown(original)||RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)||
                original.Kind!=RemoteResourceClientOperation.BlockRepair&&original.Kind!=RemoteResourceClientOperation.BlockUpgrade||actualSavedHostPermission?.Invoke()!=true||
                !RebirthRemoteResourceOwnerAction.IsHeld(player)||action==null||data?.invData==null)return null;
            var body=original.Copy().Element("action");var position=new Vector3i((int)body.Attribute("x"),(int)body.Attribute("y"),(int)body.Attribute("z"));
            var block=player.world.GetBlock(position);
            if(Convert.ToUInt64(block.rawData)!=(ulong)body.Attribute("blockRaw")||block.damage!=(int)body.Attribute("blockDamage"))return null;
            var scope=new Scope{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Action=action,Data=data,Original=original,Permission=actualSavedHostPermission,
                Position=position,Slot=(int)body.Attribute("toolSlot"),BeforeTool=body.Element("toolInput").Value,AfterTool=body.Element("toolAfter").Value};
            if(RebirthNativeItemCodec.Encode(data.invData.itemValue)!=scope.BeforeTool)return null;
            current=scope;if(!scope.Current()){scope.Dispose();return null;}return scope;
        }
        catch{if(current!=null)current.Dispose();return null;}
    }
    internal static bool BeforeBlocks(GameManager manager,List<BlockChangeInfo> changes,PlatformUserIdentifierAbs persistent)
    {
        var scope=current;if(scope==null)return true;
        try
        {
            // An unexpected command inside the exact accepted original must
            // not escape as an uncorrelated native send and later be refunded
            // as unused. Keep the original unknown instead.
            if(!scope.Current()||!scope.Invoked||!ReferenceEquals(GameManager.Instance,manager)||scope.Changes!=null||changes==null||
                changes.Count!=1||changes.Any(change=>change==null||change.blockValueRef.Type!=BlockValueRefType.Block||change.blockValueRef.BlockPosition!=scope.Position)||
                persistent!=null&&!persistent.Equals(manager.GetPersistentLocalPlayer()?.PrimaryId)||
                !RebirthOriginalNativeBlockChange.TryEncode(changes,out string exact)||!RebirthOriginalNativeBlockChange.TryDecode(exact,out var detached))
            {scope.Failed=true;return false;}
            scope.Changes=exact;
            RebirthRemoteResourceBlockOptimisticView.Capture(scope); // Before the first optimistic native setter, not a postimage inference.
            scope.ApplyingLocal=true;
            try{manager.ChangeBlocks(persistent,detached);}finally{scope.ApplyingLocal=false;} // Preserve original local method/effects.
            return false; // Only this original native SEND is replaced by its correlated custom envelope.
        }
        catch{scope.Failed=true;return false;}
    }
    internal static bool BeforeTexture(GameManager manager,BlockValueRef target,BlockFace face,int index,int actor,byte channel)
    {
        var scope=current;if(scope==null)return true;
        try
        {
            if(!scope.Current()||!scope.Invoked||!ReferenceEquals(GameManager.Instance,manager)||scope.Changes==null||scope.Texture||
                !RebirthOriginalNativeBlockChange.TryDecode(scope.Changes,out var changes)||changes.Count!=1||!changes[0].bChangeBlockValue||
                new BlockValue((uint)(ulong)scope.Original.Copy().Element("action").Attribute("blockRaw")).Block.UpgradeBlock.isair||
                target.Type!=BlockValueRefType.Block||target.BlockPosition!=scope.Position||face!=BlockFace.None||index!=0||actor!=scope.Player.entityId||channel!=byte.MaxValue)
            {scope.Failed=true;return false;}
            scope.ApplyingLocal=true;
            try{manager.SetBlockTextureClient(target,face,index,channel);}finally{scope.ApplyingLocal=false;}
            scope.Texture=true;return false;
        }
        catch{scope.Failed=true;return false;}
    }
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.SetBlocksRPC))]
internal static class RebirthRemoteResourceOriginalBlockSend
{
    [HarmonyPriority(Priority.First+1)]
    private static bool Prefix(GameManager __instance,List<BlockChangeInfo> _changes,PlatformUserIdentifierAbs _persistentPlayerId)=>
        RebirthRemoteResourceBlockNativeCapture.BeforeBlocks(__instance,_changes,_persistentPlayerId);
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.SetBlockTextureServer))]
internal static class RebirthRemoteResourceOriginalBlockTextureSend
{
    [HarmonyPriority(Priority.First+1)]
    private static bool Prefix(GameManager __instance,BlockValueRef _bvRef,BlockFace _blockFace,int _idx,int _playerIdThatChanged,byte _channel)=>
        RebirthRemoteResourceBlockNativeCapture.BeforeTexture(__instance,_bvRef,_blockFace,_idx,_playerIdThatChanged,_channel);
}
