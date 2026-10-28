// Actual pure production frame validator; no engine/network/save doubles.
// Does not establish packet direction, journal authorization or native save proof.
public static class MpALogisticsTransportFrameFixture
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-",string.Empty).ToLowerInvariant();}
 static string[] Split(string text){var bytes=Encoding.UTF8.GetBytes(text);return Enumerable.Range(0,(bytes.Length+8999)/9000).Select(i=>Convert.ToBase64String(bytes,i*9000,Math.Min(9000,bytes.Length-i*9000))).ToArray();}
 public static void Main()
 {
  string text=new string('x',18001);var parts=Split(text);
  Check(RebirthLogisticsOriginalFrame.TryAssemble(parts,Hash(text),out var joined)&&joined==text,"exact original complete frame");
  Check(!RebirthLogisticsOriginalFrame.TryAssemble(new[]{parts[0],null,parts[2]},Hash(text),out joined),"missing frame fragment is not an original");
  Check(!RebirthLogisticsOriginalFrame.TryAssemble(new[]{parts[1],parts[0],Convert.ToBase64String(new byte[]{122})},Hash(text),out joined),"mixed frame digest refuses");
  Check(!RebirthLogisticsOriginalFrame.TryAssemble(new[]{"short",parts[1]},Hash(text),out joined),"nonfinal short chunk refuses");
  Check(!RebirthLogisticsOriginalFrame.TryAssemble(new[]{new string('x',12001)},Hash(text),out joined),"oversized chunk refuses before concat");
  Check(!RebirthLogisticsOriginalFrame.TryAssemble(new string[468],Hash(text),out joined),"fragment count bound");
  Check(!RebirthLogisticsOriginalFrame.TryAssemble(parts,Hash(text).ToUpperInvariant(),out joined),"noncanonical hash refuses");
  string unicode=new string('\u4e00',1400000);
  Check(!RebirthLogisticsOriginalFrame.TryAssemble(Split(unicode),Hash(unicode),out joined),"UTF8 byte budget independent of character budget");
  string boundary=new string('a',8999)+"\ud83d\ude80tail";
  Check(RebirthLogisticsOriginalFrame.TryAssemble(Split(boundary),Hash(boundary),out joined)&&joined==boundary,"UTF8 scalar split across byte chunks remains exact");
  Console.WriteLine("PASS actual logistics original frame validator");
 }
}
