using System;using System.IO;using System.Collections.Generic;using Platform;
// Actual complete TryReattach orchestration; authenticated source/admission,
// full NPC snapshots and durable grant/terminal reads are explicit adapters.
class NetPackage{}
namespace Platform{class PlatformUserIdentifierAbs{}}
class RebirthNpcTradeOriginal{internal Guid Operation;internal string Digest="original";internal Quote CopyQuote()=>new Quote();internal class Quote{internal Guid NpcId=Guid.Empty;}}
static class RebirthNpcInventoryTransactionService{internal static object GetSnapshot(Guid npc)=>new object();}
static class RebirthNpcTradeService
{
 private static object Sync=new object();private static Dictionary<Guid,Record> Quotes=new Dictionary<Guid,Record>();
 private class Record{internal HostPrepared OwnerPreparation;internal object OwnerUnused;internal int Resolution;}
 internal class OwnerAdmission
 {
  internal static bool Available=true,Metadata=true,Bind=true;internal static int Binds;
  internal static bool TryCaptureRetained(NetPackage packet,Platform.PlatformUserIdentifierAbs user,int player,uint connection,HostPrepared.RetainedSource source,out OwnerAdmission result){result=new OwnerAdmission();return Available;}
  internal bool MetadataCurrent(NetPackage packet,Platform.PlatformUserIdentifierAbs user)=>Metadata;
  internal bool BindRetained(HostPrepared prepared){if(Bind)Binds++;return Bind;}
 }
 internal class HostTerminal{internal static bool Available=true;internal static HostTerminal ReadExisting(HostPrepared prepared)=>Available?new HostTerminal():null;}
 internal class HostPrepared
 {
  internal RebirthNpcTradeOriginal Original;internal string PathName;internal HostTerminal Terminal;
  internal bool Metadata=true,Retained=true;internal static bool Read=true,Grant=true;
  internal HostPrepared(OwnerAdmission admission,string path,string image){PathName=path;}
  internal bool MetadataCurrent(NetPackage packet,Platform.PlatformUserIdentifierAbs user)=>Metadata;
  internal bool IsRetained()=>Retained;internal bool ReadRetained()=>Read;
  internal class HostGrant{internal static HostGrant Create(HostPrepared prepared)=>new HostGrant();internal bool IsRetained()=>Grant;}
  internal class Plan{internal bool Before=true,After;internal bool Matches(object snapshot,bool after)=>after?After:Before;}
  internal class RetainedSource
  {
   internal static RetainedSource Value;internal static int Captures;internal static bool Available=true;
   internal RebirthNpcTradeOriginal Original;internal Plan NpcPlan=new Plan();internal string PathName,Image="preparation";internal bool Retained=true;
   internal bool IsRetained()=>Retained;
   internal static bool TryCapture(NetPackage p,Platform.PlatformUserIdentifierAbs u,int player,uint connection,Guid id,string digest,out RetainedSource source){Captures++;source=Value;return Available&&source!=null&&source.Original.Operation==id&&source.Original.Digest==digest;}
  }
  // ACTUAL_HOST_REATTACH
 }
 internal static void Run(string root)
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Host reattach assertion "+checks);}
  var id=Guid.NewGuid();var packet=new NetPackage();var user=new Platform.PlatformUserIdentifierAbs();
  HostPrepared Seed()
  {
   Quotes.Clear();var path=Path.Combine(root,Guid.NewGuid().ToString("N"));
   HostPrepared.Read=HostPrepared.Grant=HostPrepared.RetainedSource.Available=OwnerAdmission.Available=OwnerAdmission.Metadata=OwnerAdmission.Bind=HostTerminal.Available=true;
   HostPrepared.RetainedSource.Captures=OwnerAdmission.Binds=0;
   HostPrepared.RetainedSource.Value=new HostPrepared.RetainedSource{Original=new RebirthNpcTradeOriginal{Operation=id},PathName=path};
   var old=new HostPrepared(null,path,"preparation"){Original=HostPrepared.RetainedSource.Value.Original};Quotes[id]=new Record{OwnerPreparation=old};return old;
  }
  bool Invoke()=>HostPrepared.TryReattach(packet,user,1,2,id,"original");
  Seed();Check(Invoke()&&HostPrepared.RetainedSource.Captures==0&&OwnerAdmission.Binds==0);
  var prior=Seed();prior.Retained=false;Check(Invoke()&&HostPrepared.RetainedSource.Captures==1&&OwnerAdmission.Binds==1);
  prior=Seed();prior.Metadata=false;Check(Invoke()&&OwnerAdmission.Binds==1);
  prior=Seed();prior.Retained=false;HostPrepared.RetainedSource.Available=false;Check(!Invoke()&&OwnerAdmission.Binds==0);
  prior=Seed();prior.Retained=false;OwnerAdmission.Available=false;Check(!Invoke());
  prior=Seed();prior.Retained=false;File.WriteAllText(prior.PathName+".closed","closed");Check(!Invoke());
  prior=Seed();prior.Retained=false;HostPrepared.Read=false;Check(!Invoke());
  prior=Seed();prior.Retained=false;HostPrepared.RetainedSource.Value.NpcPlan.Before=false;Check(!Invoke());
  prior=Seed();prior.Retained=false;HostPrepared.RetainedSource.Value.NpcPlan.Before=false;HostPrepared.RetainedSource.Value.NpcPlan.After=true;Check(Invoke());
  prior=Seed();prior.Retained=false;File.WriteAllText(prior.PathName+".grant","grant");HostPrepared.Grant=false;Check(!Invoke());
  prior=Seed();prior.Retained=false;File.WriteAllText(prior.PathName+".terminal","terminal");HostTerminal.Available=false;Check(!Invoke());
  prior=Seed();prior.Retained=false;HostPrepared.RetainedSource.Value.Retained=false;Check(!Invoke());
  prior=Seed();prior.Retained=false;OwnerAdmission.Metadata=false;Check(!Invoke());
  prior=Seed();prior.Retained=false;OwnerAdmission.Bind=false;Check(!Invoke());
  Console.WriteLine(checks+" actual host reattach assertions passed; native source/admission/durable sidecar adapters NOT certified");
 }
}
class Program
{
 static void Main(){string root=Path.Combine(Path.GetTempPath(),"rebirth-host-reattach-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{RebirthNpcTradeService.Run(root);}finally{Directory.Delete(root,true);}}
}
