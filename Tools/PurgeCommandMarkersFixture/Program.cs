using System;
using System.Collections.Generic;
class EntityPlayer {public int entityId=171;}
class RebirthPoiIdentity {public string Key;}
class RebirthPoiClearanceRecord {public long Epoch;}
class Snapshot {public RebirthPoiClearanceRecord Record=new();public bool TryGet(RebirthPoiIdentity i,out RebirthPoiClearanceRecord record){record=Record;return true;}}
class RebirthPoiWorldStore {public Snapshot Published=new();}
class RebirthPoiWorldLifecycle {public static RebirthPoiWorldLifecycle Instance=new();public RebirthPoiWorldStore Store=new();public bool TryGetEvidenceStore(out RebirthPoiWorldStore store){store=Store;return true;}}
class RebirthPurgeSleeperCommand {public static bool Tracking;public static bool IsTracking(object w,EntityPlayer p,object prefab)=>Tracking;}
class Program {
 class RoomHistory {public string PoiKey;public long Epoch;public bool CommandMarkers;}
 class View {public long Epoch;public bool Assisted;}
 static Dictionary<int,RoomHistory> roomHistory=new();
    internal static bool SetCommandMarkers(EntityPlayer player,RebirthPoiIdentity identity,bool enabled)
    {
        if(player==null)return false;
        RoomHistory history;
        if(!enabled)
        {
            if(!roomHistory.TryGetValue(player.entityId,out history)||!history.CommandMarkers)return false;
            history.CommandMarkers=false;return true;
        }
        if(identity==null)return false;
        RebirthPoiWorldStore store;RebirthPoiClearanceRecord record=null;
        if(RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)&&store.Published!=null)
            store.Published.TryGet(identity,out record);
        long epoch=record?.Epoch??0;
        if(!roomHistory.TryGetValue(player.entityId,out history)||history.PoiKey!=identity.Key||history.Epoch!=epoch)
        {
            if(roomHistory.Count>512)roomHistory.Clear();
            history=new RoomHistory{PoiKey=identity.Key,Epoch=epoch};roomHistory[player.entityId]=history;
        }
        history.CommandMarkers=true;return true;
    }
    internal static bool Threshold(int tier,int total,int cleared)
    {return total>0&&(tier<4?total>2&&total-cleared<=1:total-cleared<=(tier==4?2:tier==5?3:4));}
 static bool Project(EntityPlayer player,RebirthPoiIdentity identity) {
 var result=new View{Epoch=RebirthPoiWorldLifecycle.Instance.Store.Published.Record.Epoch};
 object world=null,prefab=null;
        RoomHistory history;
        if(!roomHistory.TryGetValue(player.entityId,out history)||history.PoiKey!=identity.Key||history.Epoch!=result.Epoch)
        {
            if(roomHistory.Count>512)roomHistory.Clear();
            history=new RoomHistory{PoiKey=identity.Key,Epoch=result.Epoch};roomHistory[player.entityId]=history;
        }
result.Assisted=history.CommandMarkers||RebirthPurgeSleeperCommand.IsTracking(world,player,prefab);
 return result.Assisted;
 }
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Main(){
 var player=new EntityPlayer();var poi=new RebirthPoiIdentity{Key="farm_05"};
 Check(!Threshold(3,18,16),"native Tier3 threshold would hide the two remaining rooms");
 Check(!Project(player,poi),"ordinary projection has no command bypass");
 Check(SetCommandMarkers(player,poi,true),"command admits scoped markers");
 Check(!RebirthPurgeSleeperCommand.Tracking&&Project(player,poi),"markers survive activation job completion");
 Check(Project(player,poi),"same POI epoch retains marker admission");
 Check(SetCommandMarkers(player,null,false)&&!Project(player,poi),"explicit stop removes marker admission after job completion");
 SetCommandMarkers(player,poi,true);
 Check(!Project(player,new RebirthPoiIdentity{Key="other_poi"}),"another POI cannot inherit assistance");
 Check(!Project(player,poi),"return after a different POI does not revive prior assistance");
 SetCommandMarkers(player,poi,true);RebirthPoiWorldLifecycle.Instance.Store.Published.Record.Epoch++;
 Check(!Project(player,poi),"new epoch discards marker admission");
 SetCommandMarkers(player,poi,true);Check(!Project(new EntityPlayer{entityId=172},poi),"other player does not inherit command admission");
 Check(!SetCommandMarkers(null,poi,true)&&!SetCommandMarkers(player,null,true),"invalid admission refused");
 Console.WriteLine(checks+" PASS; actual scanner grant, scope and projection source extracted; native store adapters doubled");
 }
}
