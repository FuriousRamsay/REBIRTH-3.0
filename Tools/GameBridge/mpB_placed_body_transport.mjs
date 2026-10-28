// Deferred authoring only. Do not run before the parent all-code validation gate.
import {readFile,writeFile,mkdtemp,rm,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
function body(source,key){const start=source.indexOf(key);if(start<0)throw Error(key);const open=source.indexOf('{',start);let depth=1,end=open+1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error(key);return source.slice(start,end);}
const fragments=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceFragments.cs'),'utf8');
const queue=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceRepairSendQueue.cs'),'utf8');
const transport=await readFile(join(root,'Scripts/Survivor/Provenance/RebirthPlacedOriginalBodyTransport.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Survivor/Provenance/RebirthPlacedOriginalBodySource.cs'),'utf8');
const execution=await readFile(join(root,'Scripts/Survivor/Provenance/RebirthPlacedOriginalBodyExecution.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mpB_placed_body_transport_fixture.cs'),'utf8');
fixture=fixture.replace('// ACTUAL_FRAGMENT_TYPES',fragments.replace(/^using .*;\r?\n/gm,'')+'\n'+queue.replace(/^using .*;\r?\n/gm,''))
.replace('// ACTUAL_FRAGMENT_WRITE',body(transport,'internal static void WriteDeferredFragmentPayload('))
.replace('// ACTUAL_RELEASE_GATE',source.match(/internal static bool CanReleaseBodyCustody[^\r\n]+/)[0])
.replace('// ACTUAL_PURPOSE_OBJECTS',execution.match(/internal static bool SamePreparedBodyObjects[^\r\n]+/)[0]);
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mpB-body-transport-'));
try{const sdk=process.env.MPB_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';const versions=(await readdir(sdk)).filter(v=>/^\d+\.\d+\.\d+$/.test(v)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('SDK unavailable');const path=join(temporary,'fixture.cs'),target=join(temporary,'fixture.exe');await writeFile(path,fixture);await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+target,...['mscorlib','System','System.Core'].map(v=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+v+'.dll'),path],{windowsHide:true,timeout:30000});console.log((await run(target,[],{windowsHide:true,timeout:30000})).stdout.trim());}finally{await rm(temporary,{recursive:true,force:true});}