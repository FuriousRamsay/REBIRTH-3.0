using System;
using System.Collections.Generic;
using Quests.Requirements;

// Exact installed native requirement semantics, evaluated against the authenticated
// server EntityPlayer without local UI or mutating deserialized comparison caches.
internal static class RebirthPoiServerQuestRequirements
{
    public static bool TryCheck(Quest quest,EntityPlayer player,out bool satisfied)
    {
        satisfied=false;
        try
        {
            if(quest==null||player==null||quest.Requirements==null)return false;
            int visited=0;var path=new HashSet<BaseRequirement>();
            foreach(var requirement in quest.Requirements)
            {
                if(requirement==null)return false;
                if(requirement.Phase!=0&&requirement.Phase!=quest.CurrentPhase)continue;
                bool value;if(!Check(requirement,quest,player,path,0,ref visited,out value))return false;
                if(!value)return true;
            }
            satisfied=true;return true;
        }
        catch{return false;}
    }
    private static bool Text(string value,int limit,bool empty)
    {return value==null?empty:value.Length==0?empty:RebirthPoiAuthenticatedRallyScope.ValidIdentifier(value,limit);}
    private static bool Check(BaseRequirement requirement,Quest quest,EntityPlayer player,HashSet<BaseRequirement> path,int depth,ref int visited,out bool result)
    {
        result=false;if(requirement==null||depth>16||++visited>1024||!path.Add(requirement))return false;
        try
        {
            string id=quest.ParseVariable(requirement.ID),value=quest.ParseVariable(requirement.Value);
            if(!Text(id,512,true)||!Text(value,512,true))return false;
            Type type=requirement.GetType();
            if(type==typeof(Quests.Requirements.RequirementGroup))
            {
                var group=(Quests.Requirements.RequirementGroup)requirement;if(group.ChildRequirements==null)return false;
                var op=EnumUtils.Parse<Quests.Requirements.RequirementGroup.GroupOperator>(value);
                if(op!=Quests.Requirements.RequirementGroup.GroupOperator.AND&&op!=Quests.Requirements.RequirementGroup.GroupOperator.OR)return false;
                result=op==Quests.Requirements.RequirementGroup.GroupOperator.AND;
                foreach(var child in group.ChildRequirements)
                {
                    bool childValue;if(!Check(child,quest,player,path,depth+1,ref visited,out childValue))return false;
                    if(op==Quests.Requirements.RequirementGroup.GroupOperator.AND&&!childValue){result=false;return true;}
                    if(op==Quests.Requirements.RequirementGroup.GroupOperator.OR&&childValue){result=true;return true;}
                }
                return true;
            }
            if(type==typeof(RequirementBuff))
            {if(!Text(id,512,false)||player.Buffs==null)return false;result=player.Buffs.HasBuff(id);return true;}
            if(type==typeof(RequirementLevel))
            {if(player.Progression==null)return false;int expected=Convert.ToInt32(value);result=player.Progression.GetLevel()>=expected;return true;}
            if(type==typeof(RequirementHolding))
            {
                if(player.inventory==null||player.inventory.Hand==null)return false;
                ItemValue expected=string.IsNullOrEmpty(id)?player.inventory.Hand.BareHandItemValue:ItemClass.GetItem(id);
                ItemValue held=player.inventory.holdingItemItemValue;if(expected==null||held==null)return false;
                result=held.type==expected.type;return true;
            }
            if(type==typeof(RequirementWearing))
            {
                if(!Text(id,512,false)||player.equipment==null)return false;
                ItemValue expected=ItemClass.GetItem(id);var armor=expected==null?null:expected.ItemClass as ItemClassArmor;
                if(armor==null||armor.EquipSlot==EquipmentSlots.Count){result=false;return true;}
                int slot=(int)armor.EquipSlot;if(slot<0)return false;ItemValue worn=player.equipment.GetSlotItem(slot);
                result=worn!=null&&worn.type==expected.type;return true;
            }
            // Custom polymorphic requirements require their own qualified server adapter.
            return false;
        }
        finally{path.Remove(requirement);}
    }
}
