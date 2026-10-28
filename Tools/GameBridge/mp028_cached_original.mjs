// AUTHORED_NOT_RUN. Actual cached original preparation/entry/delivery wiring.
// Original construction/current/publication/native UI and budget are adapters.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.UiOriginal.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp028_cached_original_fixture.cs'),'utf8');
function method(key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique actual member required '+key);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed source');return source.slice(start,end);}
for(const [marker,key] of [['// ACTUAL_PREPARE','private static bool PrepareUiCachedOutput('],['// ACTUAL_ENTER','private static void EnterUiCachedOutput('],['// ACTUAL_RETAIN','private static void RetainUiOutput('],['// ACTUAL_DELIVERY','private static void ObserveUiCachedDelivery('],['// ACTUAL_GRID_GUARD','private static void GuardUiCachedGrid(']]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,method(key));}
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp028-cached-original-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
