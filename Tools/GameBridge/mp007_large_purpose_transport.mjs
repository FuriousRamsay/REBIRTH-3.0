// AUTHORED_NOT_RUN. Actual MP007 fragment envelope/receiver + shared assembler.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const wire=await readFile(join(root,'Scripts/Metabolism/Consumption/RebirthConsumptionDurableDelivery.PurposeWire.cs'),'utf8');
function method(key){let start=wire.indexOf(key);if(start<0||wire.indexOf(key,start+1)>=0)throw Error('Unique actual method required '+key);let end=wire.indexOf('{',start)+1,depth=1;for(;depth&&end<wire.length;end++){if(wire[end]==='{')depth++;else if(wire[end]==='}')depth--;}if(depth)throw Error('Unclosed source method');return wire.slice(start,end);}
// Expression-bodied serializer ends at the next method, without executing code.
const begin=wire.indexOf('    private static string PurposeFragment('),finish=wire.indexOf('    private static bool QueueInventoryPurpose(');
if(begin<0||finish<=begin)throw Error('Actual fragment serializer boundary missing');
const transport=wire.slice(begin,finish)+method('private static bool TryReceiveInventoryPurposeFragment(');
const assembler=(await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceFragments.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
let fixture=await readFile(join(root,'Tools/GameBridge/mp007_large_purpose_transport_fixture.cs'),'utf8');fixture=fixture.replace('// ACTUAL_TRANSPORT',transport).replace('// ACTUAL_ASSEMBLER',assembler);
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp007-purpose-wire-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
