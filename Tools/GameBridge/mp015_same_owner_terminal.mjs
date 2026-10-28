// AUTHORED_NOT_RUN. Execute only after the project-wide coding gate closes.
// Runs extracted production metadata handlers against disclosed native/file
// adapters; this does not launch the game or qualify remote gameplay.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function body(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique production body required: '+key);const brace=source.indexOf('{',start);let level=1,end=brace+1;for(;level&&end<source.length;end++){if(source[end]==='{')level++;else if(source[end]==='}')level--;}if(level)throw Error('Unclosed production body');return source.slice(start,end);}
const protocol=await readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCompletionSameOwnerTerminal.cs'),'utf8');
const host=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.SameOwnerNativeHost.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp015_same_owner_terminal_fixture.cs'),'utf8');
for(const [marker,value] of [
 ['// PRODUCTION_RECEIVED','[ThreadStatic]private static NativeHostOriginal issuingSameOwnerReceived;\n'+body(protocol,'internal sealed class SameOwnerReceived')],
 ['// PRODUCTION_QUERY',body(protocol,'private static void QuerySameOwnerTerminal(')],
 ['// PRODUCTION_RETIRE',body(host,'internal static bool RetireSavedSameOwnerResources(')]
]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,value);}
const output=await mkdtemp(join(tmpdir(),'rebirth-mp015-terminal-')),cs=join(output,'actual.cs'),exe=join(output,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; extracted source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
// Preserve the extracted source and executable for review after execution.
