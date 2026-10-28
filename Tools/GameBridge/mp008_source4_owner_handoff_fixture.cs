// Synthetic private producer/native codec/immutable writer/ordinary issuer.
// This cohort cannot certify real Source4 publication, cold lineage or activation.
class Recipe{internal bool IsScrap;}
class RecipeQueueItem{internal int StartingEntityId=7,Multiplier=2;internal Recipe Recipe=new Recipe();}
class World{internal bool IsRemote()=>false;}
class EntityPlayer{internal World world=new World();internal int entityId=7,Died=3;}
class TileEntityWorkstation{}
static class GameIO{internal static string GetSaveGameDir()=>"synthetic-save";}
static class RebirthSurvivorMode{internal static bool Enabled=true;internal static bool IsEnabledForCurrentWorld()=>Enabled;}
static class RebirthStablePlayerIdentity{internal sealed class Identity{internal string StorageKey="owner";}internal static bool TryResolveServerEntity(EntityPlayer a,out Identity i){i=new Identity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer a)=>"creation";}
static class RebirthLogisticsEndpointIdentity{internal static string Birth="birth";internal static bool TryGetIncarnation(object w,TileEntityWorkstation s,out string b){b=Birth;return true;}}
static class RebirthCookingHeat{internal static bool Managed(Recipe r)=>false;}
static class RebirthRecyclerRuntime{internal static bool IsMarked(Recipe r)=>false;}
static class RebirthStationGridQueue{internal static bool IsMarked(Recipe r)=>false;}
static class RebirthProductiveEarnedObservationBudget{sealed class Lease:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long n,out IDisposable l){l=new Lease();return true;}}
static class RebirthProductiveEarnedRowProof{internal const int MaximumArtifactBytes=6*1024*1024;}
static class RebirthProductiveEarnedUiCoordinator{internal static bool Allow=true;internal static byte[] Written;internal static bool WriteRemoteExact(string p,byte[] b){if(!Allow)return false;Written=b;return true;}}
static class RebirthRemoteResourcePlacedStationNativeReader{internal sealed class Image{}internal static bool TryRead(byte[] b,out Image i){i=new Image();return b.Length==1&&b[0]==1;}}
static class RebirthProductiveEarnedNativeBody{internal static byte[] Encode(RebirthRemoteResourcePlacedStationNativeReader.Image i,ulong e,out int o){o=0;return new byte[]{2};}internal static bool TryRead(byte[] b,int o,out object i,out ulong e){i=null;e=0;return b[0]==2;}}
static class RebirthRemoteResourcePlacedStationOriginal{internal sealed class SavedEarnedOwner{internal bool Valid=true;internal EntityPlayer Actor=new EntityPlayer();internal World World=>Actor.world;internal TileEntityWorkstation Station=new TileEntityWorkstation();internal RecipeQueueItem Row=new RecipeQueueItem();internal Guid Source=Guid.NewGuid();internal string Binding="binding",Proof="proof",Owner="owner",Creation="creation",WorldKey="world",Birth="birth";internal int Deaths=3,EntityId=7,OriginalCount=2;internal bool Current()=>Valid;internal byte[] Before()=>new byte[]{1};internal byte[] After()=>new byte[]{4};}}
internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    static bool installed=true;static int issues;
    static bool TryRestoreSavedSource4(RebirthRemoteResourcePlacedStationOriginal.SavedEarnedOwner source,string path,byte[] bytes,byte[] before,out bool existing){existing=false;return false;}
    internal static string NativeBinding(RecipeQueueItem r)=>"row";
    static bool Issue(EntityPlayer a,TileEntityWorkstation s,RecipeQueueItem r,string owner,string creation,int deaths,byte[] before,Guid op,string path,byte[] receipt,RebirthRemoteResourcePlacedStationOriginal.SavedEarnedOwner source)
    {if(RebirthProductiveEarnedUiCoordinator.Written==null||before[0]!=2||receipt!=RebirthProductiveEarnedUiCoordinator.Written)throw new Exception("Source receipt must precede existing issuer; Persistency must be converted");issues++;return true;}
    static void Check(bool b){if(!b)throw new Exception("Source4 assertion failed");}
    static void Main()
    {
        var s=new RebirthRemoteResourcePlacedStationOriginal.SavedEarnedOwner();
        Check(AdoptSavedSource4(s)&&issues==1); // Exact source publication precedes ordinary issuer.
        var node=XElement.Parse(Encoding.UTF8.GetString(RebirthProductiveEarnedUiCoordinator.Written));
        Check((string)node.Attribute("source")==s.Source.ToString("N")&&(string)node.Attribute("birth")==s.Birth&&(int)node.Attribute("entity")==7&&(int)node.Attribute("count")==2);
        s.Valid=false;Check(!AdoptSavedSource4(s)&&issues==1);s.Valid=true;
        s.Owner="other";Check(!AdoptSavedSource4(s)&&issues==1);s.Owner="owner";
        s.Deaths=2;Check(!AdoptSavedSource4(s)&&issues==1);s.Deaths=3;
        RebirthLogisticsEndpointIdentity.Birth="replaced";Check(!AdoptSavedSource4(s)&&issues==1);RebirthLogisticsEndpointIdentity.Birth="birth";
        s.Row.StartingEntityId=8;Check(!AdoptSavedSource4(s)&&issues==1);s.Row.StartingEntityId=7;
        s.Row.Recipe.IsScrap=true;Check(!AdoptSavedSource4(s)&&issues==1);s.Row.Recipe.IsScrap=false;
        RebirthProductiveEarnedUiCoordinator.Allow=false;Check(!AdoptSavedSource4(s)&&issues==1);RebirthProductiveEarnedUiCoordinator.Allow=true;
        RebirthSurvivorMode.Enabled=false;Check(!AdoptSavedSource4(s)&&issues==1);
        Console.WriteLine("10 Source4 owner-handoff assertions passed");
    }
}
