using System;using System.Collections;using System.Collections.Generic;
struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class Block {public string Name;public BlockValue DowngradeBlock;public string GetBlockName(){return Name;}}
struct BlockValue {public bool isair,ischild;public Block Block;}
class Chunk {}
class World {public int Reads;public HashSet<int> Missing=new HashSet<int>(); public Dictionary<Vector3i,BlockValue> Blocks=new Dictionary<Vector3i,BlockValue>();public static int toChunkXZ(int x){return x>>4;} public object GetChunkSync(int x,int z){return Missing.Contains(x)?null:new Chunk();} public BlockValue GetBlock(int x,int y,int z){Reads++;BlockValue b;return Blocks.TryGetValue(new Vector3i(x,y,z),out b)?b:new BlockValue{isair=true};}}
class EntityPlayerLocal {public World world=new World();public bool Dead;public bool IsDead(){return Dead;}}
class PrefabInstance {public Vector3i boundingBoxPosition,boundingBoxSize;}
class Check {
 struct LootSpot {public Vector3i Pos;public string Name;}
 static int beats;static void Beat(){beats++;}
// METHODS
 static void Require(bool v,string message){if(!v)throw new Exception(message);}
 static BlockValue Crate(string name,bool child=false,bool downgrade=true){return new BlockValue{ischild=child,Block=new Block{Name=name,DowngradeBlock=new BlockValue{isair=!downgrade}}};}
 static int Run(EntityPlayerLocal p,PrefabInstance pi,List<LootSpot> list){var e=FindSealedCrates(p,pi,list);int yields=0;while(e.MoveNext())yields++;return yields;}
 static void Main(){
 var p=new EntityPlayerLocal();var pi=new PrefabInstance{boundingBoxPosition=new Vector3i(-16,-2,0),boundingBoxSize=new Vector3i(48,8,16)};
 p.world.Missing.Add(1);
 p.world.Blocks[new Vector3i(-1,1,1)]=Crate("cntShippingCrateShamway");
 p.world.Blocks[new Vector3i(0,1,1)]=Crate("cntShippingCrateConstructionSupplies");
 p.world.Blocks[new Vector3i(1,1,1)]=Crate("cntShippingCrateHero",true);
 p.world.Blocks[new Vector3i(2,1,1)]=Crate("cntShippingCrateHero",false,false);
 p.world.Blocks[new Vector3i(3,1,1)]=Crate("cntWoodWritableCrate");
 p.world.Blocks[new Vector3i(16,1,1)]=Crate("cntShippingCrateHero");
 var list=new List<LootSpot>{new LootSpot{Pos=new Vector3i(-1,1,1),Name="known"}};
 int yields=Run(p,pi,list);
 Require(list.Count==2,"Missing or duplicate crate / included ordinary, child or unloaded block");
 Require(p.world.Reads==32*6*16,"Scan escaped loaded bounds");Require(yields==1&&beats==1,"Large scan did not yield every2048 reads");
 p.Dead=true;int reads=p.world.Reads;Run(p,pi,list);Require(reads==p.world.Reads,"Dead player scan continued");
 Require(!IsSealedLootCover(new BlockValue{isair=true}),"Air classified");
 foreach(string n in new[]{"hiddenSafePictureFrame_01a","hiddenSafePaintingBen","cntLootWeapons","cntLootTools"}) Require(IsSealedLootCover(Crate(n)),"Missed cover "+n);
 foreach(string n in new[]{"cntLootWeaponsInsecure","cntLootToolsInsecure","pictureFrame_01a","paintingBen","cntWallSafe"}) Require(!IsSealedLootCover(Crate(n)),"Will attack revealed/decorative block "+n);

 Console.WriteLine("PASS: actual sealed-crate scanner: negative chunk coordinates, loaded bounds, deduplication, child/ordinary/no-downgrade exclusion, construction wrapper, bounded frame yielding and dead-player stop. Native world replaced by fixture.");
 }
}
