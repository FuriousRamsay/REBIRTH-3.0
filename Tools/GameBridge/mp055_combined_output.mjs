// AUTHORED_NOT_RUN. Run only after cumulative coding is complete.
// Extracted planner/insertion control flow; native item and XML APIs are doubles.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function method(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique source required '+key);const brace=source.indexOf('{',start);let depth=1,end=brace+1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed source');return source.slice(start,end);}
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingHeat.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp055_combined_output_fixture.cs'),'utf8');
for(const [marker,key] of [['// ACTUAL_COMBINED','internal static bool TryCombinedOutput('],['// ACTUAL_INSERT','private static bool TryMillingOutput(']]){
 if(fixture.split(marker).length!==2)throw Error(marker);fixture=fixture.replace(marker,method(source,key));
}
const out=await mkdtemp(join(tmpdir(),'rebirth-mp055-combined-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
