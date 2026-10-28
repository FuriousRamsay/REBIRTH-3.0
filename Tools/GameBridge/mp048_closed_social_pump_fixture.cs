// AUTHORED_NOT_RUN. Complete production pump/prune and round-robin index.
// Canonical HostClosed/atomic social publication remain separate source cohorts.
using System;using System.Linq;using System.Collections.Generic;
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
// ACTUAL_ORDER
public static partial class RebirthNpcTradeService{
 private class Quote{internal Guid QuoteId;internal long ExpiresClockTicks;}
 private class QuoteRecord{internal Quote Quote;internal bool Consumed;internal int Resolution;internal HostPrepared OwnerPreparation;internal Unused OwnerUnused;}
 private class Unused{internal bool ReadRetained()=>true;}
 private class HostPrepared{internal HostClosed ClosedRecord;}
 private class HostClosed{internal bool HasObservedSavedSocialCompletion,Save=true;internal int Calls;internal bool TrySaveSocialCompletion(){Calls++;if(Save)HasObservedSavedSocialCompletion=true;return Save;}}
 static readonly object Sync=new object();static Dictionary<Guid,QuoteRecord> Quotes=new Dictionary<Guid,QuoteRecord>();static Dictionary<Guid,object> localAttempts=new Dictionary<Guid,object>();static RebirthNpcFairRoundRobin<Guid> QuoteOrder=new RebirthNpcFairRoundRobin<Guid>();static bool scope=true;
 static bool TradeScope(bool bind)=>scope;
 // ACTUAL_PUMP
 // ACTUAL_PRUNE
 static int checks;static void Need(bool condition){if(!condition)throw new Exception("closed social pump "+checks);checks++;}
 static QuoteRecord Add(bool closed){var operation=Guid.NewGuid();var record=new QuoteRecord{Quote=new Quote{QuoteId=operation,ExpiresClockTicks=0},Consumed=closed,Resolution=closed?1:0,OwnerPreparation=closed?new HostPrepared{ClosedRecord=new HostClosed()}:null};Quotes.Add(operation,record);QuoteOrder.Add(operation);return record;}
 static void Seed(){Quotes.Clear();QuoteOrder.Clear();localAttempts.Clear();scope=ThreadManager.Main=true;nextClosedSocialTick=0;}
 static void Tick(){nextClosedSocialTick=0;TickClosedSocial();}
 static int Calls()=>Quotes.Values.Sum(value=>value.OwnerPreparation?.ClosedRecord?.Calls??0);
 public static void Main(){
  Seed();var first=Add(true);first.OwnerPreparation.ClosedRecord.Save=false;Need(!CanPruneQuote(first,long.MaxValue));Tick();Need(Calls()==1&&!CanPruneQuote(first,long.MaxValue));first.OwnerPreparation.ClosedRecord.Save=true;Tick();Need(Calls()==2&&CanPruneQuote(first,long.MaxValue));Tick();Need(Calls()==2);
  localAttempts.Add(first.Quote.QuoteId,new object());Need(!CanPruneQuote(first,long.MaxValue));localAttempts.Clear();first.Resolution=0;Need(!CanPruneQuote(first,long.MaxValue));
  Seed();for(int i=0;i<6;i++)Add(true);Tick();Need(Calls()==4&&Quotes.Values.Count(value=>value.OwnerPreparation.ClosedRecord.HasObservedSavedSocialCompletion)==4);Tick();Need(Calls()==6&&Quotes.Values.All(value=>CanPruneQuote(value,long.MaxValue)));
  Seed();for(int i=0;i<36;i++)Add(false);for(int i=0;i<4;i++)Add(true);Tick();Need(Calls()==0&&QuoteOrder.Count==40);Tick();Need(Calls()==4&&QuoteOrder.Count==40&&QuoteOrder.CopyOrder().Distinct().Count()==40);
  Seed();Add(true);ThreadManager.Main=false;TickClosedSocial();Need(Calls()==0&&nextClosedSocialTick==0);ThreadManager.Main=true;scope=false;TickClosedSocial();Need(Calls()==0&&nextClosedSocialTick==0);scope=true;nextClosedSocialTick=long.MaxValue;TickClosedSocial();Need(Calls()==0);Tick();Need(Calls()==1);
  Seed();var unused=Add(false);Need(CanPruneQuote(unused,long.MaxValue));unused.Consumed=true;Need(!CanPruneQuote(unused,long.MaxValue));
  Console.WriteLine("PASS "+checks+" actual closed social pump/pruning assertions; native/social producers adapted");
 }
}
