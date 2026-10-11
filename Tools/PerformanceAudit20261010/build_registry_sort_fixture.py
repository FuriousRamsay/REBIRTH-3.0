from pathlib import Path
root=Path.cwd(); rel=Path('Scripts/NPC/Foundation/RebirthNpcFoundation.cs'); current=(root/rel).read_text(encoding='utf-8-sig');before=(root/'_Documentation/PerformanceAudit_20261010/before'/rel).read_text(encoding='utf-8-sig')
def block(s,marker):
 a=s.index(marker);i=s.index('{',a)+1;d=1
 while d:
  if s[i]=='{':d+=1
  elif s[i]=='}':d-=1
  i+=1
 return s[a:i]
parts=['using System;using System.Collections.Generic;using System.Diagnostics;']
parts.append(block(current,'public readonly struct RebirthNpcStableId'))
parts.append('public class RebirthNpcRuntimeState { public RebirthNpcStableId StableId; public RebirthNpcRuntimeState CloneForProjection()=>new RebirthNpcRuntimeState{StableId=StableId}; }')
for name,text in [('Before',before),('After',current)]:
 registry=block(text,'public static class RebirthNpcRuntimeRegistry')
 method=block(registry,'public static RebirthNpcRuntimeState[] GetSnapshot()')
 parts.append('static class '+name+' { static readonly object Sync=new object(); public static readonly Dictionary<int,RebirthNpcRuntimeState> ByEntityId=new Dictionary<int,RebirthNpcRuntimeState>(); '+method+' }')
parts.append('''class Program {
static Random random=new Random(741); static ulong U(){var b=new byte[8];random.NextBytes(b);return BitConverter.ToUInt64(b,0);}
static void Set(int n){Before.ByEntityId.Clear();After.ByEntityId.Clear();for(int i=0;i<n;i++){var x=new RebirthNpcRuntimeState{StableId=new RebirthNpcStableId(U(),U())};Before.ByEntityId[i]=x;After.ByEntityId[i]=x;}}
static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
static void Main(){
ulong[] edges={0,1,15,16,255,256,0x7fffffffffffffff,0x8000000000000000,ulong.MaxValue};
var ids=new List<RebirthNpcStableId>();foreach(var h in edges)foreach(var l in edges)ids.Add(new RebirthNpcStableId(h,l));
foreach(var a in ids)foreach(var b in ids){int old=string.Compare(a.ToString(),b.ToString(),StringComparison.Ordinal);int cmp=a.High.CompareTo(b.High);if(cmp==0)cmp=a.Low.CompareTo(b.Low);if(Math.Sign(old)!=Math.Sign(cmp))throw new Exception("Boundary ordering mismatch");}
Check(true,"6561 boundary pairs match ordinal hexadecimal order");
for(int run=0;run<1000;run++){Set(random.Next(0,101));var a=Before.GetSnapshot();var b=After.GetSnapshot();for(int i=0;i<a.Length;i++)if(!a[i].StableId.Equals(b[i].StableId))throw new Exception("Snapshot order mismatch");}
Check(true,"1000 randomized extracted snapshot comparisons match");
Set(1);var one=After.GetSnapshot();var two=After.GetSnapshot();Check(!ReferenceEquals(one,two)&&!ReferenceEquals(one[0],two[0])&&!ReferenceEquals(one[0],After.ByEntityId[0]),"Independent projection copies retained (state clone doubled)");
foreach(int n in new[]{64,512,2048}){Set(n);Measure("before",n,Before.GetSnapshot);Measure("after",n,After.GetSnapshot);}
}
static void Measure(string label,int n,Func<RebirthNpcRuntimeState[]> action){for(int i=0;i<5;i++)action();long start=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();for(int i=0;i<30;i++)action();watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-start;Console.WriteLine(label+" count="+n+" bytes/snapshot="+(bytes/30.0).ToString("F1")+" us/snapshot="+(watch.Elapsed.TotalMilliseconds*1000/30).ToString("F1"));}
}''')
out=root/'Tools/PerformanceAudit20261010/RegistrySortFixture';out.mkdir(exist_ok=True);(out/'Program.cs').write_text('\n'.join(parts));(out/'RegistrySortFixture.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>')
print('Extracted stable ID and old/current GetSnapshot; projection clone doubled.')
