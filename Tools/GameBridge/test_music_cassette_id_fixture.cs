using System;
public class RebirthWorldSupportState {public long MusicRevision;public object PendingGearTransfer,PendingMusicTransfer,PendingLibraryTransfer;}
class Check {
// METHODS
 static bool Previous(string id){const string prefix="FuriousRamsayCassette";int n;return id!=null&&id.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(id.Substring(prefix.Length),out n)&&n>=1&&n<=23&&id==prefix+n.ToString("00",System.Globalization.CultureInfo.InvariantCulture);}
 static void Test(string id){if(IsMusicCassette(id)!=Previous(id))throw new Exception("Mismatch: "+id);}
 static void Main(){
  var state=new RebirthWorldSupportState{MusicRevision=7};
  if(!CanBeginChange(state,7)||CanBeginChange(state,6)||CanBeginChange(null,7))throw new Exception("revision admission");
  state.PendingGearTransfer=new object();if(CanBeginChange(state,7))throw new Exception("gear custody conflict");state.PendingGearTransfer=null;
  state.PendingMusicTransfer=new object();if(CanBeginChange(state,7))throw new Exception("music custody conflict");state.PendingMusicTransfer=null;
  state.PendingLibraryTransfer=new object();if(CanBeginChange(state,7))throw new Exception("library custody conflict");state.PendingLibraryTransfer=null;
  state.MusicRevision=long.MaxValue;if(CanBeginChange(state,long.MaxValue))throw new Exception("revision overflow");
  state.MusicRevision=-1;if(CanBeginChange(state,-1))throw new Exception("negative revision");

  foreach(string culture in new[]{"en-US","fr-CA","ar-SA"}){
   System.Threading.Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo(culture);
   Test(null);Test("");Test("rebirthAudiobookMedicine");
   for(int i=-100;i<=999;i++){string n=i.ToString(System.Globalization.CultureInfo.InvariantCulture);Test("FuriousRamsayCassette"+n);Test("FuriousRamsayCassette"+i.ToString("00",System.Globalization.CultureInfo.InvariantCulture));Test("FuriousRamsayCassette "+n);}
   for(int a=0;a<128;a++)for(int b=0;b<128;b++)Test("FuriousRamsayCassette"+(char)a+(char)b);
   for(int i=1;i<=23;i++)if(!IsMusicCassette("FuriousRamsayCassette"+i.ToString("00",System.Globalization.CultureInfo.InvariantCulture)))throw new Exception("Valid cassette rejected");
  }
  Console.WriteLine("PASS: actual music admission refuses competing custody/stale or overflowing revisions; cassette predicate matches previous behavior for valid catalogue, malformed numbers and all ASCII two-character suffixes in three cultures.");
 }
}
