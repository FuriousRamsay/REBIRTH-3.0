using System;using System.Collections.Generic;using System.Linq;
// Actual own setter loop, one-use Consume guard and native SavePrefix readback.
// Native grid/save and typed permission/metadata policy remain adapters.
class ItemStack{internal int Value;internal ItemStack(int value){Value=value;}internal ItemStack Clone()=>new ItemStack(Value);}
class ItemStackGrid
{
 internal ItemStack[] items;internal int Calls;internal bool Suppress,ThrowAfter;
 internal void SetItem(int slot,ItemStack value){Calls++;if(Suppress)return;if(!RebirthNpcTradeOwnerExchange.Consume(this,slot,value))throw new Exception("setter authority refused");items[slot]=value.Clone();if(ThrowAfter)throw new Exception("unknown native return");}
}
class EntityPlayerLocal{internal int entityId=42;internal Buffs Buffs=new Buffs();internal ItemStackGrid Bag,Belt;}
class Buffs{internal Dictionary<string,float> Values=new Dictionary<string,float>();internal void SetCustomVar(string key,float value,bool sync){Values[key]=value;}}
class PlayerDataFile{internal int id=42,deaths;internal bool bDead;internal ItemStack[] Bag,Belt;internal Dictionary<string,float> Vars;}
class GameManager
{
 internal static GameManager Instance=new GameManager();internal EntityPlayerLocal Player;internal List<int> Steps=new List<int>();internal int Uploads,FailAt=-1,Saves;
 internal void SaveLocalPlayerData(){Saves++;Steps.Add((int)Player.Buffs.Values["step"]);if(Saves==FailAt)return;RebirthGearNativePlayerFile.Saved=new PlayerDataFile{Bag=Player.Bag.items.Select(s=>s.Clone()).ToArray(),Belt=Player.Belt.items.Select(s=>s.Clone()).ToArray(),Vars=new Dictionary<string,float>(Player.Buffs.Values)};}
 internal void TriggerSendOfLocalPlayerDataFile(float delay){Uploads++;}
}
class RebirthNpcTradeOriginal
{
 internal Guid Operation=Guid.NewGuid();internal string Digest="original",Account="owner";internal int Deaths;
 internal ItemStack[] CopySlots(bool bag,bool after)=>new[]{new ItemStack(bag?(after?9:1):(after?8:2))};
}
static class RebirthNpcInteractionNetworkClient{internal class NativeTradeGrant{internal RebirthNpcTradeOriginal Original;internal string GrantDigest="grant";internal bool Valid=true;internal bool Current()=>Valid;}}
static class RebirthNpcTradeService{internal class OwnerAdmission{internal class LocalPermission{internal RebirthNpcTradeOriginal Original;internal bool Valid=true;internal GrantValue Grant=new GrantValue();internal bool Current()=>Valid;internal class GrantValue{internal string Image="local-grant";}}}}
class Identity{internal string CanonicalId="owner";}
static class RebirthStablePlayerIdentity{internal static bool TryFromLocalPlatform(out Identity owner){owner=new Identity();return true;}}
static class RebirthGearNativePlayerFile{internal static PlayerDataFile Saved;internal static bool TryRead(Identity owner,out PlayerDataFile saved){saved=Saved;return saved!=null;}}
static class RebirthGearPlayerDataInventory{internal static ItemStack[] ReadSlots(PlayerDataFile data,bool bag)=>bag?data.Bag:data.Belt;}
static class RebirthRemoteResourcePlacedStationSavedReceipt{internal static bool TryRead(PlayerDataFile data,string key,out float value,out bool present){present=data.Vars.TryGetValue(key,out value);return true;}}
static class RebirthNpcTradeOwnerCheckpoint{internal static string Marker(Guid id)=>"phase";}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack left,ItemStack right)=>left.Value==right.Value;}
static class RebirthNpcTradeOwnerExchange
{
 private sealed class Change{internal bool Bag;internal int Slot;internal ItemStack Value;}
 private sealed class Hold{internal EntityPlayerLocal Player;internal ItemStackGrid Bag,Belt;internal RebirthNpcTradeOriginal Original;internal List<Change> Changes;internal int Progress;internal bool Entered,NeedsSave;internal RebirthNpcInteractionNetworkClient.NativeTradeGrant Grant;internal RebirthNpcTradeService.OwnerAdmission.LocalPermission LocalGrant;}
 private sealed class Permit{internal Hold Hold;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used;}
 private static Dictionary<Guid,Hold> holds=new Dictionary<Guid,Hold>();[ThreadStatic]private static Permit permit;
 private static bool Metadata=true;
 private static bool MetadataCurrent(Hold hold,bool descriptionChecked=false)=>Metadata;
 private static string Step(Guid id)=>"step";
 internal static bool TryHoldBefore(EntityPlayerLocal player,RebirthNpcTradeOriginal original)=>false;
 // ACTUAL_OWNER_LOOP
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Owner loop assertion "+checks);}
  Hold Seed()
  {
   var original=new RebirthNpcTradeOriginal();var player=new EntityPlayerLocal{Bag=new ItemStackGrid{items=original.CopySlots(true,false)},Belt=new ItemStackGrid{items=original.CopySlots(false,false)}};
   var hold=new Hold{Player=player,Bag=player.Bag,Belt=player.Belt,Original=original,Changes=new List<Change>{new Change{Bag=true,Slot=0,Value=new ItemStack(9)},new Change{Bag=false,Slot=0,Value=new ItemStack(8)}}};
   hold.LocalGrant=new RebirthNpcTradeService.OwnerAdmission.LocalPermission{Original=original};holds.Clear();holds.Add(original.Operation,hold);permit=null;Metadata=true;
   GameManager.Instance=new GameManager{Player=player};RebirthGearNativePlayerFile.Saved=null;return hold;
  }
  var h=Seed();Check(TryApplyLocal(h.Player,h.LocalGrant)&&h.Progress==2&&!h.Entered&&!h.NeedsSave);
  Check(h.Bag.Calls==1&&h.Belt.Calls==1&&h.Bag.items[0].Value==9&&h.Belt.items[0].Value==8);
  Check(GameManager.Instance.Steps.SequenceEqual(new[]{1,2,3,4,4})&&RebirthGearNativePlayerFile.Saved.Vars["phase"]==2);
  Check(TryApplyLocal(h.Player,h.LocalGrant)&&h.Bag.Calls==1&&h.Belt.Calls==1);
  h=Seed();var remote=new RebirthNpcInteractionNetworkClient.NativeTradeGrant{Original=h.Original};Check(!TryApply(h.Player,remote)&&h.Bag.Calls==0);
  h=Seed();h.LocalGrant.Valid=false;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Bag.Calls==0);
  h=Seed();Metadata=false;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Bag.Calls==0);
  h=Seed();h.Bag.Suppress=true;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Entered&&h.Progress==0&&h.Bag.items[0].Value==1);
  h.Bag.Suppress=false;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Bag.Calls==1);
  h=Seed();h.Bag.ThrowAfter=true;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Entered&&h.Progress==0&&h.Bag.items[0].Value==9);
  h.Bag.ThrowAfter=false;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Bag.Calls==1);
  h=Seed();GameManager.Instance.FailAt=1;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Progress==0&&!h.Entered&&h.Bag.Calls==0);
  GameManager.Instance.FailAt=-1;Check(TryApplyLocal(h.Player,h.LocalGrant)&&h.Bag.Calls==1&&h.Belt.Calls==1);
  h=Seed();GameManager.Instance.FailAt=2;Check(!TryApplyLocal(h.Player,h.LocalGrant)&&h.Progress==1&&h.NeedsSave&&!h.Entered&&h.Bag.Calls==1&&h.Belt.Calls==0);
  GameManager.Instance.FailAt=-1;Check(TryApplyLocal(h.Player,h.LocalGrant)&&h.Progress==2&&h.Bag.Calls==1&&h.Belt.Calls==1);
  h=Seed();Check(!Consume(h.Bag,0,new ItemStack(9))&&h.Bag.Calls==0);
  var once=new Permit{Hold=h,Grid=h.Bag,Slot=0,Value=new ItemStack(9)};permit=once;
  Check(!Consume(h.Bag,0,new ItemStack(9))&&!once.Used);
  Check(Consume(h.Bag,0,once.Value)&&once.Used&&!Consume(h.Bag,0,once.Value));permit=null;
  Console.WriteLine(checks+" actual owner setter-loop assertions passed; native save/grid/typed permission adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcTradeOwnerExchange.Run();}
