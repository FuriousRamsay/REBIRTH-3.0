using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Birth equipment belongs to the authority's actual native inventory, never
// a client visual roll or a repeatable refill when an existing person loads.
//
// Durable markers (saved CVars): rbNpcBirthEquipment = 1 complete, -1 GAVE UP (never "complete"),
// unset = pending. rbNpcBirthTries counts failed passes, rbNpcBirthPending is the bitmask of REQUIRED
// slots still missing (bits 0-3 armor, bit 4 weapon). Ammunition is optional and never blocks the marker.
internal static class RebirthHumanStartingLoadout
{
    private const string Marker="rbNpcBirthEquipment",Tries="rbNpcBirthTries",Pending="rbNpcBirthPending";
    private const int MaxTries=5,WeaponBit=16,AllBits=31,RetryPerPass=8;
    private const float Backoff=10f;
    private static readonly Dictionary<int,float> NextAttempt=new Dictionary<int,float>();
    private static float nextPass;
    // Persons whose birth state could not be made durable yet (no row / custody unavailable): retried from RetryTick, bounded.
    private static readonly Dictionary<int,int> PersistRetry=new Dictionary<int,int>();
    private const int PersistRetryMax=20;

    // Retry driver; call from an existing repeated server tick (see P2 matrix: RebirthHumanProgression.Tick).
    internal static void RetryTick()
    {
        var world=GameManager.Instance?.World;
        if(world==null){if(PersistRetry.Count>0)PersistRetry.Clear();if(NextAttempt.Count>0)NextAttempt.Clear();return;} // per-world bookkeeping: entity ids are reused by the next world
        if(world.IsRemote()||!ThreadManager.IsMainThread()||Time.unscaledTime<nextPass)return;
        nextPass=Time.unscaledTime+1f;
        int budget=RetryPerPass;
        if(PersistRetry.Count>0)
            foreach(int pid in new List<int>(PersistRetry.Keys))
            {
                if(!(world.GetEntity(pid) is EntityRebirthHumanoidNPC pn)||pn.IsDead()||pn.Buffs==null||++PersistRetry[pid]>PersistRetryMax||RebirthP2NpcEquipmentRestore.PersistBirth(pn,0))PersistRetry.Remove(pid);
                if(--budget<=0)break;
            }
        foreach(var runtime in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if(budget<=0)break;
            if(RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out int rid)&&world.GetEntity(rid) is EntityRebirthHumanoidNPC fresh&&!fresh.IsDead()&&fresh.Buffs!=null&&RebirthP2NpcEquipmentRestore.TryReconcileOnce(fresh)==false)budget--;
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out int id)||world.GetEntity(id) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
                npc.Buffs==null||npc.Buffs.GetCustomVar(Marker)!=0f||npc.Buffs.GetCustomVar(Tries)<=0f)continue;
            budget--;Apply(npc);
        }
    }

    internal static void Apply(EntityRebirthHumanoidNPC npc)
    {
        // Loaded people skip the birth roll below but still reconcile saved bag capacity once.
        // Saved frames and marker first: a reloaded person is restored from its records, never re-rolled.
        if(npc!=null&&npc.world!=null&&!npc.world.IsRemote()){RebirthP2NpcEquipmentRestore.TryReconcileOnce(npc);RebirthP2NpcBagCapacityMigration.TryReconcile(npc);}
        if(npc==null||npc.world==null||npc.world.IsRemote()||npc.IsPreparedRestorationPending||npc.Buffs.GetCustomVar(Marker)!=0f)return;
        int tries=(int)npc.Buffs.GetCustomVar(Tries);
        // A saved failed-birth intent (tries>0) keeps retrying after reload; otherwise loaded people never get birth gear.
        if(npc.RebirthStartingLoadoutRead&&tries<=0)return;
        if(tries>0&&NextAttempt.TryGetValue(npc.entityId,out float due)&&Time.realtimeSinceStartup<due)return;
        var definition=EntityClass.list[npc.entityClass];
        string weapons=definition.Properties.GetString("HeldWeapon");
        string armor=definition.Properties.GetString("ArmorItems");
        if(string.IsNullOrWhiteSpace(weapons)&&string.IsNullOrWhiteSpace(armor))return;
        int pending=tries>0?(int)npc.Buffs.GetCustomVar(Pending):AllBits;
        int failed=0;bool unclean=false;string lastError=null;
        // The random stream is consumed identically on every pass so a retry rebuilds the SAME selection;
        // only slots still pending and still empty are written (no duplicated or newly rolled gear).
        var random=new System.Random(npc.RebirthAppearance.Seed);
        var pieces=Items(armor).Where(v=>v.ItemClass is ItemClassArmor).ToArray();
        int missing=0;int.TryParse(definition.Properties.GetString("RandomArmorItemsMissing"),out missing);
        for(int slot=0;slot<4;slot++)
        {
            if(missing>0&&random.Next(2)==0){missing--;continue;}
            int armorSlot=slot;
            var pool=pieces.Where(v=>RebirthHumanEquipment.Accept(armorSlot,new ItemStack(v,1))).ToArray();
            if(pool.Length==0)continue;
            var piece=Create(pool[random.Next(pool.Length)],random);
            if((pending&(1<<slot))==0||!RebirthHumanEquipment.Get(npc,slot).IsEmpty())continue;
            if(!TryWrite(npc,slot,piece,out string error,out bool clean)){failed|=1<<slot;lastError=error;unclean|=!clean;}
        }
        var candidates=Items(weapons).Where(v=>RebirthHumanEquipment.Accept(4,new ItemStack(v,1))||RebirthHumanEquipment.Accept(5,new ItemStack(v,1))).ToArray();
        if(candidates.Length>0)
        {
            var weapon=Create(candidates[random.Next(candidates.Length)],random);
            if((pending&WeaponBit)!=0&&RebirthHumanEquipment.Get(npc,4).IsEmpty()&&RebirthHumanEquipment.Get(npc,5).IsEmpty())
            {
                int weaponSlot=RebirthHumanEquipment.Accept(4,weapon)?4:5;
                if(!TryWrite(npc,weaponSlot,weapon,out string error,out bool clean)){failed|=WeaponBit;lastError=error;unclean|=!clean;}
                else
                {
                    if(weaponSlot==5&&weapon.itemValue.ItemClass.Actions[0] is ItemActionRanged ranged&&ranged.MagazineItemNames?.Length>0)
                    {
                        var ammunition=ItemClass.GetItem(ranged.MagazineItemNames[0],false);
                        if(ammunition!=null&&!ammunition.IsEmpty()&&RebirthHumanEquipment.Get(npc,6).IsEmpty())
                            TryWrite(npc,6,new ItemStack(ammunition,60),out _,out _); // optional: failure is logged and rolled back, never blocks
                    }
                    npc.inventory.SetHoldingItemIdx(weaponSlot-4);
                }
            }
        }
        Finish(npc,tries,failed,unclean,lastError);
    }

    private static void Finish(EntityRebirthHumanoidNPC npc,int tries,int failed,bool unclean,string lastError)
    {
        if(failed==0)
        {
            npc.Buffs.SetCustomVar(Marker,1f,true);
            npc.Buffs.SetCustomVar(Tries,0f,true);npc.Buffs.SetCustomVar(Pending,0f,true);NextAttempt.Remove(npc.entityId);
            Persist(npc,1);
            return;
        }
        tries++;
        if(unclean||tries>=MaxTries)
        {
            // -1 means GAVE UP, never "complete": required gear is missing and an operator must look.
            npc.Buffs.SetCustomVar(Marker,-1f,true);npc.Buffs.SetCustomVar(Tries,tries,true);npc.Buffs.SetCustomVar(Pending,failed,true);
            NextAttempt.Remove(npc.entityId);
            Persist(npc,-1);
            Log.Warning("[REBIRTH NPC Loadout] birth equipment GAVE UP entity="+npc.entityId+" tries="+tries+" missingMask="+failed+" uncleanRollback="+unclean+" lastError="+lastError);
            return;
        }
        npc.Buffs.SetCustomVar(Tries,tries,true);npc.Buffs.SetCustomVar(Pending,failed,true);
        NextAttempt[npc.entityId]=Time.realtimeSinceStartup+Backoff;
        Persist(npc,0); // the CVars are not engine-persisted: record the retry intent (birthTries/birthPending) when a row exists, else the next checkpoint does
    }

    private static void Persist(EntityRebirthHumanoidNPC npc,int value)
    {
        if(RebirthP2NpcEquipmentRestore.PersistBirth(npc,value))PersistRetry.Remove(npc.entityId);
        else PersistRetry[npc.entityId]=0;
    }

    private static bool TryWrite(EntityRebirthHumanoidNPC npc,int slot,ItemStack stack,out string error,out bool clean)
    {
        error=null;clean=true;
        try{RebirthHumanEquipment.Put(npc,slot,stack,true);return true;}
        catch(Exception e)
        {
            // Roll back exactly this cell so no half-equipped birth gear survives.
            clean=Rollback(npc,slot);error=e.GetType().Name+": "+e.Message;
            Log.Warning("[REBIRTH NPC Loadout] starting write failed entity="+npc.entityId+" slot="+slot+" rollbackClean="+clean+" "+error);
            return false;
        }
    }
    private static bool Rollback(EntityRebirthHumanoidNPC npc,int slot)
    {
        try
        {
            if(slot<4)npc.equipment.SetSlotItem(slot,ItemValue.None);
            else npc.inventory.SetItem(slot-4,ItemStack.Empty.Clone());
            return RebirthHumanEquipment.Get(npc,slot).IsEmpty();
        }
        catch(Exception error){Log.Warning("[REBIRTH NPC Loadout] rollback failed slot="+slot+" "+error.GetType().Name);return false;}
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
