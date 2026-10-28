// AUTHORED_NOT_RUN: execute after the cumulative production/scenario coding gate.
// Extracts actual dispatcher prefixes; adapters inject observer exceptions.
// This is an offline isolation check, not native Save or multiplayer proof.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function body(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique source required: '+key);const brace=source.indexOf('{',start);let n=1,end=brace+1;for(;n&&end<source.length;end++){if(source[end]==='{')n++;else if(source[end]==='}')n--;}if(n)throw Error('Unclosed source');return source.slice(start,end);}
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiOwnerFile.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp_owner_observer_isolation_fixture.cs'),'utf8');
for(const [marker,scope] of [['// ACTUAL_SAVE_PREFIX','private static class Save'],['// ACTUAL_ACCEPT_PREFIX','private static class AcceptedPlayer']]){
 const method=body(body(source,scope),'private static void Prefix(').replace('private static void Prefix(','internal static void Prefix(');
 if(fixture.split(marker).length!==2)throw Error('Marker '+marker);fixture=fixture.replace(marker,method);
}
if(fixture.split('// ACTUAL_SAVE_FINISH').length!==2)throw Error('Save Finish fixture marker');
fixture=fixture.replace('// ACTUAL_SAVE_FINISH',body(body(source,'private static class Save'),'private static void Finish(').replace('private static void Finish(','internal static void Finish('));
const output=await mkdtemp(join(tmpdir(),'rebirth-owner-observers-')),cs=join(output,'actual.cs'),exe=join(output,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
