using System;
using System.Collections.Generic;

struct Vector3i {public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}public Vector3 ToVector3Center()=>new(x+.5f,y+.5f,z+.5f);}
struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);}
class Block {public static Block[] list=new Block[32];}
class BlockPoweredLight:Block {}
class BlockPoweredTrap:Block {}
class BlockRanged:Block {}
class BlockLauncher:Block {}
class TileEntityPowered {public int ChildCount;public bool Parent;public bool HasParent()=>Parent;}
class TileEntityPoweredBlock:TileEntityPowered {}
class TileEntityPoweredRangedTrap:TileEntityPoweredBlock {public string Owner;public bool IsOwner(string owner)=>Owner==owner;}
class PowerItem {public Vector3i Position;public ushort BlockID,RequiredPower;public PowerItem Parent;public List<PowerItem> Children=new();public TileEntityPowered TileEntity;}
class PowerConsumer:PowerItem {}
class PowerConsumerToggle:PowerConsumer {public bool IsToggled;}
class PowerRangedTrap:PowerConsumer {}
class PlayerData {public string Owner;}
class PlayerList {public Dictionary<string,PlayerData> Players=new();public PlayerData GetPlayerData(string id)=>Players.TryGetValue(id,out var p)?p:null;}
class GameManager {public static GameManager Instance=new();public PlayerList Players=new();public PlayerList GetPersistentPlayerList()=>Players;}
enum EnumLandClaimOwner {None,Self,Other}
struct BlockValue {public byte meta;}
class EntityEnemy {public bool Dead,IsSleeping;public Vector3 position;public bool IsDead()=>Dead;}
class Entities {public List<object> list=new();}
class World
{
    public byte Meta;public Entities Entities=new();public Dictionary<string,EnumLandClaimOwner> Claims=new();
    public BlockValue GetBlock(Vector3i p)=>new(){meta=Meta};
    public EnumLandClaimOwner GetLandClaimOwner(Vector3i p,PlayerData player)=>Claims.TryGetValue(player.Owner,out var c)?c:EnumLandClaimOwner.None;
}
class EntityVehicle {public string Owner;public World world;public string GetOwner()=>Owner;}
class PrefabInstance {public Vector3i boundingBoxPosition,boundingBoxSize;}
partial class Adapter
{
    private static float DefenseRange=12f;
    internal class Baseline {public ushort Block;public byte Meta,PreviousMeta,ExpectedMeta;public bool HasToggle,Toggle,PreviousToggle,ExpectedToggle,Applied;}
    internal class Session {public EntityVehicle Vehicle;public World World;public Dictionary<Vector3i,Baseline> Baselines=new();}
    public static bool TestEligible(PowerItem p)=>Eligible(p);
    public static bool TestExpected(Session s,PowerItem p)=>MatchesExpected(s,p);
    public static bool TestOwner(Session s,PowerItem p)=>DeviceOwnerAuthorized(s,p);
    public static bool TestPoiOwner(EntityVehicle v,PowerItem p)=>PoiOwnerAuthorized(v,p);
    public static bool TestInside(PrefabInstance p,Vector3i x)=>Inside(p,x);
    public static bool TestEnemy(World w,Vector3i p)=>EnemyNear(w,p);
}
class Program
{
    static int passed;
    static void Check(bool value,string label){if(!value)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
    static void Main()
    {
        Block.list[1]=new BlockPoweredLight();Block.list[2]=new BlockPoweredTrap();Block.list[3]=new BlockRanged();Block.list[4]=new BlockLauncher();Block.list[5]=new Block();
        var node=new PowerConsumerToggle{BlockID=1,RequiredPower=7,TileEntity=new TileEntityPoweredBlock(),Position=new(1,2,3),IsToggled=false};
        Check(Adapter.TestEligible(node),"native metered unwired leaf lamp eligible");
        node.Parent=new();Check(!Adapter.TestEligible(node),"existing parent circuit refused");node.Parent=null;
        node.Children.Add(new());Check(!Adapter.TestEligible(node),"existing children untouched");node.Children.Clear();
        node.TileEntity.Parent=true;Check(!Adapter.TestEligible(node),"native tile wire parent refused");node.TileEntity.Parent=false;
        node.TileEntity.ChildCount=1;Check(!Adapter.TestEligible(node),"native tile wire children refused");node.TileEntity.ChildCount=0;
        node.RequiredPower=0;Check(!Adapter.TestEligible(node),"unmetered decorative consumer refused");node.RequiredPower=7;
        node.BlockID=5;Check(!Adapter.TestEligible(node),"unaudited generic device refused");node.BlockID=1;
        var world=new World();var vehicle=new EntityVehicle{world=world,Owner="owner"};var session=new Adapter.Session{Vehicle=vehicle,World=world};
        session.Baselines[node.Position]=new(){Block=1,Meta=0,PreviousMeta=0,ExpectedMeta=2,HasToggle=true,PreviousToggle=false,ExpectedToggle=true};
        Check(Adapter.TestExpected(session,node),"pending native exact preimage accepted");
        world.Meta=2;node.IsToggled=true;Check(Adapter.TestExpected(session,node),"exact intended postimage accepted");
        session.Baselines[node.Position].Applied=true;world.Meta=0;node.IsToggled=false;Check(!Adapter.TestExpected(session,node),"manual return to old preimage after known applied state refused");
        world.Meta=6;node.IsToggled=true;Check(!Adapter.TestExpected(session,node),"foreign metadata change refused");
        world.Meta=2;node.IsToggled=false;Check(!Adapter.TestExpected(session,node),"foreign toggle change refused");
        node.IsToggled=true;node.BlockID=2;Check(!Adapter.TestExpected(session,node),"replacement block class refused");node.BlockID=1;
        GameManager.Instance.Players.Players["owner"]=new(){Owner="owner"};
        Check(!Adapter.TestOwner(session,node),"generic device proximity does not establish owner");
        world.Claims["owner"]=EnumLandClaimOwner.Self;Check(Adapter.TestOwner(session,node),"actual Self native land claim authorizes generic device");
        world.Claims["owner"]=EnumLandClaimOwner.Other;Check(!Adapter.TestOwner(session,node)&&!Adapter.TestPoiOwner(vehicle,node),"foreign claim blocks placed device and POI light");
        world.Claims["owner"]=EnumLandClaimOwner.None;Check(Adapter.TestPoiOwner(vehicle,node),"unclaimed native POI light authorized");
        node.TileEntity=new TileEntityPoweredRangedTrap{Owner="owner"};Check(Adapter.TestOwner(session,node),"native ranged owner authorizes outside land claim");
        ((TileEntityPoweredRangedTrap)node.TileEntity).Owner="foreign";Check(!Adapter.TestOwner(session,node),"native foreign turret owner refused");
        var poi=new PrefabInstance{boundingBoxPosition=new(0,0,0),boundingBoxSize=new(8,8,8)};
        Check(Adapter.TestInside(poi,new(0,0,0))&&!Adapter.TestInside(poi,new(8,0,0))&&!Adapter.TestInside(poi,new(0,8,0)),"native POI bounds are half-open in all dimensions");
        Check(!Adapter.TestEnemy(world,new(0,0,0)),"idle defense sees no active enemy");
        var enemy=new EntityEnemy{position=new(2,0,2),IsSleeping=true};world.Entities.list.Add(enemy);
        Check(!Adapter.TestEnemy(world,new(0,0,0)),"sleeping enemy does not wake defense");enemy.IsSleeping=false;
        Check(Adapter.TestEnemy(world,new(0,0,0)),"awake native enemy wakes defense");enemy.Dead=true;
        Check(!Adapter.TestEnemy(world,new(0,0,0)),"dead enemy does not consume active defense demand");
        Console.WriteLine($"PASS {passed} extracted actual adapter predicate checks; native/world/ownership dependencies doubled; no activation/save/network proof.");
    }
}
