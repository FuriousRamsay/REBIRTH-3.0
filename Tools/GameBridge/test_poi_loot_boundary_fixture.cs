using System;
using System.Collections.Generic;
struct Vector3i {public int x;public Vector3i(int x){this.x=x;}}
abstract class TEFeatureStorage {public abstract Vector3i ToWorldPos();}
class Loot : TEFeatureStorage {public Vector3i Pos=new Vector3i(1);public override Vector3i ToWorldPos(){return Pos;}}
class Tile {public TEFeatureStorage Current;public bool TryGetSelfOrFeature(out TEFeatureStorage value){value=Current;return value!=null;}}
class World {public Tile Tile=new Tile();public Tile GetTileEntity(Vector3i pos){return Tile;}}
class EntityPlayerLocal {public World world=new World();public bool Dead;public bool IsDead(){return Dead;}}
class Xui {public TEFeatureStorage LootContainer;}
class LocalPlayerUI {public Xui xui=new Xui();public static LocalPlayerUI Current=new LocalPlayerUI();public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p){return Current;}}
class RebirthGameBridgeCombat {public static List<int> Threats=new List<int>();public static List<int> AwakeThreats(EntityPlayerLocal p,float radius,bool ignored){return Threats;}}
class Logic {
public static bool Window=true;
private static bool LootWindowOpen(){return Window;}
// PRODUCTION_CLASS
public static bool Take(EntityPlayerLocal p,TEFeatureStorage loot){return CanTakeInspectedLoot(p,new Vector3i(1),loot);}
public static bool Complete(bool dead=false,int skipped=0,int remaining=0,bool loaded=true,string failure=null){return IsLootRunComplete(dead,skipped,remaining,loaded,failure);}
}
class Check {
static void A(bool condition,string message){if(!condition)throw new Exception(message);}
static void Main(){
var p=new EntityPlayerLocal();var source=new Loot();p.world.Tile.Current=source;LocalPlayerUI.Current.xui.LootContainer=source;
A(Logic.Take(p,source),"same inspected loaded target admitted");
p.world.Tile.Current=new Loot();A(!Logic.Take(p,source),"same-position world replacement refuses stale UI");
LocalPlayerUI.Current.xui.LootContainer=p.world.Tile.Current;A(!Logic.Take(p,source),"replacement UI cannot impersonate inspected source");
p.world.Tile.Current=source;LocalPlayerUI.Current.xui.LootContainer=source;
source.Pos=new Vector3i(2);A(!Logic.Take(p,source),"different position refused");source.Pos=new Vector3i(1);
p.Dead=true;A(!Logic.Take(p,source),"death refused");p.Dead=false;
RebirthGameBridgeCombat.Threats.Add(1);A(!Logic.Take(p,source),"new threat refused");RebirthGameBridgeCombat.Threats.Clear();
Logic.Window=false;A(!Logic.Take(p,source),"closed window refused");Logic.Window=true;
p.world.Tile=null;A(!Logic.Take(p,source),"unloaded tile refused");p.world.Tile=new Tile{Current=source};
LocalPlayerUI.Current.xui=null;A(!Logic.Take(p,source),"missing UI refused");LocalPlayerUI.Current.xui=new Xui{LootContainer=source};
A(!Logic.Take(p,null)&&!Logic.Take(null,source),"missing target/player refused");
A(Logic.Complete(),"verified empty run complete");A(!Logic.Complete(dead:true),"death incomplete");A(!Logic.Complete(skipped:1),"skipped incomplete");A(!Logic.Complete(remaining:1),"remaining incomplete");A(!Logic.Complete(loaded:false),"missing chunks incomplete");A(!Logic.Complete(failure:"conservation failure"),"failed storage incomplete even with no remaining containers");A(!Logic.Complete(failure:""),"any failure marker incomplete");A(Logic.Complete(),"next independent successful run can complete");
Console.WriteLine("PASS actual POI open-target/Take All admission and run-completion predicates: replacement/current tile identity, position, death/threat/window availability and failed storage. Native world/UI/combat doubled; click scheduling not executed.");
}}