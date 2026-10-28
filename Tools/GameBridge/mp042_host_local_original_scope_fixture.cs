// AUTHORED_NOT_RUN. Actual shared owner-original predicate, purpose-limited host-local addition.
using System;
enum RemoteResourceClientOperation{Craft,ItemRepair,BlockRepair}
class World{internal bool Remote;internal EntityPlayerLocal Primary,Registered;internal State worldState=new State();internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;internal object GetEntity(int id)=>Registered;}
class State{internal string Guid="world";}
class EntityPlayerLocal{internal World world;internal int entityId=1,Died=2;internal bool Dead,Spawned=true;internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
class RebirthStablePlayerIdentity{internal string CanonicalId="owner";internal static bool TryResolveServerEntity(EntityPlayerLocal player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
static class RebirthRemoteResourceCraftOriginal{internal static bool Personal=true;internal static bool ValidateInput(RebirthRemoteResourceClientOriginal.Intent original,out object recipe,out int count,out int quality,out int belt,out bool rich){recipe=null;count=quality=belt=0;rich=false;return Personal;}}
static class RebirthRemoteResourceClientOriginal
{
 internal sealed class Intent{internal string World="world",Creation="creation",Owner="owner";internal int Deaths=2;internal RemoteResourceClientOperation Kind;}
 // ACTUAL_CURRENT
 internal static void Exercise(){foreach(bool remote in new[]{false,true})for(int variant=0;variant<12;variant++){var world=new World{Remote=remote};var player=new EntityPlayerLocal{world=world};world.Primary=world.Registered=player;GameManager.Instance.World=world;ThreadManager.Main=RebirthRemoteResourceCraftOriginal.Personal=true;var original=new Intent();switch(variant){case 1:original.Kind=RemoteResourceClientOperation.ItemRepair;break;case 2:original.Kind=RemoteResourceClientOperation.BlockRepair;break;case 3:RebirthRemoteResourceCraftOriginal.Personal=false;break;case 4:world.Primary=new EntityPlayerLocal();break;case 5:world.Registered=new EntityPlayerLocal();break;case 6:GameManager.Instance.World=new World();break;case 7:original.Deaths--;break;case 8:original.Creation="other";break;case 9:original.Owner="other";break;case 10:player.Dead=true;break;case 11:player.Spawned=false;break;}bool expected=variant==0||remote&&variant>=1&&variant<=3;if(Current(player,original)!=expected)throw new Exception("native original purpose/owner scope "+remote+"/"+variant);}Console.WriteLine("MP042 actual owner-original scope: 24 local/remote purpose and body/life checks authored.");}
}
class Program{static void Main()=>RebirthRemoteResourceClientOriginal.Exercise();}
