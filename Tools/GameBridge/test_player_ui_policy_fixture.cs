using System;
class ItemClass { public string Id; public string GetItemName(){return Id;} }
class ItemValue { public ItemClass ItemClass; }
class ItemStack {public ItemValue itemValue;public bool Empty;public bool IsEmpty(){return Empty;} }
class RebirthAudiobookDefinition {public string SourceLiteratureId;}
static class RebirthProgressionRuntimeConfig {
public static bool TryGetAudiobook(string id,out RebirthAudiobookDefinition audio){audio=id=="audio"?new RebirthAudiobookDefinition{SourceLiteratureId="physical"}:null;return audio!=null;}
public static bool TryGetLiterature(string id,out object def){def=id=="physical"?new object():null;return def!=null;}}
class Counter {public int Step=5,MaxCount=25,Count=10;public void SetCount(int n){Count=n;}public void ForceTextRefresh(){} }
class XUiC_ItemInfoWindow {public bool isBuying;public Counter BuySellCounter=new Counter();public void RefreshBindings(){} }
class XUiC_RebirthTraderDetails:XUiC_ItemInfoWindow {}
class Check {
// SOURCE
// SELL
static ItemStack Stack(string id){return new ItemStack{itemValue=new ItemValue{ItemClass=new ItemClass{Id=id}}};}
static void Main(){
if(SourceLiterature(null)!=null||SourceLiterature(new ItemStack())!=null||SourceLiterature(Stack("music"))!=null)throw new Exception("Unrelated stack classified");
if(SourceLiterature(Stack("audio"))!="physical"||SourceLiterature(Stack("physical"))!="physical")throw new Exception("Completion identity mismatch");
var sell=new XUiC_RebirthTraderDetails();Postfix(sell,true);if(sell.BuySellCounter.Count!=25)throw new Exception("Sell not maximum");
var buy=new XUiC_RebirthTraderDetails{isBuying=true};Postfix(buy,true);if(buy.BuySellCounter.Count!=5)throw new Exception("Buy minimum changed");
sell.BuySellCounter.Count=10;Postfix(sell,false);if(sell.BuySellCounter.Count!=10)throw new Exception("Manual choice reset");
sell.BuySellCounter.MaxCount=0;Postfix(sell,true);if(sell.BuySellCounter.Count!=0)throw new Exception("Unavailable quantity");
Console.WriteLine("PASS: actual source-literature mapping shares physical/audio identity and excludes music/null items; actual trade postfix defaults sell to max, buy to minimum bundle, preserves manual selection, handles zero availability. Native registry/counter substituted.");
}}
