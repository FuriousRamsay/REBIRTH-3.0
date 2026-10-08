// Explicit synthetic span reader boundary. Actual production reader source ABI is a separate required qualification.
partial class TileEntityWorkstation {public ItemStack[] Input=new[]{new ItemStack(new ItemValue(1),1)};}
internal sealed class RebirthStationNativeInputSnapshot {
 readonly string image;RebirthStationNativeInputSnapshot(string x){image=x;}static string E(ItemStack[] s)=>string.Join("|",s.Select(x=>x.count+":"+RebirthNativeItemCodec.Encode(x.itemValue)));internal static bool TryCapture(ItemStack[] s,out RebirthStationNativeInputSnapshot result){result=s==null?null:new(E(s));return result!=null;}internal bool Matches(ItemStack[] s)=>s!=null&&E(s)==image;internal bool MatchesSerializedInput(byte[] bytes)=>System.Text.Encoding.UTF8.GetString(bytes)==image;
}
internal static class RebirthStationTerminalContents {internal static ItemStack[] Decoded;internal static bool TryReadOutput(byte[] b,int c,out ItemStack[] result){result=Decoded;return b!=null&&result!=null;}}
internal static class RebirthNativeItemConformanceReader {internal static CraftCompleteData[] Decoded;internal static Action AfterRead;internal static bool TryDecodeStationCompletions(byte[] b,out CraftCompleteData[] result){result=Decoded;AfterRead?.Invoke();return b!=null&&result!=null;}}

