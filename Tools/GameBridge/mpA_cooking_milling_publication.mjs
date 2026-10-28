// AUTHORED_NOT_RUN. Invoke only after the all-coding gate is lifted.
// Executes extracted production predicates and pre-setter classification;
// native item/queue bytes, UI boundaries and durable I/O are explicit doubles.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function method(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique production method required '+key);const arrow=source.indexOf('=>',start),brace=source.indexOf('{',start);if(arrow>=0&&arrow<brace)return source.slice(start,source.indexOf(';',arrow)+1);let depth=1,end=brace+1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed method '+key);return source.slice(start,end);}
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingMillingPublication.cs'),'utf8');
const gridSource=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationGridCustody.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mpA_cooking_milling_publication_fixture.cs'),'utf8');
for(const [marker,key] of [
 ['// ACTUAL_NATIVE_TERMINAL','internal bool NativeTerminal()'],
 ['// PRODUCTION_TERMINAL','internal bool Terminal()'],
 ['// PRODUCTION_HELD','internal static bool Held('],
 ['// PRODUCTION_LIVE_WINDOW','internal bool LiveWindowCurrent()'],
 ['// PRODUCTION_SYNC_GATE','internal static bool CanSync('],
 ['// PRODUCTION_RETAIN_UNUSED','private static void RetainUnusedAdmission('],
 ['// PRODUCTION_FINISH_UNUSED','private static void TryFinishUnusedAdmission('],
 ['// PRODUCTION_ENVELOPE','private static XElement Envelope(']
]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,method(source,key));}
fixture=fixture.replace('// PRODUCTION_GRID_HELD',method(gridSource,'internal static bool IsHeld('));
const temporary=await mkdtemp(join(tmpdir(),'rebirth-cooking-milling-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
