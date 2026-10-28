// AUTHORED_NOT_RUN. Actual Execute -> HandleTracked/Handle -> ReadStatus composition.
// Native effect services and durable identity/effect stores are explicit adapters.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function block(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique actual source required '+key);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed actual method');return source.slice(start,end);}
const [actions,status,transport,command]=await Promise.all(['RebirthCamperRemoteStationActions.cs','RebirthCamperRemoteStationCommandStatus.cs','RebirthCamperRemoteStationTransport.cs','RebirthCamperRemoteStationCommand.cs'].map(file=>readFile(join(root,'Scripts/Vehicles/Camper',file),'utf8')));
let fixture=await readFile(join(root,'Tools/GameBridge/mp071_dispatch_status_fixture.cs'),'utf8');
const replacements=[['// ACTUAL_COMMAND',command.replace(/^using .*;\r?\n/gm,'')],['// ACTUAL_STATUS',status.replace(/^using .*;\r?\n/gm,'')],['// ACTUAL_EXECUTE',block(transport,'internal static bool Execute(')],['// ACTUAL_ACTIONS_BODY',actions.slice(actions.indexOf('[ThreadStatic] private static Invocation'),actions.lastIndexOf('}'))]];
for(const [marker,source] of replacements){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,source);}
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp071-dispatch-status-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
