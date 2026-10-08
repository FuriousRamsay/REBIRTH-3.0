using System;using System.Collections.Generic;using System.Xml.Linq;
class Entity {public int entityId=7;public World world;}
class World {public Entity Present;public int Publications;public Action Callback;public Entity GetEntity(int id){return Present;}public void SpawnEntityInWorld(Entity e){Publications++;Present=e;if(Callback!=null)Callback();}}
class GameManager {public static GameManager Instance=new GameManager();public World World;}
class RebirthNpcPendingSpawn {public string ReplayKey="spawn:a";public bool IsPublishing;public XElement Write(){return new XElement("request",new XAttribute("phase",IsPublishing));}public bool TryMarkPublishing(out RebirthNpcPendingSpawn next){next=null;if(IsPublishing)return false;next=new RebirthNpcPendingSpawn{IsPublishing=true};return true;}}
class Gate {public bool Claimed;public bool TryBegin(string key,out Guid lease){lease=Guid.NewGuid();if(Claimed)return false;Claimed=true;return true;}public void ReleaseKnown(string key,Guid lease){Claimed=false;}}
public class PublishFixture {
 static object Sync=new object();static Gate SpawnAttempts=new Gate();static Dictionary<string,RebirthNpcPendingSpawn> PendingSpawns=new Dictionary<string,RebirthNpcPendingSpawn>();static bool dirty;static bool SaveWorks=true;static bool ConfirmWorks=true;
 static void SaveIfDirty(){if(!dirty)return;if(!SaveWorks)throw new Exception("save fault");dirty=false;}
 static bool HasSavedPendingSpawn(RebirthNpcPendingSpawn r){return SaveWorks;}
 static bool TryConfirmConstructedSpawn(string id,Entity e,out RebirthNpcPendingSpawn r){PendingSpawns.TryGetValue("spawn:"+id,out r);return ConfirmWorks&&r!=null;}
 static bool TryObservePublishedSpawn(string id,World w,Entity e,out RebirthNpcPendingSpawn r){r=null;return ReferenceEquals(GameManager.Instance.World,w)&&ReferenceEquals(w.Present,e);}
 // ACTUAL_METHOD
 static void Reset(World w){w.Present=null;w.Publications=0;w.Callback=null;GameManager.Instance.World=w;SpawnAttempts=new Gate();PendingSpawns.Clear();PendingSpawns.Add("spawn:a",new RebirthNpcPendingSpawn());SaveWorks=ConfirmWorks=true;dirty=false;}
 public static string Run(){int count=0;var w=new World();var e=new Entity{world=w};string reason;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);count++;};
 Reset(w);check(TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==1&&PendingSpawns["spawn:a"].IsPublishing,"saved attempt and one publication");
 check(TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==1,"already published no replay");
 Reset(w);SaveWorks=false;check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==0&&PendingSpawns["spawn:a"].IsPublishing,"save failure never publishes");SaveWorks=true;check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==0,"uncertain save phase never replayed");
 Reset(w);w.Callback=()=>{throw new Exception("after effect");};check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==1,"throw after publication uncertain");check(TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==1,"exact visible candidate reconciles no replay");
 Reset(w);w.Callback=()=>{w.Present=null;throw new Exception("unknown effect");};check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==1,"unknown publication");check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==1,"unknown cannot publish again");
 Reset(w);w.Present=new Entity{world=w};check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==0,"occupied ID refused");
 Reset(w);ConfirmWorks=false;check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==0,"journal refusal");
 Reset(w);GameManager.Instance.World=new World();check(!TryPublishConstructedSpawn("a",w,e,out reason)&&w.Publications==0,"old world refused");
 return "PASS "+count+" actual publication method checks; journal/native publication/observation/gate doubled";
 }
}