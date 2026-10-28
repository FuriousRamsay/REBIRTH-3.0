// Deferred actual AfterSnapshot hook with explicit native World/thread/count
// doubles. Does not establish output provenance, receipts or remote delivery.
using System;
using System.Collections.Generic;
namespace UnityEngine {static class Time {internal static float realtimeSinceStartup=1;}}
class EntityPlayer {internal World world;internal int entityId=1;}
class TileEntityWorkstation {internal int Position=7;internal int ToWorldPos()=>Position;}
class World
{
    internal EntityPlayer Player;internal TileEntityWorkstation Tile;
    internal object GetEntity(int id)=>Player;
    internal object GetTileEntity(int position)=>Tile;
}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthStationSnapshotHookFixture
{
    // PRODUCTION_SNAPSHOT_TYPES
    // PRODUCTION_SNAPSHOT_HOOK
    private static World world;
    private static readonly Dictionary<string,Addition> Additions=new Dictionary<string,Addition>();
    private static int drains,queues;
    private static Dictionary<int,int> Counts(Snapshot value)=>new Dictionary<int,int>{{2,3}};
    private static string Pool(EntityPlayer player,TileEntityWorkstation tile,int type)=>"original";
    private static void RememberQueue(TileEntityWorkstation tile){queues++;}
    private static void Drain(){drains++;}
    // The exact admission guard remains under test. Full native payload/outbox
    // settlement is qualified separately; this disclosed observer is a no-op.
    private static void ObserveAcceptedNativeSnapshot(Snapshot value){}
    private static Snapshot Original()
    {
        Additions.Clear();drains=queues=0;world=new World();
        var player=new EntityPlayer{world=world};var tile=new TileEntityWorkstation();
        world.Player=player;world.Tile=tile;GameManager.Instance.World=world;
        return new Snapshot{World=world,Player=player,Tile=tile,Counts=new Dictionary<int,int>{{2,1}}};
    }
    private static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    private static void Refused(Snapshot value,string reason)
    {AfterSnapshot(value,true);Need(drains==0&&queues==0&&Additions.Count==0,reason);}
    public static void Main()
    {
        var original=Original();AfterSnapshot(original,false);
        Need(drains==0&&queues==0&&Additions.Count==0,"Skipped native upload created evidence");
        original=Original();AfterSnapshot(original,true);
        Need(drains==1&&queues==1&&Additions["original"].Count==2,"Actual accepted upload delta lost");
        original=Original();GameManager.Instance.World=new World();Refused(original,"Retired World created evidence");
        original=Original();world.Player=new EntityPlayer{world=world};Refused(original,"Replaced native owner created evidence");
        original=Original();world.Tile=new TileEntityWorkstation();Refused(original,"Replaced native station created evidence");
        original=Original();original.Player.world=new World();Refused(original,"Moved native owner created evidence");
    }
}
