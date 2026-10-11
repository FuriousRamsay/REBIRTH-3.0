from pathlib import Path
p=Path('Scripts/Survivor/Progression/RebirthWorkstationCraftCompletion.cs')
b=Path('_Documentation/PerformanceAudit_20261010/before')/p

def extract(p):
 s=p.read_text(encoding='utf-8-sig'); a=s.index('    private static Dictionary<int,int> Counts(Snapshot s)'); z=s.index('    private static string Pool(',a);return s[a:z]
old=extract(b).replace('Counts(Snapshot s)','Baseline(Snapshot s)')
current=extract(p)
stub='''using System;using System.Linq;using System.Collections.Generic;using System.Diagnostics;
class ItemValue { public int type; }
class ItemStack { public ItemValue itemValue=new();public int count;public bool IsEmpty()=>count<=0||itemValue.type==0; }
class Grid { public ItemStack[] items; }
class Container { public Grid ItemGrid=new(); }
class Player {public Container bag=new(),inventory=new();}
class Tile {public ItemStack[] Output;}
class Snapshot {public Player Player;public Tile Tile;}
static class Program {
static void Check(bool x,string name){if(!x)throw new Exception(name);Console.WriteLine("PASS "+name);}
static ItemStack[] Make(Random r,int n){return Enumerable.Range(0,n).Select(_=>r.Next(5)==0?null:new ItemStack{itemValue=new(){type=r.Next(12)},count=r.Next(-1,501)}).ToArray();}
static bool Same(Dictionary<int,int> a,Dictionary<int,int>b)=>a.Count==b.Count&&a.All(p=>b.TryGetValue(p.Key,out int v)&&v==p.Value);
static void Main(){var r=new Random(871);for(int i=0;i<2000;i++){
 var s=new Snapshot{Player=new()};s.Player.bag.ItemGrid.items=Make(r,r.Next(170));s.Player.inventory.ItemGrid.items=Make(r,r.Next(13));
 if(i%2==0)s.Tile=new(){Output=Make(r,r.Next(29))};CheckCase(s);
}Console.WriteLine("PASS 2000 randomized tile/inventory snapshots match baseline");
var sample=new Snapshot{Player=new()};sample.Player.bag.ItemGrid.items=Make(r,169);sample.Player.inventory.ItemGrid.items=Make(r,12);
var saved=Counts(sample);var copy=new Dictionary<int,int>(saved);foreach(var item in sample.Player.bag.ItemGrid.items)if(item!=null)item.count=999;
Check(Same(saved,copy),"Snapshot remains independent after item mutation");
Check(!ReferenceEquals(saved,Counts(sample)),"Each capture has independent dictionary");
for(int i=0;i<1000;i++){Baseline(sample);Counts(sample);}
Measure("baseline",()=>Baseline(sample));Measure("current",()=>Counts(sample));
}
static void CheckCase(Snapshot s){if(!Same(Baseline(s),Counts(s)))throw new Exception("Parity failure");}
static void Measure(string label,Func<Dictionary<int,int>> f){long before=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();int total=0;for(int i=0;i<10000;i++)total+=f().Count;sw.Stop();Console.WriteLine($"{label}: bytes/call={(GC.GetAllocatedBytesForCurrentThread()-before)/10000.0:F2}; us/call={sw.Elapsed.TotalMicroseconds/10000:F3}; checksum={total}");}
'''
d=Path('Tools/PerformanceAudit20261010/CompletionCountsFixture');d.mkdir(exist_ok=True)
(d/'Program.cs').write_text(stub+old+current+'}\n')
(d/'CompletionCountsFixture.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
