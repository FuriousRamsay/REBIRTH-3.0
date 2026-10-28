// AUTHORED_NOT_RUN. Invoke only after all production/scenario coding is complete.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
let source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerPlacedSettlement.ClosedAuthority.cs'),'utf8');
function method(key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique production method required: '+key);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed production method');return source.slice(start,end);}
let fixture=await readFile(join(root,'Tools/GameBridge/mp027_closed_admission_fixture.cs'),'utf8');
const writer=(await readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCompletionPublication.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
for(const [marker,value] of [['// ACTUAL_PUBLICATION',writer],['// ACTUAL_ARCHIVE',method('internal bool EnsureOriginalPublished(')],['// ACTUAL_ADMISSION',method('internal static bool BeginAuthorityRemoteCompletion(')]]){if(fixture.split(marker).length!==2)throw Error('Unique marker required');fixture=fixture.replace(marker,value);}
source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerPlacedSettlement.ClosedProtocol.cs'),'utf8');
if(fixture.split('// ACTUAL_PUMP').length!==2)throw Error('Unique pump marker required');fixture=fixture.replace('// ACTUAL_PUMP',method('private static void PumpClosedAuthorityCompletions('));
const out=await mkdtemp(join(tmpdir(),'rebirth-mp027-admission-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
