using System;
using System.Collections.Generic;
using System.Xml.Linq;
namespace UnityEngine { static class Time { internal static float realtimeSinceStartup; } }
enum EquipmentSlots { Hands=4 }
class ItemClass { internal string Name;internal string GetItemName()=>Name; }
class ItemValue { internal bool Empty;internal ItemClass ItemClass;internal ItemValue[] modifications;internal bool IsEmpty()=>Empty; }
class Equipment { internal ItemValue Hands;internal ItemValue GetSlotItem(int slot){if(slot!=(int)EquipmentSlots.Hands)throw new Exception("Wrong native slot requested");return Hands;} }
class EntityPlayer { internal string Key="player";internal World world;internal bool Spawned=true;internal Equipment equipment=new Equipment();internal ItemValue[] Backpack;internal bool IsSpawned()=>Spawned; }
class PlayerEntry { internal EntityPlayer Player; }
class Tracked { internal List<PlayerEntry> list=new List<PlayerEntry>(); }
class AIDirectorPlayerManagementComponent { internal Tracked trackedPlayers=new Tracked(); }
class Director { internal AIDirectorPlayerManagementComponent Players=new AIDirectorPlayerManagementComponent();internal T GetComponent<T>() where T:class=>Players as T; }
class World { internal bool Remote;internal Director aiDirector=new Director();internal bool IsRemote()=>Remote; }
class GameManager { internal static GameManager Instance=new GameManager();internal World World; }
static class RebirthPurgeKillContributor { internal static string Identify(EntityPlayer p,World w)=>ReferenceEquals(p.world,w)?p.Key:null; }
// XML admission is covered by PurgeSupplyPolicyFixture. This fixture loads real
// default attributes but exercises linked arithmetic/equipment, not a mock parser.
static class RebirthPoiClearanceCodec { internal static void Shape(XElement e,string name,string attrs,string children)=>throw new NotSupportedException();internal static int Int(XElement e,string name)=>int.Parse(e.Attribute(name).Value); }
static class Log { internal static void Warning(string text){} }
