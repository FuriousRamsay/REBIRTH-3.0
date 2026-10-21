using System;
using System.Collections.Generic;
using System.Linq;

// Birth equipment belongs to the authority's actual native inventory, never
// a client visual roll or a repeatable refill when an existing person loads.
internal static class RebirthHumanStartingLoadout
{
    internal static void Apply(EntityRebirthHumanoidNPC npc)
    {
        if(npc==null||npc.world==null||npc.world.IsRemote()||npc.RebirthStartingLoadoutRead||
            npc.IsPreparedRestorationPending||npc.Buffs.GetCustomVar("rbNpcBirthEquipment")==1f)return;
        var definition=EntityClass.list[npc.entityClass];
        string weapons=definition.Properties.GetString("HeldWeapon");
        string armor=definition.Properties.GetString("ArmorItems");
        if(string.IsNullOrWhiteSpace(weapons)&&string.IsNullOrWhiteSpace(armor))return;
        var random=new System.Random(npc.RebirthAppearance.Seed);
        var pieces=Items(armor).Where(v=>v.ItemClass is ItemClassArmor).ToArray();
        int missing=0;int.TryParse(definition.Properties.GetString("RandomArmorItemsMissing"),out missing);
        for(int slot=0;slot<4;slot++)
        {
            if(!RebirthHumanEquipment.Get(npc,slot).IsEmpty())continue;
            if(missing>0&&random.Next(2)==0){missing--;continue;}
            var pool=pieces.Where(v=>RebirthHumanEquipment.Accept(slot,new ItemStack(v,1))).ToArray();
            if(pool.Length>0)RebirthHumanEquipment.Put(npc,slot,Create(pool[random.Next(pool.Length)],random));
        }
        var candidates=Items(weapons).Where(v=>RebirthHumanEquipment.Accept(4,new ItemStack(v,1))||RebirthHumanEquipment.Accept(5,new ItemStack(v,1))).ToArray();
        if(candidates.Length>0&&RebirthHumanEquipment.Get(npc,4).IsEmpty()&&RebirthHumanEquipment.Get(npc,5).IsEmpty())
        {
            var weapon=Create(candidates[random.Next(candidates.Length)],random);
            int slot=RebirthHumanEquipment.Accept(4,weapon)?4:5;
            RebirthHumanEquipment.Put(npc,slot,weapon);
            if(slot==5&&weapon.itemValue.ItemClass.Actions[0] is ItemActionRanged ranged&&ranged.MagazineItemNames?.Length>0)
            {
                var ammunition=ItemClass.GetItem(ranged.MagazineItemNames[0],false);
                if(ammunition!=null&&!ammunition.IsEmpty()&&RebirthHumanEquipment.Get(npc,6).IsEmpty())
                    RebirthHumanEquipment.Put(npc,6,new ItemStack(ammunition,60));
            }
            npc.inventory.SetHoldingItemIdx(slot-4);
        }
        npc.Buffs.SetCustomVar("rbNpcBirthEquipment",1f,true);
    }
    private static ItemStack Create(ItemValue prototype,System.Random random)=>new ItemStack(new ItemValue(prototype.type,1+random.Next(3),3,false),1);
    private static IEnumerable<ItemValue> Items(string names)
    {
        foreach(string name in (names??string.Empty).Split(','))
        {
            if(string.IsNullOrWhiteSpace(name))continue;
            var value=ItemClass.GetItem(name.Trim(),false);
            if(value!=null&&!value.IsEmpty())yield return value;
        }
    }
}

