using System;using System.Collections.Generic;using System.Xml.Linq;
class BlockStub{public string GetBlockName()=>"station";}
struct Vector3i{public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
class State{public string Guid="native-world";}class Chunk{}
class World{public State worldState=new();public EntityPlayer Player;public TileEntityWorkstation Station;public Chunk Chunk=new();public object GetEntity(int id)=>Player?.entityId==id?Player:null;public object GetTileEntity(Vector3i p)=>Station;public object GetChunkSync(int x,int z)=>Chunk;}
class EntityPlayer{public int entityId=1;public World world;public bool IsDead()=>false;}
class GameManager{public static GameManager Instance=new();public World World;}
static class GameIO{public static string Root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"station-original-a");public static string GetSaveGameDir()=>Root;}
class RebirthStablePlayerIdentity{public string StorageKey="owner",CanonicalId="canonical";}
partial class Origin{public string CreationId="creation";}
partial class RebirthWorldCharacterRecord{public string StablePlayerKey="owner",StablePlayerId="canonical";public Origin Origin=new();public RebirthWorldProgressionState Progression=new();}
partial class RebirthWorldProgressionState{public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new();public Dictionary<string,RebirthStationPublicationRecord> StationPublications=new();public Dictionary<string,RebirthStationTerminalIntent> StationTerminalIntents=new();public Dictionary<string,object> StationCancellationAttempts=new(),StationCancellationRefunds=new();public Dictionary<string,RebirthStationCompletionPublication> StationCompletionPublications=new();}

static class RebirthWorldCharacterService{public static RebirthWorldCharacterRecord Owner;public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord o){o=Owner;return true;}public static bool TryGetIdentity(EntityPlayer p,out RebirthStablePlayerIdentity i){i=new();return true;}public static bool ThrowDirty;public static int DirtyCalls;public static Action AfterDirty;public static void MarkDirty(RebirthWorldCharacterRecord o,string why){DirtyCalls++;if(ThrowDirty){ThrowDirty=false;throw new System.IO.IOException("before dirty");}o.Dirty=true;AfterDirty?.Invoke();}}

static class RebirthSurvivorRequestScope{public static bool Matches(string a,string b)=>a==b;}

static class XUiM_Recipes{public static IList<Recipe> GetRecipes()=>new List<Recipe>();}
class RebirthStationCompletionExpectation{private TileEntityWorkstation station;public static bool TryCreate(TileEntityWorkstation s,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,IList<Recipe> d,out RebirthStationCompletionExpectation e){e=null;if(System.Linq.Enumerable.Any(s.Queue,q=>q?.Recipe!=null))return false;e=new(){station=s};return true;}public bool MatchesLive(TileEntityWorkstation s)=>ReferenceEquals(s,station)&&!System.Linq.Enumerable.Any(s.Queue,q=>q?.Recipe!=null);public bool MatchesStation(byte[] a,byte[] b)=>true;public bool MatchesTerminal(byte[] a,byte[] b)=>true;}
class RebirthStationCompletionPublication{public XElement Write()=>new("completed");public RebirthStationCompletionPublication Clone()=>new();public bool Revalidate(string r,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,RebirthStationCompletionExpectation e)=>true;public static bool TryCreate(string r,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,RebirthStationCompletionExpectation e,RebirthStationSnapshotEvidence.Publication p,out RebirthStationCompletionPublication c){c=new();return true;}}
