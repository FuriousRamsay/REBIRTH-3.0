using System;
using System.Linq;
// Tools candidate: typed live conservation proof. No stored-data authority or queue-exit publication proof.
internal sealed class MixedExpectationCandidate
{
 internal sealed class Anchor
 {
  internal readonly RebirthStationCompletionCapture.SuccessfulOutput Paid;
  internal readonly NativePaidOrdinaryRouteLink Initial;
  private Anchor(RebirthStationCompletionCapture.SuccessfulOutput paid, NativePaidOrdinaryRouteLink route){Paid=paid;Initial=route;}
  internal static bool TryCapture(RebirthStationCompletionCapture.SuccessfulOutput paid, NativePaidOrdinaryRouteLink route,out Anchor result)
  {
   result=null;
   try {if(paid==null||route==null||!route.MatchesOriginalPaidEvent(paid)||route.IsQuarantined||route.HeldOriginalCount!=0||route.Transitions().Length!=0||!paid.MatchesReceipt()||!Matches(route,paid)||!paid.Original.IsCurrent())return false;result=new Anchor(paid,route);return true;}catch{return false;}
  }
 }
 readonly Anchor anchor;readonly NativePaidOrdinaryRouteLink route;readonly string frozenChain;
 MixedExpectationCandidate(Anchor a,NativePaidOrdinaryRouteLink r,string chain){anchor=a;route=r;frozenChain=chain;}
 static bool Same(NativePaidOrdinaryRouteLink.Cell[] a,NativePaidOrdinaryRouteLink.Cell[] b)=>a.Length==b.Length&&a.Zip(b,(x,y)=>x.Count==y.Count&&x.Image==y.Image).All(x=>x);
 static bool Matches(NativePaidOrdinaryRouteLink route,RebirthStationCompletionCapture.SuccessfulOutput paid)
 {
  var station=paid.Original.Station;var live=station.Output;
  var tail=route.CurrentTail();if(live==null||tail.Length!=live.Length)return false;
  for(int i=0;i<live.Length;i++)if(live[i]==null||live[i].count!=tail[i].Count||RebirthNativeItemCodec.Encode(live[i].itemValue)!=tail[i].Image)return false;
  return OrdinaryNativeCallsiteCandidate.TrySnapshotReceiptImages(station,out var receipts)&&receipts.SequenceEqual(route.CurrentReceipts());
 }
 internal static bool TryCreate(Anchor anchor,NativePaidOrdinaryRouteLink route,out MixedExpectationCandidate result)
 {
  result=null;
  try {
   if(anchor==null||route==null||route.IsQuarantined||route.HeldOriginalCount!=0||!route.MatchesOriginalPaidEvent(anchor.Paid)||!anchor.Paid.Original.IsCurrent()||route.OriginalPaidReceiptImage!=anchor.Initial.OriginalPaidReceiptImage||route.PaidQuantity!=anchor.Initial.PaidQuantity||!Same(route.PaidBefore(),anchor.Initial.PaidBefore())||!Same(route.PaidAfter(),anchor.Initial.PaidAfter()))return false;
   var transitions=route.Transitions();if(transitions.Length==0||transitions.Any(t=>t.Original==null||!t.Original.MatchesOriginal(anchor.Paid)))return false;
   var tail=anchor.Initial.PaidAfter();var receipts=anchor.Initial.CurrentReceipts();
   foreach(var t in transitions){if(!Same(t.Before(),tail)||!t.ReceiptBefore().SequenceEqual(receipts))return false;tail=t.After();receipts=t.ReceiptAfter();}
   if(!Same(tail,route.CurrentTail())||!receipts.SequenceEqual(route.CurrentReceipts())||!route.TryWriteStoredData(out var chain)||!Matches(route,anchor.Paid)||!anchor.Paid.Original.IsCurrent())return false;
   result=new MixedExpectationCandidate(anchor,route,chain);return true;
  }catch{return false;}
 }
 internal bool MatchesLive()
 {
  try{return anchor.Paid.Original.IsCurrent()&&route.MatchesOriginalPaidEvent(anchor.Paid)&&route.TryWriteStoredData(out var chain)&&chain==frozenChain&&Matches(route,anchor.Paid)&&anchor.Paid.Original.IsCurrent();}catch{return false;}
 }
 internal bool MatchesDecodedTerminal(ItemStack[] output,CraftCompleteData[] receipts)
 {
  try {
   if(output==null||receipts==null||!MatchesLive())return false;
   var tail=route.CurrentTail();if(output.Length!=tail.Length)return false;
   for(int i=0;i<output.Length;i++)if(output[i]?.itemValue==null||output[i].count!=tail[i].Count||RebirthNativeItemCodec.Encode(output[i].itemValue)!=tail[i].Image)return false;
   var images=receipts.Select(r=>new System.Xml.Linq.XElement("nativeReceipt",new System.Xml.Linq.XAttribute("actor",r.CrafterEntityID),new System.Xml.Linq.XAttribute("recipe",r.RecipeName??""),new System.Xml.Linq.XAttribute("scrap",r.ItemScrapped??""),new System.Xml.Linq.XAttribute("xp",r.CraftExpGain),new System.Xml.Linq.XAttribute("used",r.RecipeUsedCount),new System.Xml.Linq.XAttribute("count",r.CraftedItemStack.count),new System.Xml.Linq.XAttribute("image",RebirthNativeItemCodec.Encode(r.CraftedItemStack.itemValue))).ToString(System.Xml.Linq.SaveOptions.DisableFormatting)).ToArray();
   return images.SequenceEqual(route.CurrentReceipts())&&MatchesLive();
  }catch{return false;}
 }
 internal string FrozenChainData=>frozenChain;
 internal string OriginalPaidReceiptImage=>anchor.Initial.OriginalPaidReceiptImage;
 internal int OriginalPaidQuantity=>anchor.Initial.PaidQuantity;
}

