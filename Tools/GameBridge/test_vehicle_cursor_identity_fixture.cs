using System;
class ItemValue {public string Payload;}
class ItemStack {public int count;public ItemValue itemValue;}
static class RebirthNativeItemCodec {public static string Encode(ItemValue v){if(v.Payload=="throw")throw new Exception();return v.Payload;}}
class Check {
// METHODS
static ItemStack S(string payload,int count=1){return new ItemStack{count=count,itemValue=new ItemValue{Payload=payload}};}
static void Main(){if(!MatchesPickedCursorItem(S("same"),S("same")))throw new Exception("Same rejected");foreach(var pair in new[]{new[]{S("a"),S("b")},new[]{S("same",2),S("same")},new[]{S("throw"),S("same")},new[]{(ItemStack)null,S("same")},new[]{new ItemStack(),S("same")}})if(MatchesPickedCursorItem(pair[0],pair[1]))throw new Exception("Unsafe clear allowed");Console.WriteLine("PASS: actual cursor comparison requires equal count and complete encoded payload, refuses null/mismatch/serialization failure. Native codec substituted; no game cursor test.");}
}
