using System;using System.Collections.Generic;using System.IO;using System.Xml;
struct Vector3 { public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
enum RebirthNpcCategory { Unknown,Survivor,Bandit,SpecialHumanoid,DogCompanion,PantherCompanion }
sealed class RebirthNpcProductionProfile { public string ProfileId="specialist.medic";public bool Persistent=true;public RebirthNpcCategory Category=RebirthNpcCategory.Survivor; }
static class RebirthNpcProductionProfileCatalogue { public static RebirthNpcProductionProfile Profile=new RebirthNpcProductionProfile();public static bool TryGet(string id,out RebirthNpcProductionProfile profile){profile=Profile;return id==Profile.ProfileId;} }
static class RebirthNpcProfileRegistry { public static RebirthNpcProductionProfile ResolveRequired(string id){return RebirthNpcProductionProfileCatalogue.Profile;} }
sealed class RebirthNpcIdentityRecord { public RebirthNpcStableId StableNpcId;public string Category="Survivor",Species="human",GeneratedOrAssignedDisplayName="Original Medic"; }
sealed class RebirthNpcProfileBindingRecord { public string ProfileId="specialist.medic"; }
sealed class RebirthNpcLifecycleRecord { public string LifecycleDomain="persistent",RemovalReason="",DismissalState="active";public bool TombstoneState; }
sealed class RebirthNpcPresenceRecord { public string PresenceState="UnloadedPersistent";public uint EmbodimentGeneration=1; }
sealed class RebirthNpcVitalStateRecord { public int CurrentHealth=100;public string DeathOrIncapacitationState="alive"; }
sealed class RebirthNpcPersistentRecord
{
 public RebirthNpcIdentityRecord Identity=new RebirthNpcIdentityRecord();public RebirthNpcProfileBindingRecord Profile=new RebirthNpcProfileBindingRecord();public RebirthNpcLifecycleRecord Lifecycle=new RebirthNpcLifecycleRecord();public RebirthNpcPresenceRecord Presence=new RebirthNpcPresenceRecord();public RebirthNpcVitalStateRecord Vitals=new RebirthNpcVitalStateRecord();internal RebirthHumanNpcAppearanceDescriptor? HumanAppearance;
}
static class RebirthNpcAggregatePersistenceStore { public static RebirthNpcPersistentRecord Person;public static bool TryGet(RebirthNpcStableId stable,out RebirthNpcPersistentRecord person){person=Person;return person!=null&&person.Identity.StableNpcId==stable;} }
sealed class RebirthNpcRuntimeState { public RebirthNpcStableId StableId;public string ProfileId="specialist.medic";public bool HasHumanAppearance;public RebirthHumanNpcAppearanceDescriptor HumanAppearance; }
static class RebirthNpcRuntimeRegistry { public static EntityRebirthNPC Npc;public static bool TryGetEntityId(RebirthNpcStableId stable,out int id){id=Npc?.entityId??0;return Npc!=null&&Npc.RebirthRuntimeState.StableId==stable;}public static bool TryGet(int id,out RebirthNpcRuntimeState state){state=Npc?.RebirthRuntimeState;return Npc!=null&&Npc.entityId==id;} }
class Entity { public World world;public int entityId=9,entityClass=7; }
class EntityRebirthNPC:Entity { public bool Dead,Unload=false;public RebirthNpcRuntimeState RebirthRuntimeState;public bool IsDead(){return Dead;}public bool IsMarkedForUnload(){return Unload;} }
class World { public Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();public Entity GetEntity(int id){Entities.TryGetValue(id,out var entity);return entity;}public bool IsRemote(){return false;} }
class GameManager { public static GameManager Instance=new GameManager();public World World; }
static class GameIO { public static string Directory;public static bool HoldAfterComplete;public static string GetSaveGameDir(){return HoldAfterComplete&&File.Exists(Path.Combine(Directory,"RebirthNpcWorldIntegration.xml"))&&File.ReadAllText(Path.Combine(Directory,"RebirthNpcWorldIntegration.xml")).Contains("<replay ")?Directory+"-changed":Directory;} }
class EntityClass {public string entityClassName;public static Dictionary<int,EntityClass> list=new Dictionary<int,EntityClass>{{7,new EntityClass{entityClassName="medic"}}};public static int FromString(string name){return name=="medic"?7:8;} }
sealed class RebirthNpcWorldIdentityRecord { public RebirthNpcStableId StableId;public int AmbientEntityId;public string ProfileId,DisplayName;public long PromotedUtcTicks;public bool Persistent; }
static class RebirthNpcPersistenceFile { public static bool TryLoad(string path,Func<XmlDocument,bool> validator,out XmlDocument document,out string source,out string error){document=null;source="fixture-primary";error="";if(!File.Exists(path))return false;document=new XmlDocument{XmlResolver=null};document.Load(path);return validator(document);}public static void BlockWrite(string path,string error){throw new InvalidDataException(error);}public static bool Fail;public static void AssertWritable(string path){if(Fail)throw new IOException("injected primary failure");} }
partial class RebirthNpcWorldIntegrationService
{
 const int SchemaVersion=2;const string FileName="RebirthNpcWorldIntegration.xml";
 internal static Dictionary<string,RebirthNpcSpawnCompletion> Completions=new Dictionary<string,RebirthNpcSpawnCompletion>(StringComparer.Ordinal);static readonly object Sync=new object();internal static World loadedNativeWorld;internal static string loadedNativeDirectory;static bool loaded=true,dirty;internal static bool worldSnapshotPublicationUncertain;static long spawns;
 internal static Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> ByStable=new Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord>();
 internal static Dictionary<int,RebirthNpcWorldIdentityRecord> ByAmbient=new Dictionary<int,RebirthNpcWorldIdentityRecord>();
 internal static Dictionary<string,RebirthNpcPendingSpawn> PendingSpawns=new Dictionary<string,RebirthNpcPendingSpawn>(StringComparer.Ordinal);
 internal static HashSet<string> Replay=new HashSet<string>(StringComparer.Ordinal);
 static bool IsServer(){return true;}static void EnsureInitialized(){}static void EnsurePersistenceLoaded(){}
 static bool IsPreparedContextCurrentNoLock()=>loaded&&ReferenceEquals(loadedNativeWorld,GameManager.Instance?.World)&&!string.IsNullOrEmpty(loadedNativeDirectory)&&string.Equals(loadedNativeDirectory,GameIO.GetSaveGameDir(),StringComparison.OrdinalIgnoreCase);
 static bool TryValidateSpawnPlacement(RebirthNpcPendingSpawn request){return request.EntityClass=="medic";}
 static string GenerateName(RebirthNpcStableId stable,RebirthNpcCategory category){return "Fallback Name";}
 static void SaveIfDirty(){if(dirty){SaveNoLock();dirty=false;}}
 internal static void ReloadFixture(){loaded=false;LoadNoLock();}internal static void ForceCheckpoint(){SaveNoLock();}internal static void ResetFixture(){Completions.Clear();worldSnapshotPublicationUncertain=false;ByStable.Clear();ByAmbient.Clear();PendingSpawns.Clear();Replay.Clear();dirty=false;RebirthNpcPersistenceFile.Fail=false;}
}