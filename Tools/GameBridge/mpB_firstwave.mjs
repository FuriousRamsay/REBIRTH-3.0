// Deferred production-method fixture. Do not execute until the complete coding batch is ready.
import {readFile,writeFile,mkdtemp,rm,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function body(source,signature){
 const start=source.indexOf(signature);if(start<0)throw Error('Missing production member: '+signature);
 let open=source.indexOf('{',start),depth=1,i=open+1;
 // Members selected here contain no interpolated brace literals. Fail extraction on source drift.
 for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;else if(source[i]==='}')depth--;}
 if(depth)throw Error('Unbalanced production member: '+signature);
 return source.slice(start,i);
}
const provider=await readFile(join(root,'Scripts/AdvancedFarming/AdvancedFarmingWaterProviderRegistry.cs'),'utf8');
const light=await readFile(join(root,'Scripts/AdvancedFarming/AdvancedFarmingLightService.cs'),'utf8');
const fire=await readFile(join(root,'Scripts/Fire/RebirthFireSystem.cs'),'utf8');
const security=await readFile(join(root,'Scripts/BlockPickup/RebirthWorkstationSecurityService.cs'),'utf8');
let fixture=await readFile(join(here,'mpB_firstwave_fixture.cs'),'utf8');
fixture=fixture.replace('// PROVIDER_MEMBER',body(provider,'private static bool IsTileEntityLive('));
fixture=fixture.replace('// LIGHT_MEMBERS',[
 'private const int ExposureSearchHorizontalRadius=12;',body(light,'private struct NaturalLightExposure'),
 ...['private static bool IsExposureAreaLoaded(','private static bool IsOpenSkyColumnMemoized(','private static void TryEnqueueNeighbor('].map(s=>body(light,s))
].join('\n'));
fixture=fixture.replace('// FIRE_CLASS',body(fire,'public static class RebirthFirePersistence'));
fixture+='\n'+(await readFile(join(root,'Scripts/Fire/RebirthFirePersistenceScope.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
fixture=fixture.replace('// SECURITY_MEMBERS',[
 body(security,'private sealed class SecurityState'),
 ...['private static void SaveCurrent(','private static void EnsureManagedPrimary(','private static void Load()','private static Dictionary<Vector3i, SecurityState> ReadFile(','private static void PublishLoaded(','private static SecurityState GetOrMigrateState(','private static void SetState(','private static string GetPath()'].map(s=>body(security,s))
].join('\n'));
fixture=fixture.replace('// DURABLE_HELPER',await readFile(join(root,'Scripts/Persistence/RebirthDurableFileCommit.cs'),'utf8').then(s=>body(s,'public static class RebirthDurableFileCommit')));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpB-'));
try {
 const sdkRoot=process.env.MPB_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';
 const sdks=(await readdir(sdkRoot)).filter(s=>/^\d+\.\d+\.\d+$/.test(s)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
 if(!sdks.length)throw Error('No Roslyn SDK; set MPB_DOTNET_SDK_ROOT');
 const cs=join(temp,'mpB.cs'),exe=join(temp,'mpB.exe');await writeFile(cs,fixture);
 await run('dotnet',[join(sdkRoot,sdks.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,
  ...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
 console.log((await run(exe,[temp],{windowsHide:true,timeout:30000})).stdout.trim());
} finally {await rm(temp,{recursive:true,force:true});}
