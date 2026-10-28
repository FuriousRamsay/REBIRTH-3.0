using System;using System.Collections.Generic;
// Actual custody tracker; native synchronous release witness is an adapter.
static class RebirthNpcTradeOwnerExchange
{
 internal sealed class Release{internal bool Valid=true;internal string Original;internal bool Current()=>Valid;}
}
// ACTUAL_TRADE_RETIREMENT
static class Program
{
 static int checks;static void Check(bool value){checks++;if(!value)throw new Exception("Trade retirement source assertion "+checks);}
 static Func<RebirthNpcTradeOwnerExchange.Release,bool> Match(string original)=>release=>release.Original==original;
 static void Main()
 {
  var empty=new RebirthNpcTradeClientRetirement(Array.Empty<Func<RebirthNpcTradeOwnerExchange.Release,bool>>());Check(empty.Complete);
  var tracker=new RebirthNpcTradeClientRetirement(new[]{Match("first"),Match("second")},new[]{Match("first")});Check(!tracker.Complete);
  tracker.Observe(null);Check(!tracker.Complete);
  tracker.Observe(new RebirthNpcTradeOwnerExchange.Release{Original="first",Valid=false});Check(!tracker.Complete);
  tracker.Observe(new RebirthNpcTradeOwnerExchange.Release{Original="foreign"});Check(!tracker.Complete);
  tracker.Observe(new RebirthNpcTradeOwnerExchange.Release{Original="first"});Check(!tracker.Complete);
  tracker.Observe(new RebirthNpcTradeOwnerExchange.Release{Original="first"});Check(!tracker.Complete);
  tracker.Observe(new RebirthNpcTradeOwnerExchange.Release{Original="second"});Check(tracker.Complete);
  bool refused=false;try{new RebirthNpcTradeClientRetirement(new Func<RebirthNpcTradeOwnerExchange.Release,bool>[130]);}catch(InvalidOperationException){refused=true;}Check(refused);
  refused=false;try{new RebirthNpcTradeClientRetirement(new Func<RebirthNpcTradeOwnerExchange.Release,bool>[] {null});}catch(InvalidOperationException){refused=true;}Check(refused);
  Console.WriteLine(checks+" actual trade retirement assertions passed; native release witness NOT certified");
 }
}
