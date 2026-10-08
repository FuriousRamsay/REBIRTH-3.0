// SOURCE
class Check {
 static void Main(){
  var p=new RebirthHudTrackingPreferences();
  for(int i=0;i<15;i++)p.Add(RebirthHudTrackType.Skill,"skill"+i);
  for(int from=0;from<15;from++)for(int to=0;to<15;to++){
   var copy=p.Clone();string a=copy.Entries[from].StableId,b=copy.Entries[to].StableId;
   bool moved=copy.MoveGrid(RebirthHudTrackType.Skill,a,to%3-from%3,to/3-from/3);
   if(moved!=(from!=to)||copy.Entries[to].StableId!=a||copy.Entries[from].StableId!=b||copy.Entries.Count!=15)
    throw new System.Exception("swap "+from+" -> "+to);
   for(int i=0;i<15;i++)if(i!=from&&i!=to&&copy.Entries[i].StableId!=p.Entries[i].StableId)
    throw new System.Exception("unrelated identity moved");
  }
  if(p.MoveGrid(RebirthHudTrackType.Skill,"missing",1,0)||p.MoveGrid(RebirthHudTrackType.Skill,"skill0",-1,0))
   throw new System.Exception("invalid target accepted");
  System.Console.WriteLine("PASS actual tracking model: all225 grid drop pairs preserve identities/count, swap only endpoints, invalid source/bounds refused");
 }
}class RebirthHudTrackingPreferenceStore { public const int CurrentSchemaVersion=1; }
