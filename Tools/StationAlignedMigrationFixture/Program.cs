using System;
class Program{
static int n;static void Check(bool b,string name){if(!b)throw new Exception(name);n++;}
static void Main(){Console.WriteLine(NativeLifecycle.NativeLifecycleFixture.Run());Console.WriteLine(LayoutFixture.Run());
for(int materials=0;materials<=6;materials++){
var names=new string[materials];for(int i=0;i<materials;i++)names[i]="material"+i;
var input=ItemStack.CreateArray(3+materials);var previous=ItemStack.CreateArray(3);var timers=new[]{13.5f,-2.1474836E+09f,-0.75f};
for(int i=0;i<input.Length;i++){input[i].count=i+1;input[i].itemValue.Data="native-metadata"+i;}for(int i=0;i<3;i++){previous[i].count=4+i;previous[i].itemValue.Data="last"+i;}
Check(RebirthStationAlignedInputMigration.TryExpand(input,previous,timers,names,names,out var migrated,out var last,out var remaining),"aligned legacy migration");
Check(migrated.Length==9+materials&&last.Length==9&&remaining.Length==9,"all physical arrays aligned nine");
for(int i=0;i<3;i++)Check(last[i].count==previous[i].count&&last[i].itemValue.Data==previous[i].itemValue.Data&&remaining[i]==timers[i],"running timer and native last input preserved");
for(int i=3;i<9;i++)Check(last[i].count==0&&remaining[i]==0&&migrated[i].count==0,"new physical cells empty without invented timing");
for(int i=0;i<materials;i++)Check(migrated[9+i].count==input[3+i].count&&migrated[9+i].itemValue.Data==input[3+i].itemValue.Data,"material totals preserve exact suffix");
Check(RebirthStationAlignedInputMigration.TryExpand(migrated,last,remaining,names,names,out var again,out var lastAgain,out var timersAgain),"already aligned migrated layout idempotent");
lastAgain[0].itemValue.Data="changed";timersAgain[0]=999;migrated[0].count=999;Check(previous[0].itemValue.Data=="last0"&&timers[0]==13.5f&&input[0].count==1&&last[0].itemValue.Data=="last0"&&remaining[0]==13.5f,"all output graphs detached");
}
var cells=ItemStack.CreateArray(5);var prior=ItemStack.CreateArray(3);var time=new float[3];var original=new[]{"iron","lead"};
Check(!RebirthStationAlignedInputMigration.TryExpand(cells,prior,time,original,new[]{"lead","iron"},out _,out _,out _),"reordered material meanings refused");
Check(!RebirthStationAlignedInputMigration.TryExpand(cells,prior,time,original,new[]{"iron","brass"},out _,out _,out _),"changed material meanings refused");
Check(!RebirthStationAlignedInputMigration.TryExpand(cells,ItemStack.CreateArray(9),time,original,original,out _,out _,out _),"mixed last input generation refused");
Check(!RebirthStationAlignedInputMigration.TryExpand(cells,prior,new float[9],original,original,out _,out _,out _),"mixed timer generation refused");
time[1]=float.NaN;Check(!RebirthStationAlignedInputMigration.TryExpand(cells,prior,time,original,original,out _,out _,out _),"nonfinite native timing refused");time[1]=0;
Check(!RebirthStationAlignedInputMigration.TryExpand(cells,prior,time,new[]{"iron","IRON"},new[]{"iron","IRON"},out _,out _,out _),"ambiguous duplicate material meanings refused");
prior[0]=null;Check(!RebirthStationAlignedInputMigration.TryExpand(cells,prior,time,original,original,out var failedInput,out var failedLast,out var failedTime)&&failedInput==null&&failedLast==null&&failedTime==null,"failed migration exposes no partial images");
Console.WriteLine($"PASS {n} actual aligned migration checks; native ItemStack clone boundary doubled; no live setup/save/network/smelting/destruction adapter");}}