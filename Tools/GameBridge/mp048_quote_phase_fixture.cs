using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Xml.Linq;
// Actual binary journal and actual quote phase methods; only unrelated history
// index lookup is adapted. Native quote issuance/packet identity not certified.
// ACTUAL_QUOTE_BINARY_JOURNAL
static class RebirthCamperTransferHistoryScope
{
 internal sealed class Index{internal RebirthCamperTransferJournal.Record FindPending(Guid id)=>null;internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid id,string owner)=>new RebirthCamperTransferJournal.Record[0];internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid id,string owner)=>new RebirthCamperTransferJournal.Record[0];}
 internal static bool TryGet(RebirthCamperTransferJournal journal,out Index index){index=null;return false;}
}
class RebirthNpcTradeQuote{internal Guid QuoteId=Guid.NewGuid();internal uint ExpectedNpcInventoryRevision=1;internal long ExpiresClockTicks=DateTime.UtcNow.AddMinutes(-1).Ticks;}
class RebirthNpcTradeOriginal{internal RebirthNpcTradeQuote Quote=new RebirthNpcTradeQuote();internal Guid Operation=>Quote.QuoteId;internal string Account="owner",Creation="creation",World=Guid.NewGuid().ToString();internal RebirthNpcTradeQuote CopyQuote()=>Quote;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class HostUnused{internal RebirthNpcTradeOriginal Original;internal bool Retained=true;internal bool ReadRetained()=>Retained;}
static class RebirthNpcTradeOwnerExchange{internal class LocalUnusedBefore{internal RebirthNpcTradeOriginal Original;internal bool Valid=true;internal bool Current()=>Valid;}}
static class GameIO{internal static string Root;internal static string GetSaveGameDir()=>Root;}
class QuotePhase
{
 internal QuotePhase(){}
 private QuotePhase(RebirthNpcTradeQuote quote,string image,string directory,string account,string creation,string world){Quote=quote;Image=image;DirectoryName=directory;Account=account;Creation=creation;World=world;}
 internal string Account="owner";
 private static bool TradeScope()=>true;
 private static string Serialize(RebirthNpcTradeQuote quote)=>quote.QuoteId.ToString("N");
 private static string DirectoryFor(string save,string world,string account)=>Path.Combine(save,"warm",world,account);
 internal string Image="immutable quote scope",DirectoryName,World=Guid.NewGuid().ToString(),Creation="creation";
 internal RebirthNpcTradeQuote Quote=new RebirthNpcTradeQuote();
 // ACTUAL_QUOTE_READ
 // ACTUAL_QUOTE_PHASES
 // ACTUAL_WARM_QUOTE
 internal static QuotePhase SeedWarm(out RebirthNpcTradeOriginal original,out RebirthNpcTradeOwnerExchange.LocalUnusedBefore source){
  original=new RebirthNpcTradeOriginal();source=new RebirthNpcTradeOwnerExchange.LocalUnusedBefore{Original=original};
  string image=new XElement("npcTradeIssuedQuote",new XAttribute("version",1),new XAttribute("account",original.Account),new XAttribute("creation",original.Creation),new XAttribute("world",original.World),new XElement("quote",Serialize(original.Quote))).ToString(SaveOptions.DisableFormatting);
  var result=new QuotePhase(original.Quote,image,DirectoryFor(GameIO.Root,original.World,original.Account),original.Account,original.Creation,original.World);result.Seed();return result;
 }
 internal void Seed(){byte[] bytes=Encoding.UTF8.GetBytes(Image);new RebirthCamperTransferJournal(DirectoryName).Prepare(Quote.QuoteId,Guid.Parse(World),Creation,Quote.ExpectedNpcInventoryRevision,bytes,bytes);}
 internal QuotePhase Cold()=>new QuotePhase{Image=Image,DirectoryName=DirectoryName,World=World,Creation=Creation,Quote=Quote};
}
static class Program
{
 static int count;
 static void Check(bool value){count++;if(!value)throw new Exception("Actual quote-phase assertion "+count);}
 public static void Main()
 {
  string path=Path.Combine(Path.GetTempPath(),"rebirth-quote-phase-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
  try
  {
   var quote=new QuotePhase{DirectoryName=Path.Combine(path,"missing")};
   Check(!quote.TryEnterPermission()&&!Directory.Exists(quote.DirectoryName));Check(!quote.TryMarkUnused()&&!Directory.Exists(quote.DirectoryName));
   quote=new QuotePhase{DirectoryName=Path.Combine(path,"entered")};quote.Seed();Check(quote.TryEnterPermission());Check(!quote.TryEnterPermission());
   Check(!quote.TryMarkUnused());Check(!quote.Cold().TryMarkUnused());
   quote=new QuotePhase{DirectoryName=Path.Combine(path,"unused")};quote.Seed();Check(quote.TryMarkUnused());Check(quote.Cold().TryMarkUnused());Check(!quote.Cold().TryEnterPermission());
   quote=new QuotePhase{DirectoryName=Path.Combine(path,"changed")};quote.Seed();quote.Image+="changed";Check(!quote.TryEnterPermission());Check(!quote.TryMarkUnused());
   quote=new QuotePhase{DirectoryName=Path.Combine(path,"corrupt")};quote.Seed();Check(quote.TryEnterPermission());
   string entered=Path.Combine(quote.DirectoryName,quote.Quote.QuoteId.ToString("N")+".1.rcj");File.WriteAllText(entered,"partial entered evidence");
   Check(!quote.Cold().TryMarkUnused());Check(!quote.Cold().TryEnterPermission());Check(File.ReadAllText(entered)=="partial entered evidence");
   GameIO.Root=path;var warm=QuotePhase.SeedWarm(out var original,out var source);
   Check(QuotePhase.TryMarkLocalUnused(source,out var unused)&&!unused.TryEnterPermission());
   Check(QuotePhase.TryMarkLocalUnused(source,out var unusedRetry)&&unusedRetry.Image==unused.Image);
   warm=QuotePhase.SeedWarm(out original,out source);original.Quote.ExpiresClockTicks=DateTime.UtcNow.AddMinutes(1).Ticks;Check(!QuotePhase.TryMarkLocalUnused(source,out _)&&warm.TryEnterPermission());
   warm=QuotePhase.SeedWarm(out original,out source);source.Valid=false;Check(!QuotePhase.TryMarkLocalUnused(source,out _)&&warm.TryEnterPermission());
   warm=QuotePhase.SeedWarm(out original,out source);Check(warm.TryEnterPermission()&&!QuotePhase.TryMarkLocalUnused(source,out _));
   warm=QuotePhase.SeedWarm(out original,out source);original.Account="foreign";Check(!QuotePhase.TryMarkLocalUnused(source,out _)&&warm.TryEnterPermission());
   warm=QuotePhase.SeedWarm(out original,out source);var disposition=new HostUnused{Original=original};Check(!warm.HasSavedUnusedState&&!warm.MatchesSavedUnused(disposition));
   Check(warm.TryMarkUnused()&&warm.HasSavedUnusedState&&warm.MatchesSavedUnused(disposition));
   disposition.Retained=false;Check(!warm.MatchesSavedUnused(disposition));disposition.Retained=true;
   original.Account="foreign";Check(!warm.MatchesSavedUnused(disposition));original.Account="owner";
   original.Creation="foreign";Check(!warm.MatchesSavedUnused(disposition));original.Creation="creation";
   ThreadManager.Main=false;Check(!warm.MatchesSavedUnused(disposition));ThreadManager.Main=true;
   warm=QuotePhase.SeedWarm(out original,out source);disposition=new HostUnused{Original=original};Check(warm.TryEnterPermission()&&!warm.HasSavedUnusedState&&!warm.MatchesSavedUnused(disposition));
   Console.WriteLine(count+" actual quote-phase/binary journal assertions passed; native issuance and restore not certified");
  }
  finally
  {
   if(Path.GetDirectoryName(Path.GetFullPath(path))!=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(path).StartsWith("rebirth-quote-phase-",StringComparison.Ordinal))throw new InvalidDataException();
   Directory.Delete(path,true);
  }
 }
}
