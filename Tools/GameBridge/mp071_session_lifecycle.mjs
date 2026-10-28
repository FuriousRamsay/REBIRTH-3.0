// AUTHORED_NOT_RUN. Run only after project-wide production/scenario coding completes.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
function block(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique actual method required '+key);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed actual method');return source.slice(start,end);}
let fixture=await readFile(join(here,'mp071_session_lifecycle_fixture.cs'),'utf8');
for(const [marker,file] of [['TRANSPORT','RebirthCamperRemoteStationTransport.cs'],['ENVELOPE','RebirthCamperRemoteStationEnvelope.cs'],['FUEL_PACKAGE','NetPackageRebirthCamperFuelSwitchV2.cs'],['COMMAND','RebirthCamperRemoteStationCommand.cs'],['FUEL_STATE','RebirthCamperStationFuelState.cs']]){
 const source=await readFile(join(root,'Scripts/Vehicles/Camper',file),'utf8');const slot='// ACTUAL_'+marker;
 if(fixture.split(slot).length!==2)throw Error('Unique source slot required '+slot);
 fixture=fixture.replace(slot,source.replace(/^using .*;\r?\n/gm,''));
}
const admission=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteAdmission.cs'),'utf8');
const statusQuery=await readFile(join(root,'Scripts/Vehicles/Camper/NetPackageRebirthCamperStationCommandStatus.cs'),'utf8');
const updates=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationUpdates.cs'),'utf8');fixture=fixture.replace('// ACTUAL_WATCH',block(updates,'private static IEnumerator Watch('));
fixture=fixture.replace('// ACTUAL_STATUS_QUERY',block(statusQuery,'internal static void QueryOriginalStatus('));
fixture=fixture.replace('// ACTUAL_ADMISSION',block(admission,'public static bool CanAccess('));
const out=await mkdtemp(join(tmpdir(),'rebirth-mp071-session-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
