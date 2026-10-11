using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;

[HarmonyPatch(typeof(ConsoleCmdChunkReset),nameof(ConsoleCmdChunkReset.Execute))]
internal static class RebirthPoiAdministrativeResetHook
{
    private static bool Prefix(List<string> _params,CommandSenderInfo _senderInfo)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled||_params==null||_params.Count!=0)return true;
        var manager=GameManager.Instance;var world=manager?.World;
        if(world==null||world.IsRemote())return true;
        var sender=_senderInfo.RemoteClientInfo;
        EntityPlayer player=null;
        if(sender!=null)
        {
            if(!sender.loginDone||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender))return false;
            player=world.GetEntity(sender.entityId) as EntityPlayer;
        }
        else if(_senderInfo.IsLocalGame&&!GameManager.IsDedicatedServer)
            player=world.GetLocalPlayers().FirstOrDefault();
        if(player==null)return true;
        var position=player.position;
        var targets=manager.GetDynamicPrefabDecorator()?.GetPrefabsFromWorldPosInside(position,FastTags<TagGroup.Global>.none);
        if(targets==null||targets.Count==0)return true; // Native terrain-only branch has a separate owner.
        var originals=targets.ToArray();
        Func<bool> current=()=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&targets.SequenceEqual(originals)&&(sender==null?world.GetLocalPlayers().Contains(player):sender.loginDone&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player.entityId),sender));
        ThreadManager.StartCoroutine(RebirthPoiOwnedWorldReset.Run(world,targets,QuestEventManager.manualResetTag,-1,null,RebirthPoiResetCaller.ManualPoi,current,success=>
        {
            if(!current())return;
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output(Localization.Get(success?"xuiRebirthPoiResetCompleted":"xuiRebirthPoiResetUncertain"));
            if(success&&DynamicMeshManager.Instance!=null)
            {
                int x=World.toChunkXZ((int)position.x),z=World.toChunkXZ((int)position.z);
                for(int i=x-1;i<=x+1;i++)for(int j=z-1;j<=z+1;j++)DynamicMeshManager.Instance.AddChunk(WorldChunkCache.MakeChunkKey(i,j),addToThread:true,primary:true,null);
            }
        }));
        return false;
    }
}