from pathlib import Path
root=Path.cwd(); out=root/'Tools/PerformanceAudit20261010/PersistenceTickFixture';out.mkdir(exist_ok=True)
parts=['using System;\nstatic class GameManager { public static Holder Instance = new Holder(); public sealed class Holder { public object World = new object(); } }']
for name in ('Equipment','Inventory','Settlement'):
 for version in ('Before','After'):
  p=Path(f'Scripts/NPC/Persistence/RebirthNpc{name}Persistence.cs')
  if version=='Before':p=Path('_Documentation/PerformanceAudit_20261010/before')/p
  s=p.read_text(encoding='utf-8-sig');start=s.index('public static void Tick()');brace=s.index('{',start);depth=1;i=brace+1
  while depth:
   if s[i]=='{':depth+=1
   elif s[i]=='}':depth-=1
   i+=1
  method=s[start:i].replace('DateTime.UtcNow.Ticks','Now')
  parts.append('public static class '+name+version+''' {
public static long Now=1,nextSaveUtcTicks;
public static int Loads,Saves;
public static bool Server=true;
public static string Path="fixture";
static bool IsServer()=>Server;
static string GetPath()=>Path;
static void EnsureLoaded(){if(GameManager.Instance.World!=null && Path.Length>0) Loads++;}
static void Save(){EnsureLoaded();Saves++;}
'''+method+'\n}')
checks=[]
for name in ('Equipment','Inventory','Settlement'):
 checks.append(f'''for(int frame=0;frame<1800;frame++) {{ {name}Before.Now=1+frame*TimeSpan.TicksPerSecond/60; {name}After.Now={name}Before.Now; {name}Before.Tick(); {name}After.Tick(); }}
Check({name}Before.Loads==1801 && {name}After.Loads==1,"{name}: failed-load attempts 1801 to 1 over 30 seconds");
{name}After.Now=1+TimeSpan.FromSeconds(30).Ticks; {name}After.Tick(); Check({name}After.Loads==2,"{name}: retry at deadline");
{name}After.nextSaveUtcTicks=0; GameManager.Instance.World=null; {name}After.Tick(); Check({name}After.Loads==2 && {name}After.nextSaveUtcTicks==0,"{name}: no world does not consume deadline");
GameManager.Instance.World=new object(); {name}After.Path=""; {name}After.Tick(); Check({name}After.nextSaveUtcTicks==0,"{name}: unavailable path does not consume deadline");
{name}After.Path="fixture"; {name}After.Server=false; {name}After.Tick(); Check({name}After.Loads==2,"{name}: client does not load");
{name}After.Server=true; {name}After.Tick(); Check({name}After.Loads==3,"{name}: first ready update loads immediately");''')
parts.append('static class ConnectionManager { public static Holder Instance=new Holder(); public sealed class Holder { public bool IsServer=true; } }')
for version in ('Before','After'):
 p=Path('Scripts/NPC/Simulation/RebirthNpcSettlementSimulation.cs')
 if version=='Before': p=Path('_Documentation/PerformanceAudit_20261010/before')/p
 source=p.read_text(encoding='utf-8-sig'); start=source.index('public static void Tick()'); brace=source.index('{',start); depth=1; i=brace+1
 while depth:
  if source[i]=='{':depth+=1
  elif source[i]=='}':depth-=1
  i+=1
 method=source[start:i].replace('DateTime.UtcNow.Ticks','Now').replace('RebirthNpcSettlementPersistenceStore.EnsureLoaded();','Loads++;')
 parts.append('static class Simulation'+version+' { public static long Now=1,nextNeedsTick,nextAssignmentTick; public static int Loads,Needs,Assignments; static void TickNeeds(long now){Needs++;} static void TickAssignments(long now){Assignments++;} '+method+' }')
checks.append('for(int frame=0;frame<3600;frame++){SimulationBefore.Now=1+frame*TimeSpan.TicksPerSecond/60;SimulationAfter.Now=SimulationBefore.Now;SimulationBefore.Tick();SimulationAfter.Tick();CheckSilent(SimulationBefore.Needs==SimulationAfter.Needs && SimulationBefore.Assignments==SimulationAfter.Assignments);} Check(SimulationBefore.Loads==3600 && SimulationAfter.Loads==30,"Simulation: identical needs/assignment timing; 3600 to 30 load checks in 60 seconds");')
parts.append('class Program { static void CheckSilent(bool ok){if(!ok)throw new Exception("Simulation scheduling changed");} static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);} static void Main(){'+ '\n'.join(checks)+'}}')
(out/'Program.cs').write_text('\n'.join(parts),encoding='utf-8')
(out/'PersistenceTickFixture.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>true</EnableDefaultCompileItems></PropertyGroup></Project>')
print('Extracted actual before/after Tick methods; clock and load/save environment are test doubles.')
