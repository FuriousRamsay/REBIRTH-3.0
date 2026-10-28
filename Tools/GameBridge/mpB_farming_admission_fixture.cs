using System;
using System.Collections.Generic;
class World{}
class EntityPlayer{}
class EntityPlayerLocal:EntityPlayer{}
enum EnumDropEvent {Harvest}
class ItemValue {public string Name;public ItemValue(string name){Name=name;}public bool IsEmpty()=>Name=="";}
class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue item,int count){itemValue=item;this.count=count;}public bool IsEmpty()=>count==0;}
static class ItemClass {public static ItemValue GetItem(string name)=>new ItemValue(name);}
class Block {public bool Grown;public Dictionary<EnumDropEvent,List<SItemDropProb>> itemsToDrop=new Dictionary<EnumDropEvent,List<SItemDropProb>>();public class SItemDropProb {public string name,tag;public float prob=1;}}
struct BlockValue {public Block Block;public ItemValue ToItemValue()=>new ItemValue("cropBlock");}
static class RebirthUtilities {public static bool IsPlayerGrownHarvestCrop(BlockValue value)=>value.Block.Grown;}
static class RebirthStationGridIngredients {public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&a.itemValue.Name==b.itemValue.Name;}
static class Fixture {
// PRODUCTION_METHODS
static void Check(bool value,string text){if(!value)throw new Exception(text);}
static ItemStack[] Yield(string name,int count)=>new[]{new ItemStack(new ItemValue(name),count)};
static void Main(){var world=new World();var block=new Block{Grown=true};block.itemsToDrop[EnumDropEvent.Harvest]=new List<Block.SItemDropProb>{new Block.SItemDropProb{name="plantedCornSeed",tag="Seed"},new Block.SItemDropProb{name="corn"}};var source=new BlockValue{Block=block};
 Check(ValidOriginalYield(world,new EntityPlayer(),source,Yield("corn",1),true),"remote original base1 refused");
 Check(!ValidOriginalYield(world,new EntityPlayer(),source,Yield("corn",2),true),"remote original rerolled to host multiplier");
 Check(ValidOriginalYield(world,new EntityPlayerLocal(),source,new ItemStack[0],true),"original host zero refused");
 Check(ValidOriginalYield(world,new EntityPlayerLocal(),source,Yield("corn",3),true)&&!ValidOriginalYield(world,new EntityPlayerLocal(),source,Yield("corn",4),true),"host stochastic bounds changed");
 block.Grown=false;Check(ValidOriginalYield(world,new EntityPlayer(),source,Yield("corn",17),false),"original owner native yield substituted");
 Check(!ValidOriginalYield(world,new EntityPlayer(),source,Yield("forgedItem",1),false)&&!ValidOriginalYield(world,new EntityPlayer(),source,Yield("corn",65536),false)&&!ValidOriginalYield(world,new EntityPlayer(),source,Yield("corn",1),true),"invalid native yield admitted");
 Console.WriteLine("PASS actual owner-yield admission: remote/host original, authored item, native count bound");
}
}
