// AUTHORED_NOT_RUN. Entire actual shared receipt reader; only native type
// declarations and the creation of deterministic native-framed bytes adapted.
class EntityBuffs{internal const int Version=3;}
class PlayerDataFile{internal System.IO.MemoryStream buffData;}
class RecipeOutputKeyCases
{
 static int cases;
 static void Check(bool value,string message){if(!value)throw new System.Exception(message);cases++;}
 static byte[] Image(params System.Collections.Generic.KeyValuePair<string,float>[] values)
 {
  using(var bytes=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
  {writer.Write((byte)3);writer.Write((ushort)0);writer.Write((ushort)values.Length);foreach(var value in values){writer.Write(value.Key);writer.Write(value.Value);}writer.Flush();return bytes.ToArray();}
 }
 static System.Collections.Generic.KeyValuePair<string,float> Pair(string key,float value)=>new System.Collections.Generic.KeyValuePair<string,float>(key,value);
 static void Main()
 {
  var operation=System.Guid.Parse("12345678-1234-1234-abcd-123456789abc");string key="rbRecipeOutput_"+operation.ToString("N");
  var bytes=Image(Pair(key,1));
  Check(RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(bytes,out var keys)&&keys.Length==1&&keys[0]==operation,"exact original prefix not discovered");
  Check(RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(bytes,key,out var phase,out var present)&&present&&phase==1,"discovered marker not readable through actual phase reader");
  Check(RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(Image(Pair(key,65)),out keys)&&keys.Length==1,"last bounded prefix refused");
  Check(RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(Image(Pair(key,0),Pair("unrelated",99)),out keys)&&keys.Length==0,"cleanup zero or unrelated cvar became active original");
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,1.5f,-1f,66f})
   Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(Image(Pair(key,invalid)),out _),"invalid original prefix accepted "+invalid);
  Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(Image(Pair(key,1),Pair(key,2)),out _),"duplicate original markers accepted");
  Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(Image(Pair("rbRecipeOutput_"+operation.ToString("N").ToUpperInvariant(),1)),out _),"noncanonical operation accepted");
  Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(Image(Pair("rbRecipeOutput_"+System.Guid.Empty.ToString("N"),1)),out _),"empty operation accepted");
  var many=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string,float>>();for(int i=1;i<=66;i++)many.Add(Pair("rbRecipeOutput_"+i.ToString("x32"),1));
  Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(Image(many.ToArray()),out _),"unbounded active originals accepted");
  var truncated=new byte[bytes.Length-1];System.Array.Copy(bytes,truncated,truncated.Length);
  Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(truncated,out _),"truncated native cvar accepted");
  var trailing=new byte[bytes.Length+1];System.Array.Copy(bytes,trailing,bytes.Length);
  Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(trailing,out _),"trailing native bytes accepted");
  bytes[0]=4;Check(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(bytes,out _),"unsupported native buff version accepted");
  System.Console.WriteLine("MP028 native prefix keys: "+cases+" assertions; actual complete receipt reader, native frame construction/type adapters.");
 }
}
