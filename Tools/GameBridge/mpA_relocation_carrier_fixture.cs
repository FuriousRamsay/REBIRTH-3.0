// Disclosed doubles for item codec and envelope validation only. Native metadata
// serialization, saved journal correlation and physical delivery are NOT covered.
public sealed class ItemValue {
 public System.Collections.Generic.Dictionary<string,object> Metadata=new System.Collections.Generic.Dictionary<string,object>();
}
public sealed class ItemStack {
 public ItemValue itemValue=new ItemValue();public string Identity="original-native-station";
 public ItemStack Clone(){var copy=new ItemStack{Identity=Identity};foreach(var p in itemValue.Metadata)copy.itemValue.Metadata.Add(p.Key,p.Value);return copy;}
}
public static class RebirthCamperStationRemovalContents {
 public const string ContentsKey="contents";
 public static bool TryRead(ItemStack item,out ItemStack[] result){result=null;object value;if(!item.itemValue.Metadata.TryGetValue(ContentsKey,out value)||!(value is string))return false;if(!((string)value).StartsWith("valid:"))throw new System.InvalidOperationException();result=new ItemStack[0];return true;}
 public static ItemStack WithoutContents(ItemStack item){var copy=item.Clone();copy.itemValue.Metadata.Remove(ContentsKey);return copy;}
}
public static class RebirthCamperInventorySnapshot {
 public static byte[] Capture(ItemStack[] items){var s=items[0];var text=s.Identity;var keys=new System.Collections.Generic.List<string>(s.itemValue.Metadata.Keys);keys.Sort(System.StringComparer.Ordinal);foreach(var k in keys)text+="|"+k+"="+s.itemValue.Metadata[k];return System.Text.Encoding.UTF8.GetBytes(text);}
}
public static class MpACarrierFixture {
 static void Check(bool condition,string label){if(!condition)throw new System.Exception(label);}
 public static void Main(){
  var before=new ItemStack();before.itemValue.Metadata["queue"]="paid-original";
  var parcel=before.Clone();parcel.itemValue.Metadata[RebirthCamperStationRemovalContents.ContentsKey]="valid:tools-fuel-output";
  Check(MpACorrelation.SameItem(parcel,before),"validated consumed envelope matches");
  Check(MpACorrelation.SameItem(before,parcel),"same normalized contract reverse");
  Check(MpACorrelation.SameItem(before,before.Clone()),"empty contents control");
  var altered=before.Clone();altered.itemValue.Metadata["queue"]="different";
  Check(!MpACorrelation.SameItem(parcel,altered),"retained queue mismatch rejected");
  altered=parcel.Clone();altered.itemValue.Metadata[RebirthCamperStationRemovalContents.ContentsKey]="valid:different-parcel";
  Check(!MpACorrelation.SameItem(parcel,altered),"two retained differing parcels rejected");
  altered=before.Clone();altered.Identity="different-native-item";
  Check(!MpACorrelation.SameItem(parcel,altered),"native identity mismatch rejected");
  altered=before.Clone();altered.itemValue.Metadata[RebirthCamperStationRemovalContents.ContentsKey]=123;
  Check(!MpACorrelation.SameItem(altered,before),"wrong envelope type rejected");
  altered=before.Clone();altered.itemValue.Metadata[RebirthCamperStationRemovalContents.ContentsKey]="malformed";
  bool refused=false;try{MpACorrelation.SameItem(altered,before);}catch(System.InvalidOperationException){refused=true;}
  Check(refused,"malformed envelope fails closed through enclosing recovery guard");
  System.Console.WriteLine("PASS actual carrier equivalence with disclosed native codec/envelope doubles");
 }
}
