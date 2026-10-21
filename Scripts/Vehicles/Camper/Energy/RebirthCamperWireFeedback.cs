using UnityEngine;
using Object=UnityEngine.Object;

// Native WireNode preview and native wire-tool sound groups; no parallel line renderer.
internal sealed class RebirthCamperWireFeedback
{
    private WireNode node;
    private Transform endpoint;
    private EntityPlayerLocal actor;
    private ItemActionConnectPower action;
    internal bool Active=>node&&endpoint;
    internal string SourceName=>node&&node.Source?node.Source.name:null;
    internal Vector3 Endpoint=>node?node.LocalPosition:Vector3.zero;

    internal bool Begin(EntityPlayerLocal player,ItemActionConnectPower tool,Transform relay,bool powered)
    {
        Clear();
        if(!player||tool==null||!relay||WireManager.Instance==null)return false;
        var hand=tool.GetHandTransform(player);var wire=hand?hand.FindInChilds("wire_mesh"):null;
        var prefab=Resources.Load("Prefabs/WireNode") as GameObject;
        if(!wire||!prefab)return false;
        var instance=Object.Instantiate(prefab);node=instance.GetComponent<WireNode>();
        if(!node){Object.Destroy(instance);return false;}
        endpoint=relay;actor=player;action=tool;
        node.LocalPosition=relay.position;node.localOffset=Vector3.zero;
        node.Source=wire.gameObject;node.sourceOffset=tool.wireOffset;
        node.TogglePulse(false);node.SetPulseSpeed(360f);WireManager.Instance.AddActiveWire(node);
        player.RightArmAnimationUse=true;
        Audio.Manager.BroadcastPlay(relay.position+Origin.position,powered?"wire_live_connect":"wire_dead_connect");
        return true;
    }

    internal bool Update(EntityPlayerLocal player,ItemActionConnectPower tool)
    {
        if(!node||!endpoint||!endpoint.gameObject.activeInHierarchy||actor!=player||!ReferenceEquals(action,tool))
        {Clear();return false;}
        // Relay transforms already use floating-origin local scene coordinates.
        node.LocalPosition=endpoint.position;
        return true;
    }

    internal void Clear()
    {
        if(node){if(WireManager.Instance!=null)WireManager.Instance.RemoveActiveWire(node);Object.Destroy(node.gameObject);}
        node=null;endpoint=null;actor=null;action=null;
    }
}
