// AUTHORED_NOT_RUN. Invoke only after the all-coding gate is lifted.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
let fixture=await readFile(join(root,'Tools/GameBridge/mpA_additional_grid_publication_fixture.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.cs'),'utf8');
for(const [marker,key] of [['INSERT','private static bool Insert('],['PREPARE','internal static void PrepareUiSlots('],['UI_INSERT','internal static void UiInsert(']]){
 const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('unique production method required '+key);
 let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}
 if(depth)throw Error('unclosed production method '+key);fixture=fixture.replace('// PRODUCTION_'+marker,source.slice(start,end));
}
const temporary=await mkdtemp(join(tmpdir(),'rebirth-output-grid-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
