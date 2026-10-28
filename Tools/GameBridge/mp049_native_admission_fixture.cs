using System;
using System.Collections.Generic;
using System.Linq;

// AUTHORED_NOT_RUN. Isolated actual-coordinator fault injection; native source
// authenticity and the individual owner adapters require separate validation.
public sealed class World {public bool Remote;public bool IsRemote()=>Remote;}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World;}
public sealed class ConnectionManager {public bool IsServer=true;}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class GameIO {public static string Save="C:\\native-admission";public static string GetSaveGameDir()=>Save;}
internal static class RebirthNpcSaveQuarantineScope
{internal static bool SameDirectory(string a,string b)=>a!=null&&b!=null&&string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
internal static class RebirthNpcSaveQuarantineCapture
{internal sealed class ScopeImage {internal World World;internal string Directory;} internal sealed class Snapshot {internal ScopeImage Scope;internal uint NetworkEpoch;internal bool IsUnchangedOriginal(out string error){error=null;return true;}}}
internal sealed class RebirthNpcSaveQuarantineDomain
{internal string Name;internal bool Detached=true;internal bool IsDetached=>Detached;internal RebirthNpcSaveQuarantineDomain(string name){Name=name;}}
internal static class RebirthNpcSaveQuarantinePreparation
{
    // ACTUAL_REQUIRED
    // ACTUAL_MEMBERSHIP
    // ACTUAL_COVERAGE
    internal static string[] Names=>(string[])Required.Clone();
}
internal static class RebirthNpcRuntimeNativeAdoption
{internal sealed class NativeReentry {internal World World;internal string Directory;internal bool Valid=true;internal bool Current()=>Valid;}}
public static class RebirthDogLifecycleService
{internal sealed class NativeOwnerReentry {internal World World;internal string Directory;internal bool Valid=true;internal bool Current()=>Valid;}}
public static partial class RebirthNpcNetworkEpoch
{
    private static readonly object Sync=new object();private static World serverWorld;private static uint activeServerEpoch;
    public static int ProducerCalls;
    internal static void Set(World world,uint epoch){serverWorld=world;activeServerEpoch=epoch;}
    internal static uint GetServerEpoch(){ProducerCalls++;return activeServerEpoch;}
    private static bool CurrentServerWorld()=>ReferenceEquals(serverWorld,GameManager.Instance.World)&&serverWorld?.IsRemote()==false;
}
// ACTUAL_ADMISSION

internal static class Program
{
    private static int cases,effects;
    private static World world;
    private static RebirthNpcRuntimeNativeAdoption.NativeReentry source;
    private static RebirthNpcSaveQuarantineDomain[] domains;
    private static void Check(bool condition,string name){if(!condition)throw new Exception(name);cases++;}
    private static void Reset()
    {
        world=new World();GameManager.Instance.World=world;ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;
        RebirthNpcNetworkEpoch.Set(world,7);RebirthNpcNetworkEpoch.ProducerCalls=0;effects=0;
        source=new RebirthNpcRuntimeNativeAdoption.NativeReentry{World=world,Directory=GameIO.Save};
        domains=RebirthNpcSaveQuarantinePreparation.Names.Select(name=>new RebirthNpcSaveQuarantineDomain(name)).ToArray();
    }
    private static RebirthNpcServerNativeAdmission.Role[] Roles(Func<int,bool> action=null)
    {return RebirthNpcSaveQuarantinePreparation.Names.Select((name,index)=>new RebirthNpcServerNativeAdmission.Role(new[]{name},()=>{effects++;return action==null||action(index);})).ToArray();}
    public static int Main()
    {
        Reset();Check(domains.Length==83,"actual production roster must contain 83 domains");
        Check(RebirthNpcServerNativeAdmission.TryQualify(source,domains,Roles(),out var admitted)&&effects==83&&admitted.Current(world,GameIO.Save)&&domains.All(row=>row.IsDetached),"whole admission preserves original custody");
        int producers=RebirthNpcNetworkEpoch.ProducerCalls;Check(admitted.Current(world,GameIO.Save)&&producers==RebirthNpcNetworkEpoch.ProducerCalls,"readback never reenters epoch producer");
        source.Valid=false;Check(admitted.Current(world,GameIO.Save),"committed namespace survives native source lifetime");
        RebirthNpcNetworkEpoch.Set(world,8);Check(!admitted.Current(world,GameIO.Save),"new epoch revokes old namespace");
        Reset();Check(!RebirthNpcServerNativeAdmission.TryQualify(source,domains,Roles().Take(82).ToArray(),out admitted)&&admitted==null&&effects==0,"missing coverage blocks every effect");
        Reset();var roles=Roles();roles[82]=roles[0];Check(!RebirthNpcServerNativeAdmission.TryQualify(source,domains,roles,out admitted)&&effects==0,"duplicate coverage blocks every effect");
        Reset();roles=Roles();roles[82]=new RebirthNpcServerNativeAdmission.Role(new[]{"unknown-owner"},()=>{effects++;return true;});Check(!RebirthNpcServerNativeAdmission.TryQualify(source,domains,roles,out admitted)&&effects==0,"unknown substitute cannot conceal missing owner");
        Reset();domains[20].Detached=false;Check(!RebirthNpcServerNativeAdmission.TryQualify(source,domains,Roles(),out admitted)&&effects==0,"live outgoing member cannot admit");
        Reset();Check(!RebirthNpcServerNativeAdmission.TryQualify(source,domains,Roles(index=>index!=40),out admitted)&&admitted==null&&effects==41,"failed role retains partial staging without receipt");
        Reset();Check(!RebirthNpcServerNativeAdmission.TryQualify(source,domains,Roles(index=>{if(index==20)source.Valid=false;return true;}),out admitted)&&admitted==null&&effects==21,"native source loss stops remaining roles");
        Reset();var owner=new RebirthDogLifecycleService.NativeOwnerReentry{World=world,Directory=GameIO.Save};Check(RebirthNpcServerNativeAdmission.TryQualifyOwnerScope(owner,domains,Roles(),out admitted)&&effects==83&&admitted.Current(world,GameIO.Save),"native player bootstrap requires every role");
        Reset();owner=new RebirthDogLifecycleService.NativeOwnerReentry{World=world,Directory=GameIO.Save,Valid=false};Check(!RebirthNpcServerNativeAdmission.TryQualifyOwnerScope(owner,domains,Roles(),out admitted)&&effects==0,"invalid native player cannot bootstrap");
        Reset();Check(RebirthNpcServerNativeAdmission.TryQualify(source,domains,Roles(),out admitted),"fresh valid candidate");GameManager.Instance.World=new World();Check(!admitted.Current(world,GameIO.Save),"native world replacement revokes receipt");
        Console.WriteLine("PASS actual native admission coordinator cases="+cases);return 0;
    }
}
