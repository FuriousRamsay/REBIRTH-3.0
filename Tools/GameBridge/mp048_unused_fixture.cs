using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using Platform;
namespace Platform {class PlatformUserIdentifierAbs{}}
class NetPackage{}
static class RebirthNpcSaveQuarantineBundle{internal static void ObserveServerUnusedTrade(RebirthNpcTradeService.HostUnused unused){}}
// Local native source and positive never-entered journal are explicit adapters.
static class RebirthNpcTradeLocalOwnerLoad{internal class Source{internal RebirthNpcTradeOriginal Original;internal int Step,Phase=1;internal bool Matches(RebirthNpcTradeOriginal o)=>ReferenceEquals(o,Original);internal bool TryStage(out int step,out int phase){step=Step;phase=Phase;return true;}}}
static class GameIO{internal static string Directory;internal static string GetSaveGameDir()=>Directory;}
static class RebirthRemoteResourceDurableJournal{internal static string Digest(string image){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(image))).Replace("-","").ToLowerInvariant();}}
class RebirthNpcTradeQuote{internal Guid QuoteId,NpcId;internal uint ExpectedNpcInventoryRevision=1;internal long ExpiresClockTicks;}
class RebirthNpcTradeOriginal
{
 internal Guid Operation;internal string World=Guid.NewGuid().ToString(),Account="owner",Image="immutable original";
 internal string Digest=>RebirthRemoteResourceDurableJournal.Digest(Image);
 internal RebirthNpcTradeQuote Quote;internal bool MatchesQuote(RebirthNpcTradeQuote quote)=>ReferenceEquals(Quote,quote);internal RebirthNpcTradeQuote CopyQuote()=>Quote;
}
class Snapshot{internal Guid NpcId;internal uint Revision=1;}
static class RebirthNpcInventoryTransactionService{internal static Snapshot Value;internal static Snapshot GetSnapshot(Guid id)=>Value;}
static class RebirthNpcNetworkEpoch{internal static uint GetServerEpoch()=>1;}
public static partial class RebirthNpcTradeService
{
 internal static class HostQuote{internal static bool NeverEntered;internal static bool TryMarkLocalUnused(RebirthNpcTradeOwnerExchange.LocalUnusedBefore source,out object issued){issued=null;return NeverEntered;}internal static bool TryMarkLocalUnused(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original,out object issued){issued=null;return NeverEntered;}}
 private static object Sync=new object();
 private sealed class Issuance{internal bool TryMarkUnused()=>true;}
 private sealed class QuoteRecord{internal RebirthNpcTradeQuote Quote;internal bool Consumed;internal byte Resolution;internal object OwnerPreparation;internal HostUnused OwnerUnused;internal Issuance Issued=new Issuance();}
 private static Dictionary<Guid,QuoteRecord> Quotes=new Dictionary<Guid,QuoteRecord>();
 internal static class OwnerAdmission
 {
  internal static RebirthNpcTradeOriginal Original;internal static bool Valid=true;
  // Explicit fixture adapter: does not certify native packet identity/PDS.
  internal static bool TryDescribeSavedBefore(NetPackage p,PlatformUserIdentifierAbs u,uint c,uint e,int id,Guid op,string digest,out RebirthNpcTradeOriginal original)
  {original=Original;return Valid&&original!=null&&original.Operation==op&&original.Digest==digest;}
 }
 // ACTUAL_HOST_UNUSED
 private static int count;
 private static void Check(bool value){count++;if(!value)throw new Exception("Unused original source assertion "+count);}
 private static QuoteRecord Seed(long expiry)
 {
  Quotes.Clear();var quote=new RebirthNpcTradeQuote{QuoteId=Guid.NewGuid(),NpcId=Guid.NewGuid(),ExpiresClockTicks=expiry};
  OwnerAdmission.Original=new RebirthNpcTradeOriginal{Operation=quote.QuoteId,Quote=quote};OwnerAdmission.Valid=true;
  RebirthNpcInventoryTransactionService.Value=new Snapshot{NpcId=quote.NpcId};
  var record=new QuoteRecord{Quote=quote};Quotes.Add(quote.QuoteId,record);return record;
 }
 private static bool Retain(out HostUnused unused)=>HostUnused.TryRetain(new NetPackage(),new PlatformUserIdentifierAbs(),1,7,OwnerAdmission.Original.Operation,OwnerAdmission.Original.Digest,out unused);
 public static void Main()
 {
  string path=Path.Combine(Path.GetTempPath(),"rh-unused-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);GameIO.Directory=path;
  try
  {
   var record=Seed(DateTime.UtcNow.AddMinutes(5).Ticks);Check(!Retain(out _)&&!record.Consumed);
   RebirthNpcInventoryTransactionService.Value.Revision=2;Check(Retain(out var changed)&&record.Consumed&&record.OwnerPreparation==null&&changed.ReadRetained());
   Check(Retain(out var repeated)&&ReferenceEquals(changed,repeated));
   File.WriteAllText(changed.PathName,"changed");Check(!Retain(out _)&&record.Consumed&&ReferenceEquals(record.OwnerUnused,changed));
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);Check(Retain(out var expired)&&expired.ReadRetained());
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);record.Consumed=true;Check(!Retain(out _)&&record.OwnerUnused==null);
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);record.OwnerPreparation=new object();Check(!Retain(out _)&&record.OwnerUnused==null);
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);OwnerAdmission.Valid=false;Check(!Retain(out _)&&!record.Consumed);
   record=Seed(DateTime.UtcNow.AddMinutes(5).Ticks);RebirthNpcInventoryTransactionService.Value=null;Check(!Retain(out _)&&!record.Consumed);
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);string accountDirectory=Path.Combine(path,"RebirthData","NpcTradeOriginals",Guid.Parse(OwnerAdmission.Original.World).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(OwnerAdmission.Original.Account));
   Directory.CreateDirectory(accountDirectory);File.WriteAllText(Path.Combine(accountDirectory,record.Quote.QuoteId.ToString("N")+".unused"),"partial");
   Check(!Retain(out _)&&record.Consumed&&record.OwnerUnused!=null);Check(File.ReadAllText(record.OwnerUnused.PathName)=="partial");
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);var local=new RebirthNpcTradeLocalOwnerLoad.Source{Original=OwnerAdmission.Original};HostQuote.NeverEntered=false;
   Check(!HostUnused.TryRetainLocal(local,OwnerAdmission.Original,out _));
   HostQuote.NeverEntered=true;local.Step=1;Check(!HostUnused.TryRetainLocal(local,OwnerAdmission.Original,out _));
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);local=new RebirthNpcTradeLocalOwnerLoad.Source{Original=OwnerAdmission.Original};
   string preparedPath=Path.Combine(path,"RebirthData","NpcTradeOriginals",Guid.Parse(OwnerAdmission.Original.World).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(OwnerAdmission.Original.Account),OwnerAdmission.Original.Operation.ToString("N")+".xml");
   Directory.CreateDirectory(Path.GetDirectoryName(preparedPath));File.WriteAllText(preparedPath,"unknown preparation");Check(!HostUnused.TryRetainLocal(local,OwnerAdmission.Original,out _));
   foreach(string suffix in new[]{".tmp",".grant.tmp",".terminal.tmp",".closed.tmp"})
   {
    record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);local=new RebirthNpcTradeLocalOwnerLoad.Source{Original=OwnerAdmission.Original};
    preparedPath=Path.Combine(path,"RebirthData","NpcTradeOriginals",Guid.Parse(OwnerAdmission.Original.World).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(OwnerAdmission.Original.Account),OwnerAdmission.Original.Operation.ToString("N")+".xml");
    Directory.CreateDirectory(Path.GetDirectoryName(preparedPath));string conflict=preparedPath+suffix;File.WriteAllText(conflict,"unresolved custody");
    Check(!HostUnused.TryRetainLocal(local,OwnerAdmission.Original,out _)&&!record.Consumed&&record.OwnerUnused==null&&File.ReadAllText(conflict)=="unresolved custody");
   }
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);local=new RebirthNpcTradeLocalOwnerLoad.Source{Original=OwnerAdmission.Original};
   Check(HostUnused.TryRetainLocal(local,OwnerAdmission.Original,out var localUnused)&&localUnused.ReadRetained()&&record.Resolution==2);
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);var warm=new RebirthNpcTradeOwnerExchange.LocalUnusedBefore{Original=OwnerAdmission.Original,Valid=false};
   Check(!HostUnused.TryRetainLocalWarm(warm,out _)&&!record.Consumed);
   warm.Valid=true;HostQuote.NeverEntered=false;Check(!HostUnused.TryRetainLocalWarm(warm,out _)&&!record.Consumed);
   HostQuote.NeverEntered=true;record.OwnerPreparation=new object();Check(!HostUnused.TryRetainLocalWarm(warm,out _)&&!record.Consumed);
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);warm=new RebirthNpcTradeOwnerExchange.LocalUnusedBefore{Original=OwnerAdmission.Original};
   preparedPath=Path.Combine(path,"RebirthData","NpcTradeOriginals",Guid.Parse(warm.Original.World).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(warm.Original.Account),warm.Original.Operation.ToString("N")+".xml");
   Directory.CreateDirectory(Path.GetDirectoryName(preparedPath));File.WriteAllText(preparedPath+".grant.tmp","unknown permission publication");
   Check(!HostUnused.TryRetainLocalWarm(warm,out _)&&!record.Consumed);
   record=Seed(DateTime.UtcNow.AddMinutes(-1).Ticks);warm=new RebirthNpcTradeOwnerExchange.LocalUnusedBefore{Original=OwnerAdmission.Original};
   Check(HostUnused.TryRetainLocalWarm(warm,out var warmUnused)&&warmUnused.ReadRetained()&&record.Resolution==2&&
    HostUnused.TryRetainLocalWarm(warm,out var warmRetry)&&warmRetry.Image==warmUnused.Image&&warmRetry.Digest==warmUnused.Digest);
   File.WriteAllText(warmUnused.PathName,"changed unused publication");Check(!HostUnused.TryRetainLocalWarm(warm,out _)&&File.ReadAllText(warmUnused.PathName)=="changed unused publication");
   RebirthNpcTradeOwnerExchange.Run();
   Console.WriteLine(count+" actual host unused-source assertions passed; native identity and owner release NOT certified");
  }
  finally
  {
   if(Path.GetDirectoryName(Path.GetFullPath(path))!=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(path).StartsWith("rh-unused-",StringComparison.Ordinal))throw new InvalidDataException();
   Directory.Delete(path,true);
  }
 }
}
