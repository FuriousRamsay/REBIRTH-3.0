using System;using System.Collections.Generic;
class EntityPlayer {public int Id;}
class ItemValue {public int type;}
class ItemClass {public static bool Missing;public static ItemValue GetItem(string name){return Missing?null:new ItemValue{type=7};}}
class GameRandom {public float RandomFloat{get{return 0.5f;}}}
class World {public Dictionary<int,EntityPlayer> Players=new Dictionary<int,EntityPlayer>();public EntityPlayer GetEntity(int id){EntityPlayer p;return Players.TryGetValue(id,out p)?p:null;}public GameRandom GetGameRandom(){return new GameRandom();}}
class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}public static int FloorToInt(float v){return (int)Math.Floor(v);}}
class RebirthBackgroundBonusService {public static bool HasBonus(EntityPlayer p,string bonus){return false;}}
class RebirthTreeContributorCredit {public int PlayerId,BaseWood,SettledBaseWood,ToolType;}
class RebirthTreeHarvestLedger {public bool Destroyed;public DateTime DestroyedUtc;public string BlockName;public Dictionary<int,RebirthTreeContributorCredit> Contributors=new Dictionary<int,RebirthTreeContributorCredit>();}
class RebirthTreeWoodPayout {public int PlayerId,BaseWood,ToolType;public string BlockName;}
class Subject {
static object Gate=new object();public static Dictionary<string,RebirthTreeHarvestLedger> Ledgers=new Dictionary<string,RebirthTreeHarvestLedger>();
const double DestructionSettleDelaySeconds=2,PostDestructionRetentionSeconds=20;const string ProfessionalLoggingBonusId="logger";
public static int Cleanups,Grants,Wood;static void CleanupLocked(){Cleanups++;}static float GetTuning(string b,string k,float d){return d;}
static void GrantTreeWood(int player,int item,int count,int tool,string block){Grants++;Wood+=count;}
// PRODUCTION_CLASS
public static void Run(World world){SettleReadyLedgers(world);}
}
class Check {
static void A(bool p,string message){if(!p)throw new Exception(message);}
static void Main(){var world=new World();var credit=new RebirthTreeContributorCredit{PlayerId=1,BaseWood=4};var ledger=new RebirthTreeHarvestLedger{Destroyed=true,DestroyedUtc=DateTime.UtcNow.AddSeconds(-3),BlockName="treeOak"};ledger.Contributors[1]=credit;Subject.Ledgers["oak"]=ledger;
ItemClass.Missing=true;Subject.Run(world);A(credit.SettledBaseWood==0&&Subject.Cleanups==0&&Subject.Grants==0,"missing resource leaves credit and ledger untouched");
ItemClass.Missing=false;Subject.Run(world);A(credit.SettledBaseWood==0&&Subject.Grants==0,"offline contributor skipped without settled debit");
world.Players[1]=new EntityPlayer{Id=1};Subject.Run(world);A(credit.SettledBaseWood==4&&Subject.Grants==1&&Subject.Wood==4,"online base credit settled once");
Subject.Run(world);A(Subject.Grants==1&&Subject.Wood==4,"repeated settlement no duplicate award");
Console.WriteLine("PASS actual tree settlement: missing-resource preflight, offline pending, online base payout and no duplicate; native world/items/grant/cleanup adapters doubled.");}
}