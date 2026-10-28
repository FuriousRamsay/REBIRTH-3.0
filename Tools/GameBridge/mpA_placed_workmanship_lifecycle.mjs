// DEFERRED: authoring only. Running this file compiles/runs isolated fixtures.
// No game DLL/native save paths are opened. See scope output and accompanying doc.
import {readFile,writeFile,mkdtemp,mkdir,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const service=await readFile(join(root,'Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs'),'utf8');
const commit=await readFile(join(root,'Scripts/Persistence/RebirthDurableFileCommit.cs'),'utf8');
const assertions=await readFile(join(here,'mpA_placed_workmanship_lifecycle_fixture.cs'),'utf8');
const template=await readFile(join(here,'mpA_placed_workmanship_offline_fixture.cs'),'utf8');
function unique(text,marker){const a=text.indexOf(marker);if(a<0||text.indexOf(marker,a+marker.length)>=0)throw Error('Missing/ambiguous actual source anchor '+marker);return a;}
// Exact actual service fields/install/lifecycle and private clone/parser/recovery/
// save body ranges. Gameplay placement/native identity bodies excluded, not replaced.
const modelStart=unique(service,'public enum RebirthPlacedProvenanceKind');
const classStart=unique(service,'public static class RebirthPlacedWorkmanshipService');
const gameplayStart=unique(service,'    public static void CapturePlacement(');
const persistenceStart=unique(service,'    private static RebirthPlacedWorkmanshipRecord Clone(');
const serviceEnd=unique(service,'public static class RebirthPlacedWorkmanshipPatchInstaller');
if(!(modelStart<classStart&&classStart<gameplayStart&&gameplayStart<persistenceStart&&persistenceStart<serviceEnd))throw Error('Actual source range order changed');
const actualService=service.slice(modelStart,gameplayStart)+service.slice(persistenceStart,serviceEnd);
const commitStart=unique(commit,'public static class RebirthDurableFileCommit');
const actualCommit=commit.slice(commitStart).replace('public static class RebirthDurableFileCommit','public static class ActualRebirthDurableFileCommit');
const assertionStart=unique(assertions,'internal static class mpA_placed_workmanship_lifecycle_fixture');
let source=template;
for(const [marker,value] of [['// SERVICE_SOURCE',actualService],['// COMMIT_SOURCE',actualCommit],['// ASSERTION_SOURCE',assertions.slice(assertionStart)]])
{unique(source,marker);source=source.replace(marker,value);}
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp036-'));
await writeFile(join(temp,'.fixture-only'),'isolated actual-service offline fixture; not native save authority');
try
{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,source);
 const sdk=process.env.REBIRTH_FIXTURE_CSC_DLL||'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll';
 const dotnet=process.env.REBIRTH_FIXTURE_DOTNET||'C:/Program Files/dotnet/dotnet.exe';
 const framework='C:/Windows/Microsoft.NET/Framework64/v4.0.30319';
 await run(dotnet,[sdk,'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:'+join(framework,n+'.dll')),cs],{windowsHide:true,timeout:20000,maxBuffer:4*1024*1024});
 for(const name of ['installer','parser','cadence-shutdown','cold-tmp','backup','retry','changed-retry','cross-world'])
 {const result=await run(exe,[temp,name],{windowsHide:true,timeout:20000,maxBuffer:4*1024*1024});console.log(result.stdout.trim());}
 console.log('SCOPE: exact extracted current service/install/lifecycle/parser/save/recovery and success publication helper; native World/GameIO/connection/events/time/main-thread, synthetic detached record admission and explicit pre-effect publish/registration refusal DOUBLED. Real temporary filesystem only. NOT gameplay placement, source incarnation, game save atomicity, networking, disk-full, power-loss or multiplayer qualification.');
}
finally
{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')console.log('Retained isolated fixture directory '+temp);
 else
 {
  // Deletion target is the exact generated temp, verified inside OS temp and
  // bearing our sentinel; never derive it from supplied native save paths.
  const actual=resolve(temp),parent=resolve(tmpdir())+sep;
  if(!actual.toLowerCase().startsWith(parent.toLowerCase())||!basename(actual).startsWith('rebirth-mp036-')||!(await readFile(join(actual,'.fixture-only'),'utf8')).startsWith('isolated actual-service'))throw Error('Refuse fixture cleanup outside generated temporary workspace');
  await rm(actual,{recursive:true,force:false});
 }
}
