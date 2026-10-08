using System;
using System.Globalization;
using System.Xml.Linq;

// Native API adapter. Stored positive IDs are always resolved through current canonical actors.
internal static class RebirthNpcNativeHeaderProjection
{
    internal static bool TryCapture(EntityRebirthHumanoidNPC npc,out RebirthNpcNativeHeader header)
    {
        header=null;
        if(npc==null||npc.world==null||npc.Hand==null||npc.Hand.IsSwitching||npc.Hand.IsActionRunning()||
            npc.Hand.mode==Hand.HoldingMode.Transient||npc.Stats?.GetType()!=typeof(EntityStats)||
            npc.Stats.Stamina!=null||npc.Stats.Food!=null||npc.Stats.Water!=null||
            !Reference(npc.world,npc.belongsPlayerId,false,out var belongsKind,out var belongsKey)||
            !Reference(npc.world,npc.spawnById,true,out var spawnKind,out var spawnKey))return false;
        var home=npc.getHomePosition().position;var body=npc.bodyDamage;
        var root=new XElement("nativeHeader",new XAttribute("version",1),new XAttribute("required",1));
        Add(root,"team",npc.teamNumber);Add(root,"deathTime",npc.GetDeathTime());
        Add(root,"homeX",home.x);Add(root,"homeY",home.y);Add(root,"homeZ",home.z);Add(root,"homeRange",npc.getMaximumHomeDistance());
        Add(root,"clientId",npc.clientEntityId);Add(root,"sleeperPose",npc.lastSleeperPose);
        Add(root,"stunKnee",body.StunKnee);Add(root,"stunProne",body.StunProne);Add(root,"stunType",(int)body.CurrentStun);
        Add(root,"headState",(int)npc.currentHeadState);Add(root,"bodyPart",(int)body.bodyPartHit);
        Add(root,"statWait",npc.Stats.waitTicks);Add(root,"statNetWait",npc.Stats.netSyncWaitTicks);
        Add(root,"statEnclosed",npc.Stats.m_amountEnclosed);Add(root,"statBuffRemainder",npc.Stats.buffDamageRemainder);
        Add(root,"focusedSlot",npc.inventory.FocusedSlot);Add(root,"holdingMode",(int)npc.Hand.mode);
        Add(root,"lifetime",npc.lifetime);Add(root,"size",npc.OverrideSize);Add(root,"headSize",npc.OverrideHeadSize);Add(root,"stunDuration",body.StunDuration);
        Add(root,"ground",npc.onGround);Add(root,"sleeper",npc.IsSleeper);Add(root,"passiveSleeper",npc.IsSleeperPassive);
        Add(root,"sleeping",npc.IsSleeping);Add(root,"dancing",npc.isDancing);Add(root,"spawnShare",npc.spawnByAllowShare);Add(root,"crawler",body.ShouldBeCrawler);
        Add(root,"nameNull",npc.entityName==null);Add(root,"spawnNameNull",npc.spawnByName==null);
        root.SetAttributeValue("name",npc.entityName??"");root.SetAttributeValue("spawnName",npc.spawnByName??"");
        root.SetAttributeValue("belongsKind",belongsKind);root.SetAttributeValue("belongsKey",belongsKey);
        root.SetAttributeValue("spawnKind",spawnKind);root.SetAttributeValue("spawnKey",spawnKey);
        return RebirthNpcNativeHeader.TryRead(root,out header);
    }
    internal static bool TryPreflight(EntityRebirthHumanoidNPC npc,RebirthNpcNativeHeader header,out int belongs,out int spawn)
    {
        belongs=spawn=0;
        return npc!=null&&header!=null&&npc.inventory!=null&&header.Integer("focusedSlot")<npc.inventory.SlotCount&&
            Enum.IsDefined(typeof(EnumEntityStunType),header.Integer("stunType"))&&
            Enum.IsDefined(typeof(EnumBodyPartHit),header.Integer("bodyPart"))&&
            Enum.IsDefined(typeof(EModelBase.HeadStates),header.Integer("headState"))&&
            Resolve(npc.world,header.Text("belongsKind"),header.Text("belongsKey"),out belongs)&&
            Resolve(npc.world,header.Text("spawnKind"),header.Text("spawnKey"),out spawn);
    }
    internal static void Hydrate(EntityRebirthHumanoidNPC npc,RebirthNpcNativeHeader header,int belongs,int spawn)
    {
        // Public setters such as TeamNumber publish GameMessage; direct installed fields do not.
        npc.teamNumber=header.Integer("team");npc.entityName=header.Flag("nameNull")?null:header.Text("name");
        npc.SetDeathTime(header.Integer("deathTime"));
        npc.setHomeArea(new Vector3i(header.Integer("homeX"),header.Integer("homeY"),header.Integer("homeZ")),header.Integer("homeRange"));
        npc.clientEntityId=header.Integer("clientId");npc.lastSleeperPose=header.Integer("sleeperPose");
        var body=npc.bodyDamage;body.StunKnee=header.Integer("stunKnee");body.StunProne=header.Integer("stunProne");
        body.CurrentStun=(EnumEntityStunType)header.Integer("stunType");body.bodyPartHit=(EnumBodyPartHit)header.Integer("bodyPart");
        body.StunDuration=header.Number("stunDuration");body.ShouldBeCrawler=header.Flag("crawler");npc.bodyDamage=body;
        npc.Stats.waitTicks=header.Integer("statWait");npc.Stats.netSyncWaitTicks=header.Integer("statNetWait");
        npc.Stats.m_amountEnclosed=header.Number("statEnclosed");npc.Stats.buffDamageRemainder=header.Number("statBuffRemainder");
        npc.currentHeadState=(EModelBase.HeadStates)header.Integer("headState");npc.inventory.FocusedSlot=header.Integer("focusedSlot");
        npc.Hand.mode=(Hand.HoldingMode)header.Integer("holdingMode");npc.lifetime=header.Number("lifetime");
        npc.OverrideSize=header.Number("size");npc.OverrideHeadSize=header.Number("headSize");npc.onGround=header.Flag("ground");
        npc.IsSleeper=header.Flag("sleeper");npc.IsSleeperPassive=header.Flag("passiveSleeper");npc.IsSleeping=header.Flag("sleeping");
        npc.isDancing=header.Flag("dancing");npc.spawnByAllowShare=header.Flag("spawnShare");
        npc.spawnByName=header.Flag("spawnNameNull")?null:header.Text("spawnName");npc.belongsPlayerId=belongs;npc.spawnById=spawn;
    }
    private static bool Reference(World world,int id,bool allowNpc,out string kind,out string key)
    {
        kind="none";key=id.ToString(CultureInfo.InvariantCulture);if(id<=0)return true;
        var actor=world.GetEntity(id);
        if(actor is EntityPlayer player&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))
        {kind="player";key=identity.CanonicalId;return true;}
        if(allowNpc&&actor is EntityRebirthNPC npc&&npc.RebirthRuntimeState!=null&&!npc.RebirthRuntimeState.StableId.IsEmpty&&
            RebirthNpcRuntimeRegistry.TryGetEntityId(npc.RebirthRuntimeState.StableId,out var canonical)&&canonical==id)
        {kind="npc";key=npc.RebirthRuntimeState.StableId.ToString();return true;}
        return false;
    }
    private static bool Resolve(World world,string kind,string key,out int id)
    {
        id=0;if(world==null||world.IsRemote())return false;
        if(kind=="none")return int.TryParse(key,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out id)&&id<=0;
        if(kind=="npc"&&RebirthNpcStableId.TryParse(key,out var stable)&&RebirthNpcRuntimeRegistry.TryGetEntityId(stable,out id))
            return world.GetEntity(id) is EntityRebirthNPC npc&&npc.RebirthRuntimeState!=null&&npc.RebirthRuntimeState.StableId==stable;
        if(kind!="player"||world.Players?.list==null)return false;
        int matches=0;
        foreach(var player in world.Players.list)
            if(player!=null&&player.entityId>0&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&identity.CanonicalId==key)
            {id=player.entityId;matches++;}
        return matches==1;
    }
    private static void Add(XElement root,string name,int value)=>root.SetAttributeValue(name,value.ToString(CultureInfo.InvariantCulture));
    private static void Add(XElement root,string name,float value)=>root.SetAttributeValue(name,value.ToString("R",CultureInfo.InvariantCulture));
    private static void Add(XElement root,string name,bool value)=>root.SetAttributeValue(name,value?"1":"0");
}