// AUTHORED_NOT_RUN. Actual first-priority mobile callsite + exact source helper.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceClientTransactions.cs'),'utf8');
const key='internal static bool TryOwnMobileResourceActivation(',start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique actual mobile source owner required');
let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}
if(depth)throw Error('Actual mobile helper incomplete');
const actual=source.slice(start,end),caller=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationCraftAction.cs'),'utf8');
const call=caller.indexOf('RemoteResourceClientTransactionCoordinator.TryOwnMobileResourceActivation(player,__instance)');
if(call<0||call>caller.indexOf('RebirthCamperRemoteStationClient.Craft(')||!caller.includes('[HarmonyPrefix,HarmonyPriority(Priority.First)]'))throw Error('Actual first-priority mobile source callsite missing');
const fixture=(await readFile(join(root,'Tools/GameBridge/mp009_actual_mobile_entry_fixture.cs'),'utf8')).replace('// ACTUAL_HELPER',actual);
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp009-mobile-entry-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
