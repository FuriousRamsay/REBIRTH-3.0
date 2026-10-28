// AUTHORED_NOT_RUN. Invoke only after the all-coding gate is lifted.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
let fixture=await readFile(join(root,'Tools/GameBridge/mpA_additional_inventory_prediction_fixture.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputInventoryPrediction.cs'),'utf8');
const start=source.indexOf('internal sealed class RebirthRecipeOutputInventoryPrediction');if(start<0)throw Error('actual production predictor missing');
fixture=fixture.replace('// PRODUCTION_PREDICTOR',source.slice(start));
const temporary=await mkdtemp(join(tmpdir(),'rebirth-output-prediction-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
