using System;
class Program
{
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static ItemStack Stack(int count,int type=1)=>new ItemStack{count=count,itemValue=new ItemValue{type=type}};
    static World Client(int id=7){var w=new World();w.player=new EntityPlayerLocal{entityId=id,world=w};w.player.bag.ItemGrid.items=new[]{Stack(5)};w.player.inventory.ItemGrid.items=new[]{Stack(3)};GameManager.Instance.World=w;return w;}
    static void Main()
    {
        var peer=Client();var operation=Guid.NewGuid();
        Check(RebirthConsumptionOwnerPublication.Apply(peer,7,operation,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4)),"peer ordered source publication");
        Check(peer.player.bag.ItemGrid.items[0].count==4,"peer consumes exactly one");
        Check(!RebirthConsumptionOwnerPublication.Apply(peer,7,operation,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4)),"peer duplicate is refused");
        Check(peer.player.bag.ItemGrid.items[0].count==4,"peer duplicate does not consume twice");
        var dedicated=Client();
        Check(RebirthConsumptionOwnerPublication.Apply(dedicated,7,Guid.NewGuid(),RebirthMetabolismSourceKind.HeldToolbelt,0,Stack(3),Stack(2)),"dedicated delivery to held source");
        Check(dedicated.player.inventory.ItemGrid.items[0].count==2 && dedicated.player.bag.ItemGrid.items[0].count==5,"dedicated source cell only");
        dedicated.player.bag.ItemGrid.items[0]=Stack(9,2);
        Check(!RebirthConsumptionOwnerPublication.Apply(dedicated,7,Guid.NewGuid(),RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4)),"delayed delivery after item movement refused");
        Check(dedicated.player.bag.ItemGrid.items[0].count==9&&dedicated.player.bag.ItemGrid.items[0].itemValue.type==2,"unrelated moved item preserved");
        Check(!RebirthConsumptionOwnerPublication.Apply(dedicated,8,Guid.NewGuid(),RebirthMetabolismSourceKind.Backpack,0,Stack(9,2),Stack(8,2)),"wrong owner refused");
        Check(!RebirthConsumptionOwnerPublication.Apply(dedicated,7,Guid.NewGuid(),RebirthMetabolismSourceKind.Backpack,2,Stack(9,2),Stack(8,2)),"invalid source refused");
        var synchronized=Client();synchronized.player.bag.ItemGrid.items[0]=Stack(4);var synchronizedId=Guid.NewGuid();
        Check(RebirthConsumptionOwnerPublication.Apply(synchronized,7,synchronizedId,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4)),"native synchronization before reply acknowledged");
        Check(synchronized.player.bag.Writes==0&&synchronized.player.bag.ItemGrid.items[0].count==4,"already published reply performs no write");
        Check(!RebirthConsumptionOwnerPublication.Apply(synchronized,7,synchronizedId,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4)),"duplicate synchronized reply suppressed");
        synchronized.player.bag.ItemGrid.items[0]=Stack(3);
        Check(!RebirthConsumptionOwnerPublication.Apply(synchronized,7,Guid.NewGuid(),RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4)),"different postimage not accepted");
        Check(synchronized.player.bag.Writes==0&&synchronized.player.bag.ItemGrid.items[0].count==3,"changed postimage preserved");
        var finalServing=Client();finalServing.player.bag.ItemGrid.items[0]=Stack(1);var finalId=Guid.NewGuid();
        Check(RebirthConsumptionOwnerPublication.Apply(finalServing,7,finalId,RebirthMetabolismSourceKind.Backpack,0,Stack(1),Stack(0,0)),"final serving publishes empty source cell");
        Check(finalServing.player.bag.ItemGrid.items[0].count==0&&finalServing.player.bag.ItemGrid.items[0].itemValue.type==0,"final serving leaves no source item");
        Check(!RebirthConsumptionOwnerPublication.Apply(finalServing,7,finalId,RebirthMetabolismSourceKind.Backpack,0,Stack(1),Stack(0,0)),"duplicate final serving cannot replay");
        RebirthSurvivorMode.Enabled=true;
        var scoped=Client();var scopedId=Guid.NewGuid();string creation=RebirthSurvivorClientState.Creation;
        Check(!RebirthConsumptionOwnerPublication.Apply(scoped,7,scopedId,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4)),"unscoped Survivor publication refused");
        RebirthSurvivorClientState.Creation="22222222222222222222222222222222";
        Check(!RebirthConsumptionOwnerPublication.Apply(scoped,7,scopedId,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4),creation),"delayed previous-character publication refused");
        RebirthSurvivorClientState.Creation=creation;
        RebirthCharacterCreationHoldService.Held=true;
        Check(!RebirthConsumptionOwnerPublication.Apply(scoped,7,scopedId,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4),creation),"held replacement refuses still-old projected scope");
        RebirthCharacterCreationHoldService.Held=false;
        GameManager.Instance.World=peer;
        Check(!RebirthConsumptionOwnerPublication.Apply(scoped,7,scopedId,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4),creation),"direct retired-world application refused");
        GameManager.Instance.World=scoped;
        Check(RebirthConsumptionOwnerPublication.Apply(scoped,7,scopedId,RebirthMetabolismSourceKind.Backpack,0,Stack(5),Stack(4),creation),"matching character publication admitted");
        RebirthSurvivorMode.Enabled=false;
        ThreadManager.main=false;
        Check(!RebirthConsumptionOwnerPublication.Apply(dedicated,7,Guid.NewGuid(),RebirthMetabolismSourceKind.Backpack,0,Stack(9,2),Stack(8,2)),"off-thread publication refused");
        Console.WriteLine("SIMULATION ONLY: actual owner-publication source; native inventory/world doubles. No sockets, game effects, server debit or multiplayer gameplay validated.");
    }
}
public static class ThreadManager{public static bool main=true;public static bool IsMainThread()=>main;}
public enum RebirthMetabolismSourceKind{Backpack,HeldToolbelt}
public sealed class World{public EntityPlayerLocal player;public bool IsRemote()=>true;public EntityPlayerLocal GetPrimaryPlayer()=>player;public EntityPlayer GetEntity(int id)=>player?.entityId==id?player:null;}
// Empty native base shape required only by the exact no-op diagnostic signature.
public class EntityPlayer{public int entityId;public bool IsDead()=>false;public World world;}
public sealed class EntityPlayerLocal:EntityPlayer{public Bag bag=new Bag();public Inventory inventory=new Inventory();}
public sealed class Grid{public ItemStack[] items;}
public sealed class Bag{public Grid ItemGrid=new Grid();public int Writes;public void SetSlot(int i,ItemStack s){Writes++;ItemGrid.items[i]=s;}}
public sealed class Inventory{public Grid ItemGrid=new Grid();public void SetItem(int i,ItemStack s)=>ItemGrid.items[i]=s;}
public sealed class ItemValue{public int type;public override bool Equals(object other)=>other is ItemValue v&&v.type==type;public override int GetHashCode()=>type;}
public sealed class ItemStack{public int count;public ItemValue itemValue;public ItemStack Clone()=>new ItemStack{count=count,itemValue=new ItemValue{type=itemValue.type}};}


// Explicit character-scope boundaries: vanilla unless a fixture enables Survivor.
static class RebirthSurvivorMode{public static bool Enabled;public static bool IsEnabledForCurrentWorld()=>Enabled;}
sealed class RebirthWorldCharacterRecord{public bool IsComplete;public ScopeOrigin Origin;}
sealed class ScopeOrigin{public string CreationId;}
static class RebirthWorldCharacterService{public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=null;return false;}}
static class RebirthWorldCharacterRepository{public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>false;}
static class RebirthSurvivorRequestScope{public static bool TryNormalize(string s,out string n){n=null;if(!Guid.TryParse(s,out var g)||g==Guid.Empty)return false;n=g.ToString("N");return true;}}
static class RebirthSurvivorClientState{public static string Creation="11111111111111111111111111111111";public static string GetProjectedCreationId(EntityPlayer p)=>Creation;}

public sealed class GameManager{public static GameManager Instance=new GameManager();public World World;}
static class RebirthCharacterCreationHoldService{public static bool Held;public static bool IsHeld(EntityPlayer p)=>Held;}
