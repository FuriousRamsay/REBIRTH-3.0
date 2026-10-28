// Native CVar storage double only. The production decoder is extracted by the
// companion runner. This does not simulate guards, native death or persistence.
public sealed class EntityBuffs
{
 public readonly Dictionary<string,float> Values=new Dictionary<string,float>();
 public float GetCustomVar(string key)=>Values.TryGetValue(key,out float value)?value:0f;
}
public sealed class EntityPlayerLocal{public EntityBuffs Buffs=new EntityBuffs();}
public static class MpAOwnerDeathScopeFixture
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static EntityPlayerLocal Scope(int deaths)
 {
  var player=new EntityPlayerLocal();player.Buffs.Values["rbLogisticsOwnerDeathScope"]=1;
  player.Buffs.Values["rbLogisticsOwnerDeaths0"]=(ushort)((uint)deaths&ushort.MaxValue);
  player.Buffs.Values["rbLogisticsOwnerDeaths1"]=(ushort)((uint)deaths>>16);return player;
 }
 public static void Main()
 {
  foreach(int expected in new[]{0,1,65535,65536,16777217,int.MaxValue})
   Check(RebirthLogisticsOwnerReceipt.Read(Scope(expected),out int actual)&&actual==expected,"death count is exact beyond one float integer precision");
  var player=Scope(3);player.Buffs.Values["rbLogisticsOwnerDeathScope"]=0;
  Check(!RebirthLogisticsOwnerReceipt.Read(player,out _),"old unknown scope is not guessed as death zero");
  player=Scope(3);player.Buffs.Values["rbLogisticsOwnerDeaths0"]=float.NaN;
  Check(!RebirthLogisticsOwnerReceipt.Read(player,out _),"nonfinite word refuses");
  player=Scope(3);player.Buffs.Values["rbLogisticsOwnerDeaths0"]=3.5f;
  Check(!RebirthLogisticsOwnerReceipt.Read(player,out _),"fractional native word refuses");
  player=Scope(3);player.Buffs.Values["rbLogisticsOwnerDeaths1"]=32768;
  Check(!RebirthLogisticsOwnerReceipt.Read(player,out _),"negative/overflowing signed native death count refuses");
  Console.WriteLine("PASS actual original native death-scope word decoder");
 }
}
