using System;
class Entity { public int entityId=7;public World world; }
class World { public Entity Present;public Entity GetEntity(int id){return Present!=null&&Present.entityId==id?Present:null;} }
class GameManager { public static GameManager Instance=new GameManager();public World World; }
class RebirthNpcPendingSpawn {}
public class PublicationHost {
 static int Calls;static bool Confirm=true;static Action Callback;
 static bool TryConfirmConstructedSpawn(string key,Entity entity,out RebirthNpcPendingSpawn result){Calls++;result=Confirm?new RebirthNpcPendingSpawn():null;if(Callback!=null)Callback();return Confirm;}
 // ACTUAL_METHOD
 public static string Run(){
 int checks=0;var world=new World();var entity=new Entity{world=world};world.Present=entity;GameManager.Instance.World=world;RebirthNpcPendingSpawn result;
 Action<bool,string> check=(value,label)=>{if(!value)throw new Exception(label);checks++;};
 check(TryObservePublishedSpawn("a",world,entity,out result)&&result!=null&&Calls==1,"exact publication");
 world.Present=null;Calls=0;check(!TryObservePublishedSpawn("a",world,entity,out result)&&result==null&&Calls==0,"unpublished candidate");
 world.Present=new Entity{world=world};check(!TryObservePublishedSpawn("a",world,entity,out result)&&Calls==0,"reused id");
 world.Present=entity;var other=new World();GameManager.Instance.World=other;check(!TryObservePublishedSpawn("a",world,entity,out result)&&Calls==0,"old world");
 GameManager.Instance.World=world;entity.world=other;check(!TryObservePublishedSpawn("a",world,entity,out result)&&Calls==0,"moved candidate");entity.world=world;
 Confirm=false;check(!TryObservePublishedSpawn("a",world,entity,out result)&&result==null,"journal refusal");Confirm=true;
 Callback=()=>world.Present=new Entity{world=world};check(!TryObservePublishedSpawn("a",world,entity,out result)&&result==null,"replacement during confirmation");
 world.Present=entity;Callback=()=>GameManager.Instance.World=other;check(!TryObservePublishedSpawn("a",world,entity,out result)&&result==null,"world changed during confirmation");
 return "PASS "+checks+" actual production observation checks; native world and journal confirmation doubled";
 }
}