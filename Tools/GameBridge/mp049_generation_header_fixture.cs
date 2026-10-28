using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
// Actual domain container, generation adapter and header adapter are composed.
// Native World/thread and external baseline subscribers are explicit adapters.
class World{internal bool Remote=true;internal bool IsRemote()=>Remote;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcBaselineClientEvents{internal static bool HasSubscribers;}
static class FixturePaths{internal static readonly string Save=Path.Combine(Path.GetTempPath(),"rebirth-mp049-generation-scope");}
internal static class RebirthNpcSaveQuarantineScope{
 // ACTUAL_SCOPE_DIRECTORY_MEMBERS
}
// ACTUAL_DOMAIN
internal static partial class RebirthNpcRecoveryProjectionScope {
 private static World world;private static string directory;private static uint epoch,connection;private static ulong generation;
 // ACTUAL_GENERATION_MEMBERS
 internal static void Seed(World value){world=value;directory=FixturePaths.Save;epoch=3;connection=4;generation=9;}
 internal static bool Empty=>world==null&&directory==null&&epoch==0&&connection==0&&generation==0;
 internal static bool Original(World value)=>ReferenceEquals(world,value)&&directory==FixturePaths.Save&&epoch==3&&connection==4&&generation==9;
 internal static void Change(){generation++;}
}
internal static class RebirthNpcBaselineClientHeader {
 // ACTUAL_HEADER_FIELDS
 // ACTUAL_HEADER_ADAPTER
 internal static void Seed(World value){callbackActive=false;RebirthNpcBaselineClientEvents.HasSubscribers=false;current=new Header{World=value,Directory=FixturePaths.Save,Epoch=3,Connection=4,Generation=9,Sequence=8,Expected=7,InitialSkipped=2};}
 internal static bool Empty=>current==null;
 internal static bool Original(World value)=>current!=null&&ReferenceEquals(current.World,value)&&current.Generation==9&&current.Sequence==8;
 internal static void Change(){current.Sequence++;}
 internal static void Busy(){callbackActive=true;}
 internal static void Unknown(){current.CallbackUnknown=true;}
 internal static void Foreign(World other){current.World=other;}
 internal static void ForeignDirectory(){current.Directory=FixturePaths.Save+"-other";}
}
static class Program {
 static int assertions;static void Check(bool value){assertions++;if(!value)throw new Exception("assertion "+assertions);}
 static void Refuses(Action action){bool threw=false;try{action();}catch(InvalidOperationException){threw=true;}Check(threw);}
 public static void Main(){
  var original=new World();Action seed=()=>{ThreadManager.Main=true;RebirthNpcRecoveryProjectionScope.Seed(original);RebirthNpcBaselineClientHeader.Seed(original);};
  Func<RebirthNpcSaveQuarantineDomain> prepare=()=>RebirthNpcRecoveryProjectionScope.PrepareClientGenerationQuarantine(original,FixturePaths.Save,3,4);
  seed();var domain=prepare();Check(domain.Name=="client-recovery-generation"&&domain.IsCurrent);
  var doc=new XmlDocument();var evidence=domain.CopyEvidence(doc);Check(evidence.SelectSingleNode("clientGenerationQuarantine/domain[@id='client-baseline-header']/baselineClientHeader")!=null);
  domain.Detach();Check(domain.IsDetached&&RebirthNpcRecoveryProjectionScope.Empty&&RebirthNpcBaselineClientHeader.Empty);
  domain.Rollback();Check(domain.IsCurrent&&RebirthNpcRecoveryProjectionScope.Original(original)&&RebirthNpcBaselineClientHeader.Original(original));
  domain.Rollback();Check(domain.IsCurrent);
  seed();domain=prepare();RebirthNpcBaselineClientHeader.Change();Check(!domain.IsCurrent);Refuses(()=>domain.Detach());Check(RebirthNpcRecoveryProjectionScope.Original(original));
  seed();domain=prepare();RebirthNpcRecoveryProjectionScope.Change();Check(!domain.IsCurrent);Refuses(()=>domain.Detach());Check(RebirthNpcBaselineClientHeader.Original(original));
  seed();domain=prepare();RebirthNpcBaselineClientEvents.HasSubscribers=true;Check(!domain.IsCurrent);Refuses(()=>domain.Detach());
  seed();RebirthNpcBaselineClientEvents.HasSubscribers=true;Refuses(()=>prepare());
  seed();RebirthNpcBaselineClientHeader.Busy();Refuses(()=>prepare());
  seed();RebirthNpcBaselineClientHeader.Unknown();Refuses(()=>prepare());
  seed();RebirthNpcBaselineClientHeader.Foreign(new World());Refuses(()=>prepare());
  seed();ThreadManager.Main=false;Refuses(()=>prepare());
  seed();original.Remote=false;Refuses(()=>prepare());original.Remote=true;
  seed();Refuses(()=>RebirthNpcRecoveryProjectionScope.PrepareClientGenerationQuarantine(original,"foreign",3,4));
  seed();Refuses(()=>RebirthNpcRecoveryProjectionScope.PrepareClientGenerationQuarantine(original,FixturePaths.Save,99,4));
  seed();domain=RebirthNpcRecoveryProjectionScope.PrepareClientGenerationQuarantine(original,Path.Combine(FixturePaths.Save,"."),3,4);Check(domain.IsCurrent);domain.Detach();domain.Rollback();Check(domain.IsCurrent&&RebirthNpcRecoveryProjectionScope.Original(original));
  seed();domain=prepare();RebirthNpcBaselineClientHeader.Foreign(new World());Check(!domain.IsCurrent);Refuses(()=>domain.Detach());
  seed();domain=prepare();RebirthNpcBaselineClientHeader.ForeignDirectory();Check(!domain.IsCurrent);Refuses(()=>domain.Detach());
  seed();string changedCase=Path.Combine(Path.GetDirectoryName(FixturePaths.Save),Path.GetFileName(FixturePaths.Save).ToUpperInvariant());if(Path.DirectorySeparatorChar=='\\'){domain=RebirthNpcRecoveryProjectionScope.PrepareClientGenerationQuarantine(original,changedCase,3,4);Check(domain.IsCurrent);}else Refuses(()=>RebirthNpcRecoveryProjectionScope.PrepareClientGenerationQuarantine(original,changedCase,3,4));
  seed();domain=prepare();domain.Detach();Refuses(()=>domain.Detach());domain.Rollback();Check(domain.IsCurrent&&RebirthNpcRecoveryProjectionScope.Original(original)&&RebirthNpcBaselineClientHeader.Original(original));
  Console.WriteLine("PASS actual generation/header composition assertions="+assertions);
 }
}
